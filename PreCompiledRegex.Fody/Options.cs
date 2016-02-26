using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PrecompiledRegex.Fody
{
    internal sealed class Options
    {
        public NoOpBehavior NoOpBehavior { get; private set; }
        public IncludeFilter Include { get; private set; }
        
        public static bool TryParse(XElement config, out Options options, out string errorMessage)
        {
            var result = new Options();
            foreach (var attribute in config.Attributes())
            {
                var name = attribute.Name.ToString();
                if (name == nameof(options.NoOpBehavior))
                {
                    NoOpBehavior parsed;
                    if (Enum.TryParse(attribute.Value, out parsed))
                    {
                        result.NoOpBehavior = parsed;
                    }
                    else
                    {
                        errorMessage = $"Unexpected {nameof(options.NoOpBehavior)} value '{attribute.Value}': expected one of [{string.Join(", ", (NoOpBehavior[])Enum.GetValues(typeof(NoOpBehavior)))}]";
                        options = null;
                        return false;
                    }
                }
                else if (name == nameof(options.Include))
                {
                    IncludeFilter parsed;
                    if (Enum.TryParse(attribute.Value, out parsed))
                    {
                        result.Include = parsed;
                    }
                    else
                    {
                        errorMessage = $"Unexpected {nameof(options.Include)} value '{attribute.Value}': expected one of [{string.Join(", ", (IncludeFilter[])Enum.GetValues(typeof(IncludeFilter)))}]";
                        options = null;
                        return false;
                    }
                }
                else
                {
                    errorMessage = $"Unexpected attribute '{name}': expected one of [{nameof(options.NoOpBehavior)}, {nameof(options.Include)}]";
                    options = null;
                    return false;
                }
            }

            options = result;
            errorMessage = null;
            return true;
        }
    }

    internal enum NoOpBehavior
    {
        Warn = 0,
        Silent = 1,
    }

    internal enum IncludeFilter
    {
        All = 0,
        Compiled = 1,
    }
}
