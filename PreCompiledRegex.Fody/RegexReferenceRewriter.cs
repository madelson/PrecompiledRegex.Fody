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

namespace PrecompiledRegex.Fody
{
    internal sealed class RegexReferenceRewriter
    {
        private readonly WeavingContext context;
        private readonly RegexReferenceExtractor referenceExtractor;
        
        private RegexReferenceRewriter(WeavingContext context)
        {
            this.context = context;
            this.referenceExtractor = new RegexReferenceExtractor(context, log: false);
        }

        public static void RewriteReferences(WeavingContext context, IReadOnlyDictionary<MethodDefinition, List<RegexDefinition>> references)
        {
            if (references.Count == 0)
            {
                context.LogWarning("The assembly does not contain any regular expressions that can be precompiled. View detailed build output for more information");
            }

            using (context.Step("Rewriting Regex References"))
            {
                var regexCompiler = new RegexCompiler(context, references.SelectMany(kvp => kvp.Value));
                var compileResult = regexCompiler.Compile();
                if (!compileResult.Success)
                {
                    return;
                }

                var accessorGenerator = new CompiledRegexAccessorGenerator(context, compileResult.Assembly, compileResult.CompiledRegexes);
                var accessors = accessorGenerator.GenerateAccessors();

                var rewriter = new RegexReferenceRewriter(context);
                foreach (var kvp in references)
                {
                    rewriter.RewriteMethod(kvp.Key, kvp.Value, accessors);
                }
            }
        }

        private void RewriteMethod(MethodDefinition method, IReadOnlyCollection<RegexDefinition> definitions, IReadOnlyDictionary<RegexDefinition, RegexAccessorMethods> regexAccessors)
        {
            method.Body.SimplifyMacros();
            try
            {
                foreach (var definition in definitions)
                {
                    var reference = method.Body.Instructions
                        .Select(this.referenceExtractor.TryGetRegexReference)
                        .First(@ref => @ref != null); // todo log
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
