# Additional System Capabilities

This reference implementation could not cover all scenarios your cluster might need to address. Here are a couple additional considerations.

## Customer-managed OS and data disk encryption

While OS and data disks (and their caches) are already encrypted at rest with Microsoft-managed keys, for additional control over encryption keys you can use customer-managed keys for encyption at rest for both the OS and the data disks in your AKS cluster. This reference implementation doesn't actually use any disks in the cluster, and the OS disk is ephemeral. But if you use non-ephermeal OS disks or add data disks, consider using this added security solution.

Read more about [Bing your own keys (BYOK) with Azure disks](https://docs.microsoft.com/azure/aks/azure-disk-customer-managed-keys).

Consider using BYOK for any other disks that might be in your final solution, such as your Azure Bastion-fronted jumpboxes. Please note that your SKU choice for VMs will be limited to only those that support this feature, and regional availability will be restricted as well.

Note, we enable an Azure Policy alert detecting clusters without this feature enabled. The reference implementation will trip this policy alert because there is no `diskEncryptionSetID` provided on the cluster resource. The policy is in place as a reminder of this security feature that you might wish to use. The policy is set to "audit" not "block."

### Host-based encryption

You can take OS and data disk encryption one step further and also bring the encryption up to the Azure host. Using [Host-Based Encryption](https://docs.microsoft.com/azure/aks/enable-host-encryption) means that the temp disks now will be encrypted at rest using platform-managed keys. This will then cover encryption of the VMSS ephemeral OS disk and temp disks. Your SKU choice for VMs will be limited to only those that support this feature, and regional availability will be restricted as well. This feature is currently in preview. See more details about [VM support for host-based encryption](https://docs.microsoft.com/azure/virtual-machines/disk-encryption#encryption-at-host---end-to-end-encryption-for-your-vm-data).

Note, like above, we enable an Azure Policy detecting clusters without this feature enabled. The reference implementation will trip this policy alert because this feature is not enabled on the `agentPoolProfiles`. The policy is in place as a reminder of this security feature that you might wish to use once it is GA. The policy is set to "audit" not "block."

## Enable NSG Flow Logs

Observability into your network is critical for compliance. NSG Flow Logs will capture and log all traffic traversing your Network Watchers. This reference implementation does not deploy NSG Flow Logs for simplicity purposes. We strongly consider enabling NSG flow logs on your AKS Cluster subnets, build agent subnets, and other subnets that may be a source of traffic into your cluster.

## Management Groups

This reference implementation is expected to be deployed in a standalone subscription.  As such, Azure Policies are applied at a relatively local scope (subscription or resource group). If you have multiple subscriptions that will be under regulatory compliance, consider grouping them under a management group hiarchy that applies the relevant Azure Policies uniformally across your in-scope subscriptions. TODO: is there a CAF/LZ link to use?