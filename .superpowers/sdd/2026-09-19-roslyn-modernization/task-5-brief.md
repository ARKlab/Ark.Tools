### Task 5: Mediator generator safety filters and named cache infrastructure

**Files:**
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.{MinimalApi,Grpc,Rebus}.Generators/*Generator.cs`
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.{ApiSurface,AzureFunctions,Messaging,Mcp}.Generators/*Generator.cs` as applicable
- Modify: `tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs`

**Interfaces:**
- Produces: cheap type-only attribute predicates, cheap mapping invocation filters, and reusable named-stage cache assertions.
- Preserves: valid endpoint generation and all existing diagnostics.

- [ ] **Step 1: Add invalid-target safety tests**

For each changed attribute generator, compile one valid attributed type plus an illegally attributed method or field. Assert valid generated output remains present and no `CS8785` generator-failure diagnostic is produced.

- [ ] **Step 2: Add invocation filtering tests**

Build source containing one valid mapping invocation and many unrelated invocations. Assert generated output remains identical and the named mapping parser stage only runs for syntactic candidates.

- [ ] **Step 3: Implement safe predicates and projections**

Replace always-true attribute predicates followed by `INamedTypeSymbol` casts with `node is TypeDeclarationSyntax` predicates and nullable transforms. For Minimal API, gRPC, and Rebus invocation providers, check simple generic method name and type-argument arity before `GetSymbolInfo`.

- [ ] **Step 4: Add reusable named-stage cache assertions**

Replace tests that accept any cached stage with helpers that run against a distinct compilation containing only an unrelated edit, then assert specific parser/model/output tracking names are `Cached` or `Unchanged`. Include a relevant-edit `Modified` assertion.

- [ ] **Step 5: Run focused Mediator tests**

Run: `dotnet test tests/Ark.Tools.MediatorFramework.Tests/ --no-restore --filter "DisplayName~Generator"`

Expected: PASS.

- [ ] **Step 6: Commit**

Run:
```bash
git add src/mediator-framework tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs
git commit -m "fix(Mediator): filter generator inputs safely"
```

