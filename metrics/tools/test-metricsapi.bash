#!/bin/bash
#
# Basic integration test tool for the Metrics API. Sends a curl request to the
# API's GetParticipantUploads endpoint and writes result to stdout.
# Requires the caller is on an allowed Network.
#
# azure-env is the name of the deployment environment (e.g., "tts/dev").
# See iac/env for available environments.
#
# usage: test-metricsapi.bash <azure-env>

# shellcheck source=./tools/common.bash
source "$(dirname "$0")"/../../tools/common.bash || exit

main () {
  azure_env=$1
  # shellcheck source=./iac/env/tts/dev.bash
  source "$(dirname "$0")"/../../iac/env/"${azure_env}".bash
  # shellcheck source=./iac/iac-common.bash
  source "$(dirname "$0")"/../../iac/iac-common.bash
  verify_cloud

  # grab url for metrics api
  function_uri=$(az functionapp function show \
    --name "$METRICS_API_APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --function-name $METRICS_API_FUNCTION_NAME \
    --query invokeUrlTemplate \
    --output tsv)

  echo "Submitting request to ${function_uri}"
  curl \
    --request GET "${function_uri}" \
    --include

  printf "\n"

  script_completed
}

main "$@"
