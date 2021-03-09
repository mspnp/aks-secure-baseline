# Deploy the Workload

This point in the steps marks a significant transition in roles and purpose. At this point, you have a [AKS cluster that is deployed in an architecture that will help your compliance needs](./09-aks-cluster.md) and is [bootstrapped with core tooling](./11-gitops.md) you feel are requirements for your solution, all managed via Flux. At this point, you've got a cluster without any business workloads.

The next few steps will walk through considerations that are specific to the first workload in the cluster. Workloads are a mix of potential infrastructure changes (e.g. Azure Application Gateway routes, Azure resources for the workload itself -- such as CosmosDB for state storage and Azure Cache for Redis for cache.), privileged cluster changes (i.e. creating target namespace, creating and assigning any specific cluster or namespace roles, etc.), deciding on how that "last mile" deployment of these workloads will be handled (e.g. using the `snet-management-agents` subnet adjacent to this cluster), and workload teams which are responsible for creating the container image(s), building deployment manifests, etc. Many regulations have a clear separation of duties requirements, be sure in your case you have documented and understood change management process. How you partition this work will not be described here because there isn't a one-size-fits-most solution. Allocate time to plan, document, and educate on these concerns.

## Expected results

### Workload image built

The workload is simple ASP.NET 5.0 application that is built and deployed to show basic network isolation concerns. Because this workload is in source control only, and not pre-built and published in a public container registry for you to consume, part of the steps here will be to compile this image in a dedicated, network-restricted build agent within Azure Container Registry. You've already deployed the agent as part of a prior step. Like all images you bring to your cluster, this workload image will also pass through a quarantine approval gate.

### Workload is deployed

While typically workload deployment happens via deployment pipelines, to keep this walkthrough easier to get through, we are deploying the workload manually. It's not part of the GitOps baseline overlay, and will be done directly from your Azure Bastion jump box.

## Steps

1. Use your Azure Container Registry build agents to build and quarantine the workload.

   ```bash
   ACR_NAME_QUARANTINE=$(az deployment group show -g rg-bu0001a0005 -n cluster-stamp --query properties.outputs.quarantineContainerRegistryName.value -o tsv)

   az acr build -t quarantine/a0005/chain-api:1.0 -r $ACR_NAME_QUARANTINE --platform linux/amd64 --agent-pool acragent -f SimpleChainApi/Dockerfile https://github.com/mspnp/aks-secure-baseline#feature/regulated-web-api-ui:SimpleChainApi
   ```

   > You may see one or more `BlobNotFound` error messages in the early part of the build; this is okay, and you shouldn't terminate the command. It's polling for source code to be completely transferred from GitHub before proceeding. Azure cli will show this process in what appears to look like an error state.

   We are using your own dedicated task agents here, in a dedicated subnet, for this process. Securing your workload pipeline components are critical to having a compliant solution. Ensure your build pipeline matches your desired security posture. Consider performing image building in an Azure Container Registry that is network-isolated from your clusters (unlike what we're showing here where it's on the same virtual network for simplicity.) Ensure build logs are captured. That build ACR instance might also serve as your quarantine instance as well. Once the build is complete and post-build audits are complete, then it can be imported to your "live" registry.

1. Release the workload image from quarantine.

   ```bash
   ACR_NAME=$(az deployment group show -g rg-bu0001a0005 -n cluster-stamp --query properties.outputs.containerRegistryName.value -o tsv)

   az acr import --source quarantine/a0005/chain-api:1.0 -r $ACR_NAME_QUARANTINE -t live/a0005/chain-api:1.0 -n $ACR_NAME
   ```

1. Update workload ACR references in your kustomization files.

   ```bash
   cd ../workload
   sed -i "s/REPLACE_ME_WITH_YOUR_ACRNAME/${ACR_NAME}/g" */*/kustomization.yaml

   git commit -a -m "Update the four workload images to use my Azure Container Registry instance."
   ```

1. Push this change to your repo.

   ```bash
   git push
   ```

1. _From your Azure Bastion connection_, deploy the sample workloads to cluster.

   The sample workload will be deployed across two namespaces. An "in-scope" namespace (`a0005-i`) and an "out-of-scope" (`a0005-o`) namespace to represent a logical separation of components in this solution. The workloads that are in `a0005-i` are assumed to be directly or indirectly handling data that is in regulatory scope. The workloads that are in `a0005-o` are supporting workloads, but they themselves do not handle in-scope regulatory data. While this entire cluster is subject to being in regulatory scope, consider making it clear in your namespacing, labeling, etc. what services actively engage in the handling of critical data, vs those that are in a supportive role and should never handle or be able to handle that data. Ideally you'll want to minimize the workload in this cluster to just those workloads dealing with the data under regulatory compliance, _running non-scoped workloads in an alternate cluster_. Sometimes that isn't practical, therefor when you co-mingle the workloads, you usually need to treat almost everything in scope, but that doesn't mean you can't treat the truly in-scope components with added segregation and care.

   In addition to namespaces, the cluster also has dedicated node pools for the "in-scope" components. This helps ensure that out-of-scope workload components (where possible), do not run on the same hardware as the in-scope components.  Ideally your in-scope node pools will run just those workloads that deal with in-scope regulatory data and security agents to support the your regulatory obligations. These two node pools benefit from being on separate subnets as well, which allows finer control as the Azure Network level (NSG rules and Azure Firewall rules).

   ```bash
   cd ../workload

   # Get the workload ACR endpoint changes you committed above.
   git pull

   # Deploy "in-scope" components.  These will live in the a0005-i namespace and will be
   # scheduled on the aks-npinscope01 node pool - dedicated to just those workloads.
   kubectl apply -k a0005-i/web-frontend
   kubectl apply -k a0005-i/microservice-c

   # Deploy "out-of-scope" components. These will live in the a0005-o namespace and will
   # be scheduled on the aks-npooscope01 node pool - used for all non in-scope components.
   kubectl apply -k a0005-o/microservice-a
   kubectl apply -k a0005-o/microservice-b
   
   ```

## Workload networking

You are responsible to control the exposure of your in-cluster endpoints and to control what traffic is allowed to ingress and egress. **Kubernetes, by default, is a full-trust platform at the network level.** That means the default unit of isolation in Kubernetes is the cluster -- from a networking perspective. To achieve a zero-trust network environment, you'll need to apply in-cluster (and Azure network) constructs to build that foundation. This is done through the use of Kubernetes Network Policies and/or service mesh constructs, across all of your namespaces -- not just your workloads' namespace(s). Expect to invest time in documenting the exact network flows of your applications' components _and your baseline tooling_ to build out the in-cluster restrictions to model those expected network flows.

### Network policies

The foundation of in-cluster network security is Kubernetes Network Policies. This cluster is deployed with Azure NPM (Azure Network Policy Manager) which enforces standard Kubernetes NetworkPolicy resources across your cluster. Kubernetes Network Policies are a Layer 3 and Layer 4 construct that allow you to define what traffic is allowed into and out of a pod. Network Policies are namespaced resources, and as such you need to manage them across your namespaces. This reference implementation deploys a default "deny-all" policy to establish immediate zero-trust in the workload namespaces. Then the workload overlays just the network traffic it needs to be functional and observable. While we only applied them to the workload namespaces, all namespaces that you control (ie. _not_ `kube-system`, `gatekeeper-system`, etc.), should have zero-trust policies applied. Time wasn't invested to do that in this walkthrough because your bootstrapped solutions are likely to be bespoke to your cluster and you'll need to apply the policies that make sense for them.

For a namespace in which services will be talking to other services, **the recommended zero-trust network policy for AKS can be found in [networkpolicy-denyall.yaml](cluster-manifests/a0005-i/networkpolicy-denyall.yaml)**. This blocks ALL traffic (in and out) other than outbound to kube-dns (which is CoreDNS in AKS). If you don't need DNS resolution across all workloads in the namespace, then you can remove that from the deny all and apply it selectively to pods that do require it (if any).

#### Alternatives

If you want to have more advanced network policies than what Azure NPM supports, you'll need to bring in another solution, such as [Project Calico](https://docs.microsoft.com/azure/aks/use-network-policies#network-policy-options-in-aks). Solutions like Project Calico these extend Kubernetes Network Policies to be more advanced in their scope (such as introducing "cluster-wide" policies, advanced pod/namepsace selectors, and may even support Layer 7 constructs (routes and FQDNs). See more choices in the [CNCF Networking landscape](https://landscape.cncf.io/card-mode?category=cloud-native-network&grouping=category).

Sometimes you can combine Network Policies with the features of a service mesh to achieve your desired network traffic flow restrictions. Consider the management and complexity cost of any added network policy solution you bring into your cluster, and understand where your regulatory obligations fit into that.

### Service Mesh

The workload (split across four components - `web-frontend`, `microservice-a`, `microservice-b`, and `microservice-c`) is deployed across two separate node pools and across two separate namespaces. But because this workload represents a set of connected microservices, this workload has joined the cluster's service mesh. Doing so provides the following benefits.

* Network access is removed from all access outside of the mesh.
* Network access is limited to defined HTTP routes, with explicitly defined sources and destinations.
* mTLS encryption between all components in the mesh.
* mTLS encryption between your ingress controller and the endpoint into the mesh.
* mTLS rotation happening every 24 hours.

This reference implementation is using Open Service Mesh to demonstrate these concepts. This service mesh is currently in development and is not suitable yet for production purposes, but is currently on the [AKS roadmap](https://aka.ms/AKS/roadmap) to be [included as a managed add-on](https://github.com/Azure/AKS/issues/1787). Your choice in service mesh, if any, should be measured like any other solution you bring into your cluster -- based on things like supportability, security, features, observability, etc. Other popular CNCF choices for meshes that perform the functions listed above are: Istio, Linkerd, Traefik Mesh. See more choices in the [CNCF Service Mesh landscape](https://landscape.cncf.io/card-mode?category=service-mesh&grouping=category).

**Using a service mesh is not a requirement.** The most obvious benefit is the transparent mTLS features that typically come with service mesh implementations. Not all regulatory requirements demand TLS between components in your network. Consider the management and complexity cost of any solution you bring into your cluster, and understand where your regulatory obligations fit into that.

### Defense in depth

Network security in AKS is indeed a defense-in-depth strategy; as affordances exist at various levels, with various capabilities, both within and external to the cluster.

* The inner-most ring is applying Kubernetes Network Policy within each namespace, which applies at the _pod_ level and operates at the L3 & L4 network layers.
  * Using a network policy manager like Project Calico, can extend upon the native Kubernetes Network Policies and allow added expressiveness, and may even operate at network layers 5 through 7. They can typically target more than pods. If native Kubernetes network policies are not sufficient to describe your network flow, consider using using a solution like Project Calico.
* Service meshes bring a lot of features to the table, typically anchored around application-centric reasons (advanced traffic splitting, advanced service discovery, observability and visualization of enrolled services). However, a feature shared by many is automatic mTLS connections between services in the mesh. Not all regulatory compliance requires end-to-end encryption, and terminating TLS at the the ingress point to your private network may be sufficient. However, this reference implementation is built to encourage you to take this further. Not only do we bring TLS into the ingress controller, we then continue it directly into the workload and throughout the workload's communication via a service mesh.
* Using Azure Policy, you can restrict that all ingresses must use HTTPS, and you can restrict what IP addresses are available for all load balancers in your cluster. This ensures that network entry points are not freely able to "come and go," but instead are restricted to your defined & documented network exposure plan. Likewise you can deny any Public IP requests which would potentially circumvent any other side-stepping of network controls.
* Your ingress controller may also have an allow-list affordance that restricts invocations to originate from known sources. In this reference implementation, your ingress controller will only accept traffic from your Azure Application Gateway instance. This is in a delegated subnet, in which no other compute type is allowed to reside, making this CIDR range a trusted source scope.
* Ingress controllers often support middleware such as JWT validation, that allow you to offload traffic that may have been routed properly, but is lacking expected or valid credentials. Offloading "bad" traffic is something you want to push "left" as far a possible (out to Application Gateway if possible). If you workload is dealing is "bad" traffic, that means additional resource consumption in the cluster and also executing undesired traffic in the context of a service that might have privileged access to data under regulatory compliance.
* `LoadBalancer` requests, such as those used by your ingress controller, will manage a Standard Internal Azure Load Balancer resource. That resource should live in a dedicated subnet, with a NSG around it that limits network traffic to be sourced from your Azure Application Gateway subnet.
* This specific AKS cluster is spread across three node pools (system, in-scope, and out-of-scope node pools), each in their own _dedicated_ subnet in their own _dedicated_ VMSS pools. This allows you to reason over L3/L4 course-grained network considerations for all compute running in those subnets. These course-grained rules are applied as NSG rules on the nodepools' subnets, and are really a superset of all expected network flows into and out of your cluster.
* All of your resources live in a virtual network, with no public IP address and minimal public DNS records (due to Private Link), with the exception of your Application Gateway (which is your public entry point to web/workload traffic) and Azure Firewall (which is your public exit point for all cluster-initiated traffic). This keeps all of your resources generally undiscoverable and isolated from public internet and other Azure customers.
* [Azure Network Watcher and NSG Flow Logs](https://docs.microsoft.com/azure/network-watcher/network-watcher-nsg-flow-logging-overview) should be enabled across all critical/sensitive components of your network. This allows for logging/auditing layer 4 traffic flows and can also help triage unexpected blocked traffic. _This reference implementation did NOT enable NSG flow logs for simplicity, but your final solution should enable them._
* Web (workload) traffic is tunneled into your cluster through Azure Application Gateway. This not only serves as your ingress point to your virtual network, it also is your first point of traffic inspection. Azure Application Gateway has a WAF that detects potentially dangerous requests following the OWASP 3.1 rule set. Likewise it will expose your workload via your preferred TLS version (1.2+) with preferred ciphers (`TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384` and `TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256` in this case). Traffic that is unrecognized to Azure Application Gateway is dropped without performance impact to your cluster. Geo IP restrictions can also be applied via your Azure Application Gateway. Lastly, if you expect traffic from only specific IP addresses to hit your gateway (such as coming from Azure Front Door, an Azure Firewall, an Application Delivery Network (ADN), etc.), you can further restrict inbound traffic to via the NSG applied to its subnet. That NSG also should explicitly only allow outbound traffic to your cluster's ingress point.
* Additionally, Azure API Management can be included in the ingress path for select services (such as APIs) to provide cross-cutting concerns such as JWT validation. You'd typically include API Management for more complex concerns than just JWT validation though, so we don't recommend including it for security-only concerns. But if you plan on using it in your architecture for its other features like cache, versioning, service composition, etc., ensure you're also enabling it as another security layer as well.
* Lastly, all of your traffic originating in your cluster and adjacent subnets are egressing through a firewall. This is the last responsible moment to block and observe outbound requests. If traffic has made it past AKS Network Policies and NSGs, your firewall is standing guard. Azure Firewall is a deny-by-default platform, which means each and every allowance needs to be defined. Those allowances should be clamped specifically to the source of that traffic, using features like IP Groups to group "like" traffic together for ease and consistency of management.
  * Some customers may also include a transparent HTTP(S) proxy, such as Squid, Websense, Zscaler, as part of their egress strategy. This is not covered in this reference implementation, and is usually beyond the scope of common regulatory concerns.

Ultimately all layers build on each other, such that a failure/misconfiguration in a local-scope layer can hopefully be caught at a more course-grained layer. Ensure your network documentation includes what security controls exist at what level. Ensure all critical network rules are tested to be functioning as expected.

## Pipeline security

Your compliant cluster architecture requires a compliant inner loop development practice as well, following your documented Secure SDLC. Since this walkthrough is not focused on inner loop development practices, please dedicate time to documenting your SDL, safe deployment practices, your workload's supply chain, and hardening techniques. Consider using solutions like [GitHub Action's container-scan](https://github.com/Azure/container-scan) to check for container-level hardening concerns -- CIS benchmark alignment, CVE detections, etc. even before workloads are pushed to your container registry.

## Container security practices

This reference implementation doesn't dive into security best practices of your code, your base image selection, your `Dockerfile` layers, or your Kubernetes `Deployment` manifest.  None of that content is really specific to the architecture presented here and are best practices regardless if your workload is regulated or not. For guidance on hardening container images, see: TODO GET LINK.

### Next step

:arrow_forward: [End-to-End Validation](./13-validation.md)
