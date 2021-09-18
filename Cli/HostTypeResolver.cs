using System;
using Microsoft.Extensions.Hosting;
using Spectre.Console.Cli;

namespace Pulumi.Dungeon
{
    public sealed class HostTypeResolver : ITypeResolver, IDisposable
    {
        public HostTypeResolver(IHost host)
        {
            Host = host;
        }

        public void Dispose()
        {
            Host.Dispose();
        }

        public object? Resolve(Type? type) => type != null ? Host.Services.GetService(type) : null;

        private IHost Host { get; }
    }
}
