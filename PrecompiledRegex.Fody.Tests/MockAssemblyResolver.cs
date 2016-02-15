using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrecompiledRegex.Fody.Tests
{
    // from https://github.com/Fody/NullGuard/blob/master/Tests/Helpers/MockAssemblyResolver.cs
    internal class MockAssemblyResolver : IAssemblyResolver
    {
        public AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            return AssemblyDefinition.ReadAssembly(name.Name + ".dll");
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            throw new NotImplementedException();
        }

        public AssemblyDefinition Resolve(string fullName)
        {
            if (fullName == "System")
            {
                var codeBase = typeof(Debug).Assembly.CodeBase.Replace("file:///", string.Empty);
                return AssemblyDefinition.ReadAssembly(codeBase);
            }
            else
            {
                var codeBase = typeof(string).Assembly.CodeBase.Replace("file:///", string.Empty);
                return AssemblyDefinition.ReadAssembly(codeBase);
            }
        }

        public AssemblyDefinition Resolve(string fullName, ReaderParameters parameters)
        {
            throw new NotImplementedException();
        }
    }
}
