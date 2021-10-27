using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pulumi.Aws;
using Pulumi.Aws.Ec2;
using Pulumi.Aws.Ec2.Inputs;
using Pulumi.Aws.Ec2TransitGateway;
using RouteTable = Pulumi.Aws.Ec2.RouteTable;
using RouteTableArgs = Pulumi.Aws.Ec2.RouteTableArgs;
using RouteTableAssociation = Pulumi.Aws.Ec2.RouteTableAssociation;
using RouteTableAssociationArgs = Pulumi.Aws.Ec2.RouteTableAssociationArgs;

namespace Pulumi.Dungeon.Aws
{
    public sealed class VpcStack : StackBase<VpcStack>
    {
        public VpcStack(IOptions<Config> options, ILogger<VpcStack> logger) : base(options, logger)
        {
            var awsProvider = CreateAwsProvider();
            var awsVpcPrefix = GetPrefix(Stacks.AwsVpc);

            // azs
            var azResult = GetAvailabilityZones.InvokeAsync(
                new GetAvailabilityZonesArgs(),
                new InvokeOptions { Provider = awsProvider }).GetAwaiter().GetResult();
            var azs = azResult.Names.Select((name, index) => new { Name = name, Id = azResult.ZoneIds[index] })
                .Take(AwsConfig.Vpc.MaxAvailabilityZones).ToImmutableArray();

            AvailabilityZones = Output.Create(azs.Select(az => az.Name).ToImmutableArray());

            // vpc
            Logger.LogDebug("Creating vpc");
            var vpc = new Vpc(awsVpcPrefix,
                new VpcArgs
                {
                    CidrBlock = AwsConfig.Vpc.CidrBlock,
                    EnableDnsHostnames = true,
                    Tags = { ["Name"] = awsVpcPrefix }
                },
                new CustomResourceOptions { Provider = awsProvider });

            VpcId = vpc.Id;

            var dhcpOptions = new VpcDhcpOptions(awsVpcPrefix,
                new VpcDhcpOptionsArgs
                {
                    DomainName = AwsConfig.Route53.Internal.Domain,
                    DomainNameServers = "AmazonProvidedDNS",
                    Tags = { ["Name"] = awsVpcPrefix }
                },
                new CustomResourceOptions { Provider = awsProvider });

            new VpcDhcpOptionsAssociation(awsVpcPrefix,
                new VpcDhcpOptionsAssociationArgs
                {
                    DhcpOptionsId = dhcpOptions.Id,
                    VpcId = vpc.Id
                },
                new CustomResourceOptions { Provider = awsProvider });

            // network
            Logger.LogDebug("Creating network");
            var network = IPNetwork.Parse(AwsConfig.Vpc.CidrBlock);
            var subnetTotalIps = network.Total / (2 * azs.Length); // public & private subnet per az
            var subnetCidrMask = 32 - (subnetTotalIps.GetBitLength() - 1);
            if (subnetCidrMask is < 16 or > 28)
            {
                throw new InvalidOperationException($"Subnet cidr mask should be between 16 and 28 but was {subnetCidrMask}.");
            }
            var subnets = network.Subnet((byte)subnetCidrMask);

            var internetGateway = new InternetGateway($"{awsVpcPrefix}-igw",
                new InternetGatewayArgs
                {
                    VpcId = vpc.Id,
                    Tags = { ["Name"] = $"{awsVpcPrefix}-igw" }
                },
                new CustomResourceOptions { Provider = awsProvider });

            var publicSubnetIds = new List<Output<string>>(azs.Length);
            var privateSubnetIds = new List<Output<string>>(azs.Length);

            for (var i = 0; i < azs.Length; i++)
            {
                var publicName = $"{awsVpcPrefix}-public-{i + 1}";

                var publicSubnet = new Subnet(publicName,
                    new SubnetArgs
                    {
                        AvailabilityZone = azs[i].Name,
                        CidrBlock = subnets[2 * i].Value,
                        VpcId = vpc.Id,
                        Tags =
                        {
                            ["Name"] = publicName,
                            ["kubernetes.io/role/alb-ingress"] = "", // aws load balancer controller subnet discovery
                            ["kubernetes.io/role/elb"] = ""
                        }
                    },
                    new CustomResourceOptions { Provider = awsProvider });

                publicSubnetIds.Add(publicSubnet.Id);

                var publicRoutes = AwsConfig.Vpc.VpnCidrBlock != null && AwsConfig.Vpc.TransitGatewayId != null
                    ? new[]
                    {
                        new RouteTableRouteArgs { CidrBlock = AwsConfig.Vpc.VpnCidrBlock, TransitGatewayId = AwsConfig.Vpc.TransitGatewayId },
                        new RouteTableRouteArgs { CidrBlock = "0.0.0.0/0", GatewayId = internetGateway.Id }
                    }
                    : new[] { new RouteTableRouteArgs { CidrBlock = "0.0.0.0/0", GatewayId = internetGateway.Id } };

                var publicRouteTable = new RouteTable(publicName,
                    new RouteTableArgs
                    {
                        Routes = publicRoutes,
                        VpcId = vpc.Id,
                        Tags = { ["Name"] = publicName }
                    },
                    new CustomResourceOptions { Provider = awsProvider });

                new RouteTableAssociation(publicName,
                    new RouteTableAssociationArgs { RouteTableId = publicRouteTable.Id, SubnetId = publicSubnet.Id },
                    new CustomResourceOptions { Provider = awsProvider });

                var privateName = $"{awsVpcPrefix}-private-{i + 1}";

                var privateSubnet = new Subnet(privateName,
                    new SubnetArgs
                    {
                        AvailabilityZone = azs[i].Name,
                        CidrBlock = subnets[2 * i + 1].Value,
                        VpcId = vpc.Id,
                        Tags =
                        {
                            ["Name"] = privateName,
                            ["kubernetes.io/role/alb-ingress"] = "", // aws load balancer controller subnet discovery
                            ["kubernetes.io/role/internal-elb"] = ""
                        }
                    },
                    new CustomResourceOptions { Provider = awsProvider });

                privateSubnetIds.Add(privateSubnet.Id);

                var privateEip = new Eip(privateName,
                    new EipArgs
                    {
                        Vpc = true,
                        Tags = { ["Name"] = privateName }
                    },
                    new CustomResourceOptions { Provider = awsProvider });

                var privateNatGateway = new NatGateway(privateName,
                    new NatGatewayArgs
                    {
                        AllocationId = privateEip.Id,
                        SubnetId = publicSubnet.Id,
                        Tags = { ["Name"] = privateName }
                    },
                    new CustomResourceOptions { Provider = awsProvider });

                var privateRoutes = AwsConfig.Vpc.VpnCidrBlock != null && AwsConfig.Vpc.TransitGatewayId != null
                    ? new[]
                    {
                        new RouteTableRouteArgs { CidrBlock = AwsConfig.Vpc.VpnCidrBlock, TransitGatewayId = AwsConfig.Vpc.TransitGatewayId },
                        new RouteTableRouteArgs { CidrBlock = "0.0.0.0/0", NatGatewayId = privateNatGateway.Id }
                    }
                    : new[] { new RouteTableRouteArgs { CidrBlock = "0.0.0.0/0", NatGatewayId = privateNatGateway.Id } };

                var privateRouteTable = new RouteTable(privateName,
                    new RouteTableArgs
                    {
                        Routes = privateRoutes,
                        VpcId = vpc.Id,
                        Tags = { ["Name"] = privateName }
                    },
                    new CustomResourceOptions { Provider = awsProvider });

                new RouteTableAssociation(privateName,
                    new RouteTableAssociationArgs { RouteTableId = privateRouteTable.Id, SubnetId = privateSubnet.Id },
                    new CustomResourceOptions { Provider = awsProvider });
            }

            PublicSubnetIds = Output.All(publicSubnetIds);
            PrivateSubnetIds = Output.All(privateSubnetIds);

            if (AwsConfig.Vpc.TransitGatewayId != null)
            {
                new VpcAttachment($"{awsVpcPrefix}-tgw",
                    new VpcAttachmentArgs
                    {
                        SubnetIds = privateSubnetIds,
                        TransitGatewayId = AwsConfig.Vpc.TransitGatewayId,
                        VpcId = vpc.Id,
                        Tags = { ["Name"] = $"{awsVpcPrefix}-tgw" }
                    },
                    new CustomResourceOptions { Provider = awsProvider });
            }

            // security groups
            if (AwsConfig.Vpc.VpnCidrBlock != null)
            {
                var vpnSg = new SecurityGroup($"{awsVpcPrefix}-vpn",
                    new SecurityGroupArgs
                    {
                        Ingress =
                        {
                            new SecurityGroupIngressArgs
                            {
                                Protocol = "all",
                                FromPort = 0,
                                ToPort = 0,
                                CidrBlocks = AwsConfig.Vpc.VpnCidrBlock
                            }
                        },
                        VpcId = vpc.Id
                    },
                    new CustomResourceOptions { Provider = awsProvider });

                VpnSgId = vpnSg.Id;
            }
        }

        [Output]
        public Output<string> VpcId { get; init; }

        [Output]
        public Output<ImmutableArray<string>> AvailabilityZones { get; init; }

        [Output]
        public Output<ImmutableArray<string>> PublicSubnetIds { get; init; }

        [Output]
        public Output<ImmutableArray<string>> PrivateSubnetIds { get; init; }

        [Output]
        public Output<string>? VpnSgId { get; init; }
    }
}
