using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PrecompiledRegex.Fody
{
    internal sealed class RegexCompiler
    {
        private readonly WeavingContext context;
        private readonly IReadOnlyCollection<RegexDefinition> regexes;

        public RegexCompiler(WeavingContext context, IEnumerable<RegexDefinition> regexes)
        {
            this.context = context;
            this.regexes = regexes.Distinct().ToArray();
        }

        public Result Compile()
        {
            using (this.context.Step($"Compiling {this.regexes.Count} Regexes"))
            {
                var assemblyName = this.GetAssemblyName();
                var compilationInfos = this.GetCompilationInfos(assemblyName);
                var regexHash = this.HashCompilationInfos(compilationInfos);

                var assembly = this.GetExistingValidAssembly(assemblyName, regexHash: regexHash)
                    ?? this.Compile(assemblyName, compilationInfos, regexHash: regexHash);

                return new Result { Assembly = assembly, CompiledRegexes = compilationInfos };
            }
        }

        private AssemblyName GetAssemblyName()
        {
            return new AssemblyName(this.context.ModuleDefinition.Assembly.Name.Name + ".RegularExpressions")
            {
                Version = this.context.ModuleDefinition.Assembly.Name.Version
            };
        }

        private List<RegexCompilationInfo> GetCompilationInfos(AssemblyName assemblyName)
        {
            return this.regexes.OrderBy(r => r.Pattern)
                .ThenBy(r => r.Options)
                .Select((r, index) => new RegexCompilationInfo(r.Pattern, r.Options, fullnamespace: assemblyName.Name, name: "PrecompiledRegex" + index, ispublic: false))
                .ToList();
        }

        private string HashCompilationInfos(IReadOnlyList<RegexCompilationInfo> compilationInfos)
        {
            var strings = compilationInfos.SelectMany(ci => new[] { ci.Pattern, ((int)ci.Options).ToString(), ci.Namespace, ci.Name, ci.IsPublic ? "1" : "0" })
                .Concat(new[] { this.GetType().Assembly.GetName().Version.ToString() });

            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(memoryStream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true))
                {
                    foreach (var @string in strings)
                    {
                        writer.Write(@string);
                        writer.Write("@@"); // arbitrary separator
                    }
                }

                memoryStream.Seek(0, SeekOrigin.Begin);
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    var hashBytes = md5.ComputeHash(memoryStream);
                    var hash = Convert.ToBase64String(hashBytes);
                    return hash;
                }
            }
        }

        private string GetOutputPath(AssemblyName assemblyName) => Path.Combine(Path.GetDirectoryName(this.context.AssemblyFilePath), assemblyName.Name + ".dll");

        private string GetAssemblyDescription(string regexHash) => $"Precompiled regular expressions for {this.context.ModuleDefinition.Assembly.Name.Name} (hash = {regexHash})";

        private AssemblyDefinition GetExistingValidAssembly(AssemblyName assemblyName, string regexHash)
        {
            var outputPath = this.GetOutputPath(assemblyName);
            if (!File.Exists(outputPath)) { return null; }

            var assembly = AssemblyDefinition.ReadAssembly(outputPath);
            if (assembly.Name.Name != assemblyName.Name || !Equals(assembly.Name.Version, assemblyName.Version))
            {
                return null;
            }

            var description = (string)assembly.CustomAttributes
                .FirstOrDefault(c => c.AttributeType.Name == nameof(AssemblyDescriptionAttribute) && c.AttributeType.Namespace == typeof(AssemblyDescriptionAttribute).Namespace)
                ?.ConstructorArguments[0].Value;
            if (description != this.GetAssemblyDescription(regexHash))
            {
                return null;
            }

            this.context.LogInfo($"Re-using cached assembly {outputPath}");
            return assembly;
        }

        private AssemblyDefinition Compile(AssemblyName assemblyName, IReadOnlyList<RegexCompilationInfo> compilationInfos, string regexHash)
        {
            var descriptionAttribute = new CustomAttributeBuilder(
                typeof(AssemblyDescriptionAttribute).GetConstructor(new[] { typeof(string) }),
                new object[] { this.GetAssemblyDescription(regexHash) }
            );

            var outputPath = this.GetOutputPath(assemblyName);
            this.context.LogInfo($"Generating {outputPath}");
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            // CompileToAssembly outputs to the current directly, so we have to move it and restore it
            var originalCurrentDirectory = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = Path.GetDirectoryName(outputPath);
                Regex.CompileToAssembly(compilationInfos.ToArray(), assemblyName, new[] { descriptionAttribute });
            }
            catch (ArgumentException) when (this.TryGetRegexParseError(compilationInfos))
            {
                return null;
            }
            catch (Exception ex)
            {
                this.context.LogError($"Regex compilation failed with {ex.GetType()}: {ex.Message}");
                return null;
            }
            finally
            {
                Environment.CurrentDirectory = originalCurrentDirectory;
            }

            return AssemblyDefinition.ReadAssembly(outputPath);
        }

        private bool TryGetRegexParseError(IEnumerable<RegexCompilationInfo> compilationInfos)
        {
            var foundRegexError = false;
            foreach (var regex in compilationInfos)
            {
                try
                {
                    new Regex(regex.Pattern, regex.Options);
                }
                catch (ArgumentException constructorException)
                {
                    this.context.LogError($"Invalid regex {new RegexDefinition(regex.Pattern, regex.Options)}: {constructorException.Message}");
                    foundRegexError = true;
                }
            }

            return foundRegexError;
        }

        public sealed class Result
        {
            public bool Success => this.Assembly != null;
            public AssemblyDefinition Assembly { get; set; }
            public List<RegexCompilationInfo> CompiledRegexes { get; set; }
        }
    }
}
