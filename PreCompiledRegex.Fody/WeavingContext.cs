using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PrecompiledRegex.Fody
{
    internal sealed class WeavingContext
    {
        private readonly ModuleWeaver weaver;
        private readonly Stopwatch stopwatch;

        private WeavingContext(ModuleWeaver weaver)
        {
            this.weaver = weaver;
            this.stopwatch = Stopwatch.StartNew();
        }

        public static WeavingContext TryCreate(ModuleWeaver weaver)
        {
            var context = new WeavingContext(weaver);
            Options options;
            string errorMessage;
            if (!Options.TryParse(weaver.Config, out options, out errorMessage))
            {
                context.LogError($"Config '{weaver.Config}' is invalid: {errorMessage}");
                return null;
            }

            context.Options = options;
            return context;
        }

        public Options Options { get; private set; }

        public ModuleDefinition ModuleDefinition => this.weaver.ModuleDefinition;

        public string AssemblyFilePath => this.weaver.AssemblyFilePath;

        private string _cachedGeneratedTypeNamespace;

        public string GeneratedTypeNamespace
        { 
            get
            {
                return this._cachedGeneratedTypeNamespace
                    // only count top-level types; we don't want to double-count
                    // a namespace just because it has multiple nested types
                    ?? (this._cachedGeneratedTypeNamespace = (GuessBaseNamespace(this.ModuleDefinition.Types.Select(t => t.Namespace)) ?? this.ModuleDefinition.Assembly.Name.Name) + ".PrecompiledRegex.Fody");
            }
        }

        public void LogError(string message, SequencePoint sequencePoint = null)
        {
            if (sequencePoint != null) { this.weaver.LogErrorPoint(message, sequencePoint); }
            else { this.weaver.LogError(message); }
        }

        public void LogWarning(string message, SequencePoint sequencePoint = null)
        {
            if (sequencePoint != null) { this.weaver.LogWarningPoint(message, sequencePoint); }
            else { this.weaver.LogWarning(message); }
        }

        public void LogInfo(string message) => this.weaver.LogInfo(message);

        public void LogDebug(string message) => this.weaver.LogDebug(message);

        public IDisposable Step(string message)
        {
            this.LogDebug($"STARTING {message}");
            return new StepScope(message, this);
        }

        private sealed class StepScope : IDisposable
        {
            private readonly string message;
            private TimeSpan start;
            private WeavingContext context;

            public StepScope(string message, WeavingContext context)
            {
                this.message = message;
                this.context = context;
                this.start = context.stopwatch.Elapsed;
            }

            void IDisposable.Dispose()
            {
                var context = Interlocked.Exchange(ref this.context, null);
                if (context != null)
                {
                    var duration = context.stopwatch.Elapsed - this.start;
                    context.LogDebug($"FINISHED {message} ({duration.TotalMilliseconds:0}ms)");
                }
            }
        }

        // internal for testing
        internal static string GuessBaseNamespace(IEnumerable<string> namespaces)
        {
            var prefixCounts = new Dictionary<NamespacePrefix, int>();
            foreach (var @namespace in @namespaces)
            {
                var lastPrefixCount = -1;
                foreach (var prefix in NamespacePrefix.GetPrefixes(@namespace))
                {
                    int existingCount;
                    var nextPrefixCount = prefixCounts.TryGetValue(prefix, out existingCount) ? existingCount + 1 : 1;
                    if (nextPrefixCount < lastPrefixCount)
                    {
                        // if a.b's count is < a's count, it can't ever catch up, so just break here
                        break;
                    }
                    lastPrefixCount = prefixCounts[prefix] = nextPrefixCount;
                }
            }

            // now pick the highest frequency namespace, or the assembly name if there is none
            // we use OrderBy over Max to be reproducible in terms of ties
            var baseNamespace = prefixCounts.OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key)
                .Select(kvp => kvp.Key.ToString())
                .FirstOrDefault();
            return baseNamespace;
        }

        private struct NamespacePrefix : IEquatable<NamespacePrefix>, IComparable<NamespacePrefix>
        {
            private static readonly char[] SplitChars = new[] { '.' };

            private string[] parts;
            private int length;
            
            public static IEnumerable<NamespacePrefix> GetPrefixes(string @namespace)
            {
                if (string.IsNullOrEmpty(@namespace)) { yield break; }

                var parts = @namespace.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < parts.Length; ++i)
                {
                    yield return new NamespacePrefix { parts = parts, length = i + 1 };
                }
            }

            public bool Equals(NamespacePrefix that)
            {
                if (this.length != that.length) { return false; }

                for (var i = 0; i < this.length; ++i)
                {
                    if (this.parts[i] != that.parts[i]) { return false; }
                }

                return true;
            }

            public override bool Equals(object thatObj)
            {
                var that = thatObj as NamespacePrefix?;
                return that.HasValue && this.Equals(that.Value);
            }

            public override int GetHashCode()
            {
                var hash = 0;
                for (var i = 0; i < this.length; ++i)
                {
                    hash = unchecked((3 * hash) + this.parts[i].GetHashCode());
                }
                return hash;
            }

            public override string ToString()
            {
                return string.Join(".", this.parts.Take(this.length));
            }

            int IComparable<NamespacePrefix>.CompareTo(NamespacePrefix that)
            {
                // if one is a prefix of the other, the longer one goes first
                var minLength = Math.Min(this.length, that.length);
                if (this.parts.Take(minLength).SequenceEqual(that.parts.Take(minLength)))
                {
                    return that.length.CompareTo(this.length);
                }

                // otherwise, sort alphabetically
                return this.ToString().CompareTo(that.ToString());
            }
        }
    }
}
