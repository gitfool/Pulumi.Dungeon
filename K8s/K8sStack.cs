using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Pulumi.Dungeon.K8s
{
    public sealed class K8sStack : StackBase<K8sStack>
    {
        public K8sStack(IOptions<Config> options, ILogger<K8sStack> logger) : base(options, logger)
        {
            var eksStack = CreateStackReference(Resources.AwsEks);
            //var kubeConfig = eksStack.RequireOutput("KubeConfig").As<string>();

            var awsProvider = CreateAwsProvider();
            //var k8sProvider = CreateK8sProvider(kubeConfig);
            var k8sPrefix = GetPrefix(Resources.K8s);
        }
    }
}
