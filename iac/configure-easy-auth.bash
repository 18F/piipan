#!/bin/bash
# Configures Easy Auth for internal API communication within Piipan.
# Requires specific privileges on Azure Active Directory that the
# subscription Global Administrator role has, but a subscription
# Contributor does not.
#
# azure-env is the name of the deployment environment (e.g., "tts/dev").
# See iac/env for available environments.
#
# usage: create-resources.bash <azure-env>

source $(dirname "$0")/../tools/common.bash || exit
source $(dirname "$0")/iac-common.bash || exit

# App Service Authentication is done at the Azure tenant level
TENANT_ID=$(az account show --query homeTenantId -o tsv)

# Generate the necessary JSON object for assigning an app role to
# a service principal or managed identity
app_role_assignment () {
  principalId=$1
  resourceId=$2
  appRoleId=$3

  echo "\
  {
    \"principalId\": \"${principalId}\",
    \"resourceId\": \"${resourceId}\",
    \"appRoleId\": \"${appRoleId}\"
  }"
}

# Generate the necessary JSON object for adding an app role
# to an Active Directory app registration
app_role_manifest () {
  role=$1

  json="\
  [{
    \"allowedMemberTypes\": [
      \"User\",
      \"Application\"
    ],
    \"description\": \"Grants application access\",
    \"displayName\": \"Authorized client\",
    \"isEnabled\": true,
    \"origin\": \"Application\",
    \"value\": \"${role}\"
  }]"
  echo $json
}

# Create an Active Directory app registration with an application
# role for a given application.
create_aad_app_reg () {
  app=$1
  role=$2
  resource_group=$3

  app_uri=$(\
    az functionapp show \
    --resource-group $resource_group \
    --name $app \
    --query defaultHostName \
    --output tsv)
  app_uri="https://${app_uri}"

  # Running `az ad app create` with the `--app-roles` parameter will throw
  # an error if the app already exists and the app role is enabled
  exists=$(\
    az ad app list \
    --display-name ${app} \
    --filter "displayName eq '${app}'" \
    --query "[0].appRoles[?value == '${role}'].value" \
    --output tsv)
  if [ -z "$exists" ]; then
    app_role=$(app_role_manifest $role)
    app_id=$(\
      az ad app create \
        --display-name $app \
        --app-roles "${app_role}" \
        --available-to-other-tenants false \
        --homepage $app_uri \
        --identifier-uris $app_uri \
        --reply-urls "${app_uri}/.auth/login/aad/callback" \
        --query objectId \
        --output tsv)
  else
    app_id=$(\
      az ad app list \
        --display-name ${app} \
        --filter "displayName eq '${app}'" \
        --query "[0].objectId" \
        --output tsv)
  fi

  echo $app_id
}

# Create a service principal associated with a given AAD
# application registration
create_aad_app_sp () {
  app=$1
  aad_app_id=$2
  filter="displayName eq '${app}' and servicePrincipalType eq 'Application'"

  # `az ad sp create` throws error if service principal exits
  sp=$(\
    az ad sp list \
    --display-name $app \
    --filter "${filter}" \
    --query "[0].objectId" \
    --output tsv)
  if [ -z "$sp" ]; then
    sp=$(\
      az ad sp create \
        --id $aad_app_id \
        --query objectId \
        --output tsv)
  fi

  echo $sp
}

# Assign an application role to a service principal (generally in
# the form of a managed identity)
assign_app_role () {
  echo "Assigning app role"
  resource_id=$1
  principal_id=$2
  role=$3
  role_id=$(\
    az ad sp show \
    --id $resource_id \
    --query "appRoles[?value == '${role}'].id" \
    --output tsv)

  # Similar to `az ad app create`, `az rest` will throw error when assigning
  # an app role to an identity that already has the role.
  exists=$(\
    az rest \
    --method GET \
    --uri "https://graph.microsoft.com/v1.0/servicePrincipals/${resource_id}/appRoleAssignedTo" \
    --query "value[?principalId == '${principal_id}'].appRoleId" \
    --output tsv)
  if [ -z "$exists" ]; then
    role_json=`app_role_assignment $principal_id $resource_id $role_id`
    echo $role_json
    az rest \
    --method POST \
    --uri "https://graph.microsoft.com/v1.0/servicePrincipals/${resource_id}/appRoleAssignedTo" \
    --headers 'Content-Type=application/json' \
    --body "$role_json"
  fi
}

# Activate App Service authentication (Easy Auth) for an app
# service or function app, and require app role assignment.
# Assumes Active Directory application and associated service
# principal already exist for the app
enable_easy_auth () {
  app=$1
  resource_group=$2

  app_uri=$(\
    az functionapp show \
    --resource-group $resource_group \
    --name $app \
    --query defaultHostName \
    --output tsv)
  app_uri="https://${app_uri}"

  app_aad_client=$(\
    az ad app list \
      --display-name ${app} \
      --filter "displayName eq '${app}'" \
      --query "[0].objectId" \
      --output tsv)

  sp_filter="displayName eq '${app}' and servicePrincipalType eq 'Application'"
  app_aad_sp=$(\
    az ad sp list \
      --display-name $app \
      --filter "${sp_filter}" \
      --query "[0].objectId" \
      --output tsv)

  echo "Configuring Easy Auth settings for ${app}"
  az webapp auth update \
    --resource-group $resource_group \
    --name $app \
    --aad-allowed-token-audiences $app_uri \
    --aad-client-id $app_aad_client \
    --aad-token-issuer-url "https://sts.windows.net/${TENANT_ID}/" \
    --enabled true \
    --action LoginWithAzureActiveDirectory

  # Any client that attemps authentication must be assigned a role
  az ad sp update \
    --id $app_aad_sp \
    --set "appRoleAssignmentRequired=true"
}

main () {
  # Load agency/subscription/deployment-specific settings
  azure_env=$1
  source $(dirname "$0")/env/${azure_env}.bash
  verify_cloud

  # Name of application roles authorized to call match APIs
  STATE_API_APP_ROLE='StateApi.Query'
  ORCH_API_APP_ROLE='OrchestratorApi.Query'

  match_func_names=($(\
    get_resources $PER_STATE_MATCH_API_TAG $MATCH_RESOURCE_GROUP))

  orch_name=$(get_resources $ORCHESTRATOR_API_TAG $MATCH_RESOURCE_GROUP)

  query_tool_name=$(get_resources $QUERY_APP_TAG $RESOURCE_GROUP)

  orch_identity=$(\
    az webapp identity show \
      --name $orch_name \
      --resource-group $MATCH_RESOURCE_GROUP \
      --query principalId \
      --output tsv)

  # With per-state and orchestrator APIs created, perform the necessary
  # configurations to enable authentication and authorization of the
  # orchestrator with each state.
  #
  # For each state:
  #   - Register an Azure Active Directory (AAD) app with an application
  #     role named the value of `STATE_API_APP_ROLE`
  #   - Create a service principal (SP) for the app registation
  #   - Add the application role to the orchestrator API's identity
  #   - Configure and enable App Service Authentiction (i.e., Easy Auth)
  #     for state's Function app.
  #   - Enable requirement that authentication tokens are only issued to
  #     client applications that are assigned an app role.

  for func in "${match_func_names[@]}"
  do
    echo "Configuring Easy Auth for ${func}"

    func_app_reg_id=$(create_aad_app_reg $func $STATE_API_APP_ROLE $MATCH_RESOURCE_GROUP)
    func_app_sp=$(create_aad_app_sp $func $func_app_reg_id)
    assign_app_role $func_app_sp $orch_identity $STATE_API_APP_ROLE

    # Activate App Service Authentication for the function app
    enable_easy_auth $func $MATCH_RESOURCE_GROUP
  done

  # Configure orchestrator with app service authentication
  orch_app_reg_id=$(create_aad_app_reg $orch_name $ORCH_API_APP_ROLE $MATCH_RESOURCE_GROUP)
  orch_app_sp=$(create_aad_app_sp $orch_name $orch_app_reg_id)
  enable_easy_auth $orch_name $MATCH_RESOURCE_GROUP

  # Give query tool access to orchestrator
  query_tool_identity=$(\
    az webapp identity show \
      --name $query_tool_name \
      --resource-group $RESOURCE_GROUP \
      --query principalId \
      --output tsv)
  assign_app_role $orch_app_sp $query_tool_identity $ORCH_API_APP_ROLE

  script_completed
}

main "$@"