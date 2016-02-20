using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CompiledAssemblyToProcess
{
    public class CompiledOnlyRegexExamples
    {
        public void TestCompiledOnly()
        {
            const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.Compiled;
            new Regex("a", Options).ShouldEqual(new Regex("a", Options));
            ReferenceEquals(new Regex("a"), new Regex("a")).ShouldEqual(false);
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

        public static TException ShouldThrow<TException>(Action action)
            where TException : Exception
        {
            try { action(); }
            catch (TException ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                throw new TestFailedException($"Expected {typeof(TException)}, but got {ex}");
            }

            throw new TestFailedException($"Expected {typeof(TException)}, but no exception was thrown");
        }
    }

    internal sealed class TestFailedException : Exception
    {
        public TestFailedException(string message) : base(message) { }
    }
}
