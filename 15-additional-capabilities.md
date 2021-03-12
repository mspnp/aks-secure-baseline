# Additional System Capabilities

This reference implementation did not enable "every security option possible." This was mostly due to practicality around permissions, already existing security policies in the subscription, existing policies that might block deployment, simplicity in demonstration, etc. Because they were not trivial to deploy in this walkthrough, we wanted to ensure you at least have a list of things we'd have liked to include out of the box -- ideally as a consideration for you to introduce into your final architecture.

**In addition to what is listed here, a few "in-line" recommendations were made throughout the walkthrough. Most of those are not repeated here in this list.**

## Customer-managed OS and data disk encryption

While OS and data disks (and their caches) are already encrypted at rest with Microsoft-managed keys, for additional control over encryption keys you can use customer-managed keys for encyption at rest for both the OS and the data disks in your AKS cluster. This reference implementation doesn't actually use any disks in the cluster, and the OS disk is ephemeral. But if you use non-ephemeral OS disks or add data disks, consider using this added security solution.

Read more about [Bing your own keys (BYOK) with Azure disks](https://docs.microsoft.com/azure/aks/azure-disk-customer-managed-keys).

Consider using BYOK for any other disks that might be in your final solution, such as your Azure Bastion-fronted jumpboxes. Please note that your SKU choice for VMs will be limited to only those that support this feature, and regional availability will be restricted as well.

Note, we enable an Azure Policy alert detecting clusters without this feature enabled. The reference implementation will trip this policy alert because there is no `diskEncryptionSetID` provided on the cluster resource. The policy is in place as a reminder of this security feature that you might wish to use. The policy is set to "audit" not "block."

### Host-based encryption

You can take OS and data disk encryption one step further and also bring the encryption up to the Azure host. Using [Host-Based Encryption](https://docs.microsoft.com/azure/aks/enable-host-encryption) means that the temp disks now will be encrypted at rest using platform-managed keys. This will then cover encryption of the VMSS ephemeral OS disk and temp disks. Your SKU choice for VMs will be limited to only those that support this feature, and regional availability will be restricted as well. This feature is currently in preview. See more details about [VM support for host-based encryption](https://docs.microsoft.com/azure/virtual-machines/disk-encryption#encryption-at-host---end-to-end-encryption-for-your-vm-data).

Note, like above, we enable an Azure Policy detecting clusters without this feature enabled. The reference implementation will trip this policy alert because this feature is not enabled on the `agentPoolProfiles`. The policy is in place as a reminder of this security feature that you might wish to use once it is GA. The policy is set to "audit" not "block."

## Enable Network Watcher and Traffic Analytics

Observability into your network is critical for compliance. [Network Watcher](https://docs.microsoft.com/azure/network-watcher/network-watcher-monitoring-overview), combined with [Traffic Analysis](https://docs.microsoft.com/azure/network-watcher/traffic-analytics) will help provide a perspective into traffic traversing your networks. This reference implementation does not deploy NSG Flow Logs or Traffic Analysis by default. These features depend on a regional Network Watcher resource being installed on your subscription. Network Watchers are singletons in a subscription, and there is no reasonable way to include them in the ARM templates here accounting for both pre-existing network watchers and non-preexisting situations. We strongly encourage you to enable [NSG flow logs](https://docs.microsoft.com/azure/network-watcher/network-watcher-nsg-flow-logging-overview) on your AKS Cluster subnets, build agent subnets, Azure Application Gateway, and other subnets that may be a source of traffic into and out of your cluster. Ensure you're sending your NSG Flow Logs to a **V2 Storage Account** and set your retention period in the Storage Account for these logs to a value that is at least as long as your compliance needs (e.g. 90 days).

In addition to Network Watcher aiding in compliance considerations, it's also a highly valuable network troubleshooting utility. As your network is private and heavy with flow restrictions, troubleshooting network flow issues can be time consuming. Network Watcher can help provide additional insight when other troubleshooting means are not sufficient.

## Make use of container securityContext options

When describing your workload's security needs, leverage all relevant [`securityContext` settings](https://kubernetes.io/docs/tasks/configure-pod-container/security-context/) for your containers. This includes basic items like `fsGroup`, `runAsUser` / `runAsGroup`, and setting `allowPriviledgeEscalation` to `false` (unless required). But it also means being explicit about defining/removing Linux `capabilities` and defining your SELinux options in `seLinuxOptions`. The workloads deployed in this reference implementation do NOT represent best practices, as this reference implementation was mainly infrastructure focused.

## Customized Azure Policies

Generally speaking, the Azure Policies applied do not have workload-tuned settings applied. Specifically we're applying the **Kubernetes cluster pod security restricted standards for Linux-based workloads** initiative which does not allow tuning of settings. Consider exporting this initiative and customizing its values for your specific workload. You may wish to include all Gatekeeper `deny` Azure Policies under one custom Initiative and all `audit` Azure Policies under another to know strong "blocks" from "awareness only" policies.

While it's common for Azure Policy to exclude `kube-system` and `gatekeeper-system` to policies, consider _including_ them in your `audit` policies for _added visibility_. Including those namespaces in `deny` policies could cause cluster failure due to an unsupported configuration. You may find some that are relatively safe, such as enforcing internal load balancers and HTTPS ingresses, but be aware if you apply these you may run into support concerns.

## Management Groups

This reference implementation is expected to be deployed in a standalone subscription.  As such, Azure Policies are applied at a relatively local scope (subscription or resource group). If you have multiple subscriptions that will be under regulatory compliance, consider grouping them under a [management group hierarchy](https://docs.microsoft.com/azure/cloud-adoption-framework/ready/enterprise-scale/management-group-and-subscription-organization) that applies the relevant Azure Policies uniformly across your in-scope subscriptions.

## OCI Artifact Signing

Azure Container Registry supports the [signing of images](https://docs.microsoft.com/azure/container-registry/container-registry-content-trust), built on [CNFC Notary (v1)](https://github.com/theupdateframework/notary). This, coupled with an admission controller that supports validating signatures, can ensure that you're only running images that you've signed with your private keys. This integration is not something that is provided, today, end-to-end by Azure Container Registry and AKS (Azure Policy), and customers are bringing open source solutions like [SSE Connaisseur](https://github.com/sse-secure-systems/connaisseur) or [IBM Portieris](https://github.com/IBM/portieris). [Notary v2](https://github.com/notaryproject/notaryproject) is likely where most of the industry will be moving with regards to signing OCI Artifacts (i.e. container images and helm charts), and both the ACR and AKS roadmap includes adding a more native end-to-end experience in this space built upon this foundation.

## JIT and Conditional Access Policies

As mentioned in-line in the walkthrough, AKS' control plane supports both [Azure AD PAM JIT](https://docs.microsoft.com/azure/aks/managed-aad#configure-just-in-time-cluster-access-with-azure-ad-and-aks) and [Conditional Access Policies](https://docs.microsoft.com/azure/aks/managed-aad#use-conditional-access-with-azure-ad-and-aks). We recommend that you minimize standing permissions and leverage JIT access when performing SRE/Ops interactions with your cluster. Likewise, Conditional Access Policies will add additional layers of required authentication validation for privileged access, based on the rules you build.

## CVE Mitigation

Inline, we talked about many ISV's security agents being able to detect relevant CVEs for your cluster and workloads. But in addition to relying on tooling, you can also see [Microsoft's Security Response Center's 1st-party CVE listings](https://msrc.microsoft.com/update-guide/vulnerability) at any time. Here's [CVE-2021-27075](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2021-27075), an Information Disclosure entry from March 2021 as an example. No matter how you keep yourself informed about current CVEs, ensure you have a documented plan to stay informed.

## Expanded NetworkPolicies

Not all user-provided namespaces in this reference implementation employ a zero-trust network. For example `cluster-baseline-settings` does not. We provide an example of zero-trust networks in `a0005-i` and `a0005-o` as your reference implementation of the concept. All namespaces (other than `kube-system`, `gatekeeper-system`, and other AKS-provided namespaces) should have a maximally restrictive NetworkPolicy applied. What those policies will be will be based on the pods running in those namespaces. Ensure your accounting for readiness, liveliness, and startup probes and also accounting for metrics gathering by `oms-agent`.  Consider standarizing on ports across your workloads so that you can provide a consistent NetworkPolicy and even Azure Policy for allowed container ports.

## Custom Cluster Roles

Regulatory compliance often requires well defined roles, with specific access policies associated with that role. If one person fills multiple roles, they should be assigned the roles that are relevant to all of their job titles. This reference implementation doesn't demonstrate any specific role structure, and matter of fact, everything you did throughout this walkthrough was done with the most privileged role in the cluster. Part of your compliance work must be to define roles and map them allowed Kubernetes actions, scoped as narrow as practical. Even if one person is directly responsible for both the cluster and the workload, craft your Kubernetes ClusterRoles as if there were separate individuals, and then assign that single individual all relevant roles. Minimize any "do it all" roles, and favor role composition to achieve management at scale.

## mTLS Certificate Provider Choice

While this reference implementation uses Tresor as its TLS certificate provider for mesh communication, you may wish to use a more formal certificate provider for your mTLS implementation (if you choose to implement mTLS). You may wish to use CertManager, HashiCorp Vault, Key Vault, or even your own internal certificate provider. If you use a mesh, ensure its compatible with your certificate provider of choice.

## Ingress Controller

The ingress controller implemented in this reference implementation is relatively simplistic in implementation. It's currently using a wild-card certificate to handle default traffic when an `Ingress` resource doesn't contain a specific certificate. This might be fine for most customers, but if you have an organizational policy against using wildcard certs (even on your internal, private network), you may need to adjust your ingress controller to not support a "default certificate" and instead require ever workload to surface their own named certificate. This will impact how Azure Application Gateway is performing backend health checks.

## More Strict NSGs

The NSGs that exist around the cluster node pool subnets specifically block any SSH access attempts only allow traffic from the vnet into them. As your workloads, system security agents, etc are deployed, consider adding even more NSG rules that help define the type of traffic that should and should not be traversing those subnet boundaries. Because each nodepool lives in its own subnet, you can apply more specific rules based on known/expected traffic patterns of your workload.

## Azure Key Vault

In this reference implementation, Azure Application Gateway (AAG) is sourcing its public-facing certificate from Azure Key Vault. This is great as it help support easier certificate rotation and certificate control. However, currently Azure Application Gateway only supports this on Azure Key Vault instances that are exclusively network restricted via Private Link. This reference implementation deploys Azure Key Vault in a hybrid model, supporting private link and public access specifically to allow AAG integration. Once [Azure Application Gateway supports private link access to Key Vault](https://docs.microsoft.com/azure/application-gateway/key-vault-certs#how-integration-works), we'll update this reference implementation. If this topology will not be suitable for your deployment, change the certificate management process in AAG to abandon the use of Key Vault for the public-facing TLS certificate and [handle the management of that certificate directly within AAG](https://docs.microsoft.com/azure/application-gateway/tutorial-ssl-cli). Doing so will allow your Key Vault instance to be fully isolated.

## Tuning the Log Analytics Agent in your Cluster

The in-cluster `oms-agent` pods running in `kube-system` are the Log Analytics collection agent. They are responsible for gathering telemetry, scraping container `stdout` and `stderr` logs, and collecting Prometheus metrics. You can tune its collection settings by updating the `container-azm-ms-agentconfig.yaml` `ConfigMap` file. In this reference implementation, logging is enabled across `kube-system` and all your workloads. By default, `kube-system` is excluded from logging. Ensure you're adjusting the log collection process to achieve balance cost objectives, SRE efficiency when reviewing logs, and compliance needs.

## Tuning Azure Sentinel

Azure Sentinel was enabled in this reference implementation. No alerts were created or any sort of "usage" of it, other than enabling it. You may already be using another SIEM, likewise you may find that a SIEM is not cost effective for your solution. Evaluate if you will derive benefit from Azure Sentinel in your solution, and tune as needed.
