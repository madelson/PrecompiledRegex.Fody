using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PreCompiledRegex.Fody
{
    internal sealed class RegexReference
    {
        public RegexReference(Instruction call, Instruction pattern, Instruction options)
        {
            this.CallInstruction = call;
            this.PatternInstruction = pattern;
            this.OptionsInstruction = options;
        }

        public Instruction CallInstruction { get; }
        public Instruction PatternInstruction { get; }
        public Instruction OptionsInstruction { get; }

        public MethodDefinition Method => ((MethodReference)this.CallInstruction.Operand).Resolve();

        public RegexDefinition Definition => new RegexDefinition(
            (string)this.PatternInstruction.Operand, 
            this.OptionsInstruction != null ? RegexReferenceFinder.TryGetRegexOptions(this.OptionsInstruction).Value : RegexOptions.None
        );
    }
}
