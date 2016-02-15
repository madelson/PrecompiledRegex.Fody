using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    }
}
