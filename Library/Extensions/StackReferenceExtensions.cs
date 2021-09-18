using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Pulumi.Dungeon
{
    public static class StackReferenceExtensions
    {
        public static Output<T?> GetOutput<T>(this StackReference stack, Input<string> name) => stack.GetOutput(name).Apply(output => (T?)output);

        public static Output<T> RequireOutput<T>(this StackReference stack, Input<string> name) => stack.RequireOutput(name).Apply(output => (T)output);

        public static Output<ImmutableArray<T>> RequireOutputArray<T>(this StackReference stack, Input<string> name) => stack.RequireOutput(name).Apply(output => ((IEnumerable<object>)output).Cast<T>().ToImmutableArray());
    }
}
