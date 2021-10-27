using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulumi.Dungeon
{
    public static class EnumExtensions
    {
        public static IEnumerable<Stacks> InOrder(this Stacks stacks, bool reverse)
        {
            var order = stacks switch
            {
                Stacks.All => new[] { Stacks.AwsVpc, Stacks.AwsEks, Stacks.K8s },
                Stacks.Aws => new[] { Stacks.AwsVpc, Stacks.AwsEks },
                Stacks.AwsVpc => new[] { Stacks.AwsVpc },
                Stacks.AwsEks => new[] { Stacks.AwsEks },
                Stacks.K8s => new[] { Stacks.K8s },
                _ => throw new ArgumentOutOfRangeException(nameof(stacks))
            };
            return reverse ? order.Reverse() : order;
        }

        public static string ToName(this Stacks stack) =>
            // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
            stack switch
            {
                Stacks.Aws => "aws",
                Stacks.AwsVpc => "aws-vpc",
                Stacks.AwsEks => "aws-eks",
                Stacks.K8s => "k8s",
                _ => throw new ArgumentOutOfRangeException(nameof(stack))
            };
    }
}
