using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrecompiledRegex.Fody
{
    /// <summary>
    /// Idea for the future: we could copy the generated regex types into the assembly rather than 
    /// </summary>
    internal sealed class TypeCopier
    {
        private readonly ModuleDefinition from, to;
        private readonly Dictionary<IMemberDefinition, IMemberDefinition> mapping = new Dictionary<IMemberDefinition, IMemberDefinition>();

        private TypeCopier(ModuleDefinition from, ModuleDefinition to)
        {
            this.from = from;
            this.to = to;
        }

        public static void CopyTypes(ModuleDefinition from, ModuleDefinition to) => new TypeCopier(from, to).CopyTypes();

        private void CopyTypes()
        {
            foreach (var type in this.from.Types)
            {
                this.CopyType(type);
            }
        }

        private void CopyType(TypeDefinition fromType)
        {
            if (this.mapping.ContainsKey(fromType)) { return; }

            var copy = new TypeDefinition(fromType.Namespace, fromType.Name, fromType.Attributes);
            this.to.Types.Add(copy);
            this.mapping.Add(fromType, copy);
            
        }

        private TypeDefinition Resolve(TypeDefinition typeDefinition)
        {
            if (typeDefinition.Module == this.from)
            {
                this.CopyType(typeDefinition);
                return (TypeDefinition)this.mapping[typeDefinition];
            }

            var assembly = this.to.AssemblyResolver.Resolve(typeDefinition.Module.Assembly.FullName);
            TypeReference reference;
            if (!assembly.MainModule.TryGetTypeReference(typeDefinition.FullName, out reference))
            {
                throw new InvalidOperationException($"Cannot find type '{typeDefinition.FullName}' in assembly '{assembly.FullName}'");
            }

            return assembly.MainModule.MetadataResolver.Resolve(reference);
        }
    }
}
