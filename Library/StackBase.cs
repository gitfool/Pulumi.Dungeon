using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pulumi.Aws.Inputs;
using Pulumi.Kubernetes.Types.Inputs.Provider;
using AwsProvider = Pulumi.Aws.Provider;
using AwsProviderArgs = Pulumi.Aws.ProviderArgs;
using K8sProvider = Pulumi.Kubernetes.Provider;
using K8sProviderArgs = Pulumi.Kubernetes.ProviderArgs;

namespace Pulumi.Dungeon
{
    public abstract class StackBase<TStack> : Stack where TStack : Stack
    {
        protected StackBase(IOptions<Config> options, ILogger logger)
        {
            Config = options.Value;
            Logger = logger;
        }

        protected static string ReadResource(string name) => typeof(TStack).Assembly.ReadResource(typeof(TStack), "Resources", name);

        protected static Output<string> RenderTemplate(string name, string text, object model) => RenderTemplate(name, _ => text, model);

        protected static Output<string> RenderTemplate(string name, Func<string, string> text, object model) => Output.Create(Scriban.RenderAsync(name, text, model).AsTask());

        protected AwsProvider CreateAwsProvider() =>
            new($"{EnvName}-aws",
                new AwsProviderArgs
                {
                    AssumeRole = new ProviderAssumeRoleArgs { RoleArn = AwsConfig.Iam.DeployerRoleArn },
                    DefaultTags = new ProviderDefaultTagsArgs { Tags = GetDefaultTags() },
                    Region = AwsConfig.Region
                });

        protected K8sProvider CreateK8sProvider(Output<string> kubeConfig) =>
            new($"{EnvName}-k8s",
                new K8sProviderArgs
                {
                    KubeConfig = kubeConfig,
                    KubeClientSettings = new KubeClientSettingsArgs { Qps = 50, Burst = 100 },
                    SuppressDeprecationWarnings = true,
                    SuppressHelmHookWarnings = true,
                    HelmReleaseSettings = new HelmReleaseSettingsArgs { SuppressBetaWarning = true }
                });

        protected StackReference CreateStackReference(Stacks stack) => new($"{Config.Pulumi.Organization.Name}/{stack.ToName()}/{EnvName}");

        protected Dictionary<string, string> GetDefaultTags() => new() { ["Environment"] = EnvDisplayName };

        protected Dictionary<string, string> GetDefaultTags(Dictionary<string, string> tags) => GetDefaultTags().Concat(tags).ToDictionary(entry => entry.Key, entry => entry.Value);

        protected string GetPrefix(Stacks stack) => $"{EnvName}-{stack.ToName()}";

        protected string EnvName => EnvConfig.Name;
        protected string EnvDisplayName => EnvConfig.DisplayName;

        protected EnvironmentConfig EnvConfig => Config.Environment;
        protected AwsConfig AwsConfig => EnvConfig.Aws;
        protected K8sConfig K8sConfig => EnvConfig.K8s;

        protected Config Config { get; }
        protected ILogger Logger { get; }
    }
}
