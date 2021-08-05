using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Pulumi.Dungeon.Aws
{
    public sealed class EksStack : StackBase<EksStack>
    {
        public EksStack(IOptions<Config> options, ILogger<EksStack> logger) : base(options, logger)
        {
            var awsProvider = CreateAwsProvider();
            var awsPrefix = GetPrefix(Resources.Aws);
            var awsEksPrefix = GetPrefix(Resources.AwsEks);
            var k8sPrefix = GetPrefix(Resources.K8s);
        }
    }
}
