using System.Collections.Generic;
using Pulumi.Aws.Iam;

namespace Pulumi.Dungeon.Aws
{
    public sealed class RoleXArgs : ResourceArgs
    {
        public Input<string> AssumeRolePolicy { get; init; } = null!;
        public Dictionary<string, Input<string>> AttachedPolicies { get => _attachedPolicies ??= new Dictionary<string, Input<string>>(); init => _attachedPolicies = value; }
        public Dictionary<string, Input<string>> InlinePolicies { get => _inlinePolicies ??= new Dictionary<string, Input<string>>(); init => _inlinePolicies = value; }

        private Dictionary<string, Input<string>>? _attachedPolicies;
        private Dictionary<string, Input<string>>? _inlinePolicies;
    }

    public sealed class RoleX : ComponentResource
    {
        public RoleX(string name, RoleXArgs args, ComponentResourceOptions? options = null)
            : base("aws:iam/role:RoleX", name, args, options)
        {
            var role = new Role(name,
                new RoleArgs { AssumeRolePolicy = args.AssumeRolePolicy },
                new CustomResourceOptions { Parent = this });

            Arn = role.Arn;

            foreach (var (key, value) in args.AttachedPolicies)
            {
                new RolePolicyAttachment($"{name}-{key}",
                    new RolePolicyAttachmentArgs { Role = role.Name, PolicyArn = value },
                    new CustomResourceOptions { Parent = this });
            }

            foreach (var (key, value) in args.InlinePolicies)
            {
                new RolePolicy($"{name}-{key}",
                    new RolePolicyArgs { Role = role.Name, Policy = value },
                    new CustomResourceOptions { Parent = this });
            }

            RegisterOutputs();
        }

        [Output]
        public Output<string> Arn { get; init; }
    }
}
