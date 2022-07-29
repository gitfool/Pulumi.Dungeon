using AwsProvider = Pulumi.Aws.Provider;
using AwsProviderArgs = Pulumi.Aws.ProviderArgs;
using K8sProvider = Pulumi.Kubernetes.Provider;
using K8sProviderArgs = Pulumi.Kubernetes.ProviderArgs;

namespace Pulumi.Dungeon;

public abstract class StackBase<TStack> : Stack where TStack : Stack
{
    protected StackBase(IOptions<Config> options, ILogger logger) : base(new StackOptions { ResourceTransformations = { AddNameTag } })
    {
        Config = options.Value;
        Logger = logger;
    }

    protected static string ReadResource(string name) => typeof(TStack).Assembly.ReadResource(typeof(TStack), "Resources", name);

    protected static Output<string> RenderTemplate(string name, string text, object model) => RenderTemplate(name, _ => text, model);

    protected static Output<string> RenderTemplate(string name, Func<string, string> text, object model) => Output.Create(Scriban.RenderAsync(name, text, model).AsTask());

    protected AwsProvider CreateAwsProvider(string roleArn) => CreateAwsProvider(args => { args.AssumeRole = new ProviderAssumeRoleArgs { RoleArn = roleArn }; });

    protected AwsProvider CreateAwsProvider(Action<AwsProviderArgs>? configure = null, string? name = null)
    {
        var args = new AwsProviderArgs
        {
            Region = AwsConfig.Region,
            DefaultTags = new ProviderDefaultTagsArgs { Tags = DefaultTags }
        };
        configure?.Invoke(args);
        return new AwsProvider(name ?? $"{EnvName}-aws", args);
    }

    protected K8sProvider CreateK8sProvider(Output<string> kubeConfig) =>
        new($"{EnvName}-k8s",
            new K8sProviderArgs
            {
                EnableServerSideApply = true,
                KubeConfig = kubeConfig,
                KubeClientSettings = new KubeClientSettingsArgs { Qps = 50, Burst = 100 }
            });

    protected StackReference CreateStackReference(Stacks stack) => new($"{Config.Pulumi.Organization.Name}/{stack.ToName()}/{EnvName}");

    protected string GetPrefix(Stacks stack) => $"{EnvName}-{stack.ToName()}";

    private static ResourceTransformationResult? AddNameTag(ResourceTransformationArgs args)
    {
        if (args.Resource.GetResourceType().StartsWith("aws:"))
        {
            var tagsProperty = args.Args.GetType().GetProperty("Tags");
            var tags = (InputMap<string>?)tagsProperty?.GetValue(args.Args);
            tags?.Add("Name", args.Resource.GetResourceName());
        }
        return new ResourceTransformationResult(args.Args, args.Options);
    }

    protected string EnvName => EnvConfig.Name;
    protected string EnvDisplayName => EnvConfig.DisplayName;
    protected Dictionary<string, string> DefaultTags => EnvConfig.DefaultTags;

    protected EnvironmentConfig EnvConfig => Config.Environment;
    protected AwsConfig AwsConfig => EnvConfig.Aws;
    protected K8sConfig K8sConfig => EnvConfig.K8s;

    protected Config Config { get; }
    protected ILogger Logger { get; }
}
