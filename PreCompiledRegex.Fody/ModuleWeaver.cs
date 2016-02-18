using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PreCompiledRegex.Fody
{
    // see https://github.com/Fody/Fody/wiki/ModuleWeaver
    public sealed class ModuleWeaver
    {
        // required, injected
        public ModuleDefinition ModuleDefinition { get; set; }

        // injected
        public string AssemblyFilePath { get; set; }

        #region ---- Injected Loggers ----
        // Will log an MessageImportance.Normal message to MSBuild. OPTIONAL
        public Action<string> LogDebug { get; set; }

        // Will log an MessageImportance.High message to MSBuild. OPTIONAL
        public Action<string> LogInfo { get; set; }

        // Will log an warning message to MSBuild. OPTIONAL
        public Action<string> LogWarning { get; set; }

        // Will log an warning message to MSBuild at a specific point in the code. OPTIONAL
        public Action<string, SequencePoint> LogWarningPoint { get; set; }

        // Will log an error message to MSBuild. OPTIONAL
        public Action<string> LogError { get; set; }

        // Will log an error message to MSBuild at a specific point in the code. OPTIONAL
        public Action<string, SequencePoint> LogErrorPoint { get; set; }
        #endregion

        // required
        public void Execute()
        {
            var context = new WeavingContext(this);

            var references = RegexReferenceFinder.FindAllReferences(context);
            RegexReferenceRewriter.RewriteReferences(context, references);
        }
    }
}
