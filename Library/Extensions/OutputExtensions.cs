namespace Pulumi.Dungeon;

public static class OutputExtensions
{
    public static Output<ImmutableArray<U>> ApplyForEach<T, U>(this Output<ImmutableArray<T>> output, Func<T, U> func) => output.Apply(array => array.Select(func).ToImmutableArray());

    public static Output<Resource> AsResource<T>(this Output<T> output) where T : Resource => output.Apply(resource => (Resource)resource);

    public static Output<ImmutableArray<Resource>> AsResources<T>(this Output<ImmutableArray<T>> output) where T : Resource => output.ApplyForEach(resource => (Resource)resource);

    public static Output<ImmutableArray<T>> Flatten<T>(this Output<ImmutableArray<ImmutableArray<T>>> output) => output.Apply(arrays => arrays.SelectMany(array => array).ToImmutableArray());

    public static int GetLength(this Output<ImmutableArray<string>> output) => OutputUtilities.GetValueAsync(output.Apply(array => array.Length)).GetAwaiter().GetResult();

    public static Output<T> WhenRun<T>(this Output<T> output, Func<T, Task> func) => !Deployment.Instance.IsDryRun
        ? output.Apply(async value =>
        {
            await func(value);
            return value;
        })
        : output;
}
