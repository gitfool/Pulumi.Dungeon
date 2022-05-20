FROM mcr.microsoft.com/dotnet/runtime-deps:6.0.5-focal

LABEL org.opencontainers.image.source=https://github.com/gitfool/Pulumi.Dungeon

# Install packages
RUN apt-get update \
    && apt-get install --no-install-recommends -y bash-completion curl sudo vim \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /etc/bash_completion.d

# Install kubectl; https://kubernetes.io/docs/tasks/tools/install-kubectl/#install-kubectl-binary-via-curl
# renovate: datasource=github-tags depName=kubectl packageName=kubernetes/kubectl extractVersion=^kubernetes-(?<version>.+)$
RUN version=1.24.0 \
    && curl -fsSL https://storage.googleapis.com/kubernetes-release/release/v$version/bin/linux/amd64/kubectl -o /usr/local/bin/kubectl \
    && chmod +x /usr/local/bin/kubectl \
    && kubectl completion bash > /etc/bash_completion.d/kubectl \
    && kubectl version --client --short

# Install aws iam authenticator; https://docs.aws.amazon.com/eks/latest/userguide/install-aws-iam-authenticator.html
# renovate: datasource=github-releases depName=aws-iam-authenticator packageName=kubernetes-sigs/aws-iam-authenticator
RUN version=0.5.7 \
    && curl -fsSL https://github.com/kubernetes-sigs/aws-iam-authenticator/releases/download/v$version/aws-iam-authenticator_${version}_linux_amd64 -o /usr/local/bin/aws-iam-authenticator \
    && chmod +x /usr/local/bin/aws-iam-authenticator \
    && aws-iam-authenticator version

# Install helm; https://helm.sh/docs/intro/install/#from-the-binary-releases
# renovate: datasource=github-releases depName=helm packageName=helm/helm
RUN version=3.9.0 \
    && curl -fsSL https://get.helm.sh/helm-v$version-linux-amd64.tar.gz -o helm.tar.gz \
    && tar -xzf helm.tar.gz --directory /usr/local/bin --no-same-owner --strip=1 linux-amd64/helm \
    && rm -f helm.tar.gz \
    && helm completion bash > /etc/bash_completion.d/helm \
    && helm version --short

# Install pulumi; https://www.pulumi.com/docs/get-started/install/#manual-installation
ENV PULUMI_AUTOMATION_API_SKIP_VERSION_CHECK=true \
    PULUMI_EXPERIMENTAL=true \
    PULUMI_SKIP_UPDATE_CHECK=true

# renovate: datasource=github-releases depName=pulumi packageName=pulumi/pulumi
RUN version=3.33.1 \
    && curl -fsSL https://get.pulumi.com/releases/sdk/pulumi-v$version-linux-x64.tar.gz -o pulumi.tar.gz \
    && tar -xzf pulumi.tar.gz --directory /usr/local/bin --no-same-owner --strip=1 pulumi/pulumi \
    && rm -f pulumi.tar.gz \
    && pulumi gen-completion bash > /etc/bash_completion.d/pulumi \
    && pulumi version

# Add non-root user
RUN groupadd --gid 1000 user \
    && useradd --uid 1000 --gid 1000 --shell /bin/bash -m user \
    && echo "user ALL=(ALL:ALL) NOPASSWD: ALL" > /etc/sudoers.d/user \
    && chmod 0440 /etc/sudoers.d/user

USER user

RUN echo "alias k='kubectl'\ncomplete -o default -F __start_kubectl k" >> ~/.bash_aliases \
    && echo "alias p='pulumi'\ncomplete -o default -F __start_pulumi p" >> ~/.bash_aliases \
    && echo "alias l='ls -aF'" >> ~/.bash_aliases \
    && echo "alias ll='ls -ahlF'" >> ~/.bash_aliases \
    && echo "alias ls='ls --color=auto --group-directories-first'" >> ~/.bash_aliases \
    && helm repo add stable https://charts.helm.sh/stable \
    && helm repo update

# Install dungeon
ENV DOTNET_HostBuilder__ReloadConfigOnChange=false

WORKDIR /home/user/dungeon

COPY --chown=user:user bin/Release/linux-x64/publish .

RUN echo "alias d='cd ~/dungeon && ./pulumi-dungeon'" >> ~/.bash_aliases \
    && chmod -x config/*.yaml

ENTRYPOINT [ "./pulumi-dungeon" ]
