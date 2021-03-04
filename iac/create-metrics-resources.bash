#!/bin/bash
#
# Provisions and configures the infrastructure components for all Piipan Metrics subsystems.
# Assumes an Azure user with the Global Administrator role has signed in with the Azure CLI.
# Assumes Piipan base resources have been created in the same environment
# (for example, state-specific blob topics).
# Must be run from a trusted network.
#
# azure-env is the name of the deployment environment (e.g., "tts/dev").
# See iac/env for available environments.
#
# usage: create-metrics-resources.bash <azure-env>

source $(dirname "$0")/../tools/common.bash || exit
source $(dirname "$0")/iac-common.bash || exit

### CONSTANTS
# Default resource group for metrics system
DB_SERVER_NAME=${PREFIX}-db-metrics-${ENV}-${LOCATION}
DB_ADMIN_NAME=piipanadmin
DB_NAME=metrics
DB_TABLE_NAME=participant_uploads
# Needed for both function apps
DB_CONN_STR=`pg_connection_string $DB_SERVER_NAME $DB_NAME $DB_ADMIN_NAME`
# Name of Key Vault
VAULT_NAME_KEY=KeyVaultName
VAULT_NAME=${PREFIX}kvmetrics${ENV}${LOCATION} # vault names can't use hyphens even though the docs say they can
# Name of secret used to store the PostgreSQL metrics server admin password
PG_SECRET_NAME=metrics-pg-admin
# Base name of dashboard app
DASHBOARD_APP_NAME=piipan-dashboard
### END CONSTANTS

main () {
  # Load agency/subscription/deployment-specific settings
  azure_env=$1
  source $(dirname "$0")/env/${azure_env}.bash

  # Create Metrics resource group
  # Eventually resource group will already be created for us by partner
  echo "Creating $METRICS_RESOURCE_GROUP group"
  az group create --name $METRICS_RESOURCE_GROUP -l $LOCATION --tags Project=$PROJECT_TAG

  # Create new Key Vault for this resource group
  echo "Creating Key Vault"
  az deployment group create \
    --name $VAULT_NAME \
    --resource-group $METRICS_RESOURCE_GROUP \
    --template-file ./arm-templates/key-vault.json \
    --parameters \
      name=$VAULT_NAME \
      location=$LOCATION \
      objectId=$CURRENT_USER_OBJID \
      resourceTags="$RESOURCE_TAGS"

  # Set PG Secret in key vault
  # By default, Azure CLI will print the password set in Key Vault; instead
  # just extract and print the secret id from the JSON response.
  echo "Setting key in vault"
  export PG_SECRET=`random_password`
  printenv PG_SECRET | tr -d '\n' | az keyvault secret set \
    --vault-name $VAULT_NAME \
    --name $PG_SECRET_NAME \
    --file /dev/stdin \
    --query id

  echo "Creating Metrics database server"
  az deployment group create \
    --name metrics \
    --resource-group $METRICS_RESOURCE_GROUP \
    --template-file ./arm-templates/database-metrics.json \
    --parameters \
      administratorLogin=$DB_ADMIN_NAME \
      serverName=$DB_SERVER_NAME \
      secretName=$PG_SECRET_NAME \
      vaultName=$VAULT_NAME \
      resourceTags="$RESOURCE_TAGS"

  ### Database stuff
  # Create database within db server (command is idempotent)
  az postgres db create --name $DB_NAME --resource-group $METRICS_RESOURCE_GROUP --server-name $DB_SERVER_NAME

  ## Connect to the db server
  # For some reason PG threw an Invalid Username error when trying to set PGUSER here, so it's specified in the prompt instead.
  # export PGUSER=${DB_ADMIN_NAME}@${DB_SERVER_NAME}
  export PGHOST=`az resource show \
    --resource-group $METRICS_RESOURCE_GROUP \
    --name $DB_SERVER_NAME \
    --resource-type "Microsoft.DbForPostgreSQL/servers" \
    --query properties.fullyQualifiedDomainName -o tsv`
  export PGPASSWORD=`az keyvault secret show \
    --id "https://${VAULT_NAME}.vault.azure.net/secrets/${PG_SECRET_NAME}" \
    --query value \
    --output tsv`

  echo "Insert db table"
  psql -U $DB_ADMIN_NAME@$DB_SERVER_NAME -p 5432 -d $DB_NAME -w -v ON_ERROR_STOP=1 -X -q - <<EOF
      CREATE TABLE IF NOT EXISTS $DB_TABLE_NAME (
          id serial PRIMARY KEY,
          state VARCHAR(50) NOT NULL,
          uploaded_at timestamp NOT NULL
      );
EOF

  # Create Metrics Collect Function App in Azure
  COLLECT_APP_FILEPATH=Piipan.Metrics.Collect
  COLLECT_APP_ID=metricscol
  COLLECT_APP_NAME=${PREFIX}-func-${COLLECT_APP_ID}-${ENV}-${LOCATION}
  COLLECT_STORAGE_NAME=${PREFIX}st${COLLECT_APP_ID}${ENV}${LOCATION}
  COLLECT_FUNC=BulkUploadMetrics

  # Will need to revisit how to successfully deploy this app through an arm template
  # Need a storage account to publish function app to:
  echo "Creating storage account for $COLLECT_APP_NAME"
  az storage account create \
    --name $COLLECT_STORAGE_NAME \
    --location $LOCATION \
    --resource-group $METRICS_RESOURCE_GROUP \
    --sku Standard_LRS

  # Create the function app in Azure
  echo "Creating function app $COLLECT_APP_NAME in Azure"
  az functionapp create \
    --resource-group $METRICS_RESOURCE_GROUP \
    --consumption-plan-location $LOCATION \
    --runtime dotnet \
    --functions-version 3 \
    --name $COLLECT_APP_NAME \
    --storage-account $COLLECT_STORAGE_NAME

  # Waiting before publishing the app, since publishing immediately after creation returns an   App Not Found error
  # Waiting was the best solution I could find. More info in these GH issues:
  # https://github.com/Azure/azure-functions-core-tools/issues/1616
  # https://github.com/Azure/azure-functions-core-tools/issues/1766
  echo "Waiting to publish function app"
  sleep 60s

  echo "configure settings"
  az functionapp config appsettings set \
    --resource-group $METRICS_RESOURCE_GROUP \
    --name $COLLECT_APP_NAME \
    --settings \
      $DB_CONN_STR_KEY="$DB_CONN_STR" \
      $VAULT_NAME_KEY="$VAULT_NAME" \
    --output none

  # Assumes if any identity is set, it is the one we are specifying below
  exists=`az functionapp identity show \
    --resource-group $METRICS_RESOURCE_GROUP \
    --name $COLLECT_APP_NAME`

  if [ -z "$exists" ]; then
    # Connect creds from function app to key vault so app can connect to db
    principalId=`az functionapp identity assign \
      --resource-group $METRICS_RESOURCE_GROUP \
      --name $COLLECT_APP_NAME \
      --query principalId \
      --output tsv`

    az keyvault set-policy \
      --name $VAULT_NAME \
      --object-id $principalId \
      --secret-permissions get list
  fi

  # publish the function app
  echo "Publishing function app $COLLECT_APP_NAME"
  pushd ../metrics/src/Piipan.Metrics/$COLLECT_APP_FILEPATH
    func azure functionapp publish $COLLECT_APP_NAME --dotnet
  popd

  # Subscribe each dynamically created event blob topic to this function
<<<<<<< HEAD
  echo "set FUNCTIONS_PROVIDERS"
  FUNCTIONS_PROVIDERS=/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${METRICS_RESOURCE_GROUP}/providers
=======
  METRICS_PROVIDERS=/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${METRICS_RESOURCE_GROUP}/providers
>>>>>>> main
  SUBS_RESOURCE_GROUP=piipan-resources

  while IFS=, read -r abbr name ; do
      echo "Subscribing to ${name} blob events"
      abbr=`echo "$abbr" | tr '[:upper:]' '[:lower:]'`
      sub_name=${abbr}-blob-metrics-subscription
      topic_name=${abbr}-blob-topic

      az eventgrid system-topic event-subscription create \
          --name $sub_name \
          --resource-group $SUBS_RESOURCE_GROUP \
          --system-topic-name $topic_name \
<<<<<<< HEAD
          --endpoint ${FUNCTIONS_PROVIDERS}/Microsoft.Web/sites/${COLLECT_APP_NAME}/functions/${COLLECT_FUNC} \
=======
          --endpoint ${METRICS_PROVIDERS}/Microsoft.Web/sites/${FUNC_APP_NAME}/functions/${FUNC_NAME} \
>>>>>>> main
          --endpoint-type azurefunction \
          --included-event-types Microsoft.Storage.BlobCreated \
          --subject-begins-with /blobServices/default/containers/upload/blobs/
  done < states.csv

  # Create Metrics API Function App in Azure
  API_APP_FILEPATH=Piipan.Metrics.Api
  METRICS_API_APP_ID=metricsapi

  echo "Create $API_APP_FILEPATH in Azure"
  API_APP_NAME=`az deployment group create \
      --resource-group $METRICS_RESOURCE_GROUP \
      --template-file  ./arm-templates/function-metrics.json \
      --query properties.outputs.functionAppName.value \
      --output tsv \
      --parameters \
        functionAppName="${PREFIX}-func-${METRICS_API_APP_ID}-${ENV}-${LOCATION}" \
        resourceTags="$RESOURCE_TAGS" \
        location=$LOCATION \
        databaseConnectionStringKey="$DB_CONN_STR_KEY" \
        databaseConnectionStringValue="$DB_CONN_STR" \
        vaultNameKey="$VAULT_NAME_KEY" \
        vaultNameValue="$VAULT_NAME" \
        applicationInsightsName="${PREFIX}-ins-${METRICS_API_APP_ID}-${ENV}-${LOCATION}" \
        storageAccountName="${PREFIX}st${METRICS_API_APP_ID}${ENV}${LOCATION}"`

  # Assumes if any identity is set, it is the one we are specifying below
  exists=`az functionapp identity show \
    --resource-group $METRICS_RESOURCE_GROUP \
    --name $API_APP_NAME`

  if [ -z "$exists" ]; then
    # Connect creds from function app to key vault so app can connect to db
    principalId=`az functionapp identity assign \
      --resource-group $METRICS_RESOURCE_GROUP \
      --name $API_APP_NAME \
      --query principalId \
      --output tsv`

    az keyvault set-policy \
      --name $VAULT_NAME \
      --object-id $principalId \
      --secret-permissions get list
  fi

  echo "waiting to publish function app"
  sleep 60s

  # publish metrics function app
  echo "Publishing function app $API_APP_NAME"
  pushd ../metrics/src/Piipan.Metrics/$API_APP_FILEPATH
    func azure functionapp publish $API_APP_NAME --dotnet
  popd

  # Deploying Dashboard App here because it now relies on info from metrics api.
  # If we can get the metrics api function app name dynamically without deploying,
  # then this can be moved to its own file.
  metrics_api_uri=$(\
    az functionapp function show \
      -g $METRICS_RESOURCE_GROUP \
      -n  $API_APP_NAME \
      --function-name GetParticipantUploads \
      --query invokeUrlTemplate \
      --output tsv)

  # Create App Service resources for dashboard app
  echo "Creating App Service resources for dashboard app"
  az deployment group create \
    --name $DASHBOARD_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --template-file ./arm-templates/dashboard-app.json \
    --parameters \
      location=$LOCATION \
      resourceTags="$RESOURCE_TAGS" \
      appName=$DASHBOARD_APP_NAME \
      servicePlan=$APP_SERVICE_PLAN \
      metricsApiUri=$metrics_api_uri

  script_completed
}
main "$@"
