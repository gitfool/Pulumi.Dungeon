using System;
using System.Collections.Generic;
using System.Linq;
using Amazon;
using Amazon.AutoScaling;
using Amazon.AutoScaling.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pulumi.Aws.Ec2;
using Pulumi.Aws.Ec2.Inputs;
using Pulumi.Aws.Eks;
using Pulumi.Aws.Eks.Inputs;
using Pulumi.Aws.Iam;
using Pulumi.Aws.Ssm;
using Pulumi.Dungeon.K8s;
using Pulumi.Tls;
using LaunchTemplate = Pulumi.Aws.Ec2.LaunchTemplate;
using Tag = Amazon.AutoScaling.Model.Tag;

namespace Pulumi.Dungeon.Aws
{
    public sealed class EksStack : StackBase<EksStack>
    {
        public EksStack(IOptions<Config> options, ILogger<EksStack> logger) : base(options, logger)
        {
            var vpcStack = CreateStackReference(Resources.AwsVpc);
            var publicSubnetIds = vpcStack.RequireOutputArray<string>("PublicSubnetIds");
            var privateSubnetIds = vpcStack.RequireOutputArray<string>("PrivateSubnetIds");
            var vpnSgId = vpcStack.GetOutput<string>("VpnSgId");

            var awsProvider = CreateAwsProvider();
            var awsEksPrefix = GetPrefix(Resources.AwsEks);

            // iam
            Logger.LogDebug("Creating iam roles");
            var clusterRole = new RoleX($"{awsEksPrefix}-cluster",
                new RoleXArgs
                {
                    AssumeRolePolicy = IamHelpers.AssumeRoleForService("eks.amazonaws.com", awsProvider),
                    AttachedPolicies = { ["cluster"] = "arn:aws:iam::aws:policy/AmazonEKSClusterPolicy" }
                },
                new ComponentResourceOptions { Provider = awsProvider });
            var nodeRole = new RoleX($"{awsEksPrefix}-node",
                new RoleXArgs
                {
                    AssumeRolePolicy = IamHelpers.AssumeRoleForService("ec2.amazonaws.com", awsProvider),
                    AttachedPolicies =
                    {
                        ["policy"] = "arn:aws:iam::aws:policy/AmazonEKSWorkerNodePolicy",
                        ["ecr"] = "arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryReadOnly"
                    }
                },
                new ComponentResourceOptions { Provider = awsProvider });

            // cluster
            Logger.LogDebug("Creating eks cluster");
            var cluster = new Cluster($"{awsEksPrefix}-cluster",
                new ClusterArgs
                {
                    EnabledClusterLogTypes = AwsConfig.Eks.LogTypes,
                    RoleArn = clusterRole.Arn,
                    Version = K8sConfig.Version,
                    VpcConfig = new ClusterVpcConfigArgs
                    {
                        EndpointPrivateAccess = true,
                        SubnetIds = Output.All(publicSubnetIds, privateSubnetIds).Flatten()
                    }
                },
                new CustomResourceOptions { Protect = true, Provider = awsProvider });

            ClusterName = cluster.Name;
            KubeConfig = cluster.GetKubeConfig(EnvName, AwsConfig.Iam.DeployerRoleArn);
            var clusterSgId = cluster.VpcConfig.Apply(config => config.ClusterSecurityGroupId!);

            // root ca thumbprint; https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_providers_create_oidc_verify-thumbprint.html
            var issuer = cluster.Identities.Apply(identities => identities[0].Oidcs[0].Issuer!);
            var certificates = issuer.Apply(url => GetCertificate.InvokeAsync(new GetCertificateArgs { Url = url, VerifyChain = true }));
            var rootCAThumbprint = certificates.Apply(chain => chain.Certificates[0].Sha1Fingerprint);

            // oidc provider
            Logger.LogDebug("Creating oidc provider");
            var oidcProvider = new OpenIdConnectProvider($"{awsEksPrefix}-oidc",
                new OpenIdConnectProviderArgs
                {
                    ClientIdLists = { "sts.amazonaws.com" },
                    Url = issuer,
                    ThumbprintLists = { rootCAThumbprint }
                },
                new CustomResourceOptions { Provider = awsProvider });

            OidcArn = oidcProvider.Arn;
            OidcUrl = oidcProvider.Url;

            // addons
            Logger.LogDebug("Creating eks addons");
            foreach (var addon in AwsConfig.Eks.Addons.Values)
            {
                if (addon.Name == "vpc-cni")
                {
                    var role = new RoleX($"{awsEksPrefix}-addons-{addon.Name}",
                        new RoleXArgs
                        {
                            AssumeRolePolicy = IamHelpers.AssumeRoleForServiceAccount(OidcArn, OidcUrl, "kube-system", "aws-node", awsProvider),
                            AttachedPolicies = { ["policy"] = "arn:aws:iam::aws:policy/AmazonEKS_CNI_Policy" }
                        },
                        new ComponentResourceOptions { Provider = awsProvider });

                    new Addon($"{awsEksPrefix}-addons-{addon.Name}",
                        new AddonArgs
                        {
                            ClusterName = cluster.Name,
                            AddonName = addon.Name,
                            AddonVersion = addon.Version,
                            ResolveConflicts = addon.ResolveConflicts ?? "OVERWRITE",
                            ServiceAccountRoleArn = role.Arn
                        },
                        new CustomResourceOptions { Provider = awsProvider });
                }
                else
                {
                    new Addon($"{awsEksPrefix}-addons-{addon.Name}",
                        new AddonArgs
                        {
                            ClusterName = cluster.Name,
                            AddonName = addon.Name,
                            AddonVersion = addon.Version,
                            ResolveConflicts = addon.ResolveConflicts ?? "OVERWRITE"
                        },
                        new CustomResourceOptions { Provider = awsProvider });
                }
            }

            // aws auth; must be created before node groups!
            Logger.LogDebug("Creating aws auth");
            var k8sProvider = CreateK8sProvider(KubeConfig);
            var awsAuth = K8sStack.AwsAuth(k8sProvider, AwsConfig.Iam.DeployerRoleArn, nodeRole);

            // node groups
            Logger.LogDebug("Creating eks nodes");
            foreach (var nodeGroup in AwsConfig.Eks.NodeGroups.Values)
            {
                // optimized ami; https://docs.aws.amazon.com/eks/latest/userguide/retrieve-ami-id.html
                var imageId = Output.Create(GetParameter.InvokeAsync(
                    new GetParameterArgs { Name = $"/aws/service/eks/optimized-ami/{K8sConfig.Version}/amazon-linux-2/recommended/image_id" },
                    new InvokeOptions { Provider = awsProvider }));

                // user data
                var kubeletExtraArgs = "--allowed-unsafe-sysctls=net.ipv4.ip_unprivileged_port_start";
                var userData = Output.Tuple(cluster.Name, cluster.Endpoint, cluster.CertificateAuthority.Apply(ca => ca.Data!))
                    .Apply(((string ClusterName, string ClusterEndpoint, string ClusterCa) tuple) =>
                        RenderTemplate("EksUserData.sh", ReadResource, new { tuple.ClusterName, tuple.ClusterEndpoint, tuple.ClusterCa, K8sConfig.ContainerRuntime, kubeletExtraArgs }));

                // launch template; https://docs.aws.amazon.com/eks/latest/userguide/launch-templates.html
                var launchTemplate = new LaunchTemplate($"{awsEksPrefix}-nodes-{nodeGroup.Name}",
                    new LaunchTemplateArgs
                    {
                        ImageId = imageId.Apply(parameter => parameter.Value),
                        InstanceType = nodeGroup.InstanceType ?? AwsConfig.Ec2.InstanceType,
                        KeyName = nodeGroup.KeyName ?? AwsConfig.Ec2.KeyName,
                        MetadataOptions = new LaunchTemplateMetadataOptionsArgs { HttpEndpoint = "enabled", HttpPutResponseHopLimit = 2 },
                        Monitoring = new LaunchTemplateMonitoringArgs { Enabled = nodeGroup.Monitoring ?? AwsConfig.Ec2.Monitoring },
                        TagSpecifications =
                        {
                            new LaunchTemplateTagSpecificationArgs
                            {
                                ResourceType = "instance",
                                Tags = GetDefaultTags(new Dictionary<string, string> { ["Name"] = $"{awsEksPrefix}-node-{nodeGroup.Name}" })
                            }
                        },
                        UpdateDefaultVersion = true,
                        UserData = userData.Apply(script => script.ToBase64()),
                        VpcSecurityGroupIds = vpnSgId != null ? new[] { clusterSgId, vpnSgId! } : new[] { clusterSgId }
                    },
                    new CustomResourceOptions { Provider = awsProvider });

                // node group; https://docs.aws.amazon.com/eks/latest/userguide/managed-node-groups.html
                var managedNodeGroup = new NodeGroup($"{awsEksPrefix}-nodes-{nodeGroup.Name}",
                    new NodeGroupArgs
                    {
                        ClusterName = cluster.Name,
                        LaunchTemplate = new NodeGroupLaunchTemplateArgs
                        {
                            Id = launchTemplate.Id,
                            Version = launchTemplate.LatestVersion.Apply(version => version.ToString())
                        },
                        NodeRoleArn = nodeRole.Arn,
                        SubnetIds = privateSubnetIds,
                        ScalingConfig = new NodeGroupScalingConfigArgs
                        {
                            DesiredSize = nodeGroup.AutoScaling.DesiredCapacity,
                            MinSize = nodeGroup.AutoScaling.MinSize,
                            MaxSize = nodeGroup.AutoScaling.MaxSize
                        },
                        Labels = { ["role"] = nodeGroup.Name },
                        Taints = nodeGroup.Tainted
                            ? new NodeGroupTaintArgs[]
                            {
                                new() { Key = "role", Value = nodeGroup.Name, Effect = "NO_EXECUTE" },
                                new() { Key = "role", Value = nodeGroup.Name, Effect = "NO_SCHEDULE" }
                            }
                            : Array.Empty<NodeGroupTaintArgs>(),
                        UpdateConfig = new NodeGroupUpdateConfigArgs { MaxUnavailable = 2 }
                    },
                    new CustomResourceOptions { DependsOn = awsAuth, Protect = true, Provider = awsProvider });

                // patch node group asg tags for cluster autoscaler; workaround https://github.com/aws/containers-roadmap/issues/608
                managedNodeGroup.Resources.WhenRun(async resources =>
                {
                    var client = new AmazonAutoScalingClient(RegionEndpoint.GetBySystemName(AwsConfig.Region));
                    var asgs = resources.SelectMany(resource => resource.AutoscalingGroups).ToArray();
                    foreach (var asg in asgs)
                    {
                        var describeRequest = new DescribeTagsRequest
                        {
                            Filters =
                            {
                                new Filter
                                {
                                    Name = "auto-scaling-group",
                                    Values = { asg.Name }
                                },
                                new Filter
                                {
                                    Name = "key",
                                    Values =
                                    {
                                        "k8s.io/cluster-autoscaler/node-template/label/role",
                                        "k8s.io/cluster-autoscaler/node-template/taint/role"
                                    }
                                }
                            }
                        };
                        var describeResponse = await client.DescribeTagsAsync(describeRequest);

                        if (describeResponse.Tags.Count != 2)
                        {
                            Logger.LogDebug("Patching node group asg tags");
                            var createRequest = new CreateOrUpdateTagsRequest
                            {
                                Tags =
                                {
                                    new Tag
                                    {
                                        ResourceType = "auto-scaling-group",
                                        ResourceId = asg.Name,
                                        Key = "k8s.io/cluster-autoscaler/node-template/label/role",
                                        Value = nodeGroup.Name,
                                        PropagateAtLaunch = false
                                    },
                                    new Tag
                                    {
                                        ResourceType = "auto-scaling-group",
                                        ResourceId = asg.Name,
                                        Key = "k8s.io/cluster-autoscaler/node-template/taint/role",
                                        Value = "NoSchedule",
                                        PropagateAtLaunch = false
                                    }
                                }
                            };
                            await client.CreateOrUpdateTagsAsync(createRequest);
                        }
                    }
                });
            }
        }

        [Output]
        public Output<string> ClusterName { get; init; }

        [Output]
        public Output<string> KubeConfig { get; init; }

        [Output]
        public Output<string> OidcArn { get; init; }

        [Output]
        public Output<string> OidcUrl { get; init; }
    }
}
