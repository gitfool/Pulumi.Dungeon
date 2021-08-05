using k8s;
using k8s.KubeConfigModels;

namespace Pulumi.Dungeon.K8s
{
    public static class K8sClient
    {
        public static k8s.Kubernetes FromKubeConfig(string kubeConfig) =>
            new(KubernetesClientConfiguration.BuildConfigFromConfigObject(Yaml.LoadFromString<K8SConfiguration>(kubeConfig)));
    }
}
