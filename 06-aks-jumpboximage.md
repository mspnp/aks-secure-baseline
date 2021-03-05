# Create the AKS Jump Box Image

The first foundational networking component, the regional hub, [has been deployed](./04-networking-hub.md). Before we dive into the cluster spoke and cluster itself, we need to take a quick detour to plan and talk about cluster control plane access.

## Planning access to your cluster's control plane

Your cluster's control plane (API Server) will not be accessible to the Internet as the cluster you'll deploy is a Private Cluster. This is one of the largest differences between this reference implementation and the general purpose [AKS Baseline reference implementation](https://github.com/mspnp/aks-secure-baseline), which has its cluster control plane Internet-facing (relying on identity as the parameter, just like your Azure resource management control plane is). In order to perform Kubernetes management operations against the cluster, you'll need to access the API Server from a designated subnet (`snet-management-ops` in the cluster's virtual network `vnet-spoke-BU0001A0005-01` in this implementation). You have options on how to go about originating your ops traffic from this specific subnet.

* You could establish a VPN connection to that subnet such that you source an IP from that subnet. This would allow you to manage the cluster from any place that you can establish the VPN connection from.
* You could use Azure Shell's feature that [allows Azure Shell to be subnet-connected](https://docs.microsoft.com/azure/cloud-shell/private-vnet) (Preview).
* You could could deploy compute resources into that subnet and use that as your ops workstation.
* We do _not_ want to use the AKS nodes (or OpenSSH containers running on them) as our access points; as this would be using the management target system as the management tool, which is not reliable. Also it adds an unnecessary surface area to your cluster which would also need to be considered from a regulatory compliance perspective.

This reference implementation will be using the "compute resource in subnet" option above, typically known as a jump box. Even within this option, you have additional choices.

* Use Azure Container Instances and a custom [OpenSSH host](https://docs.linuxserver.io/images/docker-openssh-server) container
* Use Windows WVD/RDS solutions
* Use stand-alone, persistent VMs in an availability set
* Use small instance count, non-autoscaling Virtual Machine Scale Set

In all cases, you'll likely be building a "golden image" (container or VM image) to use as the base of your jump box. A jump box image should contain all the required operations tooling necessary for ops engineers to perform their duties (both routine and break-fix). You're welcome to bring your own image to this reference implementation if you have one. If you do not have one, the following steps will help you build one as an example.

## Expected results

We are going to be using Azure Image Builder to generate a Kubernetes-specific jump box. The building of the image will be performed in a dedicated network spoke with limited Internet exposure. These steps below will deploy a new dedicated image-building spoke, connected through our hub to sequester network traffic throughout the process. It will then deploy an image template and all infrastructure components for Azure Image Builder to operate. Finally you will build an image to use for your jump box.

* The network spoke will be called `vnet-spoke-bu0001a0005-00` and have a range of `10.241.0.0/28`.
* The hub's firewall will be updated to allow only the necessary outbound traffic from this spoke to complete the operation.
* The final image will be placed into the workload's resource group.

## Steps

### Deploy the spoke

1. Create the AKS jump box image builder network spoke.

   ```bash
   RESOURCEID_VNET_HUB=$(az deployment group show -g rg-enterprise-networking-hubs -n hub-region.v0 --query properties.outputs.hubVnetId.value -o tsv)

   # [This takes about one minute to run.]
   az deployment group create -g rg-enterprise-networking-spokes -f networking/spoke-BU0001A0005-00.json -p location=eastus2 hubVnetResourceId="${RESOURCEID_VNET_HUB}"
   ```

1. Update the regional hub deployment to account for the requirements of the spoke.

   Now that the first spoke network is created, the hub network's firewall needs to be updated to support the Azure Image Builder process that will execute in there. The hub firewall does NOT have any default permissive egress rules, and as such, each needed egress endpoint needs to be specifically allowed. This deployment builds on the prior with the added allowances in the firewall.

   > :eyes: If you're curious to see what changed in the regional hub, [view the diff](https://diffviewer.azureedge.net/?l=https://raw.githubusercontent.com/mspnp/aks-secure-baseline/regulated/networking/hub-region.v0.json&r=https://raw.githubusercontent.com/mspnp/aks-secure-baseline/regulated/networking/hub-region.v1.json).

   ```bash
   RESOURCEID_SUBNET_AIB=$(az deployment group show -g rg-enterprise-networking-spokes -n spoke-BU0001A0005-00 --query properties.outputs.imageBuilderSubnetResourceId.value -o tsv)

   # [This takes about three minutes to run.]
   az deployment group create -g rg-enterprise-networking-hubs -f networking/hub-region.v1.json -p location=eastus2 aksImageBuilderSubnetResourceId="${RESOURCEID_SUBNET_AIB}"
   ```

### Build and deploy the jump box image

Now that we have our image building network created, egressing through our hub, and all NSG/firewall rules applied, it's time to build and deploy our jump box image. We are using the general purpose AKS jump box image as defined in the [AKS Jump Box Image Builder repository](https://github.com/mspnp/aks-jumpbox-imagebuilder); which comes with baked-in tooling such as Azure CLI, kubectl, helm, and flux. The network rules applied in the prior steps support its build-time requirements. If you use this infrastructure to build a modified version of this image template, you may need to add additional network allowances.

1. Deploy custom Azure RBAC roles. _Optional._

   Azure Image Builder requires permissions to be granted to its runtime identity. The following deploys two _custom_ Azure RBAC roles that encapsulate those exact permissions necessary. If you do not have permissions to create Azure RBAC roles in your subscription, you can skip this step. However, in Step 2 below, you'll then be required to apply existing built-in Azure RBAC roles to the service's identity, which are more-permissive than necessary, but would be fine to use for this walkthrough.

   ```bash
   # [This takes about one minute to run.]
   az deployment sub create -u https://raw.githubusercontent.com/mspnp/aks-jumpbox-imagebuilder/main/createsubscriptionroles.json -l centralus -n DeployAibRbacRoles
   ```

1. Create the AKS jump box image template. (🛑 _if not using custom roles._)

   Next you are going to deploy the image template and Azure Image Builders's managed identity. This is being done directly into our workload resource group for simplicity. You can choose to deploy this to a separate resource group if you wish. This "golden image" generation process would typically happen out-of-band to the workload management.

   ```bash
   #NETWORKING_ROLEID=4d97b98b-1d4f-4787-a291-c67834d212e7 # Network Contributor -- Only use this if you did not, or could not, create custom roles.  This is more permission than necessary.)
   ROLEID_NETWORKING=$(az deployment sub show -n DeployAibRbacRoles --query 'properties.outputs.roleResourceIds.value.customImageBuilderNetworkingRole.guid' -o tsv)
   #IMGDEPLOY_ROLEID=b24988ac-6180-42a0-ab88-20f7382dd24c  # Contributor -- only use this if you did not, or could not, create custom roles. This is more permission than necessary.)
   ROLEID_IMGDEPLOY=$(az deployment sub show -n DeployAibRbacRoles --query 'properties.outputs.roleResourceIds.value.customImageBuilderImageCreationRole.guid' -o tsv)

   # [This takes about one minute to run.]
   az deployment group create -g rg-bu0001a0005 -u https://raw.githubusercontent.com/mspnp/aks-jumpbox-imagebuilder/main/azuredeploy.json -p buildInVnetResourceGroupName=rg-enterprise-networking-spokes buildInVnetName=vnet-spoke-BU0001A0005-00 buildInVnetSubnetName=snet-imagebuilder location=eastus2 imageBuilderNetworkingRoleGuid="${ROLEID_NETWORKING}" imageBuilderImageCreationRoleGuid="${ROLEID_IMGDEPLOY}" imageDestinationResourceGroupName=rg-bu0001a0005 -n CreateJumpBoxImageTemplate
   ```

1. Build the general-purpose AKS jump box image.

   Now we'll build the actual VM golden image we will use for our jump box using Azure Image Builder. This uses the template created in the prior step and is executed under the authority of the managed identity (and its role assignments) also created in the prior step.

   ```bash
   IMAGE_TEMPLATE_NAME=$(az deployment group show -g rg-bu0001a0005 -n CreateJumpBoxImageTemplate --query 'properties.outputs.imageTemplateName.value' -o tsv)

   # [This takes about >> thirty minutes << to run.]
   az image builder run -n $IMAGE_TEMPLATE_NAME -g rg-bu0001a0005
   ```

   > A successful run of the command above is typically shown with no output or a success message. An error state will be typically be presented if there was an error. To see if your image was built successfully, you can go to the **rg-bu0001a0005** resource group in the portal and look for a created VM Image resource. It will have the same name as the Image Template resource created in Step 2.

   This does take a significant amount of time to run while the image building is happening. Feel free to read ahead, but you should not proceed until this is complete. If you need to perform this reference implementation walk through multiple times, we suggest you create this image in a place that can survive the deleting and recreating of this reference implementation to save yourself this time in a future execution of this guide.

1. Delete image building resources. _Optional._

   Image building can be seen as a transient process, and as such, you may wish to remove all resources used as part of the process. At this point, if you are happy with your generated image, you can delete the **Image Template** (_not Image!_) in `rg-bu0001a0005`, AIB user managed identity (`mi-aks-jumpbox-imagebuilder-...`) and its role assignments. See instructions to do so in the [AKS Jump Box Image Builder guidance](https://github.com/mspnp/aks-jumpbox-imagebuilder#broom-clean-up-resources) for more details.

   Deleting these build-time resources will not delete the golden VM image you just created for your jump box. For the purposes of this walkthrough, there is no harm in leaving these transient resources behind.

## :closed_lock_with_key: Security

This specific jump box image is considered general purpose; its creation process and supply chain has not been hardened. For example, the jump box image is built on a public base image, and is pulling OS package updates from Ubuntu and Microsoft public servers. Additionally, Azure CLI, Helm, Flux, and Terraform are installed straight from the Internet. Ensure processes like these adhere to your organizational policies; pulling updates from your organization's patch servers, and storing well-known 3rd party dependencies in trusted locations that are available from your builder's subnet. If all necessary resources have been brought "network-local", the NSG and Azure Firewall allowances should be made even tighter. Also apply all standard OS hardening procedures your organization requires for privileged access machines such as these. Finally, ensure all desired security and logging agents are installed and configured. All jump boxes (or similar access solutions) should be _hardened and monitored_, as they span two distinct security zones. **Both the jump box compute (and its image/container) is an attack vector that needs to be considered when evaluating cluster access solutions** and must be considered as part of your compliance concerns.

## Pipelines and other considerations

Image building using Azure Image Builder lends itself well to having a secured, auditable, and transient image building infrastructure. Consider building pipelines around the generation of hardened and approved images to create a repeatably compliant process. Also we recommend pushing these images to your organization's [Azure Shared Image Gallery](https://docs.microsoft.com/azure/virtual-machines/shared-image-galleries) for geo-distribution and added management capabilities. These features were skipped for this reference implementation to avoid added illustrative complexity/burden.

### Next step

:arrow_forward: [Configure jump box users](./07-aks-jumpbox-users.md).
