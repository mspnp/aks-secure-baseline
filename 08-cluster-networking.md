# Deploy the Cluster Spoke

A lot of the foundation has been put in place. You have a [regional hub](./05-networking-hub.md) in which your cluster traffic will egress and a [jump box image for cluster management](./06-aks-jumpboximage.md) built along [its user(s)](./07-aks-jumpbox-users.md). Now lay out the next critical component, the cluster's spoke (virtual network).

## Networking in this architecture

The regional spoke network in which your cluster is laid into acts as the first line of defense for your cluster. This network perimeter forms a security boundary where you will restrict the network line of sight into your cluster's compute resources. It also gives your cluster the ability to use private link to talk to adjacent platform-as-a-service resources such as Key Vault and Azure Container Registry. And finally it acts as a layer to restrict and tunnel egressing traffic. All of this adds up to ensure that cluster traffic stays as isolated as possible and free from any possible external influence.

## Expected results

Your `rg-enterprise-networking-spokes` will be populated with the dedicated regional spoke network in which your cluster (and its direct adjacent resources will be connected to). This spoke will have limited Internet exposure and will support Network Security Groups (NSGs) at various levels to further limit network traffic as necessary.

* The network spoke will be called `vnet-spoke-bu0001a0005-01` and have a range of `10.240.0.0/16`.
* The spoke is broken into multiple subnets, each with a clearly defined purpose, appropriate IP range, and maximally restrictive NSG.
* DNS will be forwarded to the hub to support firewall inspection/logging and to support more complex network considerations such as DNS forwarders to your organization's DNS servers.
* The hub's firewall will be updated to allow only the necessary outbound traffic from this spoke's specific resource, management, and workload needs.

## Steps

1. Deploy the cluster spoke.

   ```bash
   RESOURCEID_VNET_HUB=$(az deployment group show -g rg-enterprise-networking-hubs -n hub-region.v0 --query properties.outputs.hubVnetId.value -o tsv)

   # [This takes about five minutes to run.]
   az deployment group create -g rg-enterprise-networking-spokes -f networking/spoke-BU0001A0005-01.json -p location=eastus2 hubVnetResourceId="${RESOURCEID_VNET_HUB}"
   ```

1. Update the regional hub deployment to account for the runtime requirements of the virtual network.

   This is an evolution of same hub template you used before, but now updated with Azure Firewall rules specific to this AKS cluster infrastructure.

   > :eyes: If you're curious to see what changed in the regional hub, [view the diff](https://diffviewer.azureedge.net/?l=https://raw.githubusercontent.com/mspnp/aks-secure-baseline/regulated/networking/hub-region.v1.json&r=https://raw.githubusercontent.com/mspnp/aks-secure-baseline/regulated/networking/hub-region.v2.json).

   ```bash
   RESOURCEID_SUBNET_AIB=$(az deployment group show -g rg-enterprise-networking-spokes -n spoke-BU0001A0005-00 --query properties.outputs.imageBuilderSubnetResourceId.value -o tsv)
   RESOURCEID_SUBNET_NODEPOOLS="['$(az deployment group show -g rg-enterprise-networking-spokes -n spoke-BU0001A0005-01 --query "properties.outputs.nodepoolSubnetResourceIds.value | join ('\',\'',@)" -o tsv)']"
   RESOURCEID_SUBNET_JUMPBOX=$(az deployment group show -g rg-enterprise-networking-spokes -n spoke-BU0001A0005-01 --query properties.outputs.jumpboxSubnetResourceId.value -o tsv)

   # [This takes about seven minutes to run.]
   az deployment group create -g rg-enterprise-networking-hubs -f networking/hub-region.v2.json -p location=eastus2 aksImageBuilderSubnetResourceId="${RESOURCEID_SUBNET_AIB}" nodepoolSubnetResourceIds="${RESOURCEID_SUBNET_NODEPOOLS}" aksJumpboxSubnetResourceId="${RESOURCEID_SUBNET_JUMPBOX}"
   ```

### Next step

:arrow_forward: [Deploy the AKS cluster](./09-aks-cluster.md)
