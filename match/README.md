# Duplicate Participation APIs

A collection of externally-available and internal APIs that:
* Allow Piipan to find duplicate participation during participant certification or recertification
* Allow agencies to find PII of individuals involved in a match using a Lookup ID

All APIs adhere to [OpenAPI specifications](./docs/openapi.md).

## External APIs
APIs that are made available to state systems integrating with Piipan make up the [Duplicate Participation API](./docs/duplicate-participation-api.md).

These include:
* [Orchestrator PII matching API](./docs/orchestrator-match.md) for learning whether an individual matches against other state participant records
* [Lookup ID API](./docs/lookup.md) for retrieving PII involved in a match

## Internal APIs
APIs that Piipan uses internally but does not expose to agencies.
* [Per-state PII matching API](./docs/state-match.md)
