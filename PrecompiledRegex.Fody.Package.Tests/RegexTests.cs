using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PrecompiledRegex.Fody.Package.Tests
{
    [TestClass]
    public class RegexTests
    {
        [TestMethod]
        public void BasicTest()
        {
            Assert.IsTrue(Regex.IsMatch("a", "a"));
        }
    }
}
