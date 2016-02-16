using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AssemblyToProcess
{
    public enum ThisIsAnEnum { A, B, C }

    public interface IThisIsAnInterface
    {
        int A { get; }

        object B();

        Regex C { get; set; }
    }

    public abstract class ThisClassIsAbstract
    {
        public abstract Regex A { get; set; }

        protected Regex fieldRegex;
    }
}
