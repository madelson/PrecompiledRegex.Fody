using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PrecompiledRegex.Fody
{
    internal sealed class RegexReferenceExtractor
    {
        private readonly WeavingContext context;
        private readonly bool log;

        public RegexReferenceExtractor(WeavingContext context, bool log)
        {
            this.context = context;
            this.log = log;
        }

        public RegexReference TryGetRegexReference(Instruction instruction)
        {
            if (!MayBeRegexReference(instruction)) { return null; }
            return this.TryGetRegexReferenceSlow(instruction);
        }
        
        private static bool MayBeRegexReference(Instruction instruction)
        {
            var opCode = instruction.OpCode.Code;
            return (opCode == Code.Newobj || opCode == Code.Call)
                && (instruction.Operand as MethodReference)?.DeclaringType.Name == nameof(Regex);
        }

        private RegexReference TryGetRegexReferenceSlow(Instruction instruction)
        {
            var method = ((MethodReference)instruction.Operand).Resolve();
            var regexMethod = RegexMethod.All.FirstOrDefault(m => m.IsEquivalentTo(method));
            if (regexMethod == null) { return null; }

            Instruction[] arguments;
            string errorMessage;
            if (!SimpleArgumentLocator.TryFindArgumentInstructions(instruction, out arguments, out errorMessage))
            {
                this.Log($"Could not determine arguments to {regexMethod} at {ToString(instruction.SequencePoint)}: {errorMessage}: it will not be precompiled");
                return null;
            }

            if (arguments.Length < regexMethod.Parameters.Count)
            {
                this.Log($"Could not determine enough arguments to {regexMethod} at {ToString(instruction.SequencePoint)}: it will not be precompiled");
                return null;
            }

            var patternInstruction = arguments[regexMethod.PatternParameterIndex];
            if (arguments[regexMethod.PatternParameterIndex].OpCode.Code != Code.Ldstr)
            {
                this.Log($"Pattern argument to {regexMethod} at {ToString(instruction.SequencePoint)} is not a constant or literal: it will not be precompiled");
                return null;
            }

            var optionsInstruction = regexMethod.OptionsParameterIndex.HasValue
                ? arguments[regexMethod.OptionsParameterIndex.Value]
                : null;
            RegexOptions options;
            if (optionsInstruction != null)
            {
                var extractedOptions = TryGetRegexOptions(optionsInstruction);
                if (!extractedOptions.HasValue)
                {
                    this.Log($"Options argument to {regexMethod} at {ToString(instruction.SequencePoint)} is not a constant or literal: it will not be precompiled");
                    return null;
                }
                options = extractedOptions.Value;
            }
            else
            {
                options = RegexOptions.None;
            }

            if (this.context.Options.Include == IncludeFilter.Compiled && !options.HasFlag(RegexOptions.Compiled))
            {
                this.Log($"Options argument to {regexMethod} at {ToString(instruction.SequencePoint)} does not have the '{nameof(RegexOptions.Compiled)}' flag: it will not be precompiled");
                return null;
            }

            var reference = new RegexReference(instruction, pattern: patternInstruction, options: optionsInstruction);
            this.Log($"Found precompilable regex {reference.Definition} at {ToString(instruction.SequencePoint)}");
            return reference;
        }

        private static string ToString(SequencePoint sequencePoint)
        {
            return sequencePoint == null
                ? "[UNKNOWN LOCATION]"
                : $"{sequencePoint.Document.Url}:line {sequencePoint.StartLine}";
        }

        private void Log(string message)
        {
            if (this.log) { this.context.LogInfo(message); }
        }

        public static RegexOptions? TryGetRegexOptions(Instruction instruction)
        {
            // based on https://github.com/jbevain/cecil/blob/55da3138f73b99bd776c1cfd5db135f4660631b5/rocks/Mono.Cecil.Rocks/MethodBodyRocks.cs
            switch (instruction.OpCode.Code)
            {
                case Code.Ldc_I4: return (RegexOptions)instruction.Operand;
                case Code.Ldc_I4_0: return (RegexOptions)0;
                case Code.Ldc_I4_1: return (RegexOptions)1;
                case Code.Ldc_I4_2: return (RegexOptions)2;
                case Code.Ldc_I4_3: return (RegexOptions)3;
                case Code.Ldc_I4_4: return (RegexOptions)4;
                case Code.Ldc_I4_5: return (RegexOptions)4;
                case Code.Ldc_I4_6: return (RegexOptions)4;
                case Code.Ldc_I4_7: return (RegexOptions)4;
                case Code.Ldc_I4_8: return (RegexOptions)4;
                case Code.Ldc_I4_M1: return (RegexOptions)(-1);
                case Code.Ldc_I4_S: return (RegexOptions)(sbyte)(instruction.Operand);
                default: return null;
            }
        }
    }
}
