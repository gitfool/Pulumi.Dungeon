#!/bin/bash
set -ex

export APISERVER_ENDPOINT='{{ clusterEndpoint }}'
export B64_CLUSTER_CA='{{ clusterCa }}'
export CONTAINER_RUNTIME='{{ containerRuntime }}'
export KUBELET_EXTRA_ARGS='{{ kubeletExtraArgs }}'

/etc/eks/bootstrap.sh {{ clusterName }}
