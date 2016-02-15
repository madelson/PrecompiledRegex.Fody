using Mono.Cecil;
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
        public IAssemblyResolver AssemblyResolver { get; set; }

        // injected
        public string AssemblyFilePath { get; set; }

        // required
        public void Execute()
        {
            var context = new WeavingContext(this);
            var regexMethods = new RegexMethods(context);
            var typeProcessor = new TypeProcessor(regexMethods, context);

            foreach (var type in this.ModuleDefinition.Types)
            {
                if (!type.IsEnum && !type.IsInterface)
                {
                    typeProcessor.PreProcessType(type);
                }
            }

            typeProcessor.PostProcessAllTypes();
        }
    }
}
