using Mono.Cecil;
using Mono.Cecil.Cil;
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

        public void LogError(string message, SequencePoint sequencePoint = null)
        {
            if (sequencePoint != null) { this.weaver.LogErrorPoint(message, sequencePoint); }
            else { this.weaver.LogError(message); }
        }

        public void LogWarning(string message, SequencePoint sequencePoint = null)
        {
            if (sequencePoint != null) { this.weaver.LogWarningPoint(message, sequencePoint); }
            else { this.weaver.LogWarning(message); }
        }

        public void LogInfo(string message) => this.weaver.LogInfo(message);

        public void LogDebug(string message) => this.weaver.LogDebug(message);
    }
}
