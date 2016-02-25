using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PrecompiledRegex.Fody
{
    internal sealed class CompiledRegexAccessorGenerator
    {
        private readonly WeavingContext context;
        /// <summary>
        /// This will either be the context module (if we're merging) or the module from a separate assembly.
        /// For now this code is written to support both
        /// </summary>
        private readonly ModuleDefinition regexModule;
        private readonly IReadOnlyCollection<RegexCompilationInfo> compiledRegexes;

        public CompiledRegexAccessorGenerator(
            WeavingContext context, 
            ModuleDefinition regexModule, 
            IReadOnlyCollection<RegexCompilationInfo> compiledRegexes)
        {
            this.context = context;
            this.regexModule = regexModule;
            this.compiledRegexes = compiledRegexes;
        }

        private ModuleDefinition Module => this.context.ModuleDefinition;

        public Dictionary<RegexDefinition, RegexAccessorMethods> GenerateAccessors()
        {
            var accessorType = new TypeDefinition("PrecompiledRegex.Fody", "RegularExpressions", TypeAttributes.NotPublic | TypeAttributes.Sealed);
            accessorType.BaseType = this.Module.TypeSystem.Object;
            this.Module.Types.Add(accessorType);

            var accessors = this.compiledRegexes.ToDictionary(
                cr => new RegexDefinition(cr.Pattern, cr.Options),
                cr => new RegexAccessorMethods(
                    accessMethod: this.GenerateAccessor(accessorType, cr),
                    accessMethodWithTimeout: this.GenerateAccessorWithTimeout(accessorType, cr)
                )
            );
            return accessors;
        }

        private MethodDefinition GenerateAccessor(TypeDefinition accessorType, RegexCompilationInfo regex)
        {
            var field = new FieldDefinition("cached" + regex.Name, FieldAttributes.Private | FieldAttributes.Static, this.Module.ImportReference(typeof(Regex)));
            accessorType.Fields.Add(field);

            var accessor = new MethodDefinition(regex.Name, MethodAttributes.Public | MethodAttributes.Static, this.Module.ImportReference(typeof(Regex)));
            accessorType.Methods.Add(accessor);

            var il = accessor.Body.GetILProcessor();
            il.Emit(OpCodes.Ldsfld, field);
            var epilogStartInstruction = Instruction.Create(OpCodes.Ldsfld, field);
            // Branch if value on stack is true, not null or non-zero
            il.Emit(OpCodes.Brtrue, epilogStartInstruction);
            il.Emit(OpCodes.Newobj, this.ImportIfNeeded(this.regexModule.GetType(regex.Namespace + "." + regex.Name).Methods.Single(c => c.IsConstructor && !c.IsStatic && !c.HasParameters)));
            il.Emit(OpCodes.Stsfld, field);
            il.Append(epilogStartInstruction);
            il.Emit(OpCodes.Ret);
            accessor.Body.OptimizeMacros();

            return accessor;
        }

        private MethodDefinition GenerateAccessorWithTimeout(TypeDefinition accessorType, RegexCompilationInfo regex)
        {
            var regexTypeReference = this.Module.ImportReference(typeof(Regex));

            var field = new FieldDefinition("cached" + regex.Name + "WithTimeout", FieldAttributes.Private | FieldAttributes.Static, regexTypeReference);
            accessorType.Fields.Add(field);

            var accessor = new MethodDefinition(regex.Name, MethodAttributes.Public | MethodAttributes.Static, regexTypeReference);
            accessor.Body.InitLocals = true;
            accessor.Body.Variables.Add(new VariableDefinition(regexTypeReference));
            accessor.Parameters.Add(new ParameterDefinition("matchTimeout", ParameterAttributes.None, this.Module.ImportReference(typeof(TimeSpan))));
            accessorType.Methods.Add(accessor);

            var il = accessor.Body.GetILProcessor();
            // var regex = cached
            il.Emit(OpCodes.Ldsfld, field);
            il.Emit(OpCodes.Stloc_0);
            // if (regex == null
            il.Emit(OpCodes.Ldloc_0);
            var startBuildingRegexInstruction = Instruction.Create(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Brfalse, startBuildingRegexInstruction);
            // || regex.MatchTimeout != matchTimeout)
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Callvirt, this.Module.ImportReference(typeof(Regex).GetProperty("MatchTimeout").GetMethod));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, this.Module.ImportReference(typeof(TimeSpan).GetMethod("op_Inequality", new[] { typeof(TimeSpan), typeof(TimeSpan) })));
            var epilogStartInstruction = Instruction.Create(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Brfalse, epilogStartInstruction);
            // regex = new [CustomRegex](matchTimeout)
            il.Append(startBuildingRegexInstruction);
            il.Emit(
                OpCodes.Newobj,
                this.ImportIfNeeded(
                    this.regexModule
                        .GetType(regex.Namespace + "." + regex.Name)
                        .Methods
                        .Single(c => c.IsConstructor && !c.IsStatic && c.Parameters.Count == 1 && c.Parameters[0].ParameterType.Name == nameof(TimeSpan))
                )
            );
            il.Emit(OpCodes.Stloc_0);
            // cached = regex
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Stsfld, field);
            // return regex
            il.Append(epilogStartInstruction);
            il.Emit(OpCodes.Ret);
            accessor.Body.OptimizeMacros();

            return accessor;
        }

        private MethodReference ImportIfNeeded(MethodDefinition regexMethod)
        {
            return this.regexModule == this.Module
                ? regexMethod
                : this.Module.ImportReference(regexMethod);
        }
    }

    internal sealed class RegexAccessorMethods
    {
        public RegexAccessorMethods(MethodDefinition accessMethod, MethodDefinition accessMethodWithTimeout)
        {
            this.AccessorMethod = accessMethod;
            this.AccessorMethodWithTimeout = accessMethodWithTimeout;
        }

        public MethodDefinition AccessorMethod { get; }
        public MethodDefinition AccessorMethodWithTimeout { get; }
    }
}
