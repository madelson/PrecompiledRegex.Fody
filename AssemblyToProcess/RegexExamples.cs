using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            // NOTE: we need to factor this out for now because creating a lambda with no captures involves branching, which 
            // we don't yet support as part of argument detection
            MatchEvaluator evaluator = m => m.Value + m.Value;

            Regex.Replace("1a2a3aBa", @"\d|b", "($0)").ShouldEqual("(1)a(2)a(3)aBa");
            Regex.Replace("1a2a3aBa", @"\d|b", evaluator).ShouldEqual("11a22a33aBa");

            Regex.Replace("1a2a3aBa", @"\d|b", "($0)", RegexOptions.IgnoreCase).ShouldEqual("(1)a(2)a(3)a(B)a");
            Regex.Replace("1a2a3aBa", @"\d|b", "($0)", RegexOptions.IgnoreCase, TimeSpan.FromHours(1)).ShouldEqual("(1)a(2)a(3)a(B)a");

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
            ClassWithInitializers.DeeplyNested.B.ShouldEqual("a|None");
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

            TestHelper.ShouldThrow<ArgumentOutOfRangeException>(() => new Regex("abc", RegexOptions.None, TimeSpan.FromSeconds(-2)));
        }

        public void TestComplexPattern()
        {
            var rfcEmailRegex = new Regex(@"(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*|""(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21\x23-\x5b\x5d-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])*"")@(?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?|\[(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?|[a-z0-9-]*[a-z0-9]:(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21-\x5a\x53-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])+)\])", RegexOptions.IgnoreCase);
            rfcEmailRegex.IsMatch("foo@foo.com").ShouldEqual(true);
            rfcEmailRegex.IsMatch("foo").ShouldEqual(false);
        }

        public void TestCapturing()
        {
            var regex = new Regex(@"^(?<base>[-+]?[0-9]*\.?[0-9]+)([eE](?<exponent>[-+]?[0-9]+))?$", RegexOptions.ExplicitCapture);

            var match1 = regex.Match("7");
            match1.Success.ShouldEqual(true);
            match1.Groups["base"].Success.ShouldEqual(true);
            match1.Groups["base"].Value.ShouldEqual("7");
            match1.Groups["exponent"].Success.ShouldEqual(false);

            var match2 = regex.Match("-3.5e+123");
            match2.Success.ShouldEqual(true);
            match2.Groups["base"].Success.ShouldEqual(true);
            match2.Groups["base"].Value.ShouldEqual("-3.5");
            match2.Groups["exponent"].Success.ShouldEqual(true);
            match2.Groups["exponent"].Value.ShouldEqual("+123");
        }

        const string FollowedByNonWord = @"(?=\W|$)";
        // from https://github.com/madelson/MedallionOData/blob/master/MedallionOData/Parser/ODataExpressionLanguageTokenizer.cs
        const string ODataTokenizerPattern = "(?<NullLiteral>null)"
                        + @"|(?<BinaryLiteral>(binary|X)'[A-Fa-f0-9]+')"
                        + @"|(?<BooleanLiteral>true|false)"
                        + @"|(?<DateTimeLiteral>datetime'(?<year>\d\d\d\d)-(?<month>\d\d)-(?<day>\d\d)T(?<hour>\d\d):(?<minute>\d\d)(:(?<second>\d\d)((?<fraction>\.\d+))?)?')"
                        + @"|(?<Int64Literal>-?[0-9]+L)"
                        + @"|(?<DecimalLiteral>-?[0-9]+(\.[0-9]+)?(M|m))"
                        + @"|(?<SingleLiteral>-?[0-9]+\.[0-9]+f)"
                        + @"|(?<DoubleLiteral>-?[0-9]+((\.[0-9]+)|(E[+-]?[0-9]+)))"
                        + @"|(?<Int32Literal>-?[0-9]+)"
                        + @"|(?<GuidLiteral>guid'(?<digits>[A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9]-[A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9]-[A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9]-[A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9]-[A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9]))"
                        + @"|(?<StringLiteral>'(?<chars>(''|[^'])*))'"
                        + @"|(?<Eq>eq" + FollowedByNonWord + ")"
                        + @"|(?<Ne>ne" + FollowedByNonWord + ")"
                        + @"|(?<Gt>gt" + FollowedByNonWord + ")"
                        + @"|(?<Ge>ge" + FollowedByNonWord + ")"
                        + @"|(?<Lt>lt" + FollowedByNonWord + ")"
                        + @"|(?<Le>le" + FollowedByNonWord + ")"
                        + @"|(?<And>and" + FollowedByNonWord + ")"
                        + @"|(?<Or>or" + FollowedByNonWord + ")"
                        + @"|(?<Not>not" + FollowedByNonWord + ")"
                        + @"|(?<Add>add" + FollowedByNonWord + ")"
                        + @"|(?<Sub>sub" + FollowedByNonWord + ")"
                        + @"|(?<Mul>mul" + FollowedByNonWord + ")"
                        + @"|(?<Div>div" + FollowedByNonWord + ")"
                        + @"|(?<Mod>mod" + FollowedByNonWord + ")"
                        + @"|(?<Asc>asc" + FollowedByNonWord + ")"
                        + @"|(?<Desc>desc" + FollowedByNonWord + ")"
                        + @"|(?<LeftParen>\()"
                        + @"|(?<RightParen>\))"
                        + @"|(?<Star>\*)"
                        + @"|(?<Identifier>[a-zA-z_][a-zA-Z_0-9]*)"
                        + @"|(?<WhiteSpace>\s+)"
                        + @"|(?<Comma>,)"
                        + @"|(?<Slash>/)"
                        + @"|(?<Error>.)" // matches any character not already matched
                        + @"|(?<Eof>$)"; // matches an empty string positioned at the end of the string

        public void TestPerformance()
        {
            var notPreCompiled = new Regex(PreventPrecompilation(ODataTokenizerPattern));
            Func<string, int> precompiledCheck = s => Regex.Matches(s, ODataTokenizerPattern).Count,
                staticCheck = s => Regex.Matches(s, PreventPrecompilation(ODataTokenizerPattern)).Count,
                instanceCheck = s => notPreCompiled.Matches(s).Count;

            var stopwatch = Stopwatch.StartNew();

            // warm up
            const string Expression = "concat(concat(City, ', '), Country) eq 'Berlin, Germany'";

            stopwatch.Restart();
            precompiledCheck(Expression).ShouldEqual(18);
            Console.WriteLine($"Precompiled cold start: {stopwatch.ElapsedMilliseconds}ms");

            stopwatch.Restart();
            staticCheck(Expression).ShouldEqual(18);
            Console.WriteLine($"Static cold start: {stopwatch.ElapsedMilliseconds}ms");

            stopwatch.Restart();
            instanceCheck(Expression).ShouldEqual(18);
            Console.WriteLine($"Instance cold start: {stopwatch.ElapsedMilliseconds}ms");

            stopwatch.Restart();
            new Regex(PreventPrecompilation(ODataTokenizerPattern), RegexOptions.Compiled).Matches(Expression).Count
                .ShouldEqual(18);
            Console.WriteLine($"Compiled cold start: {stopwatch.ElapsedMilliseconds}ms");

            const int Trials = 10000;
            var cases = new Dictionary<string, Func<string, int>>
            {
                { "Precompiled", precompiledCheck },
                { "Static", staticCheck },
                { "Instance", instanceCheck },
            };
            var results = new Dictionary<string, TimeSpan>();

            foreach (var kvp in cases)
            {
                stopwatch.Restart();
                var func = kvp.Value;
                for (var i = 0; i < Trials; ++i)
                {
                    func(Expression);
                }
                results.Add(kvp.Key, stopwatch.Elapsed);
            }

            foreach (var kvp in results)
            {
                Console.WriteLine($"{kvp.Key}: {kvp.Value.TotalMilliseconds:0}ms");
            }
        }



        private static string PreventPrecompilation(string s) => s;

        private class ClassWithInitializers
        {
            public static Regex StaticRegex { get; } = new Regex("abc", RegexOptions.IgnoreCase);
            public Regex InstanceRegex { get; } = new Regex("abc");

            public class DeeplyNested
            {
                public const RegexOptions ConstIgnoreCase = RegexOptions.IgnoreCase;

                public static readonly Regex A = new Regex("a");
                public static readonly string B = $"{A}|{A.Options}";

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
