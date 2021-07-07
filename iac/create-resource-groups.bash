#!/bin/bash
#
# Creates Piipan resource groups. Assumes an Azure user with at least
# the Contributor role has signed in with the Azure CLI.
#
# azure-env is the name of the deployment environment (e.g., "tts/dev").
# See iac/env for available environments.
#
# usage: create-resource-groups.bash <azure-env>

# shellcheck source=./tools/common.bash
source "$(dirname "$0")"/../tools/common.bash || exit

main () {
  # Load agency/subscription/deployment-specific settings
  azure_env=$1
  source "$(dirname "$0")"/env/"${azure_env}".bash
  source "$(dirname "$0")"/iac-common.bash
  verify_cloud

  # Any changes to the set of resource groups below should also
  # be made to create-service-principal.bash
  echo "Creating $RESOURCE_GROUP group"
  az group create --name "$RESOURCE_GROUP" -l "$LOCATION" --tags Project="$PROJECT_TAG"
  echo "Creating match APIs resource group"
  az group create --name "$MATCH_RESOURCE_GROUP" -l "$LOCATION" --tags Project="$PROJECT_TAG"

  script_completed
}

main "$@"
