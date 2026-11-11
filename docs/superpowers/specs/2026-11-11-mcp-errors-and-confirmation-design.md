# MCP tool errors and confirmation (elicitation) design

Status: approved 2026-11-11. Scope: `Ark.Tools.MediatorFramework.Mcp` (preview).

## Intent

Generated MCP tools must behave well by default for two readers:

- the **agent**, which must be able to correct a failed call from the error alone;
- the **human**, who must be able to confirm a destructive call without the
  model in the path.

## Goals

1. Validation and business-rule failures give the agent all data in the error
   text, not only in `StructuredContent`.
2. An opt-in, declarative confirmation on `[McpTool]`, with a default derived
   from the HTTP binding.
3. Handlers and contracts stay free of MCP types.

## Non-goals

- An elicitation abstraction that handlers can call (no use case; it would keep
  a transaction open while a human thinks).
- Workflow approvals. They are domain state: a command and a status query.
- URL-mode elicitation, secrets, payment data.
- Overridable ("soft") business rules. Possible later on the same mechanism.

## Anti-goals

- Confirmation must not be presented as a security control. A client can send
  an accepted answer without showing the user. Authorization stays in the
  existing decorators.
- A tool must not become unusable on clients without elicitation support.

## Constraints found

- SDK 2.4.0 defaults to stateless HTTP. Server-to-client `elicitation/create`
  does not work there and is removed in protocol revision `2026-07-28`.
- The SDK supports Multi Round-Trip Requests (MRTR): the tool throws
  `InputRequiredException` with `InputRequest.ForElicitation(...)`, the client
  asks the user and calls the tool again with `InputResponses`. This works in
  stateless and stateful modes. `McpServer.IsMrtrSupported` reports support.
- `McpToolErrors` catches every exception except `McpException` and
  cancellation. `InputRequiredException` must pass through it.
- `mcp-design.md` says stateful mode is required for elicitation. This is now
  wrong and must be corrected.

## Approaches

| | Approach | Trade-off |
|---|---|---|
| A | Errors only | Smallest. No human confirmation. |
| **B** | A plus declarative confirmation in the generated wrapper | Chosen. Handlers stay pure, no open transaction, works stateless through MRTR. |
| C | B plus an elicitation service for handlers | Rejected. No use case, transport concern inside the handler, transaction open during a human wait. |

Inside B, the transport is MRTR only. The legacy `ElicitAsync` path is not
implemented: it needs stateful sessions and the protocol removes it.

## Design

### Part 1: errors (default, no opt-in)

`McpToolErrors.ToToolResult` keeps `IsError = true` and the ProblemDetails in
`StructuredContent`. The text block changes:

- line 1: `{Title}: {Detail}` (as today);
- then the ProblemDetails extensions as compact JSON (`errors`,
  `businessRuleViolation`), so that a client that gives the model only the text
  loses nothing.

Unexpected failures (not 4xx) keep the generic text, as today.

### Part 2: confirmation

**Attribute.** `McpToolAttribute.Confirm` (`bool`). When not supplied, the
generator uses `true` if the contract has `[HttpEndpoint("DELETE", ...)]`,
otherwise `false`. Same pattern as `AllowAnonymous`.

**Generated wrapper.** Only for tools with `Confirm = true`, `ExecuteAsync`
gets the `McpServer` and `RequestContext<CallToolRequestParams>` parameters and
starts with one call:

```csharp
McpToolConfirmation.EnsureConfirmed(server, context, "<tool name>", "<description>");
```

**Runtime helper** `McpToolConfirmation` (new, in `Ark.Tools.MediatorFramework.Mcp`):

1. `InputResponses` contains the key `confirm`:
   - action `accept`: return, the wrapper dispatches;
   - other action: throw `McpToolDeclinedException` (new, internal), which
     the error filter maps to `IsError = true`, text
     `"Not confirmed: the user declined '<tool>'. Do not retry without a new user request."`
2. No response and `server.IsMrtrSupported`: throw `InputRequiredException`
   with one elicitation, message `"Confirm <tool>: <description>"`, empty
   schema (accept or decline is the answer).
3. No response and no MRTR support: **proceed** (call 1 of the fallback below).

**Fallback (approved).** Without MRTR the tool runs without confirmation.
Reason: the `Destructive` hint already makes clients ask for tool approval,
the HTTP DELETE endpoint has no confirmation either, and a refusal would make
these tools unusable on most clients today.

**Error filter.** `McpToolErrors` lets `InputRequiredException` pass.

### Part 3: hints from the HTTP verb (approved)

When `[McpTool]` does not set `Idempotent`, the generator uses `true` if the
contract has `[HttpEndpoint]` with verb `DELETE` or `PUT` (case-insensitive),
otherwise `false` as today.

`Destructive` does not change: every request and command stays
`Destructive = true`, `POST` included, so that clients keep asking the user
to approve each change.

## Tests

- `McpHostingTests`: business-rule violation text contains the payload;
  confirmed call dispatches; declined call returns the error and does not
  dispatch; client without MRTR dispatches.
- `GeneratorSnapshotTests`: `Confirm` explicit, `Confirm` derived from DELETE,
  explicit `Confirm = false` on a DELETE contract.
- Sample: one DELETE contract with `[McpTool]`.

## Documentation

- `docs/mediator-framework/mcp.md`: errors, `Confirm`, the "not a security
  control" warning, and the stateful-sessions sentence (line 84).
- `docs/design/mediator-framework/mcp-design.md`: the attribute table
  (lines 166-169: add `Confirm`, new `Idempotent` default), the stateful
  statements (lines 79-81 and 543-544), the error table (lines 489-499), and
  the MRTR decision.
- `CHANGELOG.md`: Added (`Confirm`), Changed (error text, `Idempotent`
  default).

## Left to the builder

- Exact SDK type names and the base class of `InputRequiredException`
  (verify against the 2.4.0 package, not the docs).
- How the test client answers an MRTR elicitation.

## Open risks

- The confirmation is not bound to the arguments: a client can change them on
  the retry. Accepted, because confirmation is not a security control.
- MRTR client support is new. Until clients adopt `2026-07-28`, most calls use
  the fallback.
