using System;
using System.Collections.Immutable;

namespace Pulumi.Dungeon
{
    public static class ImmutableExtensions
    {
        public static ImmutableDictionary<string, object> MutateWhenKind(this ImmutableDictionary<string, object> immutable, string kind, Action<ImmutableDictionary<string, object>.Builder> mutator) =>
            Mutate(immutable, resource => (string)resource["kind"] == kind, mutator);

        public static ImmutableDictionary<string, object> MutateWhenName(this ImmutableDictionary<string, object> immutable, string name, Action<ImmutableDictionary<string, object>.Builder> mutator) =>
            Mutate(immutable, resource => (string)((ImmutableDictionary<string, object>)resource["metadata"])["name"] == name, mutator);

        public static ImmutableDictionary<string, object> MutateWhenNamespaceOmitted(this ImmutableDictionary<string, object> immutable, Action<ImmutableDictionary<string, object>.Builder> mutator) =>
            Mutate(immutable, resource => !((ImmutableDictionary<string, object>)resource["metadata"]).ContainsKey("namespace"), mutator);

        public static void Exclude(this ImmutableDictionary<string, object>.Builder mutable)
        {
            mutable["apiVersion"] = "v1";
            mutable["kind"] = "List";
            mutable["items"] = Array.Empty<string>();
        }

        public static void OmitNamespace(this ImmutableDictionary<string, object>.Builder mutable) =>
            mutable["metadata"] = mutable["metadata"].Mutate(metadata => metadata.Remove("namespace"));

        public static void SetName(this ImmutableDictionary<string, object>.Builder mutable, string name) =>
            SetName(mutable, _ => name);

        public static void SetName(this ImmutableDictionary<string, object>.Builder mutable, Func<string, string> renamer) =>
            mutable["metadata"] = mutable["metadata"].Mutate(metadata => metadata["name"] = renamer((string)metadata["name"]));

        public static void SetNamespace(this ImmutableDictionary<string, object>.Builder mutable, string @namespace) =>
            mutable["metadata"] = mutable["metadata"].Mutate(metadata => metadata["namespace"] = @namespace);

        private static ImmutableDictionary<string, object> Mutate(this ImmutableDictionary<string, object> immutable, Func<ImmutableDictionary<string, object>, bool> predicate, Action<ImmutableDictionary<string, object>.Builder> mutator) =>
            predicate(immutable) ? Mutate(immutable, mutator) : immutable;

        private static ImmutableDictionary<string, object> Mutate(this object immutable, Action<ImmutableDictionary<string, object>.Builder> mutator) =>
            Mutate((ImmutableDictionary<string, object>)immutable, mutator);

        private static ImmutableDictionary<string, object> Mutate(this ImmutableDictionary<string, object> immutable, Action<ImmutableDictionary<string, object>.Builder> mutator)
        {
            var mutable = immutable.ToBuilder();
            mutator(mutable);
            return mutable.ToImmutable();
        }
    }
}
