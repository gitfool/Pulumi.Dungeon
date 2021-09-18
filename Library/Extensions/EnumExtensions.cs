using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulumi.Dungeon
{
    public static class EnumExtensions
    {
        public static IEnumerable<Resources> InOrder(this Resources resources, bool reverse)
        {
            var order = resources switch
            {
                Resources.All => new[] { Resources.AwsVpc, Resources.AwsEks, Resources.K8s },
                Resources.Aws => new[] { Resources.AwsVpc, Resources.AwsEks },
                Resources.AwsVpc => new[] { Resources.AwsVpc },
                Resources.AwsEks => new[] { Resources.AwsEks },
                Resources.K8s => new[] { Resources.K8s },
                _ => throw new ArgumentOutOfRangeException(nameof(resources))
            };
            return reverse ? order.Reverse() : order;
        }

        public static string ToName(this Resources resource) =>
            // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
            resource switch
            {
                Resources.Aws => "aws",
                Resources.AwsVpc => "aws-vpc",
                Resources.AwsEks => "aws-eks",
                Resources.K8s => "k8s",
                _ => throw new ArgumentOutOfRangeException(nameof(resource))
            };
    }
}
