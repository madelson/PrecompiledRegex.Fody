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
            var r8 = new Regex("abc", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            ReferenceEquals(r1, r2).ShouldEqual(true);
            ReferenceEquals(r2, r3).ShouldEqual(false);
            ReferenceEquals(r3, r4).ShouldEqual(true);
            ReferenceEquals(r4, r5).ShouldEqual(false);
            ReferenceEquals(r1, r6).ShouldEqual(false);
            ReferenceEquals(r6, r7).ShouldEqual(false);
            ReferenceEquals(r1, r7).ShouldEqual(false);
            ReferenceEquals(r7, r8).ShouldEqual(true);
        }

        public void TestStaticIsMatch()
        {
            Regex.IsMatch("a", "a").ShouldEqual(true);
            Regex.IsMatch("b", "a").ShouldEqual(false);
            Regex.IsMatch("A", "a").ShouldEqual(false);
            Regex.IsMatch("abc", "a.c").ShouldEqual(true);

            Regex.IsMatch("a", "a", RegexOptions.IgnoreCase).ShouldEqual(true);
            Regex.IsMatch("b", "a", RegexOptions.IgnoreCase).ShouldEqual(false);
            Regex.IsMatch("A", "a", RegexOptions.IgnoreCase).ShouldEqual(true);
            Regex.IsMatch("abc", "a . C", RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase).ShouldEqual(true);
            Regex.IsMatch("abc", "a . C", RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase, TimeSpan.FromHours(1)).ShouldEqual(true);
        }

        public void TestStaticMatch()
        {
            Regex.Match("a", "a").Success.ShouldEqual(true);
            Regex.Match("b", "a").Success.ShouldEqual(false);
            Regex.Match("A", "a").Success.ShouldEqual(false);
            Regex.IsMatch("abc", "a.c").ShouldEqual(true);

            Regex.Match("a", "a", RegexOptions.IgnoreCase).Success.ShouldEqual(true);
            Regex.Match("b", "a", RegexOptions.IgnoreCase).Success.ShouldEqual(false);
            Regex.Match("A", "a", RegexOptions.IgnoreCase).Success.ShouldEqual(true);
            Regex.IsMatch("abc", "a . C", RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase).ShouldEqual(true);
            Regex.IsMatch("abc", "a . C", RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase, TimeSpan.FromHours(1)).ShouldEqual(true);
        }

        public void TestStaticMatches()
        {
            Regex.Matches("bbb", "a").Count.ShouldEqual(0);
            Regex.Matches("ababa", "a").Count.ShouldEqual(3);
            Regex.Matches("AbAba", "a").Count.ShouldEqual(1);

            Regex.Matches("bbb", "a", RegexOptions.IgnoreCase).Count.ShouldEqual(0);
            Regex.Matches("ababa", "a", RegexOptions.IgnoreCase).Count.ShouldEqual(3);
            Regex.Matches("AbAba", "a", RegexOptions.IgnoreCase).Count.ShouldEqual(3);
            Regex.Matches("AbAba", "a", RegexOptions.IgnoreCase, TimeSpan.FromHours(1)).Count.ShouldEqual(3);
        }

        public void TestStaticSplit()
        {
            string.Join("+", Regex.Split("1a2a3A4", "a")).ShouldEqual("1+2+3A4");
            string.Join("+", Regex.Split("1a2a3A4", "a", RegexOptions.IgnoreCase)).ShouldEqual("1+2+3+4");
            string.Join("+", Regex.Split("1a2a3A4", "a", RegexOptions.IgnoreCase, TimeSpan.FromHours(1))).ShouldEqual("1+2+3+4");
        }

        public void TestStaticReplace()
        {
            Regex.Replace("1a2a3aBa", @"\d|b", "($0)").ShouldEqual("(1)a(2)a(3)aBa");
            Regex.Replace("1a2a3aBa", @"\d|b", m => m.Value + m.Value).ShouldEqual("11a22a33aBa");

            Regex.Replace("1a2a3aBa", @"\d|b", "($0)", RegexOptions.IgnoreCase).ShouldEqual("(1)a(2)a(3)a(B)a");
            Regex.Replace("1a2a3aBa", @"\d|b", "($0)", RegexOptions.IgnoreCase, TimeSpan.FromHours(1)).ShouldEqual("(1)a(2)a(3)a(B)a");

            // NOTE: we need to factor this out for now because creating a lambda with no captures involves branching, which 
            // we don't yet support as part of argument detection
            MatchEvaluator evaluator = m => m.Value + m.Value;
            Regex.Replace("1a2a3aBa", @"\d|b", evaluator, RegexOptions.IgnoreCase).ShouldEqual("11a22a33aBBa");
            Regex.Replace("1a2a3aBa", @"\d|b", evaluator, RegexOptions.IgnoreCase, TimeSpan.FromHours(1)).ShouldEqual("11a22a33aBBa");
        }

        public void TestInitializers()
        {
            ClassWithInitializers.StaticRegex.IsMatch("ABC").ShouldEqual(true, "bad static match");
            (ClassWithInitializers.StaticRegex.GetType() == typeof(Regex)).ShouldEqual(false, "should be derived type");

            new ClassWithInitializers().InstanceRegex.IsMatch("abc").ShouldEqual(true, "bad instance match");
            ReferenceEquals(new ClassWithInitializers().InstanceRegex, new ClassWithInitializers().InstanceRegex).ShouldEqual(true, "should be re-used instance");
        }

        public void TestStaticInitializer()
        {
            ReferenceEquals(ClassWithInitializers.DeeplyNested.A, new Regex("a")).ShouldEqual(true);
            ClassWithInitializers.DeeplyNested.B.GetType().ShouldEqual(typeof(Regex));
            ReferenceEquals(ClassWithInitializers.DeeplyNested.C, ClassWithInitializers.DeeplyNested.A).ShouldEqual(true);
        }

        private const string A = "a";

        public void TestConsts()
        {
            const string LocalA = "a";

            ReferenceEquals(new Regex(A), new Regex("a")).ShouldEqual(true);
            ReferenceEquals(new Regex(LocalA), new Regex("a")).ShouldEqual(true);
            ReferenceEquals(new Regex(A, ClassWithInitializers.DeeplyNested.ConstIgnoreCase), new Regex("a", RegexOptions.IgnoreCase)).ShouldEqual(true);
            ReferenceEquals(new Regex(A + LocalA, RegexOptions.IgnorePatternWhitespace), new Regex("aa", RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled)).ShouldEqual(true);
        }

        public void TestTimeouts()
        {
            var hugeString = new string('1', 10000);
            const string NotPrimeNumberPattern = @"^1?$|^(11+?)\1+$";

            TestHelper.ShouldThrow<RegexMatchTimeoutException>(() => new Regex(NotPrimeNumberPattern, RegexOptions.None, TimeSpan.FromTicks(1)).IsMatch(hugeString));
            TestHelper.ShouldThrow<RegexMatchTimeoutException>(() => Regex.IsMatch(hugeString, @"^1?$|^(11+?)\1+$", RegexOptions.None, TimeSpan.FromTicks(1)));
            Regex.IsMatch(hugeString, NotPrimeNumberPattern, RegexOptions.None, TimeSpan.FromHours(1)).ShouldEqual(true);
        }

        private class ClassWithInitializers
        {
            public static Regex StaticRegex { get; } = new Regex("abc", RegexOptions.IgnoreCase);
            public Regex InstanceRegex { get; } = new Regex("abc");

            public class DeeplyNested
            {
                public const RegexOptions ConstIgnoreCase = RegexOptions.IgnoreCase;

                public static readonly Regex A = new Regex("a");
                public static readonly Regex B = new Regex(A.ToString(), A.Options, A.MatchTimeout);

                public static readonly Regex C;

                static DeeplyNested()
                {
                    C = new Regex("a", RegexOptions.Compiled);
                }
            }
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
