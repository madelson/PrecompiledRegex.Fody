using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mono.Cecil.Rocks;

namespace PreCompiledRegex.Fody
{
    internal static class PatternAndOptionsFinder
    {
        public static KeyValuePair<string, RegexOptions>? TryFindPatternAndOptions(Instruction call)
        {
            var methodDefinition = ((MethodReference)call.Operand).Resolve();

            // TODO use this to track args:
            // http://cecil.googlecode.com/svn/trunk/decompiler/Cecil.Decompiler/Cecil.Decompiler.Cil/ControlFlowGraphBuilder.cs
            
            string pattern;
            RegexOptions options;
            if (call.OpCode == OpCodes.Newobj)
            {
                if (methodDefinition.Parameters.Count == 2)
                {
                    if (call.Previous.OpCode != OpCodes.Ldc_I4) { return null; }
                    options = (RegexOptions)call.Previous.Operand;
                }
                else { options = RegexOptions.None; }
                
                var patternInstruction = methodDefinition.Parameters.Count == 2
                    ? call.Previous.Previous
                    : call.Previous;
                if (patternInstruction.OpCode != OpCodes.Ldstr) { return null; }
                pattern = (string)patternInstruction.Operand;
            }
            else
            {
                throw new InvalidOperationException("Should never get here");
            }

            return new KeyValuePair<string, RegexOptions>(pattern, options);
        }
    }
}
