namespace Pulumi.Dungeon.Aws;

public sealed class PolicyXArgs : ResourceArgs
{
    public PolicyArgs? PolicyArgs { get; init; }
    public CustomResourceOptions? PolicyOptions { get; init; }
    public Input<string>? PolicyDocument { get; init; }
    public InputList<string> AttachedEntities { get => _attachedEntities ??= new InputList<string>(); init => _attachedEntities = value; }

    private InputList<string>? _attachedEntities;
}

public sealed class PolicyX : ComponentResource
{
    public PolicyX(string name, PolicyXArgs args, ComponentResourceOptions? options = null)
        : base("aws:iam/policy:PolicyX", name, args, options)
    {
        if (args is { PolicyDocument: null, PolicyArgs: null } or { PolicyDocument: { }, PolicyArgs: { } })
        {
            throw new ArgumentException($"Only one of {nameof(args.PolicyDocument)} or {nameof(args.PolicyArgs)} must be specified.", nameof(args));
        }

        var policy = new Policy(name,
            args.PolicyArgs ?? new PolicyArgs { PolicyDocument = args.PolicyDocument! },
            CustomResourceOptions.Merge(args.PolicyOptions, new CustomResourceOptions { Parent = this }));

        Arn = policy.Arn;
        Name = policy.Name;

        args.AttachedEntities.Apply(entities =>
        {
            foreach (var entity in entities)
            {
                CreateAttachment(entity);
            }
            return entities;
        });

        // ReSharper disable once UnusedLocalFunctionReturnValue
        CustomResource CreateAttachment(string entity)
        {
            var match = Regex.Match(entity, @"^(?<entityType>group|user|role)/(?<entityName>.+)$");
            if (!match.Success)
            {
                throw new InvalidOperationException($"Invalid entity: {entity}. Prefix with 'group/', 'user/' or 'role/' for entity type.");
            }

            var entityType = match.Groups["entityType"].Value;
            var entityName = match.Groups["entityName"].Value;

            return entityType switch
            {
                "group" => new GroupPolicyAttachment($"{name}-{entityName}",
                    new GroupPolicyAttachmentArgs { Group = entityName, PolicyArn = policy.Arn },
                    new CustomResourceOptions { Parent = this }),
                "user" => new UserPolicyAttachment($"{name}-{entityName}",
                    new UserPolicyAttachmentArgs { User = entityName, PolicyArn = policy.Arn },
                    new CustomResourceOptions { Parent = this }),
                "role" => new RolePolicyAttachment($"{name}-{entityName}",
                    new RolePolicyAttachmentArgs { Role = entityName, PolicyArn = policy.Arn },
                    new CustomResourceOptions { Parent = this }),
                _ => throw new InvalidOperationException($"Invalid entity type: {entityType}.")
            };
        }

        RegisterOutputs();
    }

    [Output]
    public Output<string> Arn { get; init; }

    [Output]
    public Output<string> Name { get; init; }
}
