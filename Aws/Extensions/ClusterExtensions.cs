using System;
using Pulumi.Aws.Eks;
using Pulumi.Dungeon.Extensions;
using Pulumi.Dungeon.K8s;

namespace Pulumi.Dungeon.Aws
{
    public static class ClusterExtensions
    {
        public static Output<string> GetKubeConfig(this Cluster cluster, string envName, string deployerRoleArn) =>
            Output.Format($@"apiVersion: v1
clusters:
  - name: {envName}
    cluster:
      server: {cluster.WaitForApiServer(TimeSpan.FromMinutes(5))}
      certificate-authority-data: {cluster.CertificateAuthority.Apply(ca => ca.Data!)}
contexts:
  - name: {envName}
    context:
      cluster: {envName}
      user: {envName}
      namespace: default
current-context: {envName}
kind: Config
users:
  - name: {envName}
    user:
      exec:
        apiVersion: client.authentication.k8s.io/v1alpha1
        command: aws-iam-authenticator
        args:
        - token
        - --cluster-id
        - {cluster.Name}
        - --role
        - {deployerRoleArn}
").Apply(Output.CreateSecret);

        private static Output<string> WaitForApiServer(this Cluster cluster, TimeSpan timeout) =>
            cluster.Endpoint.WhenRun(endpoint => new ApiServer(endpoint).WaitForHealthzAsync(timeout));
    }
}
