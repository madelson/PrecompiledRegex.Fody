using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrecompiledRegex.Fody.Tests
{
    [TestClass]
    public class ErrorAssemblyTest
    {
        [TestMethod]
        public void TestErrorAssembly()
        {
            var result = WeaverRunner.Create("ErrorAssemblyToProcess");

            Assert.AreEqual(2, result.Errors.Count);
            Assert.AreEqual(1, result.Errors.Count(e => e.Contains("Not enough )'s")));
            Assert.AreEqual(1, result.Errors.Count(e => e.Contains("argument was out of the range of valid values")));
        }
    }
}
