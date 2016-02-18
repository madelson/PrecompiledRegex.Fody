using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil.Rocks;
using Mono.Cecil.Cil;
using System.Text.RegularExpressions;
using System.IO;

namespace PreCompiledRegex.Fody
{
    internal sealed class TypeProcessor
    {
        private readonly WeavingContext context;
        private readonly RegexReferenceFinder referenceFinder;

        private readonly Dictionary<MethodDefinition, List<RegexDefinition>> replacableRegexDefinitionsByMethod
            = new Dictionary<MethodDefinition, List<RegexDefinition>>();

        public TypeProcessor(WeavingContext context)
        {
            this.context = context;
            this.referenceFinder = new RegexReferenceFinder(context);
        }

        public void PreProcessType(TypeDefinition type)
        {
            if (type.HasMethods)
            {
                foreach (var method in type.Methods)
                {
                    this.PreProcessMethod(method);
                }
            }

            // recurse on nested types, since Module.Types doesn't contain them
            if (type.HasNestedTypes)
            {
                foreach (var nestedType in type.NestedTypes)
                {
                    this.PreProcessType(nestedType);
                }
            }
        }

        private void PreProcessMethod(MethodDefinition method)
        {
            if (!method.HasBody) { return; }

            List<RegexDefinition> definitions = null;
            var referenceFinder = this.referenceFinder;
            var instructions = method.Body.Instructions;
            for (var i = 0; i < instructions.Count; ++i)
            {
                var instruction = instructions[i];
                var reference = this.referenceFinder.TryGetRegexReference(instruction);
                if (reference != null)
                {
                    (definitions ?? (definitions = new List<RegexDefinition>())).Add(reference.Definition);
                }
            }

            if (definitions != null)
            {
                this.replacableRegexDefinitionsByMethod.Add(method, definitions);
            }
        }

        public void PostProcessAllTypes()
        {
            var regexCompiler = new RegexCompiler(this.context, this.replacableRegexDefinitionsByMethod.SelectMany(kvp => kvp.Value));
            var compileResult = regexCompiler.Compile();
            if (!compileResult.Success)
            {
                return;
            }

            var accessorGenerator = new CompiledRegexAccessorGenerator(this.context, compileResult.Assembly, compileResult.CompiledRegexes);
            var accessors = accessorGenerator.GenerateAccessors();

            foreach (var referencingMethod in this.replacableRegexDefinitionsByMethod.Keys)
            {
                this.PostProcessMethod(referencingMethod, accessors);
            }
        }

        private void PostProcessMethod(MethodDefinition method, IReadOnlyDictionary<RegexDefinition, RegexAccessorMethods> regexAccessors)
        {
            var definitions = this.replacableRegexDefinitionsByMethod[method];

            method.Body.SimplifyMacros();
            try
            {
                foreach (var definition in definitions)
                {
                    var reference = method.Body.Instructions
                        .Select(this.referenceFinder.TryGetRegexReference)
                        .First(@ref => @ref != null);
                    var regexMethod = reference.RegexMethod;
                    var accessors = regexAccessors[reference.Definition];

                    var il = method.Body.GetILProcessor();
                    il.Remove(reference.PatternInstruction);
                    if (reference.OptionsInstruction != null)
                    {
                        il.Remove(reference.OptionsInstruction);
                    }

                    var getRegexMethod = regexMethod.TimeoutParameterIndex.HasValue
                        ? accessors.AccessorMethodWithTimeout
                        : accessors.AccessorMethod;
                    var getRegexInstruction = Instruction.Create(OpCodes.Call, getRegexMethod);

                    if (regexMethod.Method.IsConstructor)
                    {
                        // for a constructor, we just replace new Regex(...) with GetRegex(...)
                        il.Replace(reference.CallInstruction, getRegexInstruction);
                    }
                    else
                    {
                        // static method calls (e. g. Regex.Replace(input, pattern, replacement, options, timeout))
                        // are a bit more difficult. Since timeout is last, we can insert a call to GetRegex(...) there
                        // however, our stack then looks like input, replacement, regex, where we need
                        // to move regex to the beginning of that list to call the non-static equivalent regex.Replace(...).
                        // Thus, we'll first store all remaining args to locals and push load them back in the correct order

                        var locals = regexMethod.Parameters
                            // knock out what we've already eliminated
                            .Where((p, index) => index != regexMethod.PatternParameterIndex && index != regexMethod.OptionsParameterIndex)
                            .Select(p => new VariableDefinition(this.context.ModuleDefinition.ImportReference(p.ParameterType)))
                            .ToList();
                        if (locals.Count > 0) { method.Body.InitLocals = true; }
                        locals.ForEach(method.Body.Variables.Add);

                        // save off the values in reverse order, since we're popping off the stack
                        for (var i = locals.Count - 1; i >= 0; --i)
                        {
                            il.InsertBefore(reference.CallInstruction, Instruction.Create(OpCodes.Stloc, locals[i]));
                        }

                        // if we have a timeout, push that local
                        if (regexMethod.TimeoutParameterIndex.HasValue)
                        {
                            il.InsertBefore(reference.CallInstruction, Instruction.Create(OpCodes.Ldloc, locals.Single(v => v.VariableType.Name == nameof(TimeSpan))));
                        }

                        // get the regex
                        il.InsertBefore(reference.CallInstruction, getRegexInstruction);

                        // put the arguments back, except the timeout
                        foreach (var local in locals.Where(v => v.VariableType.Name != nameof(TimeSpan)))
                        {
                            il.InsertBefore(reference.CallInstruction, Instruction.Create(OpCodes.Ldloc, local));
                        }

                        // finally, call the equivalent instance function
                        il.Replace(reference.CallInstruction, Instruction.Create(OpCodes.Callvirt, this.context.ModuleDefinition.ImportReference(regexMethod.InstanceEquivalent)));
                    }
                }
            }
            finally
            {
                method.Body.OptimizeMacros();
            }
        }
    }
}
