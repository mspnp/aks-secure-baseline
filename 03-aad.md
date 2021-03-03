# Prep for Azure Active Directory Integration

In the prior step, you [ensured you met all prerequisites](./01-prerequisites.md) for this reference implementation deployment; now we'll prepare Azure AD for Kubernetes role-based access control (RBAC). This will ensure you have an Azure AD security group(s) and user(s) assigned for group-based Kubernetes control plane access.

## Nomenclature

We are giving this cluster a generic identifier that we'll use to build relationships between various resources. We'll assume that Business Unit 0001 is building a regulated workload identified internally as App ID 0005 in their service tree.  To that end, you may see references to `bu0001a0005` throughout the rest of this implementation. Naming conventions are an important organization technique for your resources; for your final implementation, please use what is appropriate for your team/organization.

## Azure AD tenant selection

AKS provides a separation between Azure management control plane access control and Kubernetes control plane access control. This deployment process, creating and associating Azure resources with each other, is an example of Azure management control plane access. This is a relationship between your Azure AD tenant associated with your Azure subscription and is what grants you the permissions to create networks, clusters, managed identities, and create relationships between them. Kubernetes has it's own control plane, exposed via the Cluster API endpoint, and honors the Kubernetes RBAC authorization model. This endpoint is where `kubectl` commands are executed against, for example.

AKS allows for disparate Azure AD tenants between these two control planes; one tenant can be used for Azure management control plane and another for Cluster API authorization. You can also use the same tenant for both. Regulated environments may mandate a clear tenant separation to address impact radius and potential lateral movement; at the significant added complexity and cost of managing multiple identity stores. This reference implementation will work with either model. Most customers, even in regulated environments, use a single Azure AD tenant model. Ensure your final implementation is aligned with how your organization and compliance requirements dictate identity management.

## Expected results

Following the steps below you will result in an Azure AD configuration that will be used for Kubernetes control plane (Cluster API) authorization.

| Object                         | Purpose                                                 |
|--------------------------------|---------------------------------------------------------|
| A Cluster Admin Security Group | Will be mapped to `cluster-admin` Kubernetes role.      |
| A Cluster Admin User           | Represents at least one break-glass cluster admin user. |
| Cluster Admin Group Membership | Association between the Cluster Admin User(s) and the Cluster Admin Security Group. Ideally there would be NO standing group membership associations made, but for the purposes of this material, you should have assigned the admin user(s) created above. |
| _Additional Security Groups_   | _Optional._ A security group (and its memberships) for the other built-in and custom Kubernetes roles you plan on using. |

## Steps

1. Query and save your Azure subscription's tenant id.

   ```bash
   TENANTID_AZURERBAC=$(az account show --query tenantId -o tsv)
   ```

1. Log in to the tenant where Kubernetes Cluster API authorization will be associated with.

   Capture the Azure AD Tenant ID that will be associated with your cluster's Kubernetes RBAC for Cluster API access. This is _typically_ the same tenant as your Azure RBAC, see [Azure AD tenant selection](#Azure-AD-tenant-selection) above for more details. However, if you do not have access to manage Azure AD groups and permissions, you may create a temporary tenant specifically for this walkthrough so that you're not blocked at this point.

   ```bash
   az login -t <Replace-With-ClusterApi-AzureAD-TenantId> --allow-no-subscriptions
   TENANTID_K8SRBAC=$(az account show --query tenantId -o tsv)
   ```

1. Create/identify the Azure AD security group that is going to map to the [Kubernetes Cluster Admin](https://kubernetes.io/docs/reference/access-authn-authz/rbac/#user-facing-roles) role `cluster-admin`.

   If you already have a security group that is appropriate for your cluster's admin service accounts, use that group and skip this step. If using your own group or your Azure AD administrator created one for you to use; you will need to update the group name throughout the reference implementation.

   > :warning: This cluster role is the highest-privileged role available in Kubernetes. Members of this group will have _complete access throughout the cluster_. Generally speaking, there should be **no standing access** at this level; and access is [implemented using Just-In-Time AD group membership](https://docs.microsoft.com/azure/aks/managed-aad#configure-just-in-time-cluster-access-with-azure-ad-and-aks) (_Requires Azure AD PIM found in Premium P2 SKU._). In the next step, you'll create a dedicated account for this highly-privileged, administrative role for this walkthrough. Ensure your all of your cluster's RBAC assignments and memberships are maliciously managed and auditable; aligning to minimal or no standing admin permissions and all other organization & compliance requirements.

   ```bash
   AADOBJECTNAME_GROUP_CLUSTERADMIN=cluster-admins-bu0001a000500
   AADOBJECTID_GROUP_CLUSTERADMIN=$(az ad group create --display-name $AADOBJECTNAME_GROUP_CLUSTERADMIN --mail-nickname $AADOBJECTNAME_GROUP_CLUSTERADMIN --description "Principals in this group are cluster admins in the bu0001a000500 cluster." --query objectId -o tsv)
   ```

1. Create a "break-glass" cluster administrator user for your AKS cluster.

   This steps creates a dedicated account that you can use for cluster administrative access. This account should have no standing permissions on any Azure resources; a compromise of this account then cannot be parlayed into Azure management control plane access. If using the same tenant that your Azure resources are managed with, some organizations employ an alt-account strategy. In that case, your cluster admins' alt account(s) might satisfy this step.

   ```bash
   TENANTDOMAIN_K8SRBAC=$(az ad signed-in-user show --query 'userPrincipalName' -o tsv | cut -d '@' -f 2 | sed 's/\"//')
   AADOBJECTNAME_USER_CLUSTERADMIN=bu0001a000500-admin
   AADOBJECTID_USER_CLUSTERADMIN=$(az ad user create --display-name=${AADOBJECTNAME_USER_CLUSTERADMIN} --user-principal-name ${AADOBJECTNAME_USER_CLUSTERADMIN}@${TENANTDOMAIN_K8SRBAC} --force-change-password-next-login --password ChangeMebu0001a0005AdminChangeMe --query objectId -o tsv)
   ```

1. Add the cluster admin user(s) to the cluster admin security group.

   ```bash
   az ad group member add -g $AADOBJECTID_GROUP_CLUSTERADMIN --member-id $AADOBJECTID_USER_CLUSTERADMIN
   ```

1. Create/identify additional security groups to map onto other Kubernetes RBAC roles. _Optional._

   Kubernetes has [built-in, user-facing roles](https://kubernetes.io/docs/reference/access-authn-authz/rbac/#user-facing-roles) like _admin_, _edit_, and _view_, generally to be applied at namespace levels, which can also be mapped to various Azure AD Groups. Likewise, if you know you'll have additional _custom_ Kubernetes roles created as part of your separation of duties authentication schema, you can create those security groups now as well. For this walk through, you do NOT need to map any of these additional roles.

   In the [`cluster-rbac.yaml` file](./cluster-manifests/cluster-rbac.yaml) and the various namespaced [`rbac.yaml files`](./cluster-manifests/cluster-baseline-settings/rbac.yaml), you can uncomment what you wish and replace the `<replace-with-an-aad-group-object-id...>` placeholders with corresponding new or existing AD groups that map to their purpose for this cluster or namespace. You do not need to perform this action for this walk through; they are only here for your reference. By default, in this implementation, no additional _cluster_ roles will be bound other than `cluster-admin`. For your final implementation, create custom kubernetes roles to align specifically with those job functions of your team, and create role assignments as needed. Handle [JIT access](https://docs.microsoft.com/azure/active-directory/privileged-identity-management/groups-features) at the group membership level in Azure AD via Privileged Identity Management, and leverage conditional access policies where possible. Always strive to minimize standing permissions, especially on identities that have access to in-scope components.

   :bulb: Alternatively/Additionally, you can make some of these group associations to [Azure RBAC roles](https://docs.microsoft.com/azure/aks/manage-azure-rbac). At the time of this writing, this feature is still in _preview_. This reference implementation has not been validated with that feature.

1. Set up Azure AD conditional access policies. _Optional. Requires Azure AD Premium._

To support an even stronger authentication model, consider [setting up Conditional Access Policies in Azure AD for your cluster](https://docs.microsoft.com/azure/aks/managed-aad#use-conditional-access-with-azure-ad-and-aks). This gives you further apply restrictions on access to the Kubernetes control plane (e.g. management commands executed through `kubectl`). With conditional access policies in place, you can for example, _require_ multi-factor authentication, restrict authentication to devices that are managed by your Azure AD tenant, or block non-typical sign-in attempts. You will want to apply this to Azure AD groups that are assigned to your cluster with permissions you deem warrant the extra policies (most notability the cluster admin group created above). You will not be setting that up as part of this walkthrough, but consider doing so for your final implementation as part of your defense-in-depth strategy and to support compliance requirements.

### Next step

:arrow_forward: [Prepare the target subscription](./04-subscription.md)
