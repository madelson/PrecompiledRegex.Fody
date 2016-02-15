using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
        public void CanGetAssembly()
        {
            Assert.IsNotNull(WeaverRunner.DefaultAssembly);

            try {
                var type = WeaverRunner.DefaultAssembly.GetType("PrecompiledRegex.Fody.RegularExpressions");
                foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                {
                    Console.WriteLine(field);
                }

                var methods = type.GetMethods().Where(m => m.ReturnType == typeof(Regex));
                var regex = (Regex)methods.Single().Invoke(null, new object[0]);
                Assert.AreEqual("a", regex.ToString());
            }
            catch (ReflectionTypeLoadException ex)
            {

            } 
        }
    }
}
