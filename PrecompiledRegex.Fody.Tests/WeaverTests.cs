using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using PrecompiledRegex.Fody;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PrecompiledRegex.Fody.Tests
{
    [TestClass]
    public class WeaverTests
    {
        [TestMethod]
        public void TestVerify()
        {
            var beforeAssemblyPath = WeaverRunner.DefaultAssembly.Location.Replace("2.dll", ".dll");

            Verifier.Verify(beforeAssemblyPath: beforeAssemblyPath, afterAssemblyPath: WeaverRunner.DefaultAssembly.Location);
        }

        [TestMethod]
        public void CanGetDefaultAssembly()
        {
            Assert.IsNotNull(WeaverRunner.DefaultAssembly);
        }

        [TestMethod]
        public void TestDefaultAssembly()
        {
            var testType = WeaverRunner.DefaultAssembly.GetType("AssemblyToProcess.RegexExamples");
            Assert.IsNotNull(testType);
            var instance = Activator.CreateInstance(testType);
            foreach (var method in testType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                Console.Write($"Running {method.Name}...");
                var stopwatch = Stopwatch.StartNew();
                method.Invoke(instance, new object[0]);
                Console.WriteLine($" finished ({stopwatch.Elapsed.TotalSeconds:0.0000}s)");
            }
        }

        [TestMethod]
        public void TestDefaultAssemblyReplacesAll()
        {
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(WeaverRunner.DefaultAssembly.Location);
            var mainModule = assemblyDefinition.MainModule;

            foreach (var type in mainModule.Types)
            {
                TestTypeReplacesAll(type);
            }
        }

        private static void TestTypeReplacesAll(TypeDefinition type)
        {
            foreach (var method in type.Methods)
            {
                if (method.HasBody)
                {
                    foreach (var instruction in method.Body.Instructions)
                    {
                        var reference = instruction.Operand as MethodReference;
                        if (reference != null && reference.DeclaringType.Name == nameof(Regex))
                        {
                            var definition = reference.Resolve();
                            var regexMethod = RegexMethod.All.FirstOrDefault(rm => rm.IsEquivalentTo(definition));
                            Assert.IsNull(regexMethod, $"Found {regexMethod} in {method.FullName}");
                        }
                    }
                }
            }

            foreach (var nestedType in type.NestedTypes)
            {
                TestTypeReplacesAll(nestedType);
            }
        }
    }
}
