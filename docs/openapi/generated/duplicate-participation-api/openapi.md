<!-- Generator: Widdershins v4.0.1 -->

<h1 id="duplicate-participation-api">Duplicate Participation API v1.0.0</h1>

> Scroll down for code samples, example requests and responses. Select a language for code samples from the tabs above or the mobile navigation menu.

The API where matching and lookups will occur

Base URLs:

* <a href="/v1">/v1</a>

# Authentication

* API Key (ApiKeyAuth)
    - Parameter Name: **Ocp-Apim-Subscription-Key**, in: header. 

<h1 id="duplicate-participation-api-match">Match</h1>

## Query for Matches

<a id="opIdQuery for Matches"></a>

> Code samples

```shell
# You can also use wget
curl -X POST /v1/query \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json' \
  -H 'Ocp-Apim-Subscription-Key: API_KEY'

```

`POST /query`

*Search for all matching PII records*

Queries all state databases for any PII records that are an exact match to the last name, date of birth, and social security number of persons provided in the request.

> Body parameter

> An example request to query a single person, with values for all fields

```json
{
  "data": [
    {
      "first": "string",
      "middle": "string",
      "last": "string",
      "ssn": "000-00-0000",
      "dob": "1970-01-01"
    }
  ]
}
```

<h3 id="query-for-matches-parameters">Parameters</h3>

|Name|In|Type|Required|Description|
|---|---|---|---|---|
|data|body|[[#/paths/~1query/post/requestBody/content/application~1json/schema/properties/data/items](#schema#/paths/~1query/post/requestbody/content/application~1json/schema/properties/data/items)]|true|none|

> Example responses

> A query for a single person returning a single match

```json
{
  "data": {
    "results": [
      {
        "index": 0,
        "lookup_id": "string",
        "matches": [
          {
            "first": "string",
            "middle": "string",
            "last": "string",
            "ssn": "000-00-0000",
            "dob": "1970-01-01",
            "state": "ea",
            "state_abbr": "ea",
            "exception": "string",
            "case_id": "string",
            "participant_id": "string",
            "benefits_end_month": "2021-01",
            "recent_benefit_months": [
              "2021-05",
              "2021-04",
              "2021-03"
            ],
            "protect_location": true
          }
        ]
      }
    ],
    "errors": []
  }
}
```

> A query for a single person returning no matches

```json
{
  "data": {
    "results": [
      {
        "index": 0,
        "lookup_id": null,
        "matches": []
      }
    ],
    "errors": []
  }
}
```

> A query for one person returning multiple matches

```json
{
  "data": {
    "results": [
      {
        "index": 0,
        "lookup_id": "string",
        "matches": [
          {
            "first": "string",
            "middle": "string",
            "last": "string",
            "ssn": "000-00-0000",
            "dob": "1970-01-01",
            "state": "eb",
            "state_abbr": "eb",
            "exception": "string",
            "case_id": "string",
            "participant_id": "string",
            "benefits_end_month": "2021-01",
            "recent_benefit_months": [
              "2021-05",
              "2021-04",
              "2021-03"
            ],
            "protect_location": true
          },
          {
            "first": null,
            "middle": null,
            "last": "string",
            "ssn": "000-00-0000",
            "dob": "1970-01-01",
            "state": "ec",
            "state_abbr": "ec",
            "exception": null,
            "case_id": "string",
            "participant_id": null,
            "benefits_end_month": null,
            "protect_location": null
          }
        ]
      }
    ],
    "errors": []
  }
}
```

> A query for two persons returning one match for each person

```json
{
  "data": {
    "results": [
      {
        "index": 0,
        "lookup_id": "string",
        "matches": [
          {
            "first": null,
            "middle": null,
            "last": "string",
            "ssn": "000-00-0000",
            "dob": "1970-01-01",
            "state": "ec",
            "state_abbr": "ec",
            "exception": null,
            "case_id": "string",
            "participant_id": null,
            "benefits_end_month": null,
            "protect_location": null
          }
        ]
      },
      {
        "index": 1,
        "lookup_id": "string",
        "matches": [
          {
            "first": null,
            "middle": null,
            "last": "string",
            "ssn": "000-00-0000",
            "dob": "1970-01-01",
            "state": "ec",
            "state_abbr": "ec",
            "exception": null,
            "case_id": "string",
            "participant_id": null,
            "benefits_end_month": null,
            "protect_location": null
          }
        ]
      }
    ],
    "errors": []
  }
}
```

> A query for two persons returning no matches for one person and a match for the other

```json
{
  "data": {
    "results": [
      {
        "index": 0,
        "lookup_id": null,
        "matches": []
      },
      {
        "index": 1,
        "lookup_id": "string",
        "matches": [
          {
            "first": null,
            "middle": null,
            "last": "string",
            "ssn": "000-00-0000",
            "dob": "1970-01-01",
            "state": "ec",
            "state_abbr": "ec",
            "exception": null,
            "case_id": "string",
            "participant_id": null,
            "benefits_end_month": null,
            "protect_location": null
          }
        ]
      }
    ],
    "errors": []
  }
}
```

> A query for two persons returning a successful result for one person and an error for the other person

```json
{
  "data": {
    "results": [
      {
        "index": 1,
        "lookup_id": "string",
        "matches": [
          {
            "first": null,
            "middle": null,
            "last": "string",
            "ssn": "000-00-0000",
            "dob": "1970-01-01",
            "state": "ec",
            "state_abbr": "ec",
            "exception": null,
            "case_id": "string",
            "participant_id": null,
            "benefits_end_month": null,
            "protect_location": null
          }
        ]
      }
    ],
    "errors": [
      {
        "index": 0,
        "code": "XYZ",
        "title": "Internal Server Exception",
        "detail": "Unexpected Server Error. Please try again."
      }
    ]
  }
}
```

> An example response for an invalid request

```json
{
  "errors": [
    {
      "status": "400",
      "code": "XYZ",
      "title": "Bad Request",
      "detail": "Request payload exceeds maxiumum count"
    }
  ]
}
```

<h3 id="query-for-matches-responses">Responses</h3>

|Status|Meaning|Description|Schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Successful response. Returns match response items.|Inline|
|400|[Bad Request](https://tools.ietf.org/html/rfc7231#section-6.5.1)|Bad request. Missing one of the required properties in the request body.|None|

<h3 id="query-for-matches-responseschema">Response Schema</h3>

Status Code **200**

|Name|Type|Required|Restrictions|Description|
|---|---|---|---|---|
|» data|object¦null|false|none|The response payload. Either an errors or data property will be present in the response, but not both.|
|»» results|array|true|none|Array of query results. For every person provided in the request, a result is returned for every successful query, even if no matches are found. If a query fails, the failure data will be in the errors array.|
|»»» index|integer|true|none|The index of the person that the result corresponds to, starting from 0. Index is derived from the implicit order of persons provided in the request.|
|»»» lookup_id|string¦null|false|none|The identifier of the person data, if a match is present. This ID can be used for looking up the PII of the person provided in the original request.|
|»»» matches|[object]|true|none|none|
|»»»» first|string|false|none|First name|
|»»»» middle|string|false|none|Middle name|
|»»»» last|string|true|none|Last name|
|»»»» ssn|string|true|none|Social Security number|
|»»»» dob|string(date)|true|none|Date of birth|
|»»»» state|string|false|none|State/territory two-letter postal abbreviation|
|»»»» state_abbr|string|false|none|State/territory two-letter postal abbreviation. Deprecated, superseded by `state`.|
|»»»» exception|string|false|none|Placeholder for value indicating special processing instructions|
|»»»» case_id|string|false|none|Participant's state-specific case identifier. Can be the same for multiple participants.|
|»»»» participant_id|string|false|none|Participant's state-specific identifier. Is unique to the participant. Must not be social security number or any PII.|
|»»»» benefits_end_month|string|false|none|Participant's ending benefits month|
|»»»» recent_benefit_months|[string]|false|none|List of up to the last 3 months that participant received benefits, in descending order. Each month is formatted as ISO 8601 year and month. Does not include current benefit month.|
|»»»» protect_location|boolean¦null|false|none|Location protection flag for vulnerable individuals. True values indicate that the individual’s location must be protected from disclosure to avoid harm to the individual. Apply the same protections to true and null values.|
|»» errors|array|true|none|Array of error objects corresponding to a person in the request. If a query for a single person fails, the failure data will display here.|
|»»» index|integer|true|none|The index of the person that the result corresponds to, starting from 0. Index is derived from the implicit order of persons provided in the request.|
|»»» code|string|false|none|The application-specific error code|
|»»» title|string|false|none|The short, human-readable summary of the error, consistent across all occurrences of the error|
|»»» detail|string|false|none|The human-readable explanation specific to this occurrence of the error|
|» errors|array¦null|false|none|Holds HTTP and other top-level errors. Either an errors or data property will be present in the response, but not both.|
|»» status|string|true|none|The HTTP status code|
|»» code|string|false|none|The application-specific error code|
|»» title|string|false|none|The short, human-readable summary of the error, consistent across all occurrences of the error|
|»» detail|string|false|none|The human-readable explanation specific to this occurrence of the error|

<aside class="warning">
To perform this operation, you must be authenticated by means of one of the following methods:
ApiKeyAuth
</aside>

<h1 id="duplicate-participation-api-lookup">Lookup</h1>

## Get Lookups by ID

<a id="opIdGet Lookups by ID"></a>

> Code samples

```shell
# You can also use wget
curl -X GET /v1/lookup_ids/{id} \
  -H 'Accept: application/json' \
  -H 'Ocp-Apim-Subscription-Key: API_KEY'

```

`GET /lookup_ids/{id}`

*Get the original match data related to a Lookup ID*

User can provide a Lookup ID and receive the match data associated with it

> Example responses

> A response showing a query with values for all fields

```json
{
  "data": {
    "first": "string",
    "middle": "string",
    "last": "string",
    "ssn": "000-00-0000",
    "dob": "1970-01-01"
  }
}
```

> A response showing a query with values for only required fields

```json
{
  "data": {
    "first": "string",
    "last": "string",
    "ssn": "000-00-0000",
    "dob": "1970-01-01"
  }
}
```

<h3 id="get-lookups-by-id-responses">Responses</h3>

|Status|Meaning|Description|Schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Successful response. Returns original match query request item.|Inline|
|400|[Bad Request](https://tools.ietf.org/html/rfc7231#section-6.5.1)|Bad request|None|
|404|[Not Found](https://tools.ietf.org/html/rfc7231#section-6.5.4)|Not found|None|

<h3 id="get-lookups-by-id-responseschema">Response Schema</h3>

<aside class="warning">
To perform this operation, you must be authenticated by means of one of the following methods:
ApiKeyAuth
</aside>

