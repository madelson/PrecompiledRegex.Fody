using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PreCompiledRegex.Fody
{
    // todo should use module.resolve(typeof(Regex)) instead
    internal sealed class RegexMethods
    {
        public RegexMethods(WeavingContext context)
        { 
            var regexAssembly = context.AssemblyResolver.Resolve(typeof(Regex).Assembly.GetName().Name);
            this.Type = regexAssembly.MainModule.GetType(typeof(Regex).ToString());
            this.Constructors = this.Type.Methods.Where(
                    m => !m.IsStatic 
                        && m.IsConstructor 
                        && m.Parameters.Count > 0
                        && m.Parameters.All(p => p.Name == "pattern" || p.Name == "options")
                )
                .ToArray();
        }

        public TypeDefinition Type { get; }
        public IReadOnlyList<MethodDefinition> Constructors { get; }

        public bool IsRegexMethodReference(Instruction instruction)
        {
            var opCode = instruction.OpCode;
            MethodReference reference;
            return (opCode == OpCodes.Newobj || opCode == OpCodes.Call)
                && (reference = instruction.Operand as MethodReference) != null
                && reference.DeclaringType.Name == "Regex"
                && this.IsRegexMethodReference(reference);
        }

        private bool IsRegexMethodReference(MethodReference reference)
        {
            var definition = reference.Resolve();
            return definition.IsConstructor
                && this.Constructors.Any(c => c.MetadataToken.RID == definition.MetadataToken.RID
                    && c.FullName == definition.FullName
                );
        }
    }
}
