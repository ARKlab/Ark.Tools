# NET-01 — XML docs into OpenAPI + 3.1 verification (N3)

**Category**: aspnetcore · **Priority**: **Release blocker** (decision D7) · **Scope**: FRAMEWORK + SAMPLE

## Problem

The mediator sample uses `Microsoft.AspNetCore.OpenApi` (`AddOpenApi`) but:
1. XML documentation comments on contracts/properties are **not** populated into the OpenAPI
   document (summaries/descriptions missing) — .NET 10 supports XML-comment population for
   `AddOpenApi` via the `Microsoft.AspNetCore.OpenApi` source generator (`<GenerateDocumentationFile>`
   + the OpenAPI XML comment support).
2. OpenAPI **3.1** schema output of generator-emitted endpoints is unverified (nullable handling,
   `IntroducedIn`/`RetiredIn` per-version docs, polymorphic contracts).
3. Both Swashbuckle.SwaggerUI and Scalar are present — decide deliberately.

Files: `samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/SampleStartup.cs`
(OpenAPI/Scalar/Swagger wiring), framework OpenAPI helpers in
`src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/ArkOpenApiEx.cs` and `ArkOpenApiSecurityEx.cs`.

## Steps

1. Enable XML-doc population: ensure contract projects produce XML docs (`GenerateDocumentationFile`
   is likely already on repo-wide — verify in `Directory.Build.props`) and enable the .NET 10
   `AddOpenApi` XML comment integration in the sample; verify `<summary>` from a contract type and its
   properties appear as operation/schema descriptions. If XML comments must flow from a *referenced*
   assembly (Application → WebInterface), verify the .NET 10 `AdditionalFiles`-based cross-assembly
   support and wire it; if the framework generator must contribute operation summaries (e.g. from the
   contract's XML docs), extend `ArkOpenApiEx` with a document/operation transformer that reads the
   XML file — pick the mechanism that works, document it in `design.md`.
2. Verify 3.1 output: snapshot-test the generated document for one contract per feature: nullable
   property, NodaTime type, polymorphic contract, versioned endpoint (`IntroducedIn`), multipart
   endpoint. Fix framework transformers where the schema is wrong.
3. YAML endpoint: expose the document in YAML too if trivially supported by `MapOpenApi` (it is:
   `/openapi/{documentName}.yaml`); document the route.
4. UI decision: drop Swashbuckle.SwaggerUI from the sample in favor of **Scalar only**, unless
   `AddAuthorizationCodeFlow` (see `SampleStartup.cs`) depends on SwaggerUI — in that case port the
   OAuth flow config to Scalar. Remove the unused package reference + lockfile entries.
5. Ensure descriptions also reach the exported protos where applicable (out of scope if non-trivial — note follow-up).

## Outcomes

- OpenAPI documents carry XML-doc summaries/descriptions for generated endpoints and schemas; 3.1 correctness is snapshot-verified; a single deliberate doc UI ships.

## Acceptance

- [ ] Contract `<summary>` visible in operation and schema descriptions (test inspects the document).
- [ ] 3.1 snapshot tests for nullable/NodaTime/polymorphic/versioned/multipart schemas pass.
- [ ] YAML document reachable.
- [ ] Single doc UI (Scalar) with working OAuth flow; Swashbuckle.SwaggerUI reference removed (or a recorded, deliberate decision to keep both).
- [ ] Lockfiles updated; full solution build + tests green.
