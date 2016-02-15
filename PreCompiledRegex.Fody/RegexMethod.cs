using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PreCompiledRegex.Fody
{
    internal sealed class RegexMethod
    {
        private static readonly RegexMethod[] AllArray = new[]
        {
            new RegexMethod(GetConstructor(() => new Regex(default(string)))),
            new RegexMethod(GetConstructor(() => new Regex(default(string), default(RegexOptions)))),
            new RegexMethod(GetConstructor(() => new Regex(default(string), default(RegexOptions), default(TimeSpan)))),

            new RegexMethod(GetMethod(() => Regex.IsMatch(default(string), default(string)))),
            new RegexMethod(GetMethod(() => Regex.IsMatch(default(string), default(string), default(RegexOptions)))),
            new RegexMethod(GetMethod(() => Regex.IsMatch(default(string), default(string), default(RegexOptions), default(TimeSpan)))),

            new RegexMethod(GetMethod(() => Regex.Match(default(string), default(string)))),
            new RegexMethod(GetMethod(() => Regex.Match(default(string), default(string), default(RegexOptions)))),
            new RegexMethod(GetMethod(() => Regex.Match(default(string), default(string), default(RegexOptions), default(TimeSpan)))),

            new RegexMethod(GetMethod(() => Regex.Matches(default(string), default(string)))),
            new RegexMethod(GetMethod(() => Regex.Matches(default(string), default(string), default(RegexOptions)))),
            new RegexMethod(GetMethod(() => Regex.Matches(default(string), default(string), default(RegexOptions), default(TimeSpan)))),

            new RegexMethod(GetMethod(() => Regex.Split(default(string), default(string)))),
            new RegexMethod(GetMethod(() => Regex.Split(default(string), default(string), default(RegexOptions)))),
            new RegexMethod(GetMethod(() => Regex.Split(default(string), default(string), default(RegexOptions), default(TimeSpan)))),

            new RegexMethod(GetMethod(() => Regex.Replace(default(string), default(string), default(string)))),
            new RegexMethod(GetMethod(() => Regex.Replace(default(string), default(string), default(string), default(RegexOptions)))),
            new RegexMethod(GetMethod(() => Regex.Replace(default(string), default(string), default(string), default(RegexOptions), default(TimeSpan)))),

            new RegexMethod(GetMethod(() => Regex.Replace(default(string), default(string), default(MatchEvaluator)))),
            new RegexMethod(GetMethod(() => Regex.Replace(default(string), default(string), default(MatchEvaluator), default(RegexOptions)))),
            new RegexMethod(GetMethod(() => Regex.Replace(default(string), default(string), default(MatchEvaluator), default(RegexOptions), default(TimeSpan)))),
        };

        public static IReadOnlyList<RegexMethod> All => AllArray;

        private RegexMethod(MethodBase method)
        {
            this.Method = method;
            this.Parameters = method.GetParameters();
            this.PatternParameterIndex = this.FindParameterIndex(p => p.ParameterType == typeof(string) && p.Name == "pattern").Value;
            this.OptionsParameterIndex = this.FindParameterIndex(p => p.ParameterType == typeof(RegexOptions) && p.Name == "options");
            this.TimeoutParameterIndex = this.FindParameterIndex(p => p.ParameterType == typeof(TimeSpan) && p.Name == "matchTimeout");
            this.InstanceEquivalent = this.Method.IsConstructor
                ? null
                : typeof(Regex).GetMethod(
                    this.Method.Name,
                    BindingFlags.Public | BindingFlags.Instance,
                    Type.DefaultBinder,
                    this.Parameters.Where((p, index) => index != this.PatternParameterIndex && index != this.OptionsParameterIndex && index != this.TimeoutParameterIndex)
                        .Select(p => p.ParameterType)
                        .ToArray(),
                    new ParameterModifier[0]
                );
        }

        public MethodBase Method { get; }
        public MethodInfo InstanceEquivalent { get; }
        public IReadOnlyList<ParameterInfo> Parameters { get; }
        public int PatternParameterIndex { get; }
        public int? OptionsParameterIndex { get; }
        public int? TimeoutParameterIndex { get; }
        
        public bool IsEquivalentTo(MethodDefinition method)
        {
            return method.Name == this.Method.Name
                && method.DeclaringType.Name == nameof(Regex)
                && method.DeclaringType.Namespace == typeof(Regex).Namespace
                && method.Parameters.Count == this.Parameters.Count
                && method.Parameters.Zip(
                        this.Parameters, 
                        (p1, p2) => p1.Name == p2.Name
                            && p1.ParameterType.Name == p2.ParameterType.Name
                            && p1.ParameterType.Namespace == p2.ParameterType.Namespace
                    )
                    .All(b => b);
        }

        public override string ToString() => this.Method.ToString();

        private static ConstructorInfo GetConstructor(Expression<Func<Regex>> lambda) => ((NewExpression)lambda.Body).Constructor;
        private static MethodInfo GetMethod<TResult>(Expression<Func<TResult>> lambda) => ((MethodCallExpression)lambda.Body).Method;

        private int? FindParameterIndex(Func<ParameterInfo, bool> predicate)
        {
            for (var i = 0; i < this.Parameters.Count; ++i)
            {
                if (predicate(this.Parameters[i])) { return i; }
            }
            return null;
        }
    }
}
