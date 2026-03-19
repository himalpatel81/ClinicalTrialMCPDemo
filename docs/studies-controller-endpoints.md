# Studies Controller API Reference

This document describes the endpoint exposed by `StudiesController` in the `ClinicalTrials.Api` project.

Related MCP server documentation:

- `docs/mcp-server.md`

## Controller Summary

- Controller: `StudiesController`
- Base route: `/api/studies`
- Response format: Proxies the upstream response body and content type from ClinicalTrials.gov
- Upstream service: `ClinicalTrials:BaseUrl` + `api/v2/studies/{nctId}`

Default upstream base URL from configuration:

```json
{
  "ClinicalTrials": {
    "BaseUrl": "https://clinicaltrials.gov/"
  }
}
```

## Exposed Endpoint

### `GET /api/studies/{nctId}`

Returns a single study record by NCT ID. The API acts as a thin proxy over the ClinicalTrials.gov v2 studies endpoint.

#### Route Parameters

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `nctId` | `string` | Yes | The ClinicalTrials.gov NCT identifier, for example `NCT04924608`. |

#### Request Example

```http
GET /api/studies/NCT04924608
Accept: application/json
```

Local development example from the repo:

```http
GET http://localhost:5010/api/studies/NCT04924608
Accept: application/json
```

#### Upstream Mapping

For a request such as:

```http
GET /api/studies/NCT04924608
```

the controller calls:

```http
GET https://clinicaltrials.gov/api/v2/studies/NCT04924608
```

The `nctId` value is URL-escaped before being sent upstream.

## Behavior

- The controller creates a named `HttpClient` called `ClinicalTrialsGov`.
- It forwards the request to ClinicalTrials.gov using `GET`.
- It reads the full upstream response body as text.
- It returns the upstream body unchanged.
- It returns the upstream `Content-Type` unchanged when available; otherwise it defaults to `application/json`.
- It returns the upstream HTTP status code unchanged for normal upstream responses, including non-2xx responses such as `404`.

## Response Codes

### `200 OK`

Returned when ClinicalTrials.gov successfully returns the study payload.

Response body:

- JSON returned directly from ClinicalTrials.gov.
- The schema is not reshaped by this API.

### `400 Bad Request`

Returned when `nctId` is null, empty, or whitespace.

Example response:

```json
{
  "title": "Invalid NCT ID",
  "detail": "The nctId parameter is required.",
  "status": 400
}
```

Note: because `nctId` is part of the route, this condition is mainly defensive. A missing route segment typically does not match the route at all.

### Upstream Status Pass-Through

If ClinicalTrials.gov responds with an HTTP status such as `404 Not Found`, `401 Unauthorized`, or `500 Internal Server Error`, this API returns:

- The same status code
- The same response body
- The same content type when present

### `502 Bad Gateway`

Returned when the upstream ClinicalTrials.gov service cannot be reached and an `HttpRequestException` is thrown.

Example response:

```json
{
  "title": "ClinicalTrials.gov request failed",
  "detail": "The upstream ClinicalTrials.gov service could not be reached.",
  "status": 502
}
```

### `504 Gateway Timeout`

Returned when the upstream request times out and the client request itself was not canceled.

Example response:

```json
{
  "title": "ClinicalTrials.gov request timed out",
  "detail": "The upstream ClinicalTrials.gov service did not respond in time.",
  "status": 504
}
```

## Operational Notes

- HTTPS redirection is enabled by the application pipeline.
- Authorization middleware is present, but there is no controller-level authorization attribute on this endpoint.
- OpenAPI is registered and mapped only in the `Development` environment.
- Development launch profiles expose the API on `http://localhost:5010` and `https://localhost:7001`.

## Source References

- `ClinicalTrials.Api/ClinicalTrials.Api/Controllers/StudiesController.cs`
- `ClinicalTrials.Api/ClinicalTrials.Api/Program.cs`
- `ClinicalTrials.Api/ClinicalTrials.Api/appsettings.json`
- `ClinicalTrials.Api/ClinicalTrials.Api/ClinicalTrials.Api.http`
- `ClinicalTrials.Api/ClinicalTrials.Api/Properties/launchSettings.json`
