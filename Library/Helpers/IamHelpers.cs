using System.Collections.Generic;
using Pulumi.Aws.Iam;
using Pulumi.Aws.Iam.Inputs;

namespace Pulumi.Dungeon
{
    public static class IamHelpers
    {
        public static Output<string> AllowActionForResource(string action, Output<string> resourceArn, ProviderResource provider) =>
            AllowActionsForResource(new List<string> { action }, resourceArn, provider);

        public static Output<string> AllowActionsForResource(List<string> actions, Output<string> resourceArn, ProviderResource provider) =>
            resourceArn.Apply(arn => GetPolicyDocument.InvokeAsync(
                new GetPolicyDocumentArgs
                {
                    Statements =
                    {
                        new GetPolicyDocumentStatementArgs
                        {
                            Effect = "Allow",
                            Actions = actions,
                            Resources = { arn }
                        }
                    }
                },
                new InvokeOptions { Provider = provider })
            ).Apply(policy => policy.Json);

        public static Output<string> AssumeRoleForAccount(string accountId, ProviderResource provider) =>
            Output.Create(GetPolicyDocument.InvokeAsync(
                new GetPolicyDocumentArgs
                {
                    Statements =
                    {
                        new GetPolicyDocumentStatementArgs
                        {
                            Effect = "Allow",
                            Principals =
                            {
                                new GetPolicyDocumentStatementPrincipalArgs
                                {
                                    Type = "AWS",
                                    Identifiers = { $"arn:aws:iam::{accountId}:root" }
                                }
                            },
                            Actions = { "sts:AssumeRole" }
                        }
                    }
                },
                new InvokeOptions { Provider = provider })
            ).Apply(policy => policy.Json);

        public static Output<string> AssumeRoleForService(string service, ProviderResource provider) =>
            Output.Create(GetPolicyDocument.InvokeAsync(
                new GetPolicyDocumentArgs
                {
                    Statements =
                    {
                        new GetPolicyDocumentStatementArgs
                        {
                            Effect = "Allow",
                            Principals =
                            {
                                new GetPolicyDocumentStatementPrincipalArgs
                                {
                                    Type = "Service",
                                    Identifiers = { service }
                                }
                            },
                            Actions = { "sts:AssumeRole" }
                        }
                    }
                },
                new InvokeOptions { Provider = provider })
            ).Apply(policy => policy.Json);

        public static Output<string> AssumeRoleForServiceAccount(Output<string> oidcArn, Output<string> oidcUrl, string saNamespace, string saName, ProviderResource provider) =>
            Output.Tuple(oidcArn, oidcUrl).Apply(((string OidcArn, string OidcUrl) tuple) => GetPolicyDocument.InvokeAsync(
                new GetPolicyDocumentArgs
                {
                    Statements =
                    {
                        new GetPolicyDocumentStatementArgs
                        {
                            Effect = "Allow",
                            Principals =
                            {
                                new GetPolicyDocumentStatementPrincipalArgs
                                {
                                    Type = "Federated",
                                    Identifiers = { tuple.OidcArn }
                                }
                            },
                            Actions = { "sts:AssumeRoleWithWebIdentity" },
                            Conditions =
                            {
                                new GetPolicyDocumentStatementConditionArgs
                                {
                                    Test = "StringEquals",
                                    Values = { $"system:serviceaccount:{saNamespace}:{saName}" },
                                    Variable = $"{tuple.OidcUrl}:sub"
                                }
                            }
                        }
                    }
                },
                new InvokeOptions { Provider = provider })
            ).Apply(policy => policy.Json);
    }
}
