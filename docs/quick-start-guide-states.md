# Quick-Start Guide for States

> This documentation is for state use.

> ⚠️  Under construction

A high-level view of the system architecture can be found [here](../README.md).

## System Status

APIs are in Alpha stage and under active development. We plan on having APIs ready for state testing in a sandbox environment by the end of April 2021.

## API Overview

In order to participate, states will need to:

1. Upload participant data to the system
1. Conduct matches against the system
1. Take appropriate action on those matches

These three elements translate to three main areas of the API that states will integrate into their existing eligibility systems and workflows:

1. States will upload participant data through a scheduled [CSV upload](./openapi/generated/duplicate-participation-api/openapi.md#upload) (CSV formatting instructions can be found [here](https://github.com/18F/piipan/blob/main/etl/docs/bulk-import.md))
2. States will conduct matches through [Active Matching](./openapi/generated/duplicate-participation-api/openapi.md#match)
3. States will be able to take action by referencing previous matches through [a Lookup ID](./openapi/generated/duplicate-participation-api/openapi.md#Lookup)

### Environments

Seperate endpoints and credentials will be provided for each environment.

| Environment | Purpose |
|---|---|
| Sandbox | For initial testing of the integration; fake data only |
| Staging | For testing with actual data at scale; data is not used in production |
| Production | Actual data used in the production system |

### Endpoints Overview

| Endpoint | Description | Request Type |
|---|---|---|
| `/<state-abbreviation>/upload/:filename` | uploads bulk participant data to the system | POST |
| `/query` | query for active matches | POST |
| `/lookup_ids/:id` | Returns PII for a Lookup ID | GET |

Detailed documentation for each endpoint can be found [here](./openapi/generated/duplicate-participation-api/openapi.md).

## Authentication

States will be issued API keys that are placed into request headers to authenticate a web service call. The bulk upload API requires a separate API key from the query and lookup APIs.

Example using cURL:

```
curl --request PUT '<uri>' --header 'Ocp-Apim-Subscription-Key: <api-key>'
```

## Feedback

Got any feedback for us? We track API issues through [Github Issues](https://github.com/18F/piipan/issues).

We also have a Microsoft Teams channel for daily communication with state agency engineers.


