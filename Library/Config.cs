namespace Pulumi.Dungeon;

public sealed record Config
{
    public CommandsConfig Commands { get; init; } = new();
    public EnvironmentConfig Environment { get; init; } = new();
    public PulumiConfig Pulumi { get; init; } = new();
}

public sealed record CommandsConfig
{
    public DeployCommandConfig Deploy { get; init; } = new();
}

public sealed record DeployCommandConfig
{
    public string Repair { get; init; } = null!;
}

public sealed record EnvironmentConfig
{
    public string Name { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public Dictionary<string, string> DefaultTags { get; init; } = new();
    public AwsConfig Aws { get; init; } = new();
    public K8sConfig K8s { get; init; } = new();
}

public sealed record AwsConfig
{
    public string AccountId { get; init; } = null!;
    public string Region { get; init; } = null!;
    public AwsEc2Config Ec2 { get; init; } = new();
    public AwsEksConfig Eks { get; init; } = new();
    public AwsIamConfig Iam { get; init; } = new();
    public AwsRoute53Config Route53 { get; init; } = new();
    public AwsVpcConfig Vpc { get; init; } = new();
}

public sealed record AwsAutoScalingConfig
{
    public int DesiredCapacity { get; init; }
    public int MinSize { get; init; }
    public int MaxSize { get; init; }
}

public sealed record AwsEc2Config
{
    public int EbsVolumeSize { get; init; }
    public string EbsVolumeType { get; init; } = null!;
    public string InstanceType { get; init; } = null!;
    public Dictionary<string, string> InstanceTags { get; init; } = new();
    public string KeyName { get; init; } = null!;
    public bool Monitoring { get; init; }
}

public sealed record AwsEksConfig
{
    public string? LogTypes { get; init; }
    public Dictionary<string, AwsEksAddonConfig> Addons { get; init; } = new();
    public Dictionary<string, AwsEksNodeGroupConfig> NodeGroups { get; init; } = new();
}

public sealed record AwsEksAddonConfig
{
    public string Name { get; init; } = null!;
    public string Version { get; init; } = null!;
    public string? ResolveConflicts { get; init; }
}

public sealed record AwsEksNodeGroupConfig
{
    public string Name { get; init; } = null!;
    public string? EbsVolumeType { get; init; }
    public int? EbsVolumeSize { get; init; }
    public string? InstanceType { get; init; }
    public string? KeyName { get; init; }
    public bool? Monitoring { get; init; }
    public bool Tainted { get; init; }
    public AwsAutoScalingConfig AutoScaling { get; init; } = new();
    public AwsEksNodeGroupUpdatingConfig? Updating { get; init; }
}

public sealed record AwsEksNodeGroupUpdatingConfig
{
    public int? MaxUnavailable { get; init; }
    public int? MaxUnavailablePercentage { get; init; }
}

public sealed record AwsIamConfig
{
    public string DeployerRole { get; init; } = null!;
    public string DeployerRoleArn => $"{RoleArn}/{DeployerRole}";
    public string PolicyArn { get; init; } = null!;
    public string RoleArn { get; init; } = null!;
    public string? DeployerEntities { get; init; }
    public string? EksFullAccessEntities { get; init; }
    public string? EksReadOnlyEntities { get; init; }
}

public sealed record AwsRoute53Config
{
    public AwsRoute53ZoneConfig Internal { get; init; } = new();
    public AwsRoute53ZoneConfig Internet { get; init; } = new();
}

public sealed record AwsRoute53ZoneConfig
{
    public string Domain { get; init; } = null!;
}

public sealed record AwsVpcConfig
{
    public int MaxAvailabilityZones { get; init; }
    public string CidrBlock { get; init; } = null!;
    public string? VpnCidrBlock { get; init; }
    public string? TransitGatewayId { get; init; }
}

public sealed record K8sConfig
{
    public string Version { get; init; } = null!;
    public string ContainerRuntime { get; init; } = null!;
    public bool InstallFluentBit { get; init; }
    public string AwsLbcChartVersion { get; init; } = null!;
    public string CertManagerChartVersion { get; init; } = null!;
    public string ClusterAutoscalerChartVersion { get; init; } = null!;
    public string ClusterAutoscalerImageTag { get; init; } = null!;
    public string ExternalDnsChartVersion { get; init; } = null!;
    public string FluentBitChartVersion { get; init; } = null!;
    public string FluentBitImageRepository { get; init; } = null!;
    public string FluentBitImageTag { get; init; } = null!;
}

public sealed record PulumiConfig
{
    public string? Color { get; init; }
    public PulumiOrganizationConfig Organization { get; init; } = new();
}

public sealed record PulumiOrganizationConfig
{
    public string Name { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
}
