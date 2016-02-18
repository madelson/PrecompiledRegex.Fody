using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PreCompiledRegex.Fody
{
    internal sealed class WeavingContext
    {
        private readonly ModuleWeaver weaver;

        public WeavingContext(ModuleWeaver weaver)
        {
            this.weaver = weaver;
        }

        public ModuleDefinition ModuleDefinition => this.weaver.ModuleDefinition;
        public string AssemblyFilePath => this.weaver.AssemblyFilePath;
    }
}
