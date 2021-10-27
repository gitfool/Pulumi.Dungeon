using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pulumi.Dungeon.Aws;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Pulumi.Kubernetes.Yaml;
using K8sProvider = Pulumi.Kubernetes.Provider;

namespace Pulumi.Dungeon.K8s
{
    public sealed class K8sStack : StackBase<K8sStack>
    {
        public K8sStack(IOptions<Config> options, ILogger<K8sStack> logger) : base(options, logger)
        {
            var eksStack = CreateStackReference(Stacks.AwsEks);
            var kubeConfig = eksStack.RequireOutput<string>("KubeConfig");

            var k8sProvider = CreateK8sProvider(kubeConfig);

            // environment namespace
            Logger.LogDebug("Creating environment namespace");
            var environmentNs = new Namespace(EnvName,
                new NamespaceArgs
                {
                    Metadata = new ObjectMetaArgs
                    {
                        Name = EnvName,
                        Labels =
                        {
                            ["mesh"] = EnvName,
                            ["appmesh.k8s.aws/sidecarInjectorWebhook"] = "enabled"
                        }
                    }
                },
                new CustomResourceOptions { Provider = k8sProvider });

            // environment sysctl
            Logger.LogDebug("Creating environment sysctl");
            new ConfigGroup("environment-sysctl",
                new ConfigGroupArgs { Yaml = RenderTemplate("EnvironmentSysCtl.yaml", ReadResource, new { EnvName }) },
                new ComponentResourceOptions { DependsOn = environmentNs, Provider = k8sProvider });
        }

        internal static ConfigGroup AwsAuth(K8sProvider k8sProvider, string deployerRoleArn, RoleX nodeRole)
        {
            // aws auth
            var awsAuthYaml = nodeRole.Arn.Apply(nodeRoleArn =>
                RenderTemplate("AwsAuth.yaml", ReadResource, new { deployerRoleArn, nodeRoleArn }));

            return new ConfigGroup("aws-auth",
                new ConfigGroupArgs { Yaml = awsAuthYaml },
                new ComponentResourceOptions { Provider = k8sProvider });
        }
    }
}
