using System;
using System.Collections.Generic;

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
        public AwsEc2Config Ec2 { get; init; } = new();
        public AwsEksConfig Eks { get; init; } = new();
        public AwsIamConfig Iam { get; init; } = new();
        public AwsVpcConfig Vpc { get; init; } = new();
    }

    public record AwsAutoScalingConfig
    {
        public int DesiredCapacity { get; init; }
        public int MinSize { get; init; }
        public int MaxSize { get; init; }
    }

    public record AwsEc2Config
    {
        public string InstanceType { get; init; } = null!;
        public string KeyName { get; init; } = null!;
        public bool Monitoring { get; init; }
    }

    public record AwsEksConfig
    {
        public string[] LogTypes { get; init; } = Array.Empty<string>();
        public Dictionary<string, AwsEksAddonConfig> Addons { get; init; } = new();
        public Dictionary<string, AwsEksNodeGroupConfig> NodeGroups { get; init; } = new();
    }

    public record AwsEksAddonConfig
    {
        public string Name { get; init; } = null!;
        public string Version { get; init; } = null!;
        public string? ResolveConflicts { get; init; }
    }

    public record AwsEksNodeGroupConfig
    {
        public string Name { get; init; } = null!;
        public string? InstanceType { get; init; }
        public string? KeyName { get; init; }
        public bool? Monitoring { get; init; }
        public bool Tainted { get; init; }
        public AwsAutoScalingConfig AutoScaling { get; init; } = new();
    }

    public record AwsIamConfig
    {
        public string DeployerRoleArn { get; init; } = null!;
    }

    public record AwsVpcConfig
    {
        public string CidrBlock { get; init; } = null!;
        public int? MaxAvailabilityZones { get; init; }
        public string? TransitGatewayId { get; init; }
        public string? VpnCidrBlock { get; init; }
    }

    public record K8sConfig
    {
        public string Version { get; init; } = null!;
        public string ContainerRuntime { get; init; } = null!;
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
