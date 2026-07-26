# NET-01 — OpenAPI 3.1 verification, YAML and doc-UI decision (N3)

> **Rescoped**: XML-documentation population moved to
> [GEN-09](../generator-dx/GEN-09-xml-documentation.md), which does it in the generators for both
> OpenAPI and the exported `.proto`. NET-01 keeps 3.1 verification, the YAML endpoint and the doc-UI
> decision.

**Category**: aspnetcore · **Priority**: **Release blocker** (decision D7) · **Scope**: FRAMEWORK + SAMPLE

## Problem

The mediator sample uses `Microsoft.AspNetCore.OpenApi` (`AddOpenApi`) but:
1. OpenAPI **3.1** schema output of generator-emitted endpoints is unverified (nullable handling,
   `IntroducedIn`/`RetiredIn` per-version docs, polymorphic contracts).
2. Both Swashbuckle.SwaggerUI and Scalar are present — decide deliberately.

Files: `samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/SampleStartup.cs`
(OpenAPI/Scalar/Swagger wiring), framework OpenAPI helpers in
`src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/ArkOpenApiEx.cs` and `ArkOpenApiSecurityEx.cs`.

## Steps

1. Verify 3.1 output: snapshot-test the generated document for one contract per feature: nullable
   property, NodaTime type, polymorphic contract, versioned endpoint (`IntroducedIn`), multipart
   endpoint. Fix framework transformers where the schema is wrong.
2. YAML endpoint: expose the document in YAML too if trivially supported by `MapOpenApi` (it is:
   `/openapi/{documentName}.yaml`); document the route.
3. UI decision: drop Swashbuckle.SwaggerUI from the sample in favor of **Scalar only**, unless
   `AddAuthorizationCodeFlow` (see `SampleStartup.cs`) depends on SwaggerUI — in that case port the
   OAuth flow config to Scalar. Remove the unused package reference + lockfile entries.
4. Descriptions in OpenAPI and in the exported protos are GEN-09's responsibility; do not duplicate them here.

## Outcomes

- OpenAPI 3.1 correctness of generated endpoints is snapshot-verified, the YAML document is reachable and a single deliberate doc UI ships.

## Acceptance

- [ ] 3.1 snapshot tests for nullable/NodaTime/polymorphic/versioned/multipart schemas pass.
- [ ] YAML document reachable.
- [ ] Single doc UI (Scalar) with working OAuth flow; Swashbuckle.SwaggerUI reference removed (or a recorded, deliberate decision to keep both).
- [ ] Lockfiles updated; full solution build + tests green.
