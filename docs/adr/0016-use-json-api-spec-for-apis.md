# 16. Use a subset of the JSON API Specification for all API responses

Date: 2021-06-29

## Status

Proposed

## Context

While our API endpoints serve difference purposes, they generally need to return similar data:

1. metadata about the request/response
1. HTTP-level error messages
1. the actual payload

We've been adding API endpoints organically as needed, which has resulted in different schemas and approaches to returning these data types.

Standardizing response schemas across our API's can:
- remove the need for API developers to reinvent this wheel for every API and endpoint
- help clients integrate with the API faster

While we use the [OpenAPI Specification](https://swagger.io/specification/) for our API schemas, the purpose of OpenAPI is only to descibe schemas consistently, not to determine what the schema should be. For the latter, other specifications exist. It'd be up to us to pick one or create our own.

## Decision

We adopt the following subset of the [JSON API Specification](https://jsonapi.org/) for a [top-level](https://jsonapi.org/format/#document-top-level) response schema:

A response document *MUST* contain at least one of the following top-level members:

- *data*: the document's "primary data"
- *errors*: an array of error objects
- *meta*: a meta object that contains non-standard meta-information.

The members data and errors *MUST NOT* coexist in the same document.

Primary data *MUST* be either:

- a single resource object or null, for requests that target single resources
- an array of resource objects or an empty array ([]), for requests that target resource collections

## Consequences

- We'll need to adjust our existing API endpoints to return this top-level schema.
- Breaking changes to the API
