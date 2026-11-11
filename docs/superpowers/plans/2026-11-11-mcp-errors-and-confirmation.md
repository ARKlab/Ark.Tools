# MCP Tool Errors and Confirmation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generated MCP tools give the agent complete error text, and ask the human to confirm destructive calls through MCP Multi Round-Trip Requests (MRTR).

**Architecture:** `McpToolErrors` adds the ProblemDetails extensions to the error text. A new `McpToolAttribute.Confirm` makes the generator emit one call to a new runtime helper, `McpToolConfirmation`, before dispatch. The helper throws the SDK `InputRequiredException` on the first call and reads `InputResponses` on the retry. Handlers and contracts get no MCP types.

**Tech Stack:** .NET 10, Roslyn incremental generator (`netstandard2.0`), `ModelContextProtocol.AspNetCore` 2.4.0, MSTest, AwesomeAssertions.

**Spec:** `docs/superpowers/specs/2026-11-11-mcp-errors-and-confirmation-design.md`

## Global Constraints

- No new package dependency.
- MRTR only. Do not call `McpServer.ElicitAsync`.
- Client without MRTR support: the tool runs without confirmation.
- `Destructive` defaults do not change.
- Unexpected failures (not 4xx) keep the generic text. Nothing new may leak.
- Confirmation is not a security control. The docs must say so.
- Public members need XML documentation. Warnings are errors.
- Commits: Conventional Commits, no AI attribution. Branch: `feature/mcp-confirmation`.
- Run only focused tests:
  - `dotnet test tests/Ark.Tools.MediatorFramework.Tests --no-restore --filter "FullyQualifiedName~GeneratorSnapshotTests"`
  - `dotnet test tests/Ark.Tools.MediatorFramework.Hosting.Tests --no-restore --filter "FullyQualifiedName~McpHostingTests"`

## Review Focus

1. A client sends `InputResponses["confirm"]` with a payload that is not an elicitation result (wrong shape, `null`). Expected: treated as declined, no dispatch, no 500.
2. A business-rule payload contains a value that the source-generated JSON context cannot serialize. Expected: the error result is still returned, with the `{Title}: {Detail}` line. Test in Task 1.
3. `[McpTool(Confirm = false)]` on a `DELETE` contract. Expected: no confirmation code is emitted. Test in Task 3.
4. `[HttpEndpoint("delete", ...)]` in lower case. Expected: same defaults as `DELETE`. Test in Task 3.
5. A contract with a property named `Server` or `Context` and `Confirm = true`. Expected: the generated wrapper compiles (use `FreeName`). Test in Task 3.

---

### Task 1: Complete error text

**Files:**
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.Mcp/McpToolErrors.cs`
- Modify: `tests/Ark.Tools.MediatorFramework.Hosting.Contracts/HostingContracts.cs` (new contract)
- Modify: `tests/Ark.Tools.MediatorFramework.Hosting.Tests/HostingTestFixture.cs` (handler)
- Test: `tests/Ark.Tools.MediatorFramework.Hosting.Tests/McpHostingTests.cs`
- Modify: `docs/mediator-framework/mcp.md` (section "Errors and tests"), `docs/design/mediator-framework/mcp-design.md:489-499`, `CHANGELOG.md`

**Interfaces:**
- Produces: error text format `"{Title}: {Detail}"`, then `"\n"` and the ProblemDetails `Extensions` as one compact JSON object, only when the failure is client-visible and `Extensions` is not empty.

- [ ] **Step 1: Add a contract `[McpTool(Name = "hosting.businessrule")]`** whose handler throws the existing synthetic `BusinessRuleViolationException` (see `HostingTestFixture.cs:893`). Use a violation subtype with one extra property, `Limit = 5`.

- [ ] **Step 2: Write the failing tests**

```csharp
[TestMethod]
public async Task McpBusinessRuleErrorTextContainsPayload()
{
    // call "hosting.businessrule"
    result.IsError.Should().BeTrue();
    var text = ((TextContentBlock)result.Content[0]).Text;
    text.Should().StartWith("Synthetic rule");
    text.Should().Contain("\"businessRuleViolation\"").And.Contain("\"limit\":5");
}

[TestMethod]
public async Task McpValidationErrorTextContainsFieldErrors()
{
    // call "hosting.validation"
    text.Should().StartWith("Validation failed: ").And.Contain("\"errors\":{");
}
```

Extend the existing unexpected-failure test: `text.Should().NotContain("{")`.

- [ ] **Step 3: Run** the `McpHostingTests` filter. Expected: the two new tests FAIL.

- [ ] **Step 4: Implement in `ToToolResult`.** Serialize `problemDetails.Extensions` with `McpToolErrorJsonSerializerContext` (`Dictionary<string, object>` is already registered). If serialization throws `NotSupportedException` or `JsonException`, keep the one-line text (Review Focus 2). Add a unit test for this with a payload object of an unregistered type, calling `McpToolErrors.ToToolResult` directly.

- [ ] **Step 5: Run** the filter. Expected: PASS.

- [ ] **Step 6: Docs.** `mcp.md` "Errors and tests": describe the text format. `mcp-design.md` error table: same. `CHANGELOG.md` under `## [Unreleased]` / Changed: "`Ark.Tools.MediatorFramework.Mcp`: MCP error text now includes the validation errors and the business-rule details, so agents that read only the text can correct the call."

- [ ] **Step 7: Commit** `feat(MediatorFramework): add error details to mcp tool error text`

### Task 2: Confirmation runtime helper

**Files:**
- Create: `src/mediator-framework/Ark.Tools.MediatorFramework.Mcp/McpToolConfirmation.cs`
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.Mcp/McpToolErrors.cs`
- Test: `tests/Ark.Tools.MediatorFramework.Hosting.Tests/McpHostingTests.cs`

**Interfaces:**
- Produces:

```csharp
public static class McpToolConfirmation
{
    public const string InputKey = "confirm";

    /// Returns when the call is confirmed or the client has no MRTR support.
    public static void EnsureConfirmed(
        McpServer server,
        RequestContext<CallToolRequestParams> context,
        string toolName,
        string? description);
}
```

- [ ] **Step 1: Verify the SDK API in 2.4.0.** Write a scratch file in the test project that references `InputRequiredException`, `InputRequest.ForElicitation`, `McpServer.IsMrtrSupported`, `CallToolRequestParams.InputResponses` and `InputResponse.ElicitResultJsonTypeInfo`, then `dotnet build`. Record in the commit body: the exact names, whether the API has an `[Experimental]` diagnostic, and whether `InputRequiredException` derives from `McpException`. If a name differs, use the real one in the next steps. If the API is absent in 2.4.0, stop and report: a package bump needs approval. Delete the scratch file.

- [ ] **Step 2: Write the failing unit tests** (direct calls, no host). Build `RequestContext` the way the SDK tests do; if it cannot be constructed, move these cases to the Task 4 host tests.

| Test | Arrange | Assert |
|---|---|---|
| `ConfirmationRunsWithoutMrtr` | `IsMrtrSupported == false`, no responses | no exception |
| `ConfirmationRequestsInput` | MRTR, no responses | throws `InputRequiredException` with one request, key `"confirm"`, message `"Confirm books.delete: Deletes a book."` |
| `ConfirmationAcceptReturns` | response `confirm`, action `accept` | no exception |
| `ConfirmationDeclineThrows` | action `decline` | throws `McpToolDeclinedException` |
| `ConfirmationMalformedResponseThrows` | response `confirm` = JSON `null` or `{}` | throws `McpToolDeclinedException` |

- [ ] **Step 3: Run.** Expected: compile FAIL.

- [ ] **Step 4: Implement.**
  - Message: `"Confirm {toolName}"` plus `": {description}"` when the description is not empty. Requested schema: an object with no properties.
  - `McpToolDeclinedException` is `internal sealed`, in the same file.
  - In `McpToolErrors`: let `InputRequiredException` pass through the filter (add a `catch` before the general one, if Step 1 found that it does not derive from `McpException`). Map `McpToolDeclinedException` in `ToToolResult` before the ProblemDetails mapper: `IsError = true`, no log, text `"Not confirmed: the user declined '{toolName}'. Do not retry without a new user request."`, structured ProblemDetails with `Status = 409`, `Title = "Not confirmed"`.

- [ ] **Step 5: Run.** Expected: PASS.

- [ ] **Step 6: Commit** `feat(MediatorFramework): add mcp tool confirmation helper`

### Task 3: Generator: `Confirm` and `Idempotent` defaults

**Files:**
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework/McpToolAttribute.cs`
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.Mcp.Generators/McpToolGenerator.cs` (parse at `:527-545`, model at `:1066`, `RenderTool` at `:861`)
- Test: `tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs`

**Interfaces:**
- Consumes: `McpToolConfirmation.EnsureConfirmed(server, context, toolName, description)` from Task 2.
- Produces: `public bool Confirm { get; set; }` on `McpToolAttribute`; `bool Confirm` on `ContractModel`.

- [ ] **Step 1: Write the failing snapshot tests.** One source with four contracts, one test per row:

| Contract | Expect in generated code |
|---|---|
| `[McpTool, HttpEndpoint("DELETE", "/books/{id}")]` | `Idempotent = true`, one `McpToolConfirmation.EnsureConfirmed(` call, parameters of type `McpServer` and `RequestContext<...CallToolRequestParams>` |
| `[McpTool, HttpEndpoint("delete", "/x/{id}")]` | same |
| `[McpTool(Confirm = false, Idempotent = false), HttpEndpoint("DELETE", ...)]` | `Idempotent = false`, no `EnsureConfirmed` |
| `[McpTool, HttpEndpoint("PUT", ...)]` | `Idempotent = true`, `Destructive = true`, no `EnsureConfirmed` |
| `[McpTool(Confirm = true)]` on a `POST` contract with properties `Server` and `Context` | `EnsureConfirmed` present and the generated compilation has no errors |

Also assert that an existing `POST` contract still has `Destructive = true` and `Idempotent = false`.

- [ ] **Step 2: Run** the `GeneratorSnapshotTests` filter. Expected: FAIL.

- [ ] **Step 3: Implement.**
  - Attribute: add `Confirm` with XML docs that state the DELETE default and that it is not a security control. Update the `Idempotent` docs.
  - Parse: read the verb from the first constructor argument of `httpEndpoint`, compare with `StringComparison.OrdinalIgnoreCase`. Use `HasNamedArgument` for both properties, as `AllowAnonymous` does.
  - Render: only when `model.Confirm`, add the two parameters with `FreeName("server")` and `FreeName("context")`, and emit the call as the first statement. Pass `model.Name` and the tool description as string literals (`Literal(...)`, `null` when there is none).

- [ ] **Step 4: Run.** Expected: PASS.

- [ ] **Step 5: Inspect the emitted code.** `dotnet build tests/Ark.Tools.MediatorFramework.Hosting.Contracts` and read the `.g.cs` under `obj/Debug/net10.0/generated/` (required by `AGENTS.md`).

- [ ] **Step 6: Commit** `feat(MediatorFramework): add confirm option to mcp tools`

### Task 4: End-to-end confirmation

**Files:**
- Modify: `tests/Ark.Tools.MediatorFramework.Hosting.Contracts/HostingContracts.cs`
- Modify: `tests/Ark.Tools.MediatorFramework.Hosting.Tests/HostingTestFixture.cs`
- Test: `tests/Ark.Tools.MediatorFramework.Hosting.Tests/McpHostingTests.cs`
- Modify: `tests/Ark.Tools.MediatorFramework.NativeAotApp/Program.cs` (one `Confirm` tool, so that the AOT publish covers the path)

**Interfaces:**
- Consumes: Tasks 2 and 3.

- [ ] **Step 1: Add** `[McpTool(Name = "hosting.delete"), HttpEndpoint("DELETE", "/hosting/items/{id}")]` with a handler that sets `fixture.State.Deleted = id`.

- [ ] **Step 2: Write the failing tests** with the official SDK client.

| Test | Client | Assert |
|---|---|---|
| `McpConfirmedDeleteRuns` | MRTR, elicitation handler returns `accept` | `IsError` not true, `State.Deleted == id`, handler saw message `"Confirm hosting.delete..."` |
| `McpDeclinedDeleteDoesNotRun` | MRTR, handler returns `decline` | `IsError == true`, text starts with `"Not confirmed"`, `State.Deleted` is null |
| `McpDeleteRunsWithoutMrtr` | client on a protocol version without MRTR | `IsError` not true, `State.Deleted == id` |
| `McpForgedMalformedConfirmationDoesNotRun` | raw call with `inputResponses: { "confirm": {} }` | `IsError == true`, `State.Deleted` is null |

- [ ] **Step 3: Run** the `McpHostingTests` filter. Expected: new tests FAIL only if Tasks 2-3 have a defect; fix the defect in its own file.

- [ ] **Step 4: Run** both focused filters. Expected: PASS.

- [ ] **Step 5: Commit** `test(MediatorFramework): cover mcp tool confirmation end to end`

### Task 5: Sample and documentation

**Files:**
- Modify: `samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.API/BookContracts.cs` (add `[McpTool]` to a `DELETE` contract, or add one if none exists, following the neighbours)
- Test: `samples/Ark.MediatorFramework.Sample/Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.Tests/McpToolsTests.cs` (tool is listed with `Idempotent` and `Destructive` hints)
- Modify: `docs/mediator-framework/mcp.md`
- Modify: `docs/design/mediator-framework/mcp-design.md`
- Modify: `CHANGELOG.md`
- Modify: `agents-plugins/ark-csharp/` skill `ark-mediator-framework`, only if it documents `[McpTool]` properties (grep `McpTool`); then run `apm run plugins:build` with APM 0.34.0.

- [ ] **Step 1: `mcp.md`.**
  - "Mark a contract": add `Confirm` and the verb-derived defaults (`DELETE`: `Confirm`, `Idempotent`; `PUT`: `Idempotent`).
  - New section "Confirmation": the flow (first call returns input required, the client asks the user, the retry runs), the fallback for clients without MRTR, the decline error, and the warning: "Confirmation is a user-experience aid, not a security control. A client can answer without showing the user. Keep authorization in handlers and policies."
  - Line 84: replace the stateful sentence with: confirmation works in stateless mode; stateful sessions are needed only for other server-to-client requests.

- [ ] **Step 2: `mcp-design.md`.**
  - Lines 79-81 and 543-544: stateless supports elicitation through MRTR (protocol `2026-07-28`); legacy `elicitation/create` is not used.
  - Lines 166-169: new `Idempotent` default, new `Confirm` row.
  - Lines 489-499: add rows for `InputRequiredException` (passes through) and declined confirmation (`IsError = true`).
  - New section "Confirmation and elicitation": the decision, the three approaches from the spec and why handler-level elicitation was rejected, the non-goals (workflow approvals, URL mode, soft business rules).

- [ ] **Step 3: `CHANGELOG.md`** under `## [Unreleased]`.
  - Added: "`Ark.Tools.MediatorFramework.Mcp`: `[McpTool(Confirm = true)]` asks the user to confirm a tool call before it runs. It is on by default for contracts with an HTTP `DELETE` endpoint. Clients without support for MCP multi round-trip requests run the tool without confirmation."
  - Changed: "`Ark.Tools.MediatorFramework.Mcp`: MCP tools for contracts with an HTTP `DELETE` or `PUT` endpoint are now marked idempotent by default."

- [ ] **Step 4: Verify links.** Every relative link in the two changed docs resolves; consumer docs do not link to `docs/superpowers/` or `docs/plans/`.

- [ ] **Step 5: Run** `dotnet build` and the sample `McpToolsTests`. Expected: PASS.

- [ ] **Step 6: Commit** `docs(MediatorFramework): document mcp confirmation and error text`

### Task 6: Review and pull request

- [ ] **Step 1:** Run the three mandatory reviews on the branch diff in parallel: `/ponytail:ponytail-review`, `/code-review`, `/security-review`. Fix high and critical findings, then repeat.
- [ ] **Step 2:** Push `feature/mcp-confirmation` and open a draft pull request to `master` with title `feat(MediatorFramework): add mcp tool confirmation and richer errors`.
