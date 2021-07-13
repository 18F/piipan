# 16. Use JSON API Specification as guidance for our API conventions

Date: 2021-06-29

## Status

Proposed

## Context

While our API endpoints serve difference purposes, certain request/response data remains consistent:

1. metadata about the request/response
1. error messages
1. the actual payload

We've been adding API endpoints organically as needed, which has resulted in different schemas and approaches to receiving and returning JSON data.

Standardizing request and response schemas across our API's can:
- remove the need for API developers to reinvent this wheel for every API endpoint
- help clients integrate with the API faster

While we use the [OpenAPI Specification](https://swagger.io/specification/) for our API schemas, the purpose of OpenAPI is only to descibe schemas consistently, not to determine what the schema should be. For the latter, other conventions exist. It'd be up to us to pick one or create our own.

Adopting a convention that's already widely used has some benefits:
- Scenarios and edge cases have been accounted for that we haven't encountered yet on our project
- It may open up more tooling options to us and to those who use our API's

One popular convention is the [JSON API specification](https://jsonapi.org/) which bills itself as "your anti-bikeshedding tool" for deciding how your API data should be formatted.

There are aspects of the JSON API spec that don't fit our purposes, so we're reluctant to adopt the spec wholesale. But it's very useful as general inspiration and guidance for our own conventions. When considering changes or extensions to the API, it's a great starting point.

## Decision

Guided by the [JSON API Specification](https://jsonapi.org/) for a [top-level](https://jsonapi.org/format/#document-top-level) request and response schema, we will adopt the following for our API's:

A request or response body that's meant to contain JSON data **MUST** contain at least one of these top-level properties:

- **data**: the document's "primary data"
- **errors**: an array of error objects
- **meta**: a meta object that contains non-standard meta-information.

The properties `data` and `errors` **MUST NOT** coexist in the same document.

While JSON API specifies a format for the content of `data` ([resource objects](https://jsonapi.org/format/#document-resource-objects)), we will not adopt a specific format for `data` at this time. We believe the potential data for our API's will vary and extend beyond the REST-ful structure of JSON API.

We will also adopt JSON API's [Error Objects](https://jsonapi.org/format/#error-objects) structure:

Error objects **MUST** be returned as an array keyed by `errors`.

An error object **MAY** have the following general properties in addition to context-specific properties:

- **status**: the HTTP status code applicable to this problem, expressed as a string value.
- **code**: an application-specific error code, expressed as a string value.
- **title**: a short, human-readable summary of the problem that **SHOULD NOT** change from occurrence to occurrence of the problem, except for purposes of localization.
- **detail**: a human-readable explanation specific to this occurrence of the problem. Like title, this field’s value can be localized.

An example of a basic error for a 422 HTTP response:

```
// HTTP/1.1 422 Unprocessable Entity

{
  "errors": [
    {
      "status": "422",
      "code": "XYZ",
      "title":  "Invalid Attribute",
      "detail": "First name must contain at least three characters."
    }
  ]
}
```

## Consequences

- We'll need to adjust our existing API endpoints to accept and return this top-level schema.
- Breaking changes to the existing API
