# OpenAPI modernization implementation plan

## Phase 1: Documentation and deprecation framing

- Publish modernization research, design, and migration guidance under `docs/openapi/`.
- Document the current Swashbuckle extension inventory and the planned Microsoft OpenAPI equivalents.
- Document production guidance: build-time generation, static publication, and development-only runtime UI endpoints.
- Add a compatibility matrix for .NET 10, .NET 11, and .NET 12.
- Announce that `Ark.Tools.AspNetCore.Swashbuckle` is legacy and planned for removal in .NET 12, while still supported until then.

## Phase 2: Enhanced Microsoft OpenAPI package

- Create `Ark.Tools.AspNetCore.OpenApi` for Microsoft OpenAPI integration.
- Add generator-neutral options for document metadata, API versioning, schema mappings, default responses, OData cleanup, operation IDs, and frontend extensions.
- Implement Microsoft OpenAPI document, operation, and schema transformers for Ark.Tools features.
- Integrate `Asp.Versioning.OpenApi` for document-per-version generation and URL version substitution.
- Provide build-time generation project configuration guidance using `Microsoft.Extensions.ApiDescription.Server`.
- Add samples for controller APIs and minimal APIs if minimal APIs are supported by Ark.Tools consumers.

## Phase 3: Frontend integration recipes

- Keep Swagger UI recipes for compatibility and local ad-hoc testing.
- Add Scalar as the recommended enhanced interactive UI for Microsoft OpenAPI.
- Keep Redoc as the recommended static/public reference frontend.
- Document how each frontend consumes:
  - runtime development endpoints;
  - build-time generated static JSON;
  - multi-version document lists.
- Document Redoc vendor extensions and Scalar configuration supported by Ark.Tools.

## Phase 4: Swashbuckle compatibility adapter

- Add compatibility adapters only where they reduce migration cost.
- Keep existing Swashbuckle filters functional.
- Add deprecation attributes to Swashbuckle-specific convenience APIs after the enhanced equivalent exists.
- Keep obsolete no-op filters as-is until .NET 12 to avoid unexpected source breaks.
- Ensure compatibility documentation clearly separates Swashbuckle-only APIs from generator-neutral APIs.

## Phase 5: Validation and release readiness

- Add automated tests for transformer output parity against the existing Swashbuckle behavior where feasible.
- Add sample build-time OpenAPI generation to CI for representative APIs.
- Add OpenAPI artifact diff/lint guidance, preferably using Spectral.
- Validate generated documents with Swagger UI, Scalar, and Redoc.
- Validate API-versioned documents for URL-segment versioning and deprecated/sunset versions.

## Phase 6: .NET 12 removal

Remove in the .NET 12 major version line:

- `Ark.Tools.AspNetCore.Swashbuckle` package.
- Swashbuckle-specific extension methods and filters.
- Swagger UI middleware convenience APIs that require Swashbuckle generation.
- Compatibility documentation for old Swashbuckle setup, except for archived migration notes.

Preserve beyond .NET 12:

- Microsoft OpenAPI generator integration.
- Build-time generation guidance.
- Scalar frontend guidance.
- Redoc frontend guidance against generated/static OpenAPI artifacts.
- Generator-neutral OpenAPI extension model.

## Documentation to create during implementation

- `docs/openapi/getting-started.md`: recommended enhanced setup.
- `docs/openapi/build-time-generation.md`: CI/build artifact generation and startup safety guidance.
- `docs/openapi/versioning.md`: `Asp.Versioning.OpenApi` integration and document-per-version patterns.
- `docs/openapi/frontends.md`: Swagger UI, Scalar, and Redoc setup and support matrix.
- `docs/openapi/migration-from-swashbuckle.md`: old API to new API mapping.
- `docs/openapi/extensions.md`: supported Ark.Tools and frontend vendor extensions.
- `docs/openapi/security.md`: production exposure, auth, static docs, and information-disclosure guidance.
- `docs/openapi/net12-removal.md`: final removal checklist and breaking changes.

## Compatibility timeline

| Timeframe | Default recommendation | Compatibility requirement |
| --- | --- | --- |
| .NET 10 | Start enhanced Microsoft OpenAPI work; keep Swashbuckle default for existing apps. | Preserve Swashbuckle, Swagger UI, and Redoc behavior. |
| .NET 11 | Prefer Microsoft OpenAPI for new apps; Swashbuckle marked deprecated. | Preserve compatibility and provide migration docs. |
| .NET 12 / 2027 | Microsoft OpenAPI is the supported generator. | Remove Swashbuckle compatibility APIs and package. |

## Acceptance criteria

- Existing Swashbuckle users can continue unchanged until .NET 12.
- New users have a documented Microsoft OpenAPI path with build-time generation.
- Every current Ark.Tools OpenAPI extension has an enhanced equivalent, a documented replacement, or an explicit deprecation/removal decision.
- Swagger UI, Scalar, and Redoc can all consume generated OpenAPI artifacts.
- Production deployments can avoid exposing anonymous runtime document-generation endpoints.
