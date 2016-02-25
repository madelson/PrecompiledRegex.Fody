using Mono.Cecil;
using PrecompiledRegex.Fody;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PrecompiledRegex.Fody.Tests
{
    internal class WeaverRunner
    {
        private static readonly Lazy<Assembly> LazyDefaultAssembly = new Lazy<Assembly>(() => Create("AssemblyToProcess").Assembly);

        public static Assembly DefaultAssembly => LazyDefaultAssembly.Value;

        public static Result Create(string projectName, string config = null)
        {
            var projectPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, $@"..\..\..\{projectName}\{projectName}.csproj"));
            var assemblyPath = Path.Combine(Path.GetDirectoryName(projectPath), $@"bin\Debug\{projectName}.dll");

            var oldAssemblies = Directory.GetFiles(Path.GetDirectoryName(assemblyPath), "*.dll");

            var newAssemblyPath = assemblyPath.Replace(".dll", "2.dll");
            File.Copy(assemblyPath, newAssemblyPath, overwrite: true);

            var moduleDefinition = ModuleDefinition.ReadModule(newAssemblyPath);

            var debugMessages = new List<string>();
            var infoMessages = new List<string>();
            var errors = new List<string>();
            var warnings = new List<string>();
            var weavingTask = new ModuleWeaver
            {
                ModuleDefinition = moduleDefinition,
                AssemblyFilePath = newAssemblyPath,
                Config = config != null ? XElement.Parse(config) : new XElement("PrecompiledRegex"),

                LogDebug = m => { debugMessages.Add(m); Console.WriteLine($"DEBUG {m}"); },
                LogInfo = m => { infoMessages.Add(m); Console.WriteLine($"INFO {m}"); },
                LogWarning = m => { warnings.Add(m); Console.WriteLine($"WARNING {m}"); },
                LogWarningPoint = (m, s) => { warnings.Add(m); Console.WriteLine($"WARNING at {s}: {m}"); },
                LogError = m => { errors.Add(m); Console.WriteLine($"ERROR {m}"); },
                LogErrorPoint = (m, s) => { errors.Add(m); Console.WriteLine($"ERROR at {s}: {m}"); },
            };

            weavingTask.Execute();
            moduleDefinition.Write(newAssemblyPath);

            // this code is needed if we keep the assembly separate instead of merging it
            //// loadfrom vs loadfile: https://msdn.microsoft.com/en-us/library/dd153782(v=vs.110).aspx
            //var otherNewAssemblies = Directory.GetFiles(Path.GetDirectoryName(assemblyPath), "*.dll")
            //    .Except(oldAssemblies.Concat(new[] { newAssemblyPath }));
            //foreach (var otherNewAssembly in otherNewAssemblies) { Assembly.LoadFrom(otherNewAssembly); }

            var assembly = Assembly.LoadFrom(newAssemblyPath);
            return new Result { Assembly = assembly, DebugMessages = debugMessages, InfoMessages = infoMessages, Warnings = warnings, Errors = errors };
        }

        public class Result
        {
            public Assembly Assembly { get; set; }
            public List<string> DebugMessages { get; set; }
            public List<string> InfoMessages { get; set; }
            public List<string> Warnings { get; set; }
            public List<string> Errors { get; set; }
        }
    }
}
