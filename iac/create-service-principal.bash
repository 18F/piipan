#!/bin/bash
# Creates the named service principal (SP) with the Contributor role
# for the specified deployment environment, setting a random password.
#
# If the SP already exists, the password is reset to a new random
# password.
#
# azure-env is the name of the deployment environment (e.g., "tts/dev").
# See iac/env for available environments.
#
# output-format is optional and supports the formats defined at:
# https://docs.microsoft.com/en-us/cli/azure/format-output-azure-cli
# A value of "none" is useful to prevent printing the SP password to
# stdout (e.g., when stdout is being logged). The default is "json".
#
# usage: create-service-principal.bash <azure-env> <service-principal-name> [<output-format>]

source $(dirname "$0")/../tools/common.bash || exit
source $(dirname "$0")/iac-common.bash || exit

main () {
  # Load agency/subscription/deployment-specific settings
  azure_env=$1
  source $(dirname "$0")/env/${azure_env}.bash
  verify_cloud

  # Required service principal name
  name=$2

  # Optional output format
  output=${3:-json}

  # Array of groups to which the service principal will be scoped
  RESOURCE_GROUPS=($RESOURCE_GROUP $METRICS_RESOURCE_GROUP $MATCH_RESOURCE_GROUP)

  # Create space-separated list of resource group IDs
  for g in "${RESOURCE_GROUPS[@]}"
  do
    scope=`az group show -n $g --query id --output tsv`
    SCOPES="${SCOPES:+$SCOPES }$scope"
  done

  # If the service principal does not exist, the `create-for-rbac` command will
  # create it. If the service principal does exist, `create-for-rbac` will "patch"
  # it, creating new credentials and attempting to add the appropriate scopes.
  echo "Creating/resetting service principal $name"
  az ad sp create-for-rbac \
    --name $name \
    --role Contributor \
    --scopes $SCOPES \
    --only-show-errors \
    --output $output

  script_completed
}

main "$@"
