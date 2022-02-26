# Pulumi.Dungeon

[![License](https://img.shields.io/badge/license-MIT-blue.svg?label=License&logo=github)](LICENSE)
[![GitHub Actions](https://img.shields.io/github/workflow/status/gitfool/Pulumi.Dungeon/ci/main?label=GitHub%20Actions&logo=github)](https://github.com/gitfool/Pulumi.Dungeon/actions)
[![Docker Pulls](https://img.shields.io/docker/pulls/dockfool/pulumi-dungeon.svg?label=Docker&logo=docker)](https://hub.docker.com/r/dockfool/pulumi-dungeon/tags)

`Pulumi.Dungeon` is a playground for Pulumi devops tools. See [pulumi.com](https://pulumi.com).

## Build environment on Windows with [Visual Studio](https://visualstudio.microsoft.com/vs/)

* Use [Chocolatey](https://chocolatey.org/) to install [Pulumi](https://github.com/pulumi/pulumi) from an elevated [PowerShell](https://github.com/PowerShell/PowerShell):
> choco install pulumi

Since Pulumi uses multiple executables, it excludes them from getting Chocolatey [shims](https://chocolatey.org/docs/features-shim#i-need-to-exclude-a-file-from-shimming), so add `C:\ProgramData\chocolatey\lib\pulumi\tools\Pulumi\bin` to the system `PATH` (after `C:\ProgramData\chocolatey\bin`).

Use Chocolatey to upgrade Pulumi from an elevated PowerShell:
> choco upgrade pulumi

or

> choco upgrade pulumi --version {version}

Check installed:
> pulumi version

* Use Chocolatey to install [Helm](https://github.com/helm/helm) from an elevated PowerShell:
> choco install kubernetes-helm

Check installed:
> helm version

* Manually install [Helm ECR](https://github.com/vetyy/helm-ecr) plugin (since [`helm plugin install`](https://helm.sh/docs/topics/plugins/#installing-a-plugin) is [broken on Windows](https://github.com/helm/helm/issues/7117)):
  * Download latest Windows tarball from https://github.com/vetyy/helm-ecr/releases, currently [helm-ecr_0.1.4_windows_amd64.tar.gz](https://github.com/vetyy/helm-ecr/releases/download/v0.1.4/helm-ecr_0.1.4_windows_amd64.tar.gz)
  * Extract tarball to `%APPDATA%\helm\plugins\helm-ecr`

Check installed:
> helm plugin list
