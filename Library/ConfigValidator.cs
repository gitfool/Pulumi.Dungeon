namespace Pulumi.Dungeon;

public sealed class ConfigValidator : AbstractValidator<Config>
{
    public ConfigValidator()
    {
        RuleFor(config => config.Commands.Deploy.Repair).NotEmpty();

        RuleFor(config => config.Environment.Name).NotEmpty().Matches(@"^[a-z][a-z-]*$");
        RuleFor(config => config.Environment.DisplayName).NotEmpty().Matches(@"^[A-Z][A-Za-z]*$");

        RuleFor(config => config.Environment.Aws.AccountId).NotEmpty();
        RuleFor(config => config.Environment.Aws.Region).NotEmpty();
        RuleFor(config => config.Environment.Aws.Ec2.EbsVolumeType).NotEmpty();
        RuleFor(config => config.Environment.Aws.Ec2.InstanceType).NotEmpty();
        RuleFor(config => config.Environment.Aws.Ec2.KeyName).NotEmpty();
        RuleFor(config => config.Environment.Aws.Eks.Addons).ForEach(addons => addons
            .KeyNameIndexer()
            .ChildRules(validator =>
            {
                validator.RuleFor(addon => addon.Name).NotEmpty();
                validator.RuleFor(addon => addon.Version).NotEmpty();
            }));
        RuleFor(config => config.Environment.Aws.Eks.NodeGroups).ForEach(nodeGroups => nodeGroups
            .KeyNameIndexer()
            .ChildRules(validator =>
                validator.RuleFor(nodeGroup => nodeGroup.Name).NotEmpty().Matches(@"^[a-z][a-z-]*$")));
        RuleFor(config => config.Environment.Aws.Iam.DeployerRole).NotEmpty();
        RuleFor(config => config.Environment.Aws.Iam.PolicyArn).NotEmpty();
        RuleFor(config => config.Environment.Aws.Iam.RoleArn).NotEmpty();
        RuleFor(config => config.Environment.Aws.Iam.DeployerEntities).Matches(@"^(?:group|user|role)/");
        RuleFor(config => config.Environment.Aws.Iam.EksFullAccessEntities).Matches(@"^(?:group|user|role)/");
        RuleFor(config => config.Environment.Aws.Iam.EksReadOnlyEntities).Matches(@"^(?:group|user|role)/");
        RuleFor(config => config.Environment.Aws.Route53.Internal.Domain).NotEmpty();
        RuleFor(config => config.Environment.Aws.Route53.Internet.Domain).NotEmpty();
        RuleFor(config => config.Environment.Aws.Vpc.CidrBlock).NotEmpty();

        RuleFor(config => config.Environment.K8s.Version).NotEmpty();
        RuleFor(config => config.Environment.K8s.ContainerRuntime).NotEmpty().Matches(@"^containerd|dockerd$");
        RuleFor(config => config.Environment.K8s.AwsLbcChartVersion).NotEmpty();
        RuleFor(config => config.Environment.K8s.CertManagerChartVersion).NotEmpty();
        RuleFor(config => config.Environment.K8s.ClusterAutoscalerChartVersion).NotEmpty();
        RuleFor(config => config.Environment.K8s.ClusterAutoscalerImageTag).NotEmpty();
        RuleFor(config => config.Environment.K8s.ExternalDnsChartVersion).NotEmpty();
        RuleFor(config => config.Environment.K8s.FluentBitChartVersion).NotEmpty();
        RuleFor(config => config.Environment.K8s.FluentBitImageRepository).NotEmpty();
        RuleFor(config => config.Environment.K8s.FluentBitImageTag).NotEmpty();

        RuleFor(config => config.Pulumi.Color).Matches(@"^auto|always|never|raw$");
        RuleFor(config => config.Pulumi.Organization.Name).NotEmpty().Matches(@"^[a-z][a-z-]*$");
        RuleFor(config => config.Pulumi.Organization.DisplayName).NotEmpty().Matches(@"^[A-Z][A-Za-z]*$");
    }
}
