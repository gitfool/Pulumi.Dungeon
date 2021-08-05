using System;

namespace Pulumi.Dungeon
{
    public static class EnumExtensions
    {
        public static string ToName(this Resources resource) =>
            // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
            resource switch
            {
                Resources.Aws => "aws",
                Resources.AwsEks => "aws-eks",
                Resources.K8s => "k8s",
                _ => throw new ArgumentOutOfRangeException(nameof(resource))
            };

        public static Resources[] ToOrderedArray(this Resources resource) =>
            resource switch
            {
                Resources.All => new[] { Resources.AwsEks, Resources.K8s },
                Resources.Aws => new[] { Resources.AwsEks },
                Resources.AwsEks => new[] { Resources.AwsEks },
                Resources.K8s => new[] { Resources.K8s },
                _ => throw new ArgumentOutOfRangeException(nameof(resource))
            };
    }
}
