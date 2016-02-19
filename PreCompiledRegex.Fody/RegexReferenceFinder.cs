using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PreCompiledRegex.Fody
{
    internal sealed class RegexReferenceFinder
    {
        private readonly WeavingContext context;
        private readonly RegexReferenceExtractor referenceExtractor;

        private readonly Dictionary<MethodDefinition, List<RegexDefinition>> references
            = new Dictionary<MethodDefinition, List<RegexDefinition>>();

        private RegexReferenceFinder(WeavingContext context)
        {
            this.context = context;
            this.referenceExtractor = new RegexReferenceExtractor(context, log: true);
        }

        public static Dictionary<MethodDefinition, List<RegexDefinition>> FindAllReferences(WeavingContext context)
        {
            using (context.Step("Finding Regex References"))
            {
                var finder = new RegexReferenceFinder(context);

                foreach (var type in context.ModuleDefinition.Types)
                {
                    finder.FindAllReferences(type);
                }

                return finder.references;
            }
        }

        public void FindAllReferences(TypeDefinition type)
        {
            if (type.HasMethods)
            {
                foreach (var method in type.Methods)
                {
                    this.FindAllReferences(method);
                }
            }

            // recurse on nested types, since Module.Types doesn't contain them
            if (type.HasNestedTypes)
            {
                foreach (var nestedType in type.NestedTypes)
                {
                    this.FindAllReferences(nestedType);
                }
            }
        }

        private void FindAllReferences(MethodDefinition method)
        {
            if (!method.HasBody) { return; }

            List<RegexDefinition> definitions = null;
            var referenceExtractor = this.referenceExtractor;
            foreach (var instruction in method.Body.Instructions)
            {
                var reference = referenceExtractor.TryGetRegexReference(instruction);
                if (reference != null)
                {
                    (definitions ?? (definitions = new List<RegexDefinition>())).Add(reference.Definition);
                }
            }

            if (definitions != null)
            {
                this.references.Add(method, definitions);
            }
        }

    }
}
