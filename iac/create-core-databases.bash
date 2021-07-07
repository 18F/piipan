#!/bin/bash

# shellcheck source=./tools/common.bash
source "$(dirname "$0")"/../tools/common.bash || exit

set_constants () {
  DB_SERVER_NAME=$PREFIX-psql-core-$ENV
  DB_ADMIN_NAME=piipanadmin
  SUPERUSER=$DB_ADMIN_NAME

  METRICS_DB_NAME=metrics

  VAULT_NAME=$PREFIX-kv-core-$ENV
  PG_SECRET_NAME=core-pg-admin

  PSQL_OPTS=(-v ON_ERROR_STOP=1 -X -q)
  TEMPLATE_DB=template1

  # Name of Azure Active Directory admin for PostgreSQL server
  PG_AAD_ADMIN=piipan-admins

  PRIVATE_DNS_ZONE=$(private_dns_zone)
}

init_db () {
  db=$1
  owner=$db

  create_role "$owner"
  config_role "$owner"

  create_db "$db"
  set_db_owner "$db" "$owner"
  config_db "$db"
}

create_db () {
  db=$1
  psql "${PSQL_OPTS[@]}" -d "$TEMPLATE_DB" -f - <<EOF
    SELECT 'CREATE DATABASE $db TEMPLATE $TEMPLATE_DB'
      WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$db')\gexec
EOF
}

create_role () {
  role=$1
  psql "${PSQL_OPTS[@]}" -d "$TEMPLATE_DB" -f - <<EOF
    DO \$\$
    BEGIN
      CREATE ROLE $role;
      EXCEPTION WHEN DUPLICATE_OBJECT THEN
      RAISE NOTICE 'role "$role" already exists';
    END
    \$\$;
EOF
}

config_db () {
  db=$1
  psql "${PSQL_OPTS[@]}" -d "$db" -f - <<EOF
    REVOKE ALL ON DATABASE $db FROM public;
    REVOKE ALL ON SCHEMA public FROM public;
    CREATE EXTENSION IF NOT EXISTS plpgsql WITH SCHEMA pg_catalog;
EOF
}

config_role () {
  role=$1
  psql "${PSQL_OPTS[@]}" -d "$TEMPLATE_DB" -f - <<EOF
    ALTER ROLE $role PASSWORD NULL;
    ALTER ROLE $role NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOLOGIN;
EOF
}

set_db_owner () {
  db=$1
  owner=$2
  psql "${PSQL_OPTS[@]}" -d "$db" -f - <<EOF
    -- "superuser" account under Azure is not so super; must be a member of the
    -- owner role before being able to create a database with it as owner
    GRANT $owner to $SUPERUSER;
    ALTER DATABASE $db OWNER TO $owner;
    REVOKE $owner from $SUPERUSER;
EOF
}

create_managed_role () {
  db=$1
  func=$2
  role=${func//-/_}

  principal_id=$(\
    az webapp identity show \
      -n "$func" \
      -g "$RESOURCE_GROUP" \
      --query principalId \
      -o tsv)
  app_id=$(\
    az ad sp show \
      --id "$principal_id" \
      --query appId \
      -o tsv)

  # Establish a managed identity role for an application's
  # system-assigned identity.
  psql "${PSQL_OPTS[@]}" -d "$db" -f - <<EOF
    SET aad_validate_oids_in_tenant = off;
    DO \$\$
    BEGIN
      CREATE ROLE $role LOGIN PASSWORD '$app_id' IN ROLE azure_ad_user;
      EXCEPTION WHEN DUPLICATE_OBJECT THEN
      RAISE NOTICE 'role "$role" already exists';
    END
    \$\$;
EOF
}

config_managed_role () {
  db=$1
  func=$2
  role=${func//-/_}

  psql "${PSQL_OPTS[@]}" -d "$db" -f - <<EOF
    GRANT CONNECT,TEMPORARY ON DATABASE $db TO $role;
    GRANT USAGE ON SCHEMA public TO $role;
    ALTER ROLE $role NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
EOF
}

grant_read_access () {
  db=$1
  func=$2
  role=${func//-/_}

  psql "${PSQL_OPTS[@]}" -d "$db" -f - <<EOF
    GRANT SELECT ON ALL TABLES IN SCHEMA public TO $role;
EOF
}

grant_read_write_access () {
  db=$1
  func=$2
  role=${func//-/_}

  psql "${PSQL_OPTS[@]}" -d "$db" -f - <<EOF
    GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE ON ALL TABLES IN SCHEMA public TO $role;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO $role;
EOF
}

main () {
  # Load agency/subscription/deployment-specific settings
  azure_env=$1
  # shellcheck source=./iac/env/tts/dev.bash
  source "$(dirname "$0")"/env/"${azure_env}".bash
  # shellcheck source=./iac/iac-common.bash
  source "$(dirname "$0")"/iac-common.bash
  verify_cloud

  set_constants

  # Set PG Secret in key vault
  # By default, Azure CLI will print the password set in Key Vault; instead
  # just extract and print the secret id from the JSON response.
  echo "Setting key in vault"
  PG_SECRET=$(random_password)
  export PG_SECRET
  printenv PG_SECRET | tr -d '\n' | az keyvault secret set \
    --vault-name "$VAULT_NAME" \
    --name "$PG_SECRET_NAME" \
    --file /dev/stdin \
    --query id

  echo "Creating core database server"
  az deployment group create \
    --name core-db \
    --resource-group "$RESOURCE_GROUP" \
    --template-file ./arm-templates/database-core.json \
    --parameters \
      administratorLogin=$DB_ADMIN_NAME \
      serverName="$DB_SERVER_NAME" \
      secretName="$PG_SECRET_NAME" \
      vaultName="$VAULT_NAME" \
      vnetName="$VNET_NAME" \
      subnetName="$DB_2_SUBNET_NAME" \
      privateEndpointName="$CORE_DB_PRIVATE_ENDPOINT_NAME" \
      privateDnsZoneName="$PRIVATE_DNS_ZONE" \
      resourceTags="$RESOURCE_TAGS" \
      eventHubName="$EVENT_HUB_NAME"

  export PGOPTIONS='--client-min-messages=warning'
  PGHOST=$(az resource show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DB_SERVER_NAME" \
    --resource-type "Microsoft.DbForPostgreSQL/servers" \
    --query properties.fullyQualifiedDomainName -o tsv)
  export PGHOST
  export PGPASSWORD=$PG_SECRET
  export PGUSER=${DB_ADMIN_NAME}@${DB_SERVER_NAME}

  echo "Baseline $TEMPLATE_DB before creating new databases from it"
  config_db "$TEMPLATE_DB"

  # Create and configure metrics DB
  init_db $METRICS_DB_NAME

  echo "Create $METRICS_DB_NAME table"
  psql "${PSQL_OPTS[@]}" -d $METRICS_DB_NAME -f - <<EOF
      CREATE TABLE IF NOT EXISTS participant_uploads(
          id serial PRIMARY KEY,
          state VARCHAR(50) NOT NULL,
          uploaded_at timestamp NOT NULL
      );
EOF

  # AAD / managed identity
  az ad group create --display-name "$PG_AAD_ADMIN" --mail-nickname "$PG_AAD_ADMIN"
  PG_AAD_ADMIN_OBJID=$(az ad group show --group $PG_AAD_ADMIN --query objectId --output tsv)
  az postgres server ad-admin create \
    --resource-group "$RESOURCE_GROUP" \
    --server "$DB_SERVER_NAME" \
    --display-name "$PG_AAD_ADMIN" \
    --object-id "$PG_AAD_ADMIN_OBJID"

  exists=$(az ad group member check \
    --group "$PG_AAD_ADMIN" \
    --member-id "$CURRENT_USER_OBJID" \
    --query value -o tsv)

  if [ "$exists" = "true" ]; then
    echo "$CURRENT_USER_OBJID is already a member of $PG_AAD_ADMIN"
  else
    # Temporarily add current user as a PostgreSQL AD admin
    # to allow provisioning of managed identity roles
    az ad group member add \
      --group "$PG_AAD_ADMIN" \
      --member-id "$CURRENT_USER_OBJID"
  fi

  # Authenticate under the AD "superuser" group, in order to create managed
  # identities. Assumes the current user is a member of PG_AAD_ADMIN.
  aad_pgpassword=$(az account get-access-token --resource-type oss-rdbms \
    --query accessToken --output tsv)
  export PGPASSWORD=$aad_pgpassword
  export PGUSER=${PG_AAD_ADMIN}@$DB_SERVER_NAME

  echo "Configuring database access for $METRICS_API_APP_NAME"
  create_managed_role "$METRICS_DB_NAME" "$METRICS_API_APP_NAME"
  config_managed_role "$METRICS_DB_NAME" "$METRICS_API_APP_NAME"
  grant_read_access "$METRICS_DB_NAME" "$METRICS_API_APP_NAME"

  echo "Configuring database access for $METRICS_COLLECT_APP_NAME"
  create_managed_role "$METRICS_DB_NAME" "$METRICS_COLLECT_APP_NAME"
  config_managed_role "$METRICS_DB_NAME" "$METRICS_COLLECT_APP_NAME"
  grant_read_write_access "$METRICS_DB_NAME" "$METRICS_COLLECT_APP_NAME"

  if [ "$exists" = "true" ]; then
    echo "Leaving $CURRENT_USER_OBJID as a member of $PG_AAD_ADMIN"
  else
    # Revoke temporary assignment of current user as a PostgreSQL AD admin
    az ad group member remove \
      --group "$PG_AAD_ADMIN" \
      --member-id "$CURRENT_USER_OBJID"
  fi

  echo "Secure database connection"
  ./remove-external-network.bash \
    "$azure_env" \
    "$RESOURCE_GROUP" \
    "$DB_SERVER_NAME"

  script_completed
}

main "$@"
