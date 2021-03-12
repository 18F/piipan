# Default location
LOCATION=westus

# Default resource group
RESOURCE_GROUP=rg-core-dev

# Resource group for matching API
MATCH_RESOURCE_GROUP=rg-match-dev

# Resource group for metrics
METRICS_RESOURCE_GROUP=rg-metrics-dev

# Prefix for resource identifiers
PREFIX=fns

# Deployment environment for resource identifiers
ENV=$(basename "${BASH_SOURCE%.*}")

# Either AzureCloud or AzureUSGovernment
CLOUD_NAME=AzureCloud
