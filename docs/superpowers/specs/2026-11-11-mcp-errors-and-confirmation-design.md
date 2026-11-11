# MCP tool errors and confirmation (elicitation) design

Status: approved 2026-11-11. Scope: `Ark.Tools.MediatorFramework.Mcp` (preview).

## Intent

Generated MCP tools must behave well by default for two readers:

- the **agent**, which must be able to correct a failed call from the error alone;
- the **human**, who is asked by the client to confirm a destructive call,
  so the answer does not come from the model (on a client that co-operates).

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

A call-tool filter keyed on tool metadata (the way `[Authorize]` works) was
considered in place of the generated call. It is not chosen: it needs a new
metadata attribute and a runtime lookup for what is one generated line.

## Design

### Part 1: errors (default, no opt-in)

`McpToolErrors.ToToolResult` keeps `IsError = true` and the ProblemDetails in
`StructuredContent`. The text block changes:

- first `{Title}: {Detail}` (as today);
- then, on a new line, the ProblemDetails extensions as one compact JSON
  object (`errors`, `businessRuleViolation`), so that a client that gives the
  model only the text loses nothing. Keys keep the CLR property names, as in
  `StructuredContent`. The default JSON encoder is used, so values stay
  escaped.

Unexpected failures (not 4xx) keep the generic text, as today. If the
extensions cannot be serialized, the result has the first line only and
ProblemDetails without extensions (today this case throws).

### Part 2: confirmation

**Attribute.** `McpToolAttribute.Confirm` (`bool`). When not supplied, the
generator uses `true` if the contract has `[HttpEndpoint("DELETE", ...)]`,
otherwise `false`. Same pattern as `AllowAnonymous`.

**Generated wrapper.** Only for tools with `Confirm = true`, `ExecuteAsync`
gets a `RequestContext<CallToolRequestParams>` parameter and starts with one
call:

```csharp
McpToolConfirmation.EnsureConfirmed(context, "<tool name>", "<description>");
```

**Runtime helper** `McpToolConfirmation` (new, in `Ark.Tools.MediatorFramework.Mcp`):

1. `InputResponses` contains the key `confirm`:
   - action `accept`: return, the wrapper dispatches;
   - other action: throw `BusinessRuleViolationException` (title
     `Not confirmed`, status 409; changed during implementation, because the
     analyzers require public exception types), which
     the error filter maps to `IsError = true`, text
     `"Not confirmed: the user declined '<tool>'. Do not retry without a new user request."`
   A response that is not a valid elicitation result counts as declined.
2. No response, and the client supports MRTR and declares the elicitation
   capability: throw `InputRequiredException` with one elicitation, message
   `"Confirm <tool>: <description>"`, empty schema (accept or decline is the
   answer).
3. No response and the client cannot be asked: **proceed** (the fallback).

**Fallback (approved).** A client without MRTR or without the elicitation
capability runs the tool without confirmation. Reason: clients may ask for
tool approval from the `Destructive` hint, the HTTP DELETE endpoint has no
confirmation either, and a refusal would make these tools unusable on most
clients today. The server never enforces confirmation.

**Arguments in the message (approved).** The message ends with a new line
and `Arguments: ` plus the call arguments as one compact JSON object, so the
user can see which item is affected. The helper reads them from
`context.Params.Arguments`; the generator does not change for this. Each value
longer than 200 characters is cut and followed by the number of hidden
characters, because the arguments are written by the model and can be long (an
attachment, for example). The cut is for each value and not for the whole
message, so that one long argument cannot hide the others (changed during
implementation after a security review finding; the first version cut the
whole JSON at 500 characters). The default JSON encoder is used.

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
- Sample: `Book_DeleteRequest` with `[McpTool]`.

The plan extends this list (validation text, malformed answers, verb casing,
`PUT`, name collisions).

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
