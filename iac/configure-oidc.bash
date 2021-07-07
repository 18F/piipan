#!/bin/bash
#
# Sets the IDP_CLIENT_SECRET application setting for the web app with the 
# given application name. Assumes an Azure user with the Global
# Administrator role has signed in with the Azure CLI. Assumes the existence
# of a keyvault that follows the naming convention: <prefix>-kv-oidc-<env>.
# Assumes the keyvault contains a secret that follows the naming convention:
# <app_name>-oidc-secret.
#
# usage: configure-oidc.bash <azure-env> <app-name>

# shellcheck source=./tools/common.bash
source "$(dirname "$0")"/../tools/common.bash || exit

main () {
  # Load agency/subscription/deployment-specific settings
  azure_env=$1
  source "$(dirname "$0")"/env/"${azure_env}".bash
  # shellcheck source=./iac/iac-common.bash
  source "$(dirname "$0")"/iac-common.bash
  verify_cloud

  APP_NAME=$2

  echo "Getting secret from keyvault"
  if secret=$(az keyvault secret show \
      --vault-name "$PREFIX-kv-oidc-$ENV" \
      --name "$APP_NAME-oidc-secret" \
      --query value \
      --output tsv)
  then
    echo "Setting IDP_CLIENT_SECRET for $APP_NAME"
    # capture the output to prevent secret from being written to terminal
    res=$(az webapp config appsettings set \
      --name "$APP_NAME" \
      --resource-group "$RESOURCE_GROUP" \
      --settings IDP_CLIENT_SECRET="$secret")
  else
    echo "ERROR getting secret from keyvault"
  fi

  script_completed
}

main "$@"