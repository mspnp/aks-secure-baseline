# End-to-End Validation

Now that you have a [simulated regulated workload deployed](./12-workload.md), you can start validating and exploring this reference implementation of the [AKS Baseline Cluster for Regulated Workloads](./). In addition to the workload, there are some observability validation you can perform as well.

## Validate the Web App

This section will help you to validate the workload is exposed correctly and responding to HTTP requests.

### Steps

1. Get the public IP of Azure Application Gateway.

   > :book: The app team conducts a final acceptance test to be sure that traffic is flowing end-to-end as expected, so they place a request against the Azure Application Gateway endpoint.

   ```bash
   # query the Azure Application Gateway Public Ip
   APPGW_PUBLIC_IP=$(az deployment group show -g rg-enterprise-networking-spokes -n spoke-BU0001A0005-01 --query properties.outputs.appGwPublicIpAddress.value -o tsv)
   ```

1. Create an DNS `A` Record 🛑

   > :bulb: You can simulate this via a local hosts file modification. You're welcome to add a real DNS entry for your specific deployment's application domain name, if you have access to do so.

   Map the Azure Application Gateway public IP address to the application domain name. To do that, please edit your hosts file (`C:\Windows\System32\drivers\etc\hosts` or `/etc/hosts`) and add the following record to the end: `${APPGW_PUBLIC_IP} bicycle.contoso.com`

1. Browse to the site (e.g. <https://bicycle.contoso.com>).

   > :bulb: A TLS warning will be present due to using a self-signed certificate.

1. Review the emitted page details.

   This page shows a series of attempted network traffic attempts in your cluster. Due to Azure Firewall, NSGs, service mesh rules, and Kuberentes Network Policies, only a select number of requests will be successful, the rest will be blocked. Generally speaking, the allowed network flow is Ingress -> web-frontend -> microservice-a -> microservice-b -> microservice-c. All other attempted connections are denied. Likewise, most Internet-bound traffic was denied, and only microservice-a was able to make the request.

## Validate Web Application Firewall functionality

Your workload is placed behind a Web Application Firewall (WAF), which has rules designed to stop intentionally malicious activity. You can test this by triggering one of the built-in rules with a request that looks malcious.

   > :bulb: This reference implementation enables the built-in OWASP 3.1 ruleset, in **Prevention** mode.

### Steps

1. Browse to the site with the following appended to the URL: `?sql=DELETE%20FROM` (e.g. <https://bicycle.contoso.com/?sql=DELETE%20FROM>).
1. Observe that your request was blocked by Application Gateway's WAF rules and your workload never saw this potentially dangerous request.
1. Blocked requests (along with other gateway data) will be visible in the attached Log Analytics workspace. Execute the following query to show WAF logs, for example.

   ```
   AzureDiagnostics
   | where ResourceProvider == "MICROSOFT.NETWORK" and Category == "ApplicationGatewayFirewallLog"
   | order by TimeGenerated desc
   ```

## Validate Cluster Azure Monitor Insights and Logs

Monitoring your cluster is critical, especially when you're running a production cluster. Azure Monitor is configured to surface cluster logs, here you can see those logs as they are generated. [Azure Monitor for containers](https://docs.microsoft.com/azure/azure-monitor/insights/container-insights-overview) is configured on this cluster for this purpose.

### Steps

1. In the Azure Portal, navigate to your AKS cluster resource.
1. Click _Insights_ to see see captured data.

You can also execute [queries](https://docs.microsoft.com/azure/azure-monitor/log-query/get-started-portal) on the [cluster logs captured](https://docs.microsoft.com/azure/azure-monitor/insights/container-insights-log-search).

1. In the Azure Portal, navigate to your AKS cluster resource.
1. Click _Logs_ to see and query log data.
   :bulb: There are several examples on the _Kubernetes Services_ category.

## Validate Workload Logs

The example workload uses the standard dotnet logger interface, which are captured in `ContainerLogs` in Azure Monitor. You could also include additional logging and telemetry frameworks in your workload, such as Application Insights. Here are the steps to view the built-in application logs.

### Steps

1. In the Azure Portal, navigate to your AKS cluster resource group (`rg-bu0001a0005`).
1. Select your Log Analytic Workspace resource.
1. Execute the following query.

   ```
   ContainerLog
   | where Image contains "chain-api"
   | order by TimeGenerated desc
   ```

## Cluster Access Logs

This reference implementation logs all AKS control plane interactions in the associated Log Analytics workspace. Specifically this is enabled through the use of `kube-audit-admin` Diagnostics setting that was enabled on the cluster.

### Steps

1. In the Azure Portal, navigate to your AKS cluster resource group (`rg-bu0001a0005`).
1. Select your Log Analytic Workspace resource.
1. Execute the following query.

   ```
   AzureDiagnostics 
   | where Category == 'kube-audit-admin'
   | order by TimeGenerated desc 
   ```

This returns all Kubernetes API Server interaction happening in your cluster, OTHER than most `GET` requests. Basically any interaction that might potentially have the capability to modifying the system. Even an "idle" cluster can fill this log quickly (don't be surprised to see over 200 messages in a 30 minute window). Most regulations do not require it, but if you disable `kube-audit-admin` and instead simply enable `kube-audit` the system will _also_ log all of the `GET` (read) requests as well. This will _dramatically_ increase the number of logs, but you will then see 100% of the requests to the API Server. Never enable both at the same time.

## Azure Policy Changes

Azure policy definitions sync with your cluster about once every 15 minutes. To see when they sync you can execute the following query.

```
ContainerLog
| where Image contains "policy-kubernetes-addon"
| where LogEntry contains "Syncing policies with cluster"
| order by TimeGenerated desc 
```

And audit results will be sent to Azure Policy about once every 30 minutes. To see when they sync you can execute the following query.

ContainerLog
| where Image contains "policy-kubernetes-addon"
| where LogEntry contains "Sending audit result"
| order by TimeGenerated desc 

### Next step

:arrow_forward: [Clean Up Azure Resources](./14-cleanup.md)
