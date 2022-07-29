# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/runtime-deps:6.0.7-jammy

LABEL org.opencontainers.image.source=https://github.com/gitfool/Pulumi.Dungeon

# Install packages
RUN <<EOF
    set -ex
    apt-get update
    apt-get install --no-install-recommends -y bash-completion curl sudo vim
    rm -rf /var/lib/apt/lists/*
    mkdir -p /etc/bash_completion.d
EOF

# Install libssl 1.1; workaround dotnet 5.0 runtime dependency removed from ubuntu jammy
# renovate: datasource=repology depName=libssl1.1 packageName=ubuntu_20_04/openssl versioning=loose
RUN <<EOF
    set -ex
    version=1.1.1f-1ubuntu2.16
    curl -fsSL https://launchpad.net/ubuntu/+archive/primary/+files/libssl1.1_${version}_amd64.deb -o libssl1.1.deb
    DEBIAN_FRONTEND=noninteractive dpkg -i libssl1.1.deb
    rm -f libssl1.1.deb
EOF

# Install kubectl; https://kubernetes.io/docs/tasks/tools/install-kubectl/#install-kubectl-binary-via-curl
# renovate: datasource=github-tags depName=kubectl packageName=kubernetes/kubectl extractVersion=^kubernetes-(?<version>.+)$
RUN <<EOF
    set -ex
    version=1.24.3
    curl -fsSL https://storage.googleapis.com/kubernetes-release/release/v$version/bin/linux/amd64/kubectl -o /usr/local/bin/kubectl
    chmod +x /usr/local/bin/kubectl
    kubectl completion bash > /etc/bash_completion.d/kubectl
    kubectl version --client --short
EOF

# Install aws iam authenticator; https://docs.aws.amazon.com/eks/latest/userguide/install-aws-iam-authenticator.html
# renovate: datasource=github-releases depName=aws-iam-authenticator packageName=kubernetes-sigs/aws-iam-authenticator
RUN <<EOF
    set -ex
    version=0.5.9
    curl -fsSL https://github.com/kubernetes-sigs/aws-iam-authenticator/releases/download/v$version/aws-iam-authenticator_${version}_linux_amd64 -o /usr/local/bin/aws-iam-authenticator
    chmod +x /usr/local/bin/aws-iam-authenticator
    aws-iam-authenticator version
EOF

# Install helm; https://helm.sh/docs/intro/install/#from-the-binary-releases
# renovate: datasource=github-releases depName=helm packageName=helm/helm
RUN <<EOF
    set -ex
    version=3.9.2
    curl -fsSL https://get.helm.sh/helm-v$version-linux-amd64.tar.gz -o helm.tar.gz
    tar -xzf helm.tar.gz --directory /usr/local/bin --no-same-owner --strip=1 linux-amd64/helm
    rm -f helm.tar.gz
    helm completion bash > /etc/bash_completion.d/helm
    helm version --short
EOF

# Install pulumi; https://www.pulumi.com/docs/get-started/install/#manual-installation
ENV PULUMI_AUTOMATION_API_SKIP_VERSION_CHECK=true \
    PULUMI_EXPERIMENTAL=true \
    PULUMI_SKIP_UPDATE_CHECK=true

# renovate: datasource=github-releases depName=pulumi packageName=pulumi/pulumi
RUN <<EOF
    set -ex
    version=3.36.0
    curl -fsSL https://get.pulumi.com/releases/sdk/pulumi-v$version-linux-x64.tar.gz -o pulumi.tar.gz
    tar -xzf pulumi.tar.gz --directory /usr/local/bin --no-same-owner --strip=1 pulumi/pulumi
    rm -f pulumi.tar.gz
    pulumi gen-completion bash > /etc/bash_completion.d/pulumi
    pulumi version
EOF

# Add non-root user
RUN <<EOF
    set -ex
    groupadd --gid 1000 user
    useradd --uid 1000 --gid 1000 --shell /bin/bash -m user
    echo "user ALL=(ALL:ALL) NOPASSWD: ALL" > /etc/sudoers.d/user
    chmod 0440 /etc/sudoers.d/user
EOF

USER user

RUN <<EOF
    set -ex
    echo "alias k='kubectl'\ncomplete -o default -F __start_kubectl k" >> ~/.bash_aliases
    echo "alias p='pulumi'\ncomplete -o default -F __start_pulumi p" >> ~/.bash_aliases
    echo "alias l='ls -aF'" >> ~/.bash_aliases
    echo "alias ll='ls -ahlF'" >> ~/.bash_aliases
    echo "alias ls='ls --color=auto --group-directories-first'" >> ~/.bash_aliases
    helm repo add stable https://charts.helm.sh/stable
    helm repo update
EOF

# Install dungeon
ENV DOTNET_HostBuilder__ReloadConfigOnChange=false

WORKDIR /home/user/dungeon

COPY --chown=user:user bin/Release/linux-x64/publish .

RUN <<EOF
    set -ex
    echo "alias d='cd ~/dungeon && ./pulumi-dungeon'" >> ~/.bash_aliases
    chmod -x config/*.yaml
EOF

ENTRYPOINT [ "./pulumi-dungeon" ]
