# Deploy the Workload

TODO

This point in the steps marks a significant transition in roles and purpose. At this point, you have a AKS cluster that is deployed in an architecture that will help your compliance needs and is bootstrapped with core additions you feel are requirements for your solution, all managed via Flux. A cluster without any business workloads, essentially. The next few steps will walk through considerations that are specific to the first workload in the cluster. Workloads are a mix of potential infrastructure changes (e.g. Azure Application Gateway routes, Azure Resources for the workload itself -- such as CosmosDB for state storage and Azure Cache for Redis for cache.), privileged cluster changes (i.e. creating target namespace, creating and assigning, any specific cluster or namespace roles, etc.), deciding on how that "last mile" deployment of these workloads will be handled (e.g. using the ops subnet adjacent to this cluster), and workload teams which are responsible for creating the container image(s), building deployment manifests, etc. Many regulations have a clear separation of duties requirements, be sure in your case you have documented and understood change management process. How you partition this work will not be described here because there isn't a one-size-fits-most solution. Allocate time to plan, document, and educate on these concerns.

<!-- The cluster now has an [Traefik configured with a TLS certificate](./13-secret-management-and-ingress-controller.md). The last step in the process is to deploy the workload, which will demonstrate the system's functions. -->

## Expected results

### Workload image built

The workload is simple ASP.NET 5.0 application that we built to show network isolation concerns. Because this workload is in source control only, and not pre-built for you to consume, part of the steps here will be to compile this image in a dedicated, network-restricted build agent within Azure Container Registry. You've already deployed the agent as part of a prior step.

### Workload passes through quarantine

All images you bring to your cluster, workload or system-level, should pass through a quarantine approval gate.

### Workload is deployed

While typically of the above would have happened via a deployment pipeline, to keep this walkthrough easier to get through, we are deploying the workload manually. It's not part of the GitOps baseline overlay, and will be done directly from your Azure Bastion jump box.

TODO: Consider returning a quarantine registry in the outputs of the arm template so these lines can technically look different, even if they are the same values :)

## Steps

1. Use your Azure Container Registry build agents to build and quarantine the workload.

   ```bash
   ACR_NAME_QUARANTINE=$(az deployment group show -g rg-bu0001a0005 -n cluster-stamp --query properties.outputs.containerRegistryName.value -o tsv)

   az acr build -t quarantine/a0005/chain-api:1.0 -r $ACR_NAME_QUARANTINE --platform linux/amd64 --agent-pool acragent -f SimpleChainApi/Dockerfile https://github.com/mspnp/aks-secure-baseline#feature/regulated-web-api-ui:SimpleChainApi
   ```

   > You may see one or more `BlobNotFound` error messages in the early part of the build; this is okay, and you shouldn't terminate the command. It's polling for source code to be completely transferred from GitHub before proceeding. Azure cli will show this process in what appears to look like an error state.

   We are using your own dedicated task agents here, in a dedicated subnet, for this process. Securing your workload pipeline components are critical to having a compliant solution. Ensure your build pipeline matches your desired security posture. Consider performing image building in an Azure Container Registry that is network-isolated from your clusters (unlike what we're showing here where it's on the same virtual network for simplicity.) Ensure build logs are captured (streamed at build time and available afterwards via `az acr taskrun logs`). That build instance might also serve as your quarantine instance as well. Once the build is complete and post-build audits are complete, then it can be imported to your "live" registry. Please note, again for simplicity only, the task runner that is doing the build is not egressed through the Azure Firewall -- in your final implementation strongly consider egressing ACR task runner traffic through Azure firewall for observability and control.

1. Release the workload image from quarantine.

   ```bash
   ACR_NAME=$(az deployment group show -g rg-bu0001a0005 -n cluster-stamp --query properties.outputs.containerRegistryName.value -o tsv)

   az acr import --source quarantine/a0005/chain-api:1.0 -r $ACR_NAME_QUARANTINE -t live/a0005/chain-api:1.0 -n $ACR_NAME
   ```

1. Update workload ACR references in your kustomization files.

   ```bash
   cd ../workload
   grep -lr REPLACE_ME_WITH_YOUR_ACRNAME --include=kustomization.yaml | xargs sed -i "s/REPLACE_ME_WITH_YOUR_ACRNAME/${ACR_NAME}/g"

   git commit -a -m "Update workload images to use my Azure Container Registry instance."
   ```

1. Push this changes to your repo.

   ```bash
   git push
   ```

1. _From your Azure Bastion connection_, get this update.

   ```bash
   git pull
   ```

1. _From your Azure Bastion connection_, deploy the sample workloads to cluster.

   The sample workload will be deployed across two namespaces. An "in-scope" namespace and an "out-of-scope" namespace to represent a logical separation of components in this solution. The workloads that are "in-scope" are assumed to be specifically handling data that is in regulatory scope. The workloads that are in "out-of-scope" are supporting workloads, but they themselves do not handle in-scope regulatory data. While this entire cluster is subject to being in regulatory scope, consider making it clear in your namespaceing, labeling, etc what services actively engage in the handling of critical data, vs those that are in a supportive role and should never handle or be able to handle that data. Ideally you'll want to minimize the workload in this cluster to just those workloads dealing with the data under regulatory compliance, running non-scoped workloads in an alternate cluster. Sometimes that isn't practical, therefor when you co-mingle the workloads, you usually need to treat almost everything in scope, but that doesn't mean you can't treat the truly in-scope components with added segregation and care.

   In addition to namespaces, the cluster also has dedicated node pools for the truly in-scope components. This helps ensure that out-of-scope workload components (where possible), do not run on the same hardware as the in-scope components.  Ideally your in-scope node pools will run just those workloads that deal with in-scope regulatory data and security agents to support the your regulatory obligations.

   ```bash
   cd ../workload

   # Deploy "In-Scope" components.  These will live in the a0005-i namespace and will be
   # scheduled on the aks-npinscope01 node pool - dedicated to just those workloads.
   kubectl apply -k a0005-i/microservice-web
   kubectl apply -k a0005-i/microservice-c

   # Deploy "Out-of-Scope" components. These will live in the a0005-o namespace and will
   # be scheduled on the aks-npooscope01 node pool - used for all non in-scope components.
   kubectl apply -k a0005-o/microservice-a
   kubectl apply -k a0005-o/microservice-b
   
   ```

   While we are deploying across two separate node pools, and across two separate namespaces, this workload has joined the cluster's service mesh as a unified workload. Doing so provides the following benefits.

     * Network access is removed from all access outside of the mesh.
     * Network access is limited to defined routes, with explicitly defined sources, destinations (including restricting to known endpoints and routes exclusively).
     * mTLS encryption between all components in the mesh.
     * mTLS encryption between your ingress controller and the endpoint into the mesh.
     * mTLS rotation happening every 24 hours.

   The key takeaways here are to control exposure of your endpoints within your cluster. Kubernetes, by default, is a full-trust platform at the network level. Through the use of Network Policies and/or service mesh constructs, you need to build a zero-trust environment, across all of your namespaces -- not just your workloads' namespace(s). Expect to invest time in documenting the exact network flows of your applications' components and your baseline tooling to build out the in-cluster restrictions to model those expected network flows.

   Network security in AKS can be thought of in defense in depth.

     * The inner-most ring is applying Network Policy within each namespace, which applies at the _pod_ level and operates at L3 & L4 network layers.
       * Additionally, 3rd-party offerings like Calico, can extend upon the native Kubernetes Network Policies and allow added restrictions that operate at network layers 5 through 7. It also can target more than pods. If native network policies are not sufficient to describe your network flow, consider using using a solution like Calico.
     * Service meshes bring a lot of features to the table and while you might pick a service mesh for various application-centric reasons (advanced traffic splitting, advanced service discovery, observability and visualization of enrolled services). A feature shared by many is automatic mTLS connections between services in the mesh. Not all regulatory compliance requires end-to-end encryption, and terminating TLS at the the ingress point to your private network may be sufficient. However, this reference implementation is built to encourage you to take this further. Not only do we bring TLS into the ingress controller, we then continue it directly into the workload and throughout the workload's communication.
     * Using Azure Policy, you can restrict that all Ingresses must use HTTPS, and you can restrict what IP addresses are available for all LoadBalancers in your cluster. This ensures that network entry points are not freely able to "come and go," but instead restricted to a defined and documented network exposure plan. Likewise you can deny any Public IP requests which would potentially circumvent any other side-stepping of network controls.
     * Your ingress controller may also have an allow-list construct that restricts invocations to originate from known sources. In this reference implementation, your ingress controller will only accept traffic from your Azure Application Gateway instance. This is in a delegated subnet, in which no other compute type is allowed to reside, making this CIDR range a trusted source scope.
     * Ingress controllers often support middleware such as JWT validation, that allow you to offload traffic that isn't may have been routed properly, but is lacking expected or valid credentials.
     * LoadBalancer requests, such as those used by your ingress controller, will manage a Standard Internal Azure Load Balancer resource. That resource should live in a dedicated subnet, with a NSG around it that limit network traffic to be sourced from your Azure Application Gateway subnet.
     * This specific AKS cluster is spread across three node pools (system, in-scope, and out-of-scope node pools), each in their own _dedicated_ subnet in their own _dedicated_ VMSS pools. This allows you to reason over L3/L4 course-grained network considerations for all compute running in those subnets. These course-grained rules are applied as NSG rules on the nodepool subnets, and are really a superset of all expected network flows into and out of your cluster -- including operations network flows (such as raw node-access via SSH).
     * All of your resources live in a virtual network, with no public IP address and no to minimal public DNS records (due to Private Link), with the exception of your Application Gateway -- which is your entry point to web/workload traffic and Azure Firewall -- which is your exit point for all cluster-initiated traffic. This keeps all of your resources generally undiscoverable and isolated from public internet and other Azure customers. Network watcher and NSG Flow Logs should be enabled across all network layers for ultimate visibility into traffic and to help triage unexpected blocked traffic.
     * Web Traffic is tunneled into your cluster through Azure Application Gateway. This not only serves as your ingress point to your vnet, it also is your first point of traffic inspection. Azure Application Gateway has a WAF that detects potentially dangerous requests following the OWASP 3.1 rule set. Likewise it will expose your network entrypoint via your preferred TLS version (1.2+) with preferred ciphers. Traffic that is unrecognized to Azure Application Gateway, it dropped without performance impact to your cluster. Geo IP restrictions can also be applied via your Azure Application Gateway. Lastly, if you expect traffic from only specific IP addresses to hit your gateway, you can further restrict inbound traffic to your Azure Application Gateway via the NSG applied to its subnet. That NSG also should explicitly only allow outbound traffic to your cluster's ingress point.
     * Additionally, Azure API Management can be included in the ingress path for select services (such as APIs) to provide cross-cutting concerns such as JWT validation. You'd typically include API Management for more complex concerns than just JWT validation though, so we don't recommend including it for security-only concerns .. but if you plan on using it in your architecture for its other features like cache, versioning, service composition, etc., ensure you're also enabling it as another security point in the network hops as well.
     * Some customers may also include a transparent HTTP(S) proxy, such as Squid or Websense, as part of their egress strategy. This is not covered in this reference implementation.
     * Lastly, all of your traffic originating in your cluster and adjacent subnets are egressing through a firewall. This is the last responsible moment to block and observe outbound requests. If traffic has made it past AKS Network Policies and NSGs, your NVA is standing guard. Azure firewall is a deny-by-default platform, which means each and every allowance needs to be defined. Those allowances should be clamped specifically to the source of that traffic, using features like IP Groups to group "like" traffic together for ease and consistency of management.

   When you think of network traffic, you need to consider in-cluster pod-to-pod, pod-to-AKS-controlplane, operations-to-AKS-controlplane, pod-to-azure resources, azure resources themselves (Private Link), pod-to-world, world-to-pod (via managed and controlled ingress).

   Ultimately all layers build on each other, such that a failure/misconfiguration in a local-scope layer can hopefully be caught at a more course-grained layer.

   All workloads would should be deployed via your pipeline agents. We're deploying by hand here simply to expedite the walkthrough.

1. _From your Azure Bastion connection_, Wait until workload is ready to process requests

   ```bash
   TODO: FIGURE OUT A GOOD CONSOLIDATED CHECK
   ```

--- STOPPED HERE ----

<!--

> :book: The Contoso app team is about to conclude this journey, but they need an app to test their new infrastructure. For this task they've picked out the venerable [ASP.NET Core Docker sample web app](https://github.com/dotnet/dotnet-docker/tree/master/samples/aspnetapp).

1. Deploy the ASP.NET Core Docker sample web app

   > The workload definition demonstrates the inclusion of a Pod Disruption Budget rule, ingress configuration, and pod (anti-)affinity rules for your reference.

   ```bash
   kubectl apply -f https://raw.githubusercontent.com/mspnp/aks-secure-baseline/main/workload/aspnetapp.yaml
   ```

1. Wait until is ready to process requests running

   ```bash
   kubectl wait --namespace a0008 --for=condition=ready pod --selector=app.kubernetes.io/name=aspnetapp --timeout=90s
   ```

1. Check your Ingress resource status as a way to confirm the AKS-managed Internal Load Balancer is functioning

   > In this moment your Ingress Controller (Traefik) is reading your ingress resource object configuration, updating its status, and creating a router to fulfill the new exposed workloads route. Please take a look at this and notice that the address is set with the Internal Load Balancer IP from the configured subnet.

   ```bash
   kubectl get ingress aspnetapp-ingress -n a0008
   ```

   > At this point, the route to the workload is established, SSL offloading configured, and a network policy is in place to only allow Traefik to connect to your workload. Therefore, please expect a `403` HTTP response if you attempt to connect to it directly.

1. Give a try and expect a `403` HTTP response

   ```bash
   kubectl -n a0008 run -i --rm --tty curl --image=mcr.microsoft.com/powershell --limits=cpu=200m,memory=128M -- curl -kI https://bu0001a0008-00.aks-ingress.contoso.com -w '%{remote_ip}\n'
   ```
-->

## Security tooling

Your compliant cluster architecture requires a compliant inner loop development practice as well. Since this walkthrough is not focused on inner loop development practices, please dedicate time to documenting your safe deployment practices and your workload's supply chain and hardening techniques. Consider using solutions like [GitHub Action's container-scan](https://github.com/Azure/container-scan) to check for container-level hardening concerns -- CIS benchmark alignment, CVE detections, etc. even before the image is pushed to your container registry.

### Next step

:arrow_forward: [End-to-End Validation](./13-validation.md)
