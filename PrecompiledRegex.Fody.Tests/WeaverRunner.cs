using Mono.Cecil;
using PreCompiledRegex.Fody;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PrecompiledRegex.Fody.Tests
{
    internal class WeaverRunner
    {
        private static readonly Lazy<Assembly> LazyDefaultAssembly = new Lazy<Assembly>(Create);

        public static Assembly DefaultAssembly => LazyDefaultAssembly.Value;

        public static Assembly Create()
        {
            var projectPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, @"..\..\..\AssemblyToProcess\AssemblyToProcess.csproj"));
            var assemblyPath = Path.Combine(Path.GetDirectoryName(projectPath), @"bin\Debug\AssemblyToProcess.dll");

            var oldAssemblies = Directory.GetFiles(Path.GetDirectoryName(assemblyPath), "*.dll");

            var newAssemblyPath = assemblyPath.Replace(".dll", "2.dll");
            File.Copy(assemblyPath, newAssemblyPath, overwrite: true);

            var moduleDefinition = ModuleDefinition.ReadModule(newAssemblyPath);
            var weavingTask = new ModuleWeaver
            {
                ModuleDefinition = moduleDefinition,
                AssemblyResolver = new MockAssemblyResolver(),
                AssemblyFilePath = newAssemblyPath,
            };

            weavingTask.Execute();
            moduleDefinition.Write(newAssemblyPath);

            // loadfrom vs loadfile: https://msdn.microsoft.com/en-us/library/dd153782(v=vs.110).aspx
            var otherNewAssemblies = Directory.GetFiles(Path.GetDirectoryName(assemblyPath), "*.dll")
                .Except(oldAssemblies.Concat(new[] { newAssemblyPath }));
            foreach (var otherNewAssembly in otherNewAssemblies) { Assembly.LoadFrom(otherNewAssembly); }

            return Assembly.LoadFrom(newAssemblyPath);
        }
    }
}
