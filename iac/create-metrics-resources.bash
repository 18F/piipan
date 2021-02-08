#!/bin/bash
#
# Provisions and configures the infrastructure components for all Piipan Metrics subsystems.
# Assumes an Azure user with the Global Administrator role has signed in with the Azure CLI.
# Assumes Piipan base resources have been created in the same environment
# (for example, state-sepcific blob topics).
# Must be run from a trusted network.
#
# usage: create-metrics-resources.bash

source $(dirname "$0")/../tools/common.bash || exit
source $(dirname "$0")/iac-common.bash || exit

### CONSTANTS
# Default resource group for metrics system
RESOURCE_GROUP=piipan-metrics
LOCATION=westus
DB_SERVER_NAME=piipan-metrics-db
DB_ADMIN_NAME=piipanadmin
DB_NAME=metrics
DB_TABLE_NAME=participant_uploads
# Name of Key Vault
VAULT_NAME=metrics-secret-keeper
# Name of secret used to store the PostgreSQL metrics server admin password
PG_SECRET_NAME=metrics-pg-admin
### END CONSTANTS

# Create Metrics resource group
echo "Creating $RESOURCE_GROUP group"
az group create --name $RESOURCE_GROUP -l $LOCATION --tags Project=$PROJECT_TAG

# Create new Key Vault for this resource group
echo "Creating Key Vault"
az deployment group create \
  --name $VAULT_NAME \
  --resource-group $RESOURCE_GROUP \
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
  --resource-group $RESOURCE_GROUP \
  --template-file ./arm-templates/metrics.json \
  --parameters \
    administratorLogin=$DB_ADMIN_NAME \
    serverName=$DB_SERVER_NAME \
    secretName=$PG_SECRET_NAME \
    vaultName=$VAULT_NAME \
    resourceTags="$RESOURCE_TAGS"

### Database stuff
# Create database within db server (command is idempotent)
az postgres db create --name $DB_NAME --resource-group $RESOURCE_GROUP --server-name $DB_SERVER_NAME

## Connect to the db server
# For some reason PG threw an Invalid Username error when trying to set PGUSER here, so it's specified in the prompt instead.
# export PGUSER=${DB_ADMIN_NAME}@${DB_SERVER_NAME}
export PGHOST=`az resource show \
  --resource-group $RESOURCE_GROUP \
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

### Function App stuff
FUNC_APP_NAME=PiipanMetricsFunctions
FUNC_NAME=BulkUploadMetrics
FUNC_STORAGE_NAME=piipanmetricsstorage

# Need a storage account to publish function app to:
echo "Creating storage account $FUNC_STORAGE_NAME"
az storage account create \
  --name $FUNC_STORAGE_NAME \
  --location $LOCATION \
  --resource-group $RESOURCE_GROUP \
  --sku Standard_LRS

# Create the function app in Azure
echo "Creating function app $FUNC_APP_NAME in Azure"
az functionapp create \
  --resource-group $RESOURCE_GROUP \
  --consumption-plan-location $LOCATION \
  --runtime dotnet \
  --functions-version 3 \
  --name $FUNC_APP_NAME \
  --storage-account $FUNC_STORAGE_NAME

# Waiting before publishing the app, since publishing immediately after creation returns an App Not Found error
# Waiting was the best solution I could find. More info in these GH issues:
# https://github.com/Azure/azure-functions-core-tools/issues/1616
# https://github.com/Azure/azure-functions-core-tools/issues/1766
echo "Waiting to publish function app"
sleep 60s

# Configure settings on function app
echo "Configure settings on function app"
DB_CONN_STR=`pg_connection_string $DB_SERVER_NAME $DB_NAME $DB_ADMIN_NAME`

az functionapp config appsettings set \
  --resource-group $RESOURCE_GROUP \
  --name $FUNC_APP_NAME \
  --settings \
    $DB_CONN_STR_KEY="$DB_CONN_STR" \
  --output none

# Assumes if any identity is set, it is the one we are specifying below
exists=`az functionapp identity show \
  --resource-group $RESOURCE_GROUP \
  --name $FUNC_APP_NAME`

if [ -z "$exists" ]; then
  # Connect creds from function app to key vault so app can connect to db
  principalId=`az functionapp identity assign \
    --resource-group $RESOURCE_GROUP \
    --name $FUNC_APP_NAME \
    --query principalId \
    --output tsv`

  az keyvault set-policy \
    --name $VAULT_NAME \
    --object-id $principalId \
    --secret-permissions get list
fi


# publish the function app
echo "Publishing function app $FUNC_APP_NAME"
pushd ../metrics/src/Piipan.Metrics/$FUNC_APP_NAME
  func azure functionapp publish $FUNC_APP_NAME --dotnet
popd

# Subscribe each dynamically created event blob topic to this function
FUNCTIONS_PROVIDERS=/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}/providers
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
        --endpoint ${FUNCTIONS_PROVIDERS}/Microsoft.Web/sites/${FUNC_APP_NAME}/functions/${FUNC_NAME} \
        --endpoint-type azurefunction \
        --included-event-types Microsoft.Storage.BlobCreated \
        --subject-begins-with /blobServices/default/containers/upload/blobs/
done < states.csv

# Create Metrics API Function App in Azure
METRICS_FUNC_APP_NAME=PiipanMetricsApi

echo "Creating function app $METRICS_FUNC_APP_NAME in Azure"
az functionapp create \
  --resource-group $RESOURCE_GROUP \
  --consumption-plan-location $LOCATION \
  --runtime dotnet \
  --functions-version 3 \
  --name $METRICS_FUNC_APP_NAME \
  --storage-account $FUNC_STORAGE_NAME

# wait for app creation
echo "Waiting to publish api function app"
sleep 60s

echo "Configure settings on $METRICS_FUNC_APP_NAME"
az functionapp config appsettings set \
  --resource-group $RESOURCE_GROUP \
  --name $METRICS_FUNC_APP_NAME \
  --settings \
    $DB_CONN_STR_KEY="$DB_CONN_STR" \
  --output none

# Assumes if any identity is set, it is the one we are specifying below
exists=`az functionapp identity show \
  --resource-group $RESOURCE_GROUP \
  --name $METRICS_FUNC_APP_NAME`

if [ -z "$exists" ]; then
  # Connect creds from function app to key vault so app can connect to db
  principalId=`az functionapp identity assign \
    --resource-group $RESOURCE_GROUP \
    --name $METRICS_FUNC_APP_NAME \
    --query principalId \
    --output tsv`

  az keyvault set-policy \
    --name $VAULT_NAME \
    --object-id $principalId \
    --secret-permissions get list
fi

# publish metrics function app
echo "Publishing function app $METRICS_FUNC_APP_NAME"
pushd ../metrics/src/Piipan.Metrics/$METRICS_FUNC_APP_NAME
  func azure functionapp publish $METRICS_FUNC_APP_NAME --dotnet
popd
