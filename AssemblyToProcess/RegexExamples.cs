using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AssemblyToProcess
{
    public class RegexExamples
    {
        public void TestRegexObjectNoOptions()
        {
            var regex = new Regex("a");
            regex.IsMatch("b").ShouldEqual(false, "b");
            regex.IsMatch("a").ShouldEqual(true, "a");
            regex.IsMatch("A").ShouldEqual(false, "A");
            (regex.GetType() == typeof(Regex)).ShouldEqual(false);
        }

        public void TestRegexObjectWithOptions()
        {
            var regex = new Regex("a", RegexOptions.IgnoreCase);
            regex.IsMatch("b").ShouldEqual(false);
            regex.IsMatch("a").ShouldEqual(true);
            regex.IsMatch("A").ShouldEqual(true);
            (regex.GetType() == typeof(Regex)).ShouldEqual(false);
        }

        public void TestRegexObjectWithOptionsAndTimeout()
        {
            var regex = new Regex("a", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(10));
            regex.IsMatch("b").ShouldEqual(false);
            regex.IsMatch("a").ShouldEqual(true);
            regex.IsMatch("A").ShouldEqual(true);
            (regex.GetType() == typeof(Regex)).ShouldEqual(false);
        }

        public void TestRegexIdentity()
        {
            var r1 = new Regex("abc");
            var r2 = new Regex("abc", RegexOptions.None);
            var r3 = new Regex("abc", RegexOptions.None, TimeSpan.FromSeconds(1));
            var r4 = new Regex("abc", RegexOptions.None, TimeSpan.FromSeconds(1));
            var r5 = new Regex("abc", RegexOptions.None, TimeSpan.FromSeconds(2));
            var r6 = new Regex("ABC", RegexOptions.IgnoreCase);
            var r7 = new Regex("abc", RegexOptions.IgnoreCase);

            ReferenceEquals(r1, r2).ShouldEqual(true);
            ReferenceEquals(r2, r3).ShouldEqual(false);
            ReferenceEquals(r3, r4).ShouldEqual(true);
            ReferenceEquals(r4, r5).ShouldEqual(false);
            ReferenceEquals(r1, r6).ShouldEqual(false);
            ReferenceEquals(r6, r7).ShouldEqual(false);
            ReferenceEquals(r1, r7).ShouldEqual(false);
        }
    }
    
    internal static class TestHelper
    {
        public static T ShouldEqual<T>(this T @this, T that, string message = null)
        {
            if (!EqualityComparer<T>.Default.Equals(@this, that))
            {
                throw new TestFailedException($"Expected '{that}', but was '{@this}'{(message != null ? ": " + message : null)}");
            }
            return @this;
        }
    }

    internal sealed class TestFailedException : Exception
    {
        public TestFailedException(string message) : base(message) { }
    }
}
