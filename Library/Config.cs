using System;

namespace Pulumi.Dungeon
{
    public record Config
    {
        public CommandsConfig Commands { get; init; } = new();
        public EnvironmentConfig Environment { get; init; } = new();
        public PulumiConfig Pulumi { get; init; } = new();
    }

    public record CommandsConfig
    {
        public DeployCommandConfig Deploy { get; init; } = new();
    }

    public record DeployCommandConfig
    {
        public string[] Repair { get; init; } = Array.Empty<string>();
    }

    public record EnvironmentConfig
    {
        public string Name { get; init; } = null!;
        public string DisplayName { get; init; } = null!;
        public AwsConfig Aws { get; init; } = new();
        public K8sConfig K8s { get; init; } = new();
    }

    public record AwsConfig
    {
        public string AccountId { get; init; } = null!;
        public string Region { get; init; } = null!;
        public AwsIamConfig Iam { get; init; } = new();
    }

    public record AwsIamConfig
    {
        public string DeployerRoleArn { get; init; } = null!;
    }

    public record K8sConfig
    {
        public string Version { get; init; } = null!;
    }

    public record PulumiConfig
    {
        public PulumiOrganizationConfig Organization { get; init; } = new();
    }

    public record PulumiOrganizationConfig
    {
        public string Name { get; init; } = null!;
        public string DisplayName { get; init; } = null!;
    }
}
