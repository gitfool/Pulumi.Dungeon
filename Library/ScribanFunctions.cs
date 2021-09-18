using System.Threading.Tasks;
using Pulumi.Utilities;
using Scriban.Runtime;

namespace Pulumi.Dungeon
{
    public sealed class PulumiFunctions : ScriptObject
    {
        public static Task<string> Output(Output<string> output) => OutputUtilities.GetValueAsync(output);
    }

    internal sealed class ScribanFunctions : ScriptObject
    {
        public ScribanFunctions() : base(1, false)
        {
            ((ScriptObject)Default.Clone(true)).CopyTo(this);
        }

        private static readonly ScriptObject Default = new DefaultFunctions();

        private class DefaultFunctions : ScriptObject
        {
            public DefaultFunctions() : base(1, false)
            {
                SetValue("pulumi", new PulumiFunctions(), true);
            }
        }
    }
}
