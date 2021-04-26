# 13. Use Private Links as much as possible

Date: 2021-04-26

## Status

Accepted

## Context

By default, Azure resources are accessed through the public internet. From a security standpoint, this is not ideal because it exposes network traffic to eavesdropping.

Azure has a variety of methods to secure the Paas (Platform-as-a-Service) resources we use, which can be layered on top of one another. Until now, we have been using Firewall and WAF policies as our main security layer. As we move towards a production system, additional security layers are desired, particularly ones that entirely avoid communication over a public network.

Virtualization can achieve this. In a traditional on-premises setup, resources would be housed on a Virtual Machine. Since Azure resources are all PaaS systems, Azure uses what they call Private Links and Private Endpoints within Virtual Networks (VNet) to achieve a virtualized and entirely private setup.

Microsoft documentation on this subject is extensive, and specific implementation varies between resource types (see resources below for a starting point).

Here’s a brief overview of how these resources fit together, using a Postgres database server and a Function App that communicates with it as an example:
1. A VNet is established, with subnets dedicated to certain resource types
1. For the database server, a private link is created and claims one of the VNet’s subnets.
1. An App Service plan is created for the function app and any other apps needing to talk to this database server, claiming another one of the VNet’s subnets
1. The function app is put onto the App Service plan, allowing it to be integrated into the Vnet
1. Once an app is integrated into the VNet, it will automatically use the database server’s private link to communicate with it.
1. Public access to the database server is then disabled, allowing only resources in the VNet to communicate with it.

Azure has an older service to use with VNets called Service Endpoints, and this was explored as an option. With Service Endpoints, there is no extra resource to implement—the cost is built into the VNet itself. Private Links are a separate resource with its own costs. However, Service Endpoints still use the public IP address of a PaaS resource in order to communicate with the VNet. With Private Links, a resource gets a private IP address on the Virtual Network, effectively injecting the resource into the VNet.

For every new type of resource needing Virtual Network integration, a cost-benefit analysis should be done. For many Azure resources, integrating with a Virtual Network requires a more costly pricing plan. API Management, for example, would need a pricing plan upwards of [~$2,700 per month](https://docs.microsoft.com/en-us/azure/api-management/api-management-using-with-internal-vnet#availability). Web Apps will need a [Premium Plan](https://docs.microsoft.com/en-us/azure/azure-functions/functions-networking-options#matrix-of-networking-features). Therefore it’s worth considering the “must-haves” versus the “nice-to-haves” of VNet integration.

## Decision

We plan to deploy as many Azure resources as possible into a Virtual Network, unless we deem it unreasonably costly to do so.

We also plan to keep using Firewall and WAF policies to limit public inbound traffic.

The first resource we integrated into our VNet was the Postgres database server that will house the system’s PII, and all apps that communicate directly with this server’s databases. Future plans involve integrating:
1. Metrics Postgres server, and the apps that talk to that server
1. Orchestrator API
1. Per-state blob storage accounts
1. Web apps (dashboard and query tool)
1. API Management

## Consequences

Since it’s more straightforward for resources that communicate with each other to be housed in the same Virtual Network, resources needing the same VNet will be grouped into the same resource group.

To save on cost, we will use one Premium App Service plan and house all VNet-integrated apps on it.

Cost may also encourage us to use a different resource group and/or pricing plan schemas for dev and testing environments than we do for production.

## Resources
- [What is Azure Private Endpoint?](https://docs.microsoft.com/en-us/azure/private-link/private-endpoint-overview)
- [What is Azure Private Link?](https://docs.microsoft.com/en-us/azure/private-link/private-link-overview)
- [Private Link for Azure Database for PostgreSQL-Single server](https://docs.microsoft.com/en-us/azure/postgresql/concepts-data-access-and-security-private-link)
- [Forum: Service Endpoints vs. Private Endpoints?](https://acloud.guru/forums/az-500-microsoft-azure-security-technologies/discussion/-M5IkN1SzQcDUNRyvaVL/Service%20endpoints%20vs.%20Private%20Endpoints%3F#:~:text=Both%20appear%20to%20allow%20a,IP%20address%20in%20your%20subnet.))
- [Integrate Azure Functions with an Azure virtual network by using private endpoints](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-vnet)
