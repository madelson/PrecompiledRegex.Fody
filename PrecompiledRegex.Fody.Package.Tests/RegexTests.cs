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
            var result = Regex.IsMatch("a", "a");
            Assert.IsTrue(result);

            var result2 = new Regex("c") == new Regex("c");
            Assert.IsTrue(result2);

            var regex = new Regex("xyz", RegexOptions.IgnoreCase);
            Assert.AreEqual(2, regex.Matches("xyZ,XYz").Count);
        }
    }
}
