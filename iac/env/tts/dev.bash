# Default location
LOCATION=westus

# Default resource group
RESOURCE_GROUP=piipan-resources

# Resource group for matching API
MATCH_RESOURCE_GROUP=piipan-match

# Resource group for metrics
METRICS_RESOURCE_GROUP=piipan-metrics

# Prefix for resource identifiers
PREFIX=fns

# Deployment environment for resource identifiers
ENV=$(basename "${BASH_SOURCE%.*}")
