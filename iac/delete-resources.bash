#!/bin/bash
#
# Delete all resources in the defined resource groups, leaving the resource
# groups themselves in place.
#
# usage: delete-resources.bash <azure-env>

# shellcheck source=./tools/common.bash
source "$(dirname "$0")"/../tools/common.bash || exit

purge () {
  local group=$1
  echo "Deleting all resources from $group..."

  # Disable info output by querying for non-existent property
  az deployment group create \
    -g "$group" \
    -f "$(dirname "$0")"/arm-templates/unique-string.json \
    --mode Complete \
    --query doesNotExist
}

main () {
  # Load agency/subscription/deployment-specific settings
  azure_env=$1
  # shellcheck source=./iac/env/tts/dev.bash
  source "$(dirname "$0")"/env/"${azure_env}".bash
  # shellcheck source=./iac/iac-common.bash
  source "$(dirname "$0")"/iac-common.bash
  verify_cloud

  echo "This script will delete all resources hosted on $CLOUD_NAME in ${azure_env}."
  echo "CAUTION: This action is not reversible and will delete all data".
  read -p "Proceed with bulk delete? (Yes or No) " -r
  if [[ $REPLY =~ ^[yY]es$ ]]; then

    purge "$RESOURCE_GROUP"
    purge "$MATCH_RESOURCE_GROUP"

  else
    exit 1
  fi

  script_completed
}

main "$@"
