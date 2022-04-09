namespace Pulumi.Dungeon.Aws;

public sealed class BootstrapStack : StackBase<BootstrapStack>
{
    public BootstrapStack(IOptions<Config> options, ILogger<BootstrapStack> logger) : base(options, logger)
    {
        var awsProvider = CreateAwsProvider();

        // iam
        Logger.LogDebug("Creating iam roles");
        var deployerRole = new RoleX(AwsConfig.Iam.DeployerRole,
            new RoleXArgs
            {
                RoleArgs = new RoleArgs
                {
                    Name = AwsConfig.Iam.DeployerRole,
                    AssumeRolePolicy = IamHelpers.AssumeRoleForAccount(AwsConfig.AccountId, awsProvider)
                },
                RoleOptions = new CustomResourceOptions { DeleteBeforeReplace = true },
                InlinePolicies = { ["policy"] = RenderTemplate("AwsDeployerPolicy.json", ReadResource, new { Aws = AwsConfig }) }
            },
            new ComponentResourceOptions { Provider = awsProvider });

        Logger.LogDebug("Creating iam policies");
        new PolicyX(AwsConfig.Iam.DeployerRole,
            new PolicyXArgs
            {
                PolicyArgs = new PolicyArgs
                {
                    Name = AwsConfig.Iam.DeployerRole,
                    PolicyDocument = IamHelpers.AllowActionForResource("sts:AssumeRole", deployerRole.Arn, awsProvider)
                },
                PolicyOptions = new CustomResourceOptions { DeleteBeforeReplace = true },
                AttachedEntities = AwsConfig.Iam.DeployerEntities.ToArray()
            },
            new ComponentResourceOptions { Provider = awsProvider });
    }
}
