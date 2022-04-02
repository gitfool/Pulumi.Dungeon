namespace Pulumi.Dungeon;

public sealed class HostTypeRegistrar : ITypeRegistrar
{
    public HostTypeRegistrar(IHostBuilder hostBuilder)
    {
        HostBuilder = hostBuilder;
    }

    public ITypeResolver Build() => new HostTypeResolver(HostBuilder.Build());

    public void Register(Type service, Type implementation)
    {
        HostBuilder.ConfigureServices((_, services) => services.AddSingleton(service, implementation));
    }

    public void RegisterInstance(Type service, object implementation)
    {
        HostBuilder.ConfigureServices((_, services) => services.AddSingleton(service, implementation));
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        HostBuilder.ConfigureServices((_, services) => services.AddSingleton(service, _ => factory()));
    }

    private IHostBuilder HostBuilder { get; }
}
