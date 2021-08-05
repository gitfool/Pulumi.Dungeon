using System;
using System.Threading.Tasks;

namespace Pulumi.Dungeon.Extensions
{
    public static class OutputExtensions
    {
        public static Output<T> As<T>(this Output<object> output) => output.Apply(x => (T)x);

        public static Output<Resource> AsResource<T>(this Output<T> output) where T : Resource => output.Apply(x => (Resource)x);

        public static Output<T> WhenRun<T>(this Output<T> output, Func<T, Task> func) =>
            !Deployment.Instance.IsDryRun ? output.Apply(async value => { await func(value); return value; }) : output;
    }
}
