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
            regex.IsMatch("b").ShouldEqual(false);
            regex.IsMatch("a").ShouldEqual(true);
            regex.IsMatch("A").ShouldEqual(false);
            regex.Options.HasFlag(RegexOptions.Compiled).ShouldEqual(true);
        }

        public void TestRegexObjectWithOptions()
        {
            var regex = new Regex("a", RegexOptions.IgnoreCase);
            regex.IsMatch("b").ShouldEqual(false);
            regex.IsMatch("a").ShouldEqual(true);
            regex.IsMatch("A").ShouldEqual(true);
            regex.Options.HasFlag(RegexOptions.Compiled).ShouldEqual(true);
        }

        public void TestRegexObjectWithOptionsAndTimeout()
        {
            var regex = new Regex("a", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(10));
            regex.IsMatch("b").ShouldEqual(false);
            regex.IsMatch("a").ShouldEqual(true);
            regex.IsMatch("A").ShouldEqual(true);
            regex.Options.HasFlag(RegexOptions.Compiled).ShouldEqual(true);
        }
    }
    
    internal static class TestHelper
    {
        public static T ShouldEqual<T>(this T @this, T that)
        {
            if (!EqualityComparer<T>.Default.Equals(@this, that))
            {
                throw new TestFailedException($"Expected '{that}', but was '{@this}'");
            }
            return @this;
        }
    }

    internal sealed class TestFailedException : Exception
    {
        public TestFailedException(string message) : base(message) { }
    }
}
