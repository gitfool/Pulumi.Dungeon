using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.TypeInspectors;

namespace Pulumi.Dungeon
{
    public sealed class ConfigTypeInspector : TypeInspectorSkeleton
    {
        public ConfigTypeInspector(ITypeInspector inner)
        {
            Inner = inner;
        }

        public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object? container) =>
            Inner.GetProperties(type, container).Where(property => !Regex.IsMatch(property.Name, @"Password|Secret")); // ignore secrets

        private ITypeInspector Inner { get; }
    }
}
