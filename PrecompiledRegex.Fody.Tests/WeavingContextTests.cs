using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrecompiledRegex.Fody.Tests
{
    [TestClass]
    public class WeavingContextTests
    {
        [TestMethod]
        public void TestGuessBaseNamespace()
        {
            Assert.IsNull(WeavingContext.GuessBaseNamespace(Enumerable.Empty<string>()));

            Assert.AreEqual("Foo.Bar", WeavingContext.GuessBaseNamespace(new[] { "Foo.Bar", "Foo.Bar.Baz", "Foo.Bar", "Foo.Bar.Blah", "A" }));

            Assert.AreEqual("A", WeavingContext.GuessBaseNamespace(new[] { "A", "A.B", "B.C", "B" }));
        }
    }
}
