using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PreCompiledRegex.Fody
{
    internal struct RegexDefinition : IEquatable<RegexDefinition>
    {
        public RegexDefinition(string pattern, RegexOptions options)
        {
            this.Pattern = pattern;
            // compiled is not part of the definition
            this.Options = (options & ~RegexOptions.Compiled);
        }

        public string Pattern { get; }
        public RegexOptions Options { get; }

        public bool Equals(RegexDefinition that)
        {
            return this.Pattern == that.Pattern && this.Options == that.Options;
        }

        public override bool Equals(object thatObj)
        {
            var that = thatObj as RegexDefinition?;
            return that.HasValue && this.Equals(that.Value);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<string>.Default.GetHashCode(this.Pattern)
                ^ EqualityComparer<RegexOptions>.Default.GetHashCode(this.Options);
        }

        public override string ToString() => $"({this.Pattern.Replace("\r", @"\r").Replace("\n", @"\n")}, {this.Options})";
    }
}
