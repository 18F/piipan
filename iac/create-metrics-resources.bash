#!/bin/bash
#
# Provisions and configures the infrastructure components for all Piipan Metrics subsystems.
# Assumes an Azure user with the Global Administrator role has signed in with the Azure CLI.
# Assumes Piipan base resource groups, resources have been created in the same environment
# (for example, state-specific blob topics).
# Must be run from a trusted network.
#
# azure-env is the name of the deployment environment (e.g., "tts/dev").
# See iac/env for available environments.
#
# usage: create-metrics-resources.bash <azure-env>

# shellcheck source=./tools/common.bash
source "$(dirname "$0")"/../tools/common.bash || exit

set_constants () {
  DB_SERVER_NAME=$PREFIX-psql-core-$ENV
  DB_NAME=metrics
  # Dashboard App Info
  DASHBOARD_APP_NAME=$PREFIX-app-dashboard-$ENV
  DASHBOARD_FRONTDOOR_NAME=$PREFIX-fd-dashboard-$ENV
  DASHBOARD_WAF_NAME=wafdashboard${ENV}
  # Metrics Collection Info
  COLLECT_APP_FILEPATH=Piipan.Metrics.Collect
  COLLECT_STORAGE_NAME=${PREFIX}st${METRICS_COLLECT_APP_ID}${ENV}
  COLLECT_FUNC=BulkUploadMetrics
  # Metrics API Info
  API_APP_FILEPATH=Piipan.Metrics.Api
  API_APP_STORAGE_NAME=${PREFIX}st${METRICS_API_APP_ID}${ENV}
}

main () {
  # Load agency/subscription/deployment-specific settings
  azure_env=$1
  source "$(dirname "$0")"/env/"${azure_env}".bash
  # shellcheck source=./iac/iac-common.bash
  source "$(dirname "$0")"/iac-common.bash
  verify_cloud

  set_constants

  # Create Metrics Collect Function App in Azure

  # Will need to revisit how to successfully deploy this app through an arm template
  # Need a storage account to publish function app to:
  echo "Creating storage account for $METRICS_COLLECT_APP_NAME"
  az storage account create \
    --name "$COLLECT_STORAGE_NAME" \
    --location "$LOCATION" \
    --resource-group "$RESOURCE_GROUP" \
    --sku Standard_LRS \
    --tags Project=$PROJECT_TAG

  # Create the function app in Azure
  echo "Creating function app $METRICS_COLLECT_APP_NAME in Azure"
  az functionapp create \
    --resource-group "$RESOURCE_GROUP" \
    --plan "$APP_SERVICE_PLAN_FUNC_NAME" \
    --runtime dotnet \
    --functions-version 3 \
    --name "$METRICS_COLLECT_APP_NAME" \
    --storage-account "$COLLECT_STORAGE_NAME" \
    --assign-identity "[system]" \
    --tags Project=$PROJECT_TAG

  # Integrate function app into Virtual Network
  echo "Integrating $METRICS_COLLECT_APP_NAME into virtual network"
  az functionapp vnet-integration add \
    --name "$METRICS_COLLECT_APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --subnet "$FUNC_SUBNET_NAME" \
    --vnet "$VNET_NAME"

  # Allow only incoming traffic from Event Grid
  # Only set rule if it does not exist, to avoid error
  exists=$(\
    az functionapp config access-restriction show \
      -n "$METRICS_COLLECT_APP_NAME" \
      -g "$RESOURCE_GROUP" \
      --query "ipSecurityRestrictions[?ip_address == 'AzureEventGrid'].ip_address" \
      -o tsv)
  if [ -z "$exists" ]; then
    az functionapp config access-restriction add \
      -n "$METRICS_COLLECT_APP_NAME" \
      -g "$RESOURCE_GROUP" \
      --priority 100 \
      --service-tag AzureEventGrid
  fi

  # Configure log streaming for function app
  metrics_collect_function_id=$(\
    az functionapp show \
      -n "$METRICS_COLLECT_APP_NAME" \
      -g "$RESOURCE_GROUP" \
      -o tsv \
      --query id)
  hub_rule_id=$(\
    az eventhubs namespace authorization-rule list \
      --resource-group "$RESOURCE_GROUP" \
      --namespace-name "$EVENT_HUB_NAME" \
      --query "[?name == 'RootManageSharedAccessKey'].id" \
      -o tsv)

  az monitor diagnostic-settings create \
    --name "stream-logs-to-event-hub" \
    --resource "$metrics_collect_function_id" \
    --event-hub "logs" \
    --event-hub-rule "$hub_rule_id" \
    --logs '[
      {
        "category": "FunctionAppLogs",
        "enabled": true
      }
    ]'

  # Waiting before publishing the app, since publishing immediately after creation returns an   App Not Found error
  # Waiting was the best solution I could find. More info in these GH issues:
  # https://github.com/Azure/azure-functions-core-tools/issues/1616
  # https://github.com/Azure/azure-functions-core-tools/issues/1766
  echo "Waiting to publish function app"
  sleep 60

  echo "configure settings"
  db_conn_str=$(pg_connection_string "$DB_SERVER_NAME" "$DB_NAME" "${METRICS_COLLECT_APP_NAME//-/_}")
  az functionapp config appsettings set \
    --resource-group "$RESOURCE_GROUP" \
    --name "$METRICS_COLLECT_APP_NAME" \
    --settings \
      $DB_CONN_STR_KEY="$db_conn_str" \
      $CLOUD_NAME_STR_KEY="$CLOUD_NAME" \
    --output none

  # publish the function app
  echo "Publishing function app $METRICS_COLLECT_APP_NAME"
  pushd ../metrics/src/Piipan.Metrics/$COLLECT_APP_FILEPATH
    func azure functionapp publish "$METRICS_COLLECT_APP_NAME" --dotnet
  popd

  # Subscribe each dynamically created event blob topic to this function
  METRICS_PROVIDERS=/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}/providers
  SUBS_RESOURCE_GROUP=$RESOURCE_GROUP

  while IFS=, read -r abbr name ; do
      echo "Subscribing to ${name} blob events"
      abbr=$(echo "$abbr" | tr '[:upper:]' '[:lower:]')
      sub_name=evgs-${abbr}metricsupload-${ENV}
      topic_name=$(state_event_grid_topic_name "$abbr" "$ENV")

      az eventgrid system-topic event-subscription create \
          --name "$sub_name" \
          --resource-group "$SUBS_RESOURCE_GROUP" \
          --system-topic-name "$topic_name" \
          --endpoint "${METRICS_PROVIDERS}/Microsoft.Web/sites/${METRICS_COLLECT_APP_NAME}/functions/${COLLECT_FUNC}" \
          --endpoint-type azurefunction \
          --included-event-types Microsoft.Storage.BlobCreated \
          --subject-begins-with /blobServices/default/containers/upload/blobs/
  done < states.csv

  # Create Metrics API Function App in Azure

  # Will need to revisit how to successfully deploy this app through an arm template
  # Need a storage account to publish function app to:
  echo "Creating storage account for metrics api"
  az storage account create \
    --name "$API_APP_STORAGE_NAME" \
    --location "$LOCATION" \
    --resource-group "$RESOURCE_GROUP" \
    --sku Standard_LRS \
    --tags Project=$PROJECT_TAG

  # Create the function app in Azure
  echo "Creating function app $METRICS_API_APP_NAME"
  az functionapp create \
    --resource-group "$RESOURCE_GROUP" \
    --plan "$APP_SERVICE_PLAN_FUNC_NAME" \
    --runtime dotnet \
    --functions-version 3 \
    --name "$METRICS_API_APP_NAME" \
    --storage-account "$API_APP_STORAGE_NAME" \
    --assign-identity "[system]" \
    --tags Project=$PROJECT_TAG

  # Integrate function app into Virtual Network
  echo "Integrating $METRICS_API_APP_NAME into virtual network"
  az functionapp vnet-integration add \
    --name "$METRICS_API_APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --subnet "$FUNC_SUBNET_NAME" \
    --vnet "$VNET_NAME"

  db_conn_str=$(pg_connection_string "$DB_SERVER_NAME" "$DB_NAME" "${METRICS_API_APP_NAME//-/_}")
  az functionapp config appsettings set \
      --resource-group "$RESOURCE_GROUP" \
      --name "$METRICS_API_APP_NAME" \
      --settings \
        $DB_CONN_STR_KEY="$db_conn_str" \
        $CLOUD_NAME_STR_KEY="$CLOUD_NAME" \
      --output none

  # Configure log streaming for function app
  metrics_api_function_id=$(\
    az functionapp show \
      -n "$METRICS_API_APP_NAME" \
      -g "$RESOURCE_GROUP" \
      -o tsv \
      --query id)
  hub_rule_id=$(\
    az eventhubs namespace authorization-rule list \
      --resource-group "$RESOURCE_GROUP" \
      --namespace-name "$EVENT_HUB_NAME" \
      --query "[?name == 'RootManageSharedAccessKey'].id" \
      -o tsv)

  az monitor diagnostic-settings create \
    --name "stream-logs-to-event-hub" \
    --resource "$metrics_api_function_id" \
    --event-hub "logs" \
    --event-hub-rule "$hub_rule_id" \
    --logs '[
      {
        "category": "FunctionAppLogs",
        "enabled": true
      }
    ]'

  echo "Waiting to publish function app"
  sleep 60

  # publish metrics function app
  echo "Publishing function app $METRICS_API_APP_NAME"
  pushd ../metrics/src/Piipan.Metrics/$API_APP_FILEPATH
    func azure functionapp publish "$METRICS_API_APP_NAME" --dotnet
  popd

  ## Dashboard stuff

  echo "Create Front Door and WAF policy for dashboard app"
  suffix=$(web_app_host_suffix)
  dashboard_host=${DASHBOARD_APP_NAME}${suffix}
  ./add-front-door-to-app.bash \
    "$azure_env" \
    "$RESOURCE_GROUP" \
    "$DASHBOARD_FRONTDOOR_NAME" \
    "$DASHBOARD_WAF_NAME" \
    "$dashboard_host"

  front_door_id=$(\
  az network front-door show \
    --name "$DASHBOARD_FRONTDOOR_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query frontdoorId \
    --output tsv)
  echo "Front Door iD: ${front_door_id}"

  metrics_api_uri=$(\
    az functionapp function show \
      -g "$RESOURCE_GROUP" \
      -n "$METRICS_API_APP_NAME" \
      --function-name $METRICS_API_FUNCTION_NAME \
      --query invokeUrlTemplate \
      --output tsv)

  # Create App Service resources for dashboard app
  echo "Creating App Service resources for dashboard app"
  az deployment group create \
    --name "$DASHBOARD_APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --template-file ./arm-templates/dashboard-app.json \
    --parameters \
      location="$LOCATION" \
      resourceTags="$RESOURCE_TAGS" \
      appName="$DASHBOARD_APP_NAME" \
      servicePlan="$APP_SERVICE_PLAN" \
      metricsApiUri="$metrics_api_uri" \
      eventHubName="$EVENT_HUB_NAME" \
      idpOidcConfigUri="$DASHBOARD_APP_IDP_OIDC_CONFIG_URI" \
      idpClientId="$DASHBOARD_APP_IDP_CLIENT_ID"

  ./configure-oidc.bash "$azure_env" "$DASHBOARD_APP_NAME"

  script_completed
}
main "$@"
