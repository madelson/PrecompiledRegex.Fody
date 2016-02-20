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
    }
}
