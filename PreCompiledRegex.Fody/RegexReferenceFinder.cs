using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PreCompiledRegex.Fody
{
    internal sealed class RegexReferenceFinder
    {
        private readonly WeavingContext context;

        public RegexReferenceFinder(WeavingContext context)
        {
            this.context = context;
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
            if (regexMethod == null) { return null; } // todo logging?

            Instruction[] arguments;
            string errorMessage;
            if (!SimpleArgumentLocator.TryFindArgumentInstructions(instruction, out arguments, out errorMessage))
            {
                return null; // todo log
            }

            if (arguments.Length < regexMethod.Parameters.Count)
            {
                return null; // todo log
            }

            var patternInstruction = arguments[regexMethod.PatternParameterIndex];
            if (arguments[regexMethod.PatternParameterIndex].OpCode.Code != Code.Ldstr)
            {
                return null; // todo log
            }

            var optionsInstruction = regexMethod.OptionsParameterIndex.HasValue
                ? arguments[regexMethod.OptionsParameterIndex.Value]
                : null;
            if (optionsInstruction != null && !TryGetRegexOptions(optionsInstruction).HasValue)
            {
                return null; // todo log
            }

            return new RegexReference(instruction, pattern: patternInstruction, options: optionsInstruction);
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
