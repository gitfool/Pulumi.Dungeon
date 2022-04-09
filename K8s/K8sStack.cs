using K8sProvider = Pulumi.Kubernetes.Provider;

namespace Pulumi.Dungeon.K8s;

public sealed class K8sStack : StackBase<K8sStack>
{
    public K8sStack(IOptions<Config> options, ILogger<K8sStack> logger) : base(options, logger)
    {
        var eksStack = CreateStackReference(Stacks.AwsEks);
        var clusterName = eksStack.RequireOutput<string>("ClusterName");
        var kubeConfig = eksStack.RequireOutput<string>("KubeConfig");
        var oidcArn = eksStack.RequireOutput<string>("OidcArn");
        var oidcUrl = eksStack.RequireOutput<string>("OidcUrl");

        var awsProvider = CreateAwsProvider(AwsConfig.Iam.DeployerRoleArn);
        var k8sProvider = CreateK8sProvider(kubeConfig);
        var k8sPrefix = GetPrefix(Stacks.K8s);

        // environment namespace
        Logger.LogDebug("Creating environment namespace");
        var environmentNs = new Namespace(EnvName,
            new NamespaceArgs
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = EnvName,
                    Labels =
                    {
                        ["mesh"] = EnvName,
                        ["appmesh.k8s.aws/sidecarInjectorWebhook"] = "enabled"
                    }
                }
            },
            new CustomResourceOptions { Provider = k8sProvider });

        // environment sysctl
        Logger.LogDebug("Creating environment sysctl");
        new ConfigGroup("environment-sysctl",
            new ConfigGroupArgs { Yaml = RenderTemplate("EnvironmentSysCtl.yaml", ReadResource, new { EnvName }) },
            new ComponentResourceOptions { DependsOn = environmentNs, Provider = k8sProvider });

        // kube prometheus stack crds; https://github.com/prometheus-community/helm-charts/tree/main/charts/kube-prometheus-stack/crds
        Logger.LogDebug("Installing kube prometheus stack crds");
        var kubePrometheusStackCrds = new ConfigGroup("kube-prometheus-stack-crds",
            new ConfigGroupArgs { Yaml = ReadResource("KubePrometheusStackCrds.yaml") },
            new ComponentResourceOptions { Provider = k8sProvider });

        // fluent bit; https://github.com/fluent/helm-charts/tree/main/charts/fluent-bit
        Logger.LogDebug("Installing fluent bit");
        var fluentBitRole = new RoleX($"{k8sPrefix}-fluent-bit",
            new RoleXArgs
            {
                AssumeRolePolicy = IamHelpers.AssumeRoleForServiceAccount(oidcArn, oidcUrl, "kube-system", "fluent-bit", awsProvider),
                InlinePolicies = { ["policy"] = ReadResource("FluentBitPolicy.json") }
            },
            new ComponentResourceOptions { Provider = awsProvider });

        var fluentBitValues = fluentBitRole.Arn.Apply(roleArn =>
            new Dictionary<string, object>
            {
                ["image"] = new
                {
                    repository = K8sConfig.FluentBitImageRepository,
                    tag = K8sConfig.FluentBitImageTag,
                    pullPolicy = "IfNotPresent"
                },
                ["logLevel"] = "warning",
                ["config"] = new
                {
                    service = ReadResource("FluentBitService.ini"),
                    inputs = ReadResource("FluentBitInputs.ini"),
                    filters = ReadResource("FluentBitFilters.ini"),
                    outputs = ReadResource("FluentBitOutputs.ini"),
                    customParsers = ReadResource("FluentBitParsers.ini")
                },
                ["luaScripts"] = new Dictionary<string, string> { ["filters.lua"] = ReadResource("FluentBitFilters.lua") },
                ["priorityClassName"] = "system-cluster-critical",
                ["resources"] = new
                {
                    requests = new { cpu = "50m", memory = "50Mi" },
                    limits = new { memory = "100Mi" }
                },
                ["serviceMonitor"] = new { enabled = true },
                ["prometheusRule"] = new
                {
                    enabled = true,
                    rules = new[]
                    {
                        new
                        {
                            alert = "FluentBitNoOutputBytesProcessed",
                            expr = "rate(fluentbit_output_proc_bytes_total[5m]) == 0",
                            annotations = new
                            {
                                description = "Fluent Bit instance {{ $labels.instance }} output plugin {{ $labels.name }} has not processed any bytes for at least 15 minutes.",
                                summary = "No output bytes processed"
                            },
                            @for = "15m",
                            labels = new { severity = "critical" }
                        }
                    }
                },
                ["dashboards"] = new { enabled = true },
                ["serviceAccount"] = new { annotations = new Dictionary<string, string> { ["eks.amazonaws.com/role-arn"] = roleArn } },
                ["tolerations"] = new[]
                {
                    new { effect = "NoExecute", @operator = "Exists" },
                    new { effect = "NoSchedule", @operator = "Exists" }
                }
            }.ToDictionary()); // workaround https://github.com/pulumi/pulumi/issues/8013

        new Release("fluent-bit",
            new ReleaseArgs
            {
                Namespace = "kube-system",
                Name = "fluent-bit",
                RepositoryOpts = new RepositoryOptsArgs { Repo = "https://fluent.github.io/helm-charts" },
                Chart = "fluent-bit",
                Version = K8sConfig.FluentBitChartVersion,
                Values = fluentBitValues,
                Atomic = true
            },
            new CustomResourceOptions { DependsOn = kubePrometheusStackCrds.Ready(), Provider = k8sProvider });

        // cert manager; https://github.com/jetstack/cert-manager/tree/master/deploy/charts/cert-manager
        Logger.LogDebug("Installing cert manager");
        var certManagerRole = new RoleX($"{k8sPrefix}-cert-manager",
            new RoleXArgs
            {
                AssumeRolePolicy = IamHelpers.AssumeRoleForServiceAccount(oidcArn, oidcUrl, "cert-manager", "cert-manager", awsProvider),
                InlinePolicies = { ["policy"] = ReadResource("CertManagerPolicy.json") }
            },
            new ComponentResourceOptions { Provider = awsProvider });

        var certManagerCrds = new ConfigGroup("cert-manager-crds",
            new ConfigGroupArgs { Yaml = ReadResource("CertManagerCrds.yaml") },
            new ComponentResourceOptions { Provider = k8sProvider });

        var certManagerNs = new Namespace("cert-manager",
            new NamespaceArgs { Metadata = new ObjectMetaArgs { Name = "cert-manager" } },
            new CustomResourceOptions { Provider = k8sProvider });

        var certManagerValues = certManagerRole.Arn.Apply(roleArn =>
            new Dictionary<string, object>
            {
                ["prometheus"] = new
                {
                    enabled = true,
                    servicemonitor = new { enabled = true }
                },
                ["serviceAccount"] = new { annotations = new Dictionary<string, string> { ["eks.amazonaws.com/role-arn"] = roleArn } },
                ["cainjector"] = new
                {
                    nodeSelector = new { role = "infra" },
                    tolerations = new[] { new { key = "role", @operator = "Exists" } }
                },
                ["startupapicheck"] = new
                {
                    annotations = new Dictionary<string, string> { ["appmesh.k8s.aws/sidecarInjectorWebhook"] = "disabled" },
                    nodeSelector = new { role = "infra" },
                    tolerations = new[] { new { key = "role", @operator = "Exists" } }
                },
                ["webhook"] = new
                {
                    nodeSelector = new { role = "infra" },
                    tolerations = new[] { new { key = "role", @operator = "Exists" } }
                },
                ["nodeSelector"] = new { role = "infra" },
                ["tolerations"] = new[] { new { key = "role", @operator = "Exists" } }
            }.ToDictionary()); // workaround https://github.com/pulumi/pulumi/issues/8013

        var certManagerRelease = new Release("cert-manager",
            new ReleaseArgs
            {
                Namespace = "cert-manager",
                Name = "cert-manager",
                RepositoryOpts = new RepositoryOptsArgs { Repo = "https://charts.jetstack.io" },
                Chart = "cert-manager",
                Version = K8sConfig.CertManagerChartVersion,
                Values = certManagerValues,
                Atomic = true,
                SkipCrds = true
            },
            new CustomResourceOptions { DependsOn = { certManagerCrds.Ready(), certManagerNs, kubePrometheusStackCrds.Ready() }, Provider = k8sProvider });

        // aws load balancer controller; https://github.com/aws/eks-charts/tree/master/stable/aws-load-balancer-controller
        Logger.LogDebug("Installing aws load balancer controller");
        var awsLbcRole = new RoleX($"{k8sPrefix}-aws-load-balancer-controller",
            new RoleXArgs
            {
                AssumeRolePolicy = IamHelpers.AssumeRoleForServiceAccount(oidcArn, oidcUrl, "kube-system", "aws-load-balancer-controller", awsProvider),
                InlinePolicies = { ["policy"] = ReadResource("AwsLoadBalancerPolicy.json") }
            },
            new ComponentResourceOptions { Provider = awsProvider });

        var awsLbcCrds = new ConfigGroup("aws-load-balancer-controller-crds",
            new ConfigGroupArgs { Yaml = ReadResource("AwsLoadBalancerCrds.yaml") },
            new ComponentResourceOptions { Provider = k8sProvider });

        var awsLbcValues = Output.Tuple(clusterName, awsLbcRole.Arn).Apply(((string ClusterName, string RoleArn) tuple) =>
            new Dictionary<string, object>
            {
                ["clusterName"] = tuple.ClusterName,
                ["enableCertManager"] = true,
                ["serviceMonitor"] = new { enabled = true },
                ["serviceAccount"] = new { annotations = new Dictionary<string, string> { ["eks.amazonaws.com/role-arn"] = tuple.RoleArn } },
                ["nodeSelector"] = new { role = "infra" },
                ["tolerations"] = new[] { new { key = "role", @operator = "Exists" } }
            }.ToDictionary()); // workaround https://github.com/pulumi/pulumi/issues/8013

        new Release("aws-load-balancer-controller", // ingress records with alb.ingress.kubernetes.io annotations depend on chart finalizers
            new ReleaseArgs
            {
                Namespace = "kube-system",
                Name = "aws-load-balancer-controller",
                RepositoryOpts = new RepositoryOptsArgs { Repo = "https://aws.github.io/eks-charts" },
                Chart = "aws-load-balancer-controller",
                Version = K8sConfig.AwsLbcChartVersion,
                Values = awsLbcValues,
                Atomic = true,
                SkipCrds = true
            },
            new CustomResourceOptions { DependsOn = { awsLbcCrds.Ready(), certManagerRelease, kubePrometheusStackCrds.Ready() }, Provider = k8sProvider });

        // cluster autoscaler; https://github.com/kubernetes/autoscaler
        Logger.LogDebug("Installing cluster autoscaler");
        var clusterAutoscalerRole = new RoleX($"{k8sPrefix}-cluster-autoscaler",
            new RoleXArgs
            {
                AssumeRolePolicy = IamHelpers.AssumeRoleForServiceAccount(oidcArn, oidcUrl, "kube-system", "cluster-autoscaler", awsProvider),
                InlinePolicies = { ["policy"] = ReadResource("ClusterAutoscalerPolicy.json") }
            },
            new ComponentResourceOptions { Provider = awsProvider });

        var clusterAutoscalerValues = Output.Tuple(clusterName, clusterAutoscalerRole.Arn).Apply(((string ClusterName, string RoleArn) tuple) =>
            new Dictionary<string, object>
            {
                ["nameOverride"] = "cluster-autoscaler",
                ["image"] = new { tag = $"v{K8sConfig.ClusterAutoscalerImageTag}" },
                ["priorityClassName"] = "system-cluster-critical",
                ["serviceMonitor"] = new
                {
                    enabled = true,
                    @namespace = "kube-system",
                    selector = new { }
                },
                //["prometheusRule"] = new
                //{
                //    enabled = true,
                //    @namespace = "kube-system",
                //    rules = new[] { new { } }
                //},
                ["cloudProvider"] = "aws",
                ["autoDiscovery"] = new
                {
                    enabled = true,
                    clusterName = tuple.ClusterName
                },
                ["awsRegion"] = AwsConfig.Region,
                ["extraArgs"] = new Dictionary<string, object>
                {
                    ["v"] = 0,
                    ["balance-similar-node-groups"] = true,
                    ["expander"] = "least-waste",
                    ["skip-nodes-with-local-storage"] = false,
                    ["skip-nodes-with-system-pods"] = false
                },
                ["podAnnotations"] = new Dictionary<string, string> { ["cluster-autoscaler.kubernetes.io/safe-to-evict"] = "false" },
                ["rbac"] = new { serviceAccount = new { annotations = new Dictionary<string, string> { ["eks.amazonaws.com/role-arn"] = tuple.RoleArn } } },
                ["nodeSelector"] = new { role = "infra" },
                ["tolerations"] = new[] { new { key = "role", @operator = "Exists" } }
            }.ToDictionary()); // workaround https://github.com/pulumi/pulumi/issues/8013

        new Release("cluster-autoscaler",
            new ReleaseArgs
            {
                Namespace = "kube-system",
                Name = "cluster-autoscaler",
                RepositoryOpts = new RepositoryOptsArgs { Repo = "https://kubernetes.github.io/autoscaler" },
                Chart = "cluster-autoscaler",
                Version = K8sConfig.ClusterAutoscalerChartVersion,
                Values = clusterAutoscalerValues,
                Atomic = true
            },
            new CustomResourceOptions { DependsOn = kubePrometheusStackCrds.Ready(), Provider = k8sProvider });

        // external dns; https://github.com/bitnami/charts/tree/master/bitnami/external-dns
        Logger.LogDebug("Installing external dns");
        var externalDnsRole = new RoleX($"{k8sPrefix}-external-dns",
            new RoleXArgs
            {
                AssumeRolePolicy = IamHelpers.AssumeRoleForServiceAccount(oidcArn, oidcUrl, "kube-system", "external-dns", awsProvider),
                InlinePolicies = { ["policy"] = ReadResource("ExternalDnsPolicy.json") }
            },
            new ComponentResourceOptions { Provider = awsProvider });

        var externalDnsValues = externalDnsRole.Arn.Apply(roleArn =>
            new Dictionary<string, object>
            {
                ["logLevel"] = "info",
                ["policy"] = "sync",
                ["provider"] = "aws",
                ["registry"] = "txt",
                ["txtOwnerId"] = AwsConfig.Route53.Internal.Domain,
                ["txtSuffix"] = "-txt",
                ["metrics"] = new
                {
                    enabled = true,
                    serviceMonitor = new { enabled = true }
                },
                ["serviceAccount"] = new { annotations = new Dictionary<string, string> { ["eks.amazonaws.com/role-arn"] = roleArn } },
                ["nodeSelector"] = new { role = "infra" },
                ["tolerations"] = new[] { new { key = "role", @operator = "Exists" } }
            }.ToDictionary()); // workaround https://github.com/pulumi/pulumi/issues/8013

        new Release("external-dns",
            new ReleaseArgs
            {
                Namespace = "kube-system",
                Name = "external-dns",
                RepositoryOpts = new RepositoryOptsArgs { Repo = "https://charts.bitnami.com/bitnami" },
                Chart = "external-dns",
                Version = K8sConfig.ExternalDnsChartVersion,
                Values = externalDnsValues,
                Atomic = true
            },
            new CustomResourceOptions { DependsOn = kubePrometheusStackCrds.Ready(), Provider = k8sProvider });
    }

    internal static ConfigGroup AwsAuth(K8sProvider k8sProvider, string deployerRoleArn, RoleX nodeRole, RoleX fullAccessRole, RoleX readOnlyRole)
    {
        // aws auth
        var awsAuthYaml = Output.Tuple(nodeRole.Arn, fullAccessRole.Arn, readOnlyRole.Arn).Apply(((string NodeRoleArn, string FullAccessRoleArn, string ReadOnlyRoleArn) tuple) =>
            RenderTemplate("AwsAuth.yaml", ReadResource, new { deployerRoleArn, tuple.NodeRoleArn, tuple.FullAccessRoleArn, tuple.ReadOnlyRoleArn }));

        return new ConfigGroup("aws-auth",
            new ConfigGroupArgs { Yaml = awsAuthYaml },
            new ComponentResourceOptions { Provider = k8sProvider });
    }
}
