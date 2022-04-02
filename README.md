# Pulumi.Dungeon

[![License](https://img.shields.io/badge/license-MIT-blue.svg?label=License&logo=github)](LICENSE)
[![GitHub Actions](https://img.shields.io/github/workflow/status/gitfool/Pulumi.Dungeon/ci/main?label=GitHub%20Actions&logo=github)](https://github.com/gitfool/Pulumi.Dungeon/actions)
[![Docker Pulls](https://img.shields.io/docker/pulls/dockfool/pulumi-dungeon.svg?label=Docker&logo=docker)](https://hub.docker.com/r/dockfool/pulumi-dungeon/tags)

`Pulumi.Dungeon` is a playground for Pulumi devops tools. See [pulumi.com](https://pulumi.com).

## Build environment on Windows with [Visual Studio](https://visualstudio.microsoft.com/vs/)

* Use [Chocolatey](https://chocolatey.org) to install [Pulumi](https://github.com/pulumi/pulumi) from an elevated [PowerShell](https://github.com/PowerShell/PowerShell):
> choco install pulumi

Since Pulumi uses multiple executables, it excludes them from getting Chocolatey [shims](https://chocolatey.org/docs/features-shim#i-need-to-exclude-a-file-from-shimming), so add `C:\ProgramData\chocolatey\lib\pulumi\tools\Pulumi\bin` to the system `PATH` (after `C:\ProgramData\chocolatey\bin`).

## Deploy environment

* Configure environment config in [Cli/config](https://github.com/gitfool/Pulumi.Dungeon/tree/main/Cli/config)
  * [Cli/config/alpha](https://github.com/gitfool/Pulumi.Dungeon/blob/main/Cli/config/alpha.yaml) which extends [Cli/config/_global](https://github.com/gitfool/Pulumi.Dungeon/blob/main/Cli/config/_global.yaml)
* Validate environment config
  * `pulumi-dungeon config alpha [--yaml]`
* Bootstrap environment to create aws deployer role and policy
  * `pulumi-dungeon deploy alpha awsbootstrap`
* Deploy environment stacks
  * `pulumi-dungeon deploy alpha [--skip-preview] [--yes]`
