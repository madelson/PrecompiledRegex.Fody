using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PrecompiledRegex.Fody.Tests
{
    [TestClass]
    public class OptionsTest
    {
        [TestMethod]
        public void TestOptionsParsing()
        {
            Options options;
            string errorMessage;

            var empty = XElement.Parse("<PrecompiledRegex />");
            Assert.AreEqual(true, Options.TryParse(empty, out options, out errorMessage), errorMessage);
            Assert.IsNull(errorMessage);
            Assert.AreEqual(NoOpBehavior.Warn, options.NoOpBehavior);
            Assert.AreEqual(IncludeFilter.All, options.Include);

            var populated = XElement.Parse("<PrecompiledRegex NoOpBehavior='Silent' Include='Compiled' />");
            Assert.AreEqual(true, Options.TryParse(populated, out options, out errorMessage), errorMessage);
            Assert.IsNull(errorMessage);
            Assert.AreEqual(NoOpBehavior.Silent, options.NoOpBehavior);
            Assert.AreEqual(IncludeFilter.Compiled, options.Include);

            var badNoOp = XElement.Parse("<PrecompiledRegex NoOpBehavior='Foo' />");
            Assert.AreEqual(false, Options.TryParse(badNoOp, out options, out errorMessage));
            Assert.IsNull(options);
            Assert.IsTrue(errorMessage.Contains("Foo"), errorMessage);

            var badInclude = XElement.Parse("<PrecompiledRegex Include='Bar' />");
            Assert.AreEqual(false, Options.TryParse(badInclude, out options, out errorMessage));
            Assert.IsNull(options);
            Assert.IsTrue(errorMessage.Contains("Bar"), errorMessage);

            var badAttribute = XElement.Parse("<PrecompileRegex x='y' />");
            Assert.AreEqual(false, Options.TryParse(badAttribute, out options, out errorMessage));
            Assert.IsNull(options);
            Assert.IsTrue(errorMessage.Contains("x"), errorMessage);
        }
    }
}
