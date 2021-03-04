# Service principals in Azure

Service principals are used to allow third-party tools to authenticate with Azure and perform operations like deploying code.

## Creating service principals

The `iac/create-service-principal` script is used to create a service principal and assign it a [Contributor](https://docs.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#contributor) role that is scoped to the resource group(s) used by `piipan`.

Steps to create a service principal:
1. Sign in with the Azure CLI `login` command :
```
    az login
```
2. Run `create-service-principal` and provide it with the [deployment environment](./iac.md#deployment-environments) and the name of the service principal to be created:
```
    cd iac
    ./create-service-principal.bash <azure-env> <service-principal-name>
```

The service principal will be created and credentials will be printed to `stdout`.

The script supports an optional second parameter that will be passed as the [`--output` format](https://docs.microsoft.com/en-us/cli/azure/format-output-azure-cli) used when creating the service principal. Passing `none` will suppress credentials from being printed out:

```
    ./create-service-principal.bash <azure-env> <service-principal-name> none
```

## Refreshing/rotating service principal credentials

To refresh/rotate credentials, simply follow the steps above — if `create-service-principal` is passed the name of an existing service principal, it will reset any credentials, including their duration, and print the new credentials to `stdout`.

## Notes
- Service principal credentials default to a duration of 1 year.
- Service principals are granted access to individual components (e.g., resource groups, subscriptions, App Service applications, etc) that live within a single Azure tenant.
- Use `az ad app list` to show a list of all service principals.
- Use `az role assignment list --all --assignee http://<service-principal-name> --query "[].scope"` to show a list of all the resources to which a service principal has access.
- To view a list of all service principals in the Azure Portal UI, go to Portal > Azure Active Directory > App Registrations > All Applications.