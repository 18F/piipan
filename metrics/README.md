# Piipan Metrics

*Piipan subsystem for monitoring other subsystems*

## Sources

The `metrics/src` directory contains:

* [Metrics Functions Function App](./src/PiipanMetricsFunctions) - monitors the other subsystems and stores the metrics data
* [Metrics API Function App](./src/PiipanMetricsApi) - serves metrics data to external systems
* [Metrics Shared Models](./src/Piipan.Metrics.Models) - maps data from metrics db into application objects

## Summary

### Metrics Functions

Metrics Functions is an Azure Function App made up of Azure Event Grid event functions.

For now, there's a 1-1 relationship between a specific metric needing to be captured and a function within this app.

### Metrics API

The Metrics API is a Function App made up of Azure HTTPTrigger event functions.

For now, there's a 1-1 relationship between an API endpoint and a function within this app.

## Testing

`Piipan.Metrics.Test` holds all tests for the Piipan Metrics Subsystem

``` bash
$ cd metrics/tests/Piipan.Metrics.Tests
```

If running for the first time, do `dotnet build`, then:

``` bash
$ dotnet test
```

