# Infrastructure-as-Code

## Prerequisites

All prerequisites are available in [Azure Cloud Shell](https://docs.microsoft.com/en-us/azure/cloud-shell/overview).

- [Azure Command Line Interface (CLI)](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) >= 2.23.0
- [Azure Functions Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download)
- `bash` shell, `/dev/urandom` – included in macOS, Linux, Git for Windows
- `psql` client for PostgreSQL

## Steps
To (re)create the Azure resources that `piipan` uses:
1. Run `install-extensions` to install Azure CLI extensions required by the `az` commands used by our IaC:
```
    ./iac/install-extensions.bash
```
2. Connect to a trusted network. Currently, only the GSA network block is trusted.
3. Configure the desired Azure cloud; either `AzureCloud` or `AzureUSGovernment`:
```
    az cloud set --name AzureCloud
```
4. Sign in with the Azure CLI `login` command. An account with at least the Contributor role on the subscription is required.
```
    az login
```

5. Run `create-resources`, which deploys Azure Resource Manager (ARM) templates and runs associated scripts, specifying the [name of the deployment environment](#deployment-environments).
```
    cd iac
    ./create-resources.bash tts/dev
```

## Deployment environments

Configuration for each environment is in `iac/env` in a corresponding, `source`-able bash script.

| Name | Description |
|---|---|
| `tts/dev`  | TTS-owned Azure commercial cloud, updated continuously within a sprint |
| `tts/test` | TTS-owned Azure commercial cloud, updated at the end of each sprint |


## Environment variables

#### Automatically configured
The following environment variables are pre-configured by the Infrastructure-as-Code for Functions or Apps that require them. Most often they are used to [bind backing services to application code](https://12factor.net/backing-services) via connection strings.

| Name | Value | Used by |
|---|---|---|
| `DatabaseConnectionString` | ADO.NET-formatted database connection string. If `Password` has the value `{password}`; i.e., `password` in curly quotes, then it is a partial connection string indicating the use of managed identities. An access token must be retrieved at run-time (e.g., via [AzureServiceTokenProvider](https://docs.microsoft.com/en-us/dotnet/api/overview/azure/service-to-service-authentication)) to build the full connection string.  | Piipan.Etl, Piipan.Match.State, Piipan.Metrics.Collect, Piipan.Metrics.Api |
| `BlobStorageConnectionString` | Azure Storage Account connection string for accessing blobs. | Piipan.Etl |
| `OrchApiUri` | URI for the Orchestrator API endpoint. | Piipan.QueryTool |
| `StateApiEndpointStrings` | Serialized JSON array of endpoints strings for accessing each per-state matching API. | Piipan.Match.Orchestrator |
| `StateApiUriStrings` | Serialized JSON array of valid URI strings for each per-state matching API's `/query` endpoint. | Piipan.Match.Orchestrator |
| `StateName` | Name of the state associated with the Function App instance. | Piipan.Match.State |
| `StateAbbr` | Abbreviation of the state associated with the Function App instance. | Piipan.Match.State |
| `MetricsApiUri` | URI for the Metrics API endpoint. | Piipan.Dashboard |
| `KeyVaultName` | Name of key vault resource needed to acquire a secret | Piipan.Metrics.Api, Piipan.Metrics.Collect |
| `CloudName` | Name of the active Azure cloud environment, either `AzureCloud` or `AzureUSGovernment` | Piipan.Etl, Piipan.Match.State, Piipan.Metrics.Api, Piipan.Metrics.Collect |
| `LookupConnectionString` | Azure Storage Account connection string for accessing Table Storage service used for storing lookup IDs. | Piipan.Match.Orchestrator |
| `LookupTableName` | Name of the Table Storage table where lookup IDs are stored.  | Piipan.Match.Orchestrator |


## `SysType` resource tag

 The below resource tagging scheme is used for key Piipan components, using the `SysType` ("System Type") tag. This tag is used to ease enumeration of resource instances in IaC and to make a resource's system-level purpose more obvious in the Azure Portal. While a resource's name can make obvious its system type, often Azure naming restrictions and cloud-level uniqueness requirements can make those names inscrutable.

| Value | Description |
|---|---|
| PerStateEtl | one of _N_ function apps for per-state bulk ETL process |
| PerStateStorage | one of _N_ storage accounts for per-state bulk PII uploads |
| PerStateMatchApi | one of _N_ API Function Apps for Per-state matching |
| OrchestratorApi | the single Function App for the Orchestrator API |
| DashboardApp | the single Dashboard App Service |
| QueryApp | the single Query tool App Service |
| DupPartApi | the single API Management instance for the external-facing matching API |

In the Azure Portal, tags can be added to resource lists using the "Manage view" and/or "Edit columns" menu item that appears at the top left of the view. Specific tag values can also be filtered via "Add filter".

In the Azure CLI, `az resource list` can be used. Be sure to query for only the resources in the environment-specific resource group (e.g., `-dev`, `-test`, etc.):
```
az resource list  --tag SysType=PerStateMatchApi --query "[? resourceGroup == 'rg-match-dev' ].name"
```

## Notes
- `iac/states.csv` contains the comma-delimited records of participating states/territories. The first field is the [two-letter postal abbreviation](https://pe.usps.com/text/pub28/28apb.htm); the second field is the name of the state/territory.
- For development, dummy state/territories are used (e.g., the state of `Echo Alpha`, with an abbreviation of `EA`).
- If you forget to connect to a trusted network and `create-resources` fails, connect to the network, then re-run the script.
- If you have recently deleted all the Piipan resource groups and are re-creating the infrastructure from scratch and get an `Exist soft deleted vault with the same name` error, try `az keyvault purge --name <vault-name>`. See output of `az keyvault list-deleted` for the name of the vault, which should correspond to `VAULT_NAME` in `create-resources.bash`.
- Some Azure CLI provisioning commands will return before all of their behind-the-scenes operations complete in the Azure environment. Very occasionally, subsequent provisioning commands in `create-resources` will fail as it won't be able to locate services it expects to be present; e.g., `Can't find app with name` when publishing a Function to a Function App. As a workaround, re-run the script.
- .NET 5 with Azure Functions v3 is [not (yet) supported by Microsoft](https://github.com/Azure/azure-functions-host/issues/6674).
- `iac/.azure` contains local Azure CLI configuration that is used by `create-resources`
- In order for IaC to automatically configure the OIDC client secrets for the Dashboard and Query Tool applications, the secrets need to be present in a key vault with a particular naming format. See `configure-oidc.bash` for details.
