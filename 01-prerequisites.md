# Prerequisites

This is the starting point for the end-to-end instructions on deploying the [AKS Baseline for Regulated Workloads reference implementation](./README.md). There is required access and tooling you'll need in order to accomplish this. Follow the instructions below and on the subsequent pages so that you can get your environment and subscription ready to proceed with the AKS cluster creation.

## Symbols

Throughout this walkthrough, take note of the following symbol.

>🛑 -  __Manual Modification Required__. When this symbol appears on a step, you will need to modify the commands as indicated prior to running them.

## Steps

1. An Azure subscription. If you don't have an Azure subscription, you can create a [free account](https://azure.microsoft.com/free).

   > 💡 The user initiating the following deployment process _must_ have the following minimal set of Azure Role-Based Access Control (RBAC) roles:
   >
   > * [Contributor role](https://docs.microsoft.com/azure/role-based-access-control/built-in-roles#contributor) is _required_ at the _subscription_ level to have the ability to create resource groups, create & assign policy, and perform deployments at both the subscription and resource group level.
   > * [User Access Administrator role](https://docs.microsoft.com/azure/role-based-access-control/built-in-roles#user-access-administrator) is _required_ at the subscription level since you'll be performing role assignments to managed identities across various resource groups.

1. An Azure AD tenant to associate your Kubernetes RBAC Cluster API authentication to.

   > 💡 The user or service principal initiating the deployment process _must_ have the following minimal set of Azure AD permissions assigned:
   >
   > * Azure AD [User Administrator](https://docs.microsoft.com/azure/active-directory/users-groups-roles/directory-assign-admin-roles#user-administrator-permissions) is _required_ to create a "break glass" AKS admin Active Directory Security Group and User. Alternatively, you could get your Azure AD admin to create this for you when instructed to do so.
   >   * If you are not part of the User Administrator group in the tenant associated to your Azure subscription, consider [creating a new tenant](https://docs.microsoft.com/azure/active-directory/fundamentals/active-directory-access-create-new-tenant#create-a-new-tenant-for-your-organization) to use while evaluating this implementation.

   The Azure AD tenant backing your Cluster's API RBAC does NOT need to be the same tenant associated with your Azure subscription. Your organization may have dedicated Azure AD tenants used specifically as a separation between Azure resource management, and Kubernetes control plane access. Ensure you're following your organization's practices when it comes to separation of identity stores to ensure limited "impact radius" on any compromised accounts.

1. Latest [Azure CLI installed](https://docs.microsoft.com/cli/azure/install-azure-cli?view=azure-cli-latest) or you can perform this from Azure Cloud Shell by clicking below.

   [![Launch Azure Cloud Shell](https://docs.microsoft.com/azure/includes/media/cloud-shell-try-it/launchcloudshell.png)](https://shell.azure.com)

   Ensure you're logged into the subscription in which you plan on deploying this reference to.

1. Enable preview features.

   * Follow the instructions to [enable Azure AD Pod Identity](https://docs.microsoft.com/azure/aks/use-azure-ad-pod-identity#before-you-begin) on your target subscription. You do not need to install the preview CLI extension or follow other instructions on this page.

1. Fork this repository and clone it locally. 🛑

   ```bash
   GITHUB_ACCOUNT_NAME=YOUR-GITHUB-ACCOUNT-NAME-GOES-HERE

   git clone https://github.com/${GITHUB_ACCOUNT_NAME}/aks-regulated-baseline.git
   cd aks-regulated-baseline
   ```

   > 💡 The steps shown here and elsewhere in the reference implementation use Bash shell commands. On Windows, you can use the [Windows Subsystem for Linux](https://docs.microsoft.com/windows/wsl/about#what-is-wsl-2) to run Bash.

1. Ensure [OpenSSL is installed](https://github.com/openssl/openssl#download) in order to generate the example self-signed certs used in this implementation.

### Next step

:arrow_forward: [Generate TLS Certificates](./02-ca-certificates.md).
