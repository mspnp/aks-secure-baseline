# Prepare Cluster Subscription

In the prior step, you've set up an Azure AD tenant to fullfil your [cluster's control plane (Cluster API) authorization](./03-aad.md) needs for this reference implementation deployment; now we'll prepare the subscription in which will be hosting this workload. This includes creating the resource groups and applying some high-level Azure Policies to govern our deployments.

## Subscription and resource group topology

This reference implementation is split across several resource groups in a single subscription. This is to replicate the fact that many organizations will split certain responsibilities into specialized subscriptions (e.g. regional hubs/vwan in a _Connectivity_ subscription and workloads in landing zone subscriptions). We expect you to explore this reference implementation within a single subscription, but when you implement this cluster at your organization, you will need to take what you've learned here and apply it to your expected subscription and resource group topology (such as those [offered by the Cloud Adoption Framework](https://docs.microsoft.com/azure/cloud-adoption-framework/decision-guides/subscriptions/).) This single subscription, multiple resource group model is for simplicity of demonstration purposes only.

## Expected results

### Resource groups created

The following three resource groups will be created in the steps below.

| Name                            | Purpose                                   |
|---------------------------------|-------------------------------------------|
| rg-enterprise-networking-hubs   | Contains all of your organization's regional hubs. A regional hubs include an egress firewall, Azure Bastion, and Log Analytics for network logging. |
| rg-enterprise-networking-spokes | Contains all of your organization's regional spokes and related networking resources. All spokes will peer with their regional hub and subnets will egress through the regional firewall in the hub. |
| rg-bu0001a0005                  | Contains the regulated cluster resources. |

Both Azure Kubernetes Service and Azure Image Builder Service use a concept of a dynamically-created _infrastructure_ resource group. So in addition to the three resource groups mentioned above, as you follow these instructions, you'll end up with five resource groups; two of which are automatically created and their lifecycle tied to their owning service. You will not see these two infrastructure resource groups get created until later in the walkthrough when their owning service is created.

### Azure Policy applied

To help govern our resources, there are policies we apply over the scope of these resource groups. These policies will also be created in the steps below.

| Policy Name                    | Scope                           | Purpose                                                                                           |
|--------------------------------|---------------------------------|---------------------------------------------------------------------------------------------------|
| Enable Azure Defender Standard | Subscription                    | Ensures that Azure Defender for Kubernetes, Container Service, and Key Vault are always enabled.  |
| Allowed resource types         | rg-enterprise-networking-hubs   | Restricts the hub resource group to just relevant networking resources.                           |
| Allowed resource types         | rg-enterprise-networking-spokes | Restricts the spokes resource group to just relevant networking resources.                        |
| Allowed resource types         | rg-bu0001a0005                  | Restricts the workload resource group to just resources necessary for this specific architecture. |
| No public AKS clusters         | rg-bu0001a0005                  | Restricts the creation of AKS clusters to only those with private Cluster API server.             |
| No out-of-date AKS clusters    | rg-bu0001a0005                  | Restricts the creation of AKS clusters to only recent versions.                          |
| No AKS clusters without RBAC   | rg-bu0001a0005                  | Restricts the creation of AKS clusters to only those that are Azure AD RBAC enabled. |
| No AKS clusters without Azure Policy | rg-bu0001a0005                  | Restricts the creation of AKS clusters to only those that have Azure Policy enabled. |
| No AKS clusters without BYOK OS & Data Disk Encryption | rg-bu0001a0005                  | Restricts the creation of AKS clusters to only those that have customer-managed disk encryption enabled. (_This is in audit only mode, as not all customers may wish to do this._) |
| No AKS clusters without encryption-at-host | rg-bu0001a0005                  | Restricts the creation of AKS clusters to only those that have the Encryption-At-Host feature enabled. (_This is in audit only mode, as not all customers may wish to do this._) |
| No App Gateways w/out WAF      | rg-bu0001a0005                  | Restricts the creation of Azure Application Gateway to only the WAF SKU. |

For this reference implementation, our Azure Policies applied to these resource groups are maximally restrictive on what resource types are allowed to be deployed and what features they must have enabled/disable. If you alter the deployment by adding additional Azure resources, you may need to update the _Allowed resource types_ policy for that resource group to accommodate your modification.

This is not an exhaustive list of Azure Policies that you can create or assign, and instead an example of the types of polices you should consider having in place. Policies like these help prevent a misconfiguration of a service that would expose you to unplanned compliance concerns. Let the Azure control plane guard against configurations that are untenable for your compliance requirements as an added safeguard. While we deploy policies at the subscription and resource group scope, your organization may also utilize management groups. We've found it's best to also ensure your local subscription and resource groups have "scope-local" policies specific to its needs, so it doesn't take a dependency on a higher order policy existing or not -- even if that leads to a duplication of policy.

Also, depending on your workload subscription scope, some of the policies applied above may be better suited at the subscription level (like no public AKS clusters). Since we don't assume you're coming to this walkthrough with a dedicated subscription, we've scoped the restrictions to only those resource groups we ask you to create. Apply your policies where it makes the most sense to do so in your final implementation.

### Security Center activated

As mentioned in the Azure Policy section above, we enable the following Azure Security Center's services.

* [Azure Defender for kubernetes](https://docs.microsoft.com/azure/security-center/defender-for-kubernetes-introduction)
* [Azure Defender for container registries](https://docs.microsoft.com/azure/security-center/defender-for-container-registries-introduction)
* [Azure Defender for key vault](https://docs.microsoft.com/azure/security-center/defender-for-key-vault-introduction)

Not only do we enable them in the steps below by default, but also set up an Azure Policy that ensures they stay enabled.

## Steps

1. Login into the Azure subscription that you'll be deploying into.

   ```bash
   az login -t $TENANTID_AZURERBAC
   ```

1. Verify you're on the correct subscription.

   ```bash
   az account show

   # If not, select the correct subscription
   # az account set -s <subscription name or id>
   ```

1. Perform subscription-level deployment.

   This will deploy the resource groups, Azure Policies, and Azure Security Center configuration all as identified above.

   ```bash
   # [This may take up to six minutes to run.]
   az deployment sub create -f subscription.json -l centralus
   ```

   If you do not have permissions on your subscription to enable Azure Defender (which requires the Azure RBAC role of _Subscription Owner_ or _Security Admin_), then instead execute the following variation of the same command. This will not enable Azure Defender services nor will Azure Policy attempt to enable the same (the policy will still be created, but in audit-only mode). Your final implementation should be to a subscription with these security services activated.

   ```bash
   # [This may take up to five minutes to run.]
   az deployment sub create -f subscription.json -l centralus -p enableAzureDefender=false enforceAzureDefenderAutoDeployPolicies=false
   ```

## Azure Security Benchmark

Your Azure _subscription_ should have the **Azure Security Benchmark** Azure Policy initiative applied. While we could deploy it in ARM (as above), we don't want to step on anything already existing in your subscription, since you can only have it applied once for Security Center to detect it properly. If you have the ability to apply it without any negative impact on other work your subscription, you can do so by doing the following.

### Steps

1. Open the [**Regulatory Compliance** screen in Security Center](https://portal.azure.com/#blade/Microsoft_Azure_Security/SecurityMenuBlade/22)
1. Click on **Manage Compliance Policies**
1. Click on your subscription
1. Ensure the **Azure Security Benchmark** is applied as the **Security Center default policy**.
1. You'll want to ensure all relevant standards (e.g. **PCI DSS 3.2.1**) are **Enabled** under **Industry & regulatory standards**
1. The **Regulatory Compliance** dashboard in Security Center might take an hour or two to reflect any modifications you've made.

**None of the above is required for this walkthrough**, but we want to ensure you're aware of these subscription-level policies and how you can enable them for your final implementation. All subscriptions containing PCI workloads should have the **PCI DSS 3.2.1** Industry & regulatory standards reports enabled.

## Other Azure Policies

Consider evaluating additional Azure Policies to help guard your subscription from undesirable resource deployments. Here are some to consider.

* [PCI-DSS 3.2.1 Blueprint](https://docs.microsoft.com/azure/governance/blueprints/samples/pci-dss-3.2.1/)
* Allowed locations
* Allowed locations for resource groups
* External accounts with read permissions should be removed from your subscription
* External accounts with write permissions should be removed from your subscription
* External accounts with owner permissions should be removed from your subscription
* Network interfaces should not have public IPs

Like the Azure Security Benchmark, we'd like to apply these, and similar, in this walkthrough; but we acknowledge that they might be disruptive if you are deploying this walkthrough to a shared subscription with other existing resources. Please take the time to review the [built-in Azure Policies](https://portal.azure.com/#blade/Microsoft_Azure_Policy/PolicyMenuBlade/Definitions) and the [ability to create your own](https://docs.microsoft.com/azure/governance/policy/tutorials/create-and-manage), and craft policies that will help keep you within regulatory compliance from an Azure Resource location & features perspective.

## Compliance documentation

A summary of all of [Microsoft and Azure's compliance offerings are available](https://docs.microsoft.com/compliance/regulatory/offering-home). If you're looking for compliance reports (AOC, Shared Responsibility Matrix), you can access them via the Azure Portal. Your regulatory requirements may require you to have a copy of these documents available.

### Steps

1. Open the [**Regulatory Compliance** screen in Security Center](https://portal.azure.com/#blade/Microsoft_Azure_Security/SecurityMenuBlade/22)
1. Click on **Audit Reports**
1. Select your interest (e.g. **PCI**)
1. Access whatever documents are available (e.g. **PCI DSS 3.2.1 - Azure Shared Responsibility Matrix** or **Azure PCI DSS 3.2.1 AOC Package**)

### Next step

:arrow_forward: [Deploy the regional hub network](./05-networking-hub.md).
