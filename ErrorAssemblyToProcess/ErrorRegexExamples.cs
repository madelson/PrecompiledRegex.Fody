using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ErrorAssemblyToProcess
{
    public class ErrorRegexExamples
    {
        public void BasicExamples()
        {
            var patternNotConstant = new Regex(new string('a', Environment.TickCount % 4));
            var optionsNotConstant = new Regex("a", (RegexOptions)Enum.Parse(typeof(RegexOptions), "Compiled"));
            var branchingPattern = new Regex((Environment.TickCount % 2) == 0 ? "a" : "b");

            var wrappedPattern = new Regex(Wrap("a"));
            var wrappedOptions = new Regex("a", Wrap(RegexOptions.IgnoreCase));
            var multiWrapped = new Regex(Wrap(Wrap(Wrap(Wrap("a")))), Wrap(Wrap(Wrap(Wrap(RegexOptions.CultureInvariant)))));

            var badPattern = new Regex("(");
            var badOptions = new Regex("a", (RegexOptions)(-1));
        }

        private static T Wrap<T>(T value) => value;
    }
}
