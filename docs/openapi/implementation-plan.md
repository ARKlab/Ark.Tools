# OpenAPI modernization implementation plan

## Phase 1: Introduce enhanced Microsoft OpenAPI now

- Create `Ark.Tools.AspNetCore.OpenApi` for Microsoft OpenAPI integration in the current modernization cycle, not in .NET 11.
- Add generator-neutral options for document metadata, API versioning, schema mappings, default responses, OData cleanup, operation IDs, static spec hosting, and frontend extensions.
- Implement Microsoft OpenAPI document, operation, and schema transformers for Ark.Tools features.
- Integrate `Asp.Versioning.OpenApi` for document-per-version generation and URL version substitution.
- Configure OpenAPI JSON generation for every normal build, including development builds.
- Make generated specs available to unit/integration tests for snapshot and retrocompatibility checks.
- Host generated specs from the API as static JSON files rather than runtime-generated documents.

## Phase 2: Documentation and compatibility framing

- Publish modernization research, design, and migration guidance under `docs/openapi/`.
- Document the current Swashbuckle extension inventory and the planned Microsoft OpenAPI equivalents.
- Document always-on generation, API-hosted static specs, and test validation guidance.
- Add a compatibility matrix for .NET 10, .NET 11, and .NET 12.
- Announce that `Ark.Tools.AspNetCore.Swashbuckle` is legacy and planned for removal in .NET 12, while still supported until then.
- Add samples for controller APIs and minimal APIs if minimal APIs are supported by Ark.Tools consumers.

## Phase 3: API-hosted frontend integrations

- Expose Swagger UI, Scalar, and Redoc directly from the API wherever API documentation is enabled.
- Keep `Swashbuckle.AspNetCore.SwaggerUI` as the Swagger UI bundled asset provider during compatibility.
- Evaluate `Swashbuckle.AspNetCore.ReDoc` as the preferred bundled Redoc option during compatibility; evaluate `Redoc.AspNetCore` before introducing it as a new dependency.
- Add Scalar as the recommended enhanced interactive UI for Microsoft OpenAPI using `Scalar.AspNetCore` bundled assets.
- Configure every UI to consume API-hosted static JSON rather than runtime-generated OpenAPI endpoints.
- Document how each frontend consumes:
  - API-hosted static JSON;
  - multi-version document lists;
  - OAuth/security UI settings.
- Document Redoc vendor extensions and Scalar configuration supported by Ark.Tools.

## Phase 4: Swashbuckle compatibility adapter

- Add compatibility adapters only where they reduce migration cost.
- Keep existing Swashbuckle filters functional.
- Inventory `Swashbuckle.AspNetCore.Annotations` usage and define mappings for annotations that Ark.Tools will support in the enhanced generator.
- Inventory `Swashbuckle.AspNetCore.Filters` usage, including sample use of `IExamplesProvider<T>`, and define a generator-neutral examples story.
- Add deprecation attributes to Swashbuckle-specific convenience APIs after the enhanced equivalent exists.
- Keep obsolete no-op filters as-is until .NET 12 to avoid unexpected source breaks.
- Ensure compatibility documentation clearly separates Swashbuckle-only APIs from generator-neutral APIs.

## Phase 5: Validation and release readiness

- Add automated tests for transformer output parity against the existing Swashbuckle behavior where feasible.
- Add always-on OpenAPI generation to CI for representative APIs.
- Add OpenAPI artifact diff/lint guidance, preferably using Spectral, and compatibility assertions in tests.
- Validate generated documents with Swagger UI, Scalar, and Redoc.
- Validate API-versioned documents for URL-segment versioning and deprecated/sunset versions.

## Phase 6: .NET 12 removal

Remove in the .NET 12 major version line:

- `Ark.Tools.AspNetCore.Swashbuckle` package.
- Swashbuckle-specific extension methods and filters.
- Swagger UI and Redoc middleware convenience APIs that require Swashbuckle runtime generation.
- Compatibility documentation for old Swashbuckle setup, except for archived migration notes.

Preserve beyond .NET 12:

- Microsoft OpenAPI generator integration.
- Build-time generation guidance.
- API-hosted Swagger UI guidance when Swagger UI consumes static specs without Swashbuckle generation.
- Scalar frontend guidance.
- Redoc frontend guidance against generated/static OpenAPI artifacts.
- Generator-neutral OpenAPI extension model.

## Documentation to create during implementation

- `docs/openapi/getting-started.md`: recommended enhanced setup.
- `docs/openapi/build-time-generation.md`: development/CI/build artifact generation, startup safety guidance, and test integration.
- `docs/openapi/versioning.md`: `Asp.Versioning.OpenApi` integration and document-per-version patterns.
- `docs/openapi/frontends.md`: API-hosted Swagger UI, Scalar, and Redoc setup and support matrix.
- `docs/openapi/migration-from-swashbuckle.md`: old API, annotations, filters, and examples mapping.
- `docs/openapi/extensions.md`: supported Ark.Tools and frontend vendor extensions.
- `docs/openapi/security.md`: production exposure, auth, static docs, and information-disclosure guidance.
- `docs/openapi/net12-removal.md`: final removal checklist and breaking changes.

## Compatibility timeline

| Timeframe | Default recommendation | Compatibility requirement |
| --- | --- | --- |
| Now / .NET 10 | Introduce enhanced Microsoft OpenAPI package and always-on spec generation. | Preserve Swashbuckle, Swagger UI, and Redoc behavior for existing apps. |
| .NET 11 | Microsoft OpenAPI is preferred for new apps; Swashbuckle generation is marked deprecated. | Preserve compatibility and provide migration docs. |
| .NET 12 / 2027 | Microsoft OpenAPI is the supported generator. | Remove Swashbuckle generation compatibility APIs and package. |

## Acceptance criteria

- Existing Swashbuckle users can continue unchanged until .NET 12.
- New users have a documented Microsoft OpenAPI path with always-on generation and API-hosted static specs.
- Every current Ark.Tools OpenAPI extension has an enhanced equivalent, a documented replacement, or an explicit deprecation/removal decision.
- Swagger UI, Scalar, and Redoc are exposed directly by the API and consume generated static OpenAPI artifacts.
- Production deployments can avoid exposing anonymous runtime document-generation endpoints while still hosting static OpenAPI specs.
