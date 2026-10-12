# Contract design checklist — design

Date: 2026-10-12
Status: approved in brainstorming, pending written-spec review

## Goal

When an agent (or a developer) designs or changes a Mediator Framework contract,
every design question is answered before code is written. The checklist lives
once, in the consumer user guide; the `ark-mediator-framework` skill references
it and enforces it as a gate.

Success criteria:

- `docs/mediator-framework/contract-design-checklist.md` is the single source of
  the questions, defaults, per-field table and brief template.
- The skill fetches the checklist, carries only an index of section names and
  hard defaults, and refuses to implement before the human confirms the brief.
- Skill-creator evals show the new skill covers all checklist sections, tags
  answers, asks for legal decisions instead of inventing them, and writes no code
  before confirmation, where the current skill does not.

## Decisions taken while brainstorming

| Topic | Decision |
| --- | --- |
| Who answers | The agent drafts every answer, tags it `derived (source)` or `ASK`, then asks the human the `ASK` items one at a time. No implementation before explicit confirmation. |
| Brief output | Agreed in chat, pasted into the PR description. Decisions not expressed by code go into XML docs (`<summary>`/`<remarks>`). Decisions already expressed by an attribute (route, verb, version, scope) are not repeated in prose. |
| Update semantics | `PUT` is always a full replace, including nulls. An absent field means "default", never "untouched". A partial update requires a dedicated, intent-specific operation. |
| Business rules | Each state-dependent rule is a named `BusinessRuleViolation` subclass and is documented on the contract as a possible failure. |
| API conventions | Codified as defaults; a deviation needs a recorded reason. |
| Resource-level denial | Decided per contract. Prefer `403` unless it discloses existence: unguessable ids (GUIDs) → `404`; guessable sequential ids (int) → `403` is harmless. The brief records the id type and the reason. |
| Enums | `EvolvableEnum<T>` with `NOT_SET = 0` on every public contract. Exceptions: enums controlled externally (e.g. a third-party interface adapter) and `[Flags]` enums; the brief names the exception. |
| Breaking changes | Avoid them. The checklist asks for the additive path first (optional field with default, new operation); a new `[Versioning]` contract needs a written reason the additive path is impossible. |
| GDPR scope | Full privacy-by-design: purpose, lawful basis, retention period and DPIA trigger are answered before coding. They are always `ASK`, owned by a human, and must be verified with the DPO/legal; the agent never decides them. |
| Personal data in messages | Split by audience. Public/cross-application events and messages carry identifiers only; personal data is not allowed. Internal background messages (same application, owned queue) may carry classified personal data. Sensitive personal data and special-category data (GDPR Art. 9/10) never. Every queue, dead-letter queue and outbox has an intrinsic TTL; the brief records it. Platform encryption at rest is the baseline; per-message or per-column encryption is an additional measure the brief decides on. |
| Single source | The doc is canonical. The skill links it via the raw GitHub URL and keeps a ~30-line index of section names and hard defaults so the gate works if the fetch fails. |
| Skill description | Stays scoped to consuming Mediator Framework; it gains design/review triggers but no generic API-design or GDPR triggers. |

## Part 1 — user guide

New file `docs/mediator-framework/contract-design-checklist.md`:

0. **How to use it.** Draft, tag (`derived (source)` / `ASK`), ask one at a time,
   confirm, implement. "N/A" needs a reason. Brief goes to chat and the PR;
   non-attribute decisions go to XML docs.
1. **Operation.** Purpose; kind (query, request, command, event, message); owner;
   synchronous vs background.
2. **Fields and updatability.** Per-field table for every model the contract
   touches:
   `Field | Type | Updatability | Required | Null meaning | Default | Concurrency | Validation | Personal-data class`.
   Updatability labels: Updatable (`Input`), Create-only (`Create : Input`),
   Output-only (`Output`), Context-supplied (handler/server context, never in the
   body), Identity (operation envelope route key). PUT full replace; partial
   update = own operation.
3. **Types and representation.** Value objects for domain primitives (Vogen,
   sensitive value objects); NodaTime type choice (`Instant`, `LocalDate`,
   `ZonedDateTime`, …); decimal precision; units and currency; normalisation.
4. **Validation.** Stateless FluentValidation rules (presence, length, range,
   format, collection size, cross-field); reject unknown `EvolvableEnum` input;
   maximum page size.
5. **Business rules.** `Rule | State needed | Named violation` (uniqueness,
   referenced entity exists, state transitions, quotas, ownership).
6. **Failure contract.** Every public failure with ProblemDetails type/code,
   HTTP status and gRPC status: validation 400, rule violation 400, 403, 404,
   concurrency 409, ETag 412.
7. **Concurrency and idempotency.** ETag/`If-Match` or version field; retry-safe
   POST; message redelivery and deduplication key.
8. **API design.** Defaults: `/api/v{version}/<plural-kebab-resource>/{id}/<sub-resource>`;
   non-CRUD actions as sub-paths; list query shape (paging, sort, filter); status
   per operation kind; gRPC service = plural resource, method = `VerbNoun`;
   event/message naming. Deviations need a reason.
9. **Lists, streaming and files.** Paging, sort and filter shape and maximum
   page size, or an `IAsyncEnumerable` stream instead; attachments: size,
   count, content types, download disposition.
10. **Limits and abuse.** Maximum body, page, attachment and stream sizes; rate
    limiting (host route group); expensive queries and how they are bounded.
11. **Transport exposure and serialization.** Which of HTTP, gRPC, MCP, Azure
    Functions, MessagePack and why; STJ source-generated context; protobuf field
    numbers; MCP tool description and whether the operation is destructive.
12. **Authorization.** Operation policy (`Ark.Tools.Authorization`); resource-level
    check with the 403/404 rule above; list scoping; field visibility; identity
    for background messages and re-check on consume.
13. **Messaging.** Publisher and subscribers; logical names and `FormerNames`;
    retry, dead-letter, outbox, idempotency; intrinsic TTL of every queue,
    dead-letter queue and outbox.
14. **Versioning and compatibility.** Additive first; enum rule; reserved
    protobuf numbers and logical names; API-surface snapshot, OpenAPI and proto
    diffs reviewed.
15. **Privacy (GDPR).** Personal-data class per field (personal, special
    category Art. 9, pseudonymous, not personal); purpose, lawful basis,
    retention, DPIA trigger (`ASK`, DPO-verified); minimisation; exposure across
    outputs, transports, logs, telemetry and third parties; cross-application
    events and messages carry identifiers only, internal messages may carry
    classified personal data, sensitive or special-category data never, and
    every queue and outbox they reach has a TTL; per-message or
    per-column encryption on top of platform encryption at rest; erasure including temporal history and
    audit; access and export. Sub-section mapping each answer to
    Ark.Tools.Compliance (classification attributes, sensitive value objects,
    `ARKPII*`, `ArkComplianceSurface.txt`, SQL policies) and the manual checks
    when it is not used.
16. **Audit and observability.** Persisted audit (who, when, what); telemetry and
    log tags without personal data; metrics.
17. **Documentation.** XML docs on contract and every property (feeds OpenAPI,
    MCP, gRPC); documented failures.
18. **Tests.** Application, transport-boundary and messaging tests proving each
    decision.
- **Brief template.** Markdown for the PR description.

Edits to existing docs:

- `contract-design-workflow.md`: replace the "Required contract brief" table with
  a link to the checklist; keep the decision tree, shaping, boundaries and test
  ownership.
- `versioning.md`: the "Add an enum entry" row distinguishes plain enums
  (breaking) from `EvolvableEnum` (compatible).
- `README.md`: add the checklist to learning-path step 3a.
- `docs/plans/mediator-framework/tasks/aspnetcore/NET-03-json-patch.md`: add a
  step and acceptance item to update the checklist (fields and updatability)
  and the skill when PATCH ships.
- `CHANGELOG.md`: one entry under `## [Unreleased]`.

## Part 2 — skill

`agents-plugins/ark-csharp/.apm/skills/ark-mediator-framework/SKILL.md`, about
150 lines:

- Description scoped to Mediator Framework consumers, adding design and review
  of contracts and their fields, routes, gRPC methods, validation, business
  rules, authorization, versioning and personal data.
- "Contract design gate" replaces the 11-bullet information gate: fetch the
  checklist, draft and tag, ask `ASK` items one at a time (legal items always
  `ASK`), confirm, implement, put the brief in the PR and decisions in XML docs.
  Without a human to answer, stop after the brief.
- "Checklist index": the 18 section names with their hard defaults.
- Keep, trimmed: workflow, generator boundary, transactional messaging,
  guardrails.
- New red flags: plain enum on a public contract; unclassified personal-data
  field; agent decided lawful basis or retention; PUT that skips absent fields;
  business rule without a named violation; 403/404 chosen without the id type.
- New rationalizations: "we control all clients, so a plain enum is fine";
  "internal queue, retention doesn't matter".
- Bump `ark-csharp` to `1.5.0`; regenerate with `apm install` and
  `apm run plugins:build`; verify with `apm run plugins:check` and
  `apm audit --ci`.

## Evaluation (skill-creator)

- Baseline: snapshot of the current skill; compare old vs new.
- Prompts (subagents cannot converse, so each asks for the brief and the
  questions, no implementation):
  1. Update Customer A's contact email and phone in a CRM service.
  2. Add `Status` (Draft/Published/Archived) to Book and publish a
     `BookArchived` event consumed by other applications.
  3. Rename `Book.Author` to an `Authors` list in the v1 contract.
- Assertions: all 18 sections present; every answer tagged; legal items `ASK`;
  per-field table with updatability labels; `EvolvableEnum` chosen; named
  violations; 403/404 reasoned from id type; no code written.
- Review through the skill-creator static viewer; iterate until accepted;
  offer description optimisation at the end.
- Eval files stay in the session scratchpad, not in the skill folder (APM would
  publish them). The PR summarises results.

## Out of scope

- PATCH support. NET-03 owns it and now updates the checklist and the skill.
- New generator and analyzer diagnostics. Existing ones already enforce part of
  the checklist: `ARKMF020` (version prefix without the version token),
  `ARKMF003` (mass assignment), `ARKMF012`/`ARKMF059` (route and query binding),
  `ARKMF016` (duplicate operation name), `ARKMF055` (MCP tool documentation),
  `ARKCORE001`-`004` (evolvable enum shape), `ARKPII001` (unclassified
  personal-data names) and `ARKAPI002` (API surface changed). Candidate
  follow-ups that would turn checklist defaults into diagnostics:
  - a plain enum on a public contract member (suggest `EvolvableEnum<T>`);
  - a `[SensitivePersonalData]` member reachable from a `[Message]` or `[Event]`
    contract;
  - a route that does not match `/api/v{version}/<plural-kebab-resource>`;
  - a gRPC method name that is not `VerbNoun`;
  - a public contract or property without XML documentation (today only MCP
    tools are checked, by `ARKMF055`).
