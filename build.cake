#load nuget:?package=Cake.Dungeon&prerelease

Build.SetParameters
(
    title: "Pulumi.Dungeon",

    defaultLog: true,

    runBuildSolutions: true,
    runBuildPublish: true,
    runDockerBuild: true,
    runPublishToDocker: true,

    sourceDirectory: Build.Directories.Root,

    buildPublishProjectPatterns: new[] { "Cli/*.csproj" },

    buildEmbedAllSources: true,
    dockerBuildPull: true,
    dockerPushLatest: true,
    dockerPushSkipDuplicate: true,

    dockerImages: new[]
    {
        new DockerImage
        {
            Repository = "dockfool/pulumi-dungeon",
            Context = "Cli"
        }
    }
);

Build.Run();
