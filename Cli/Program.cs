namespace Pulumi.Dungeon;

public static class Program
{
    public static Task<int> Main(string[] args) =>
        CreateHostBuilder(args).RunCommandAsync(args);

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(args)
            .ConfigureServices()
            .UseSerilog();
}
