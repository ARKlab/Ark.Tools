# Privacy-by-default PRD design review

**Status:** Proposed for discussion  
**Date:** 2026-09-10  
**Scope:** Challenge the runtime and declaration model in [`privacy-by-default-prd.md`](privacy-by-default-prd.md) before further implementation

## 1. Executive summary

The goals of the privacy-by-default PRD remain sound:

- cleartext personal data must not enter diagnostic sinks accidentally;
- analyzers should make unsafe logging and data egress visible at build time;
- sensitive values should have an explicit, audited reveal path;
- untyped strings still need a runtime pattern-scanning fallback.

The current design is more complicated than those goals require. The generated sensitive value object is already the security boundary: its default string, formatted, debugger, and conversion surfaces are redacted. Once all sensitive values are required to use that generated shape, a second runtime object-graph redactor duplicates work and creates a less reliable security model.

This review proposes the following reset:

1. Treat generated sensitive value objects as intrinsically sensitive. Do not rediscover sensitivity by walking arbitrary object graphs at runtime.
2. Keep the runtime policy small: one process-level HMAC key, plus optional pattern scanning for untyped text.
3. Let NLog use its normal formatting and serialization paths. Generated `ToString`, `IFormattable`, and `ISpanFormattable` implementations provide the safe representation.
4. Keep classification metadata only where it serves inventory, analyzer, SQL, transport, or egress policy. Do not use it as a prerequisite for sink safety.
5. Render exception messages as messages and pattern-scan them when configured. Do not replace every exception message with `***ARKPII***`.
6. Keep exception message, exception data, and stack trace as separate contracts. Preserve native stack-trace rendering.

This document is a design review, not an implementation request. No production behavior should be changed until the decisions in §12 are confirmed.

## 2. What the PRD gets right

The following parts should remain:

- A sensitive value needs a generated, validated representation rather than a convention applied to arbitrary strings.
- `Reveal(CompliancePurpose)` is the explicit boundary for cleartext serialization or other reviewed egress.
- Analyzer enforcement is the first line of defense. Runtime redaction is a safety net, not a substitute for compile-time review.
- Pattern scanning is useful for legacy strings, third-party exceptions, and values that have already lost their CLR type.
- Redaction must be scoped to Ark-owned layouts and integration points. A library cannot safely intercept every custom NLog target or every arbitrary serializer.
- Transport serialization may require cleartext, but that path must be explicit and inventoried rather than reached through ordinary formatting.

## 3. Main design challenge: intrinsic sensitivity versus runtime classification

### 3.1 The generated type already owns the sink-safe representation

The generated value object currently provides all of the ordinary accidental-disclosure surfaces:

- `ToString()`;
- `IFormattable.ToString`;
- `ISpanFormattable.TryFormat`;
- debugger display;
- NLog/simple-serializer formatting;
- equality and normal value semantics without exposing the backing value.

The generated code also owns the selected redaction mode. This means a log statement containing an `EmailAddress` does not need a runtime traversal to discover that the value is sensitive. The value already knows how it may be rendered.

The current additional path through `ComplianceRedactor.Redact(object?)` is therefore problematic:

- it recursively walks arbitrary dictionaries, collections, properties, and fields;
- it needs reflection metadata caches, cycle detection, depth limits, item limits, and failure behavior;
- it has to guess how to rebuild objects after discovering a sensitive child;
- it can disagree with the generated value object's own rendering policy;
- it makes NativeAOT and custom collection behavior part of the compliance boundary;
- it creates a false impression that arbitrary object graphs are safely redacted.

This is a large runtime surface for a guarantee that generated value types already provide directly.

### 3.2 Proposed boundary

The compliance runtime should not answer:

> “Can I recursively inspect and reconstruct this arbitrary object graph?”

It should answer only:

> “How should already-rendered text be pattern-scanned, and what HMAC key does the generated value policy use?”

The proposed runtime responsibilities are:

- hold one immutable HMAC key, when HMAC redaction is selected;
- expose the redaction operation used by generated sensitive types;
- pattern-scan text at explicitly configured Ark layout or integration boundaries;
- provide a marker/fail-closed fallback for unsupported direct sensitive-value operations.

The runtime should not recursively inspect arbitrary values. NLog and other serializers should retain responsibility for traversing their own supported object graphs.

### 3.3 One HMAC key, one policy

There are currently two possible sources of the HMAC key:

- generated value code constructs an HMAC redactor from `ARK_TOOLS_COMPLIANCE_HMAC_KEY`;
- `ComplianceRedactionOptions` supplies a key to `ComplianceRedactor`.

Those paths can diverge. A generated value's `ToString()` can therefore produce a different pseudonym from a value handled by a runtime redactor configured with different options.

The revised design should have one process-level immutable compliance runtime configuration. The generated code and integrations must use the same HMAC provider and the same key. Configuration should fail at startup when HMAC redaction is selected without a valid key; it should not silently create independently configured redactors.

The key is the only sensitive runtime value required by this model. Redaction mode belongs to the generated type or its generator configuration, not to recursive runtime classification.

## 4. Do we still need classification attributes?

The statement that generated sensitive structs are intrinsically sensitive should remove a declaration ambiguity, but it does not automatically remove every classification concept.

These are separate concerns:

| Concern | Needs intrinsic generated type | May need classification metadata |
|---|---:|---:|
| Safe `ToString` and formatting | Yes | No |
| Accidental diagnostic logging | Yes | No |
| Analyzer recognition of a sensitive value | Yes | No, if the type is the source of truth |
| Data inventory and discovery | No | Yes |
| Personal versus sensitive-personal policy | No | Yes |
| Secret versus personal-data policy | No | Yes |
| SQL column and transport documentation | No | Yes |
| Retention, residency, or egress rules | No | Yes |
| HMAC versus erase selection | Yes, through generator/type policy | Optional, but must not be rediscovered dynamically |

### Proposed declaration model

- `[SensitiveValueObject<T>]` remains the generator contract. The generated type is intrinsically sensitive.
- The generator receives the redaction behavior as an explicit type-level setting, or applies a documented default.
- `[PersonalData]`, `[SensitivePersonalData]`, `[Secret]`, and `[Pseudonymous]` remain on the generated value-object declaration for inventory and policy tooling, not as usage-site annotations or runtime proof that a value needs redaction.
- A separate `[SensitiveData]` marker should not be introduced merely to make runtime redaction discoverable.
- The redaction setting is part of the generated type and its generated rendering methods. Developers do not repeat classification attributes at usage sites; analyzers guide them toward dedicated types such as `SensitiveEmail` or `PrivacyEmail` when policy variants are needed.
- Ordinary `string` remains ordinary text. The analyzer should require a generated sensitive type or an explicit reviewed exemption where the domain says a string carries regulated data.

This preserves the useful taxonomy without requiring every sink to rediscover it.

Classification metadata is retained on the value-object declaration. The important invariant is that sink safety comes from the generated type and cannot depend on an attribute being repeated at a usage site.

## 5. NLog design after the reset

### 5.1 Use native formatting and serialization

NLog already owns the difficult parts of its object graph:

- message-template formatting;
- nested object traversal for JSON layouts;
- collections and dictionaries;
- simple `IFormattable` values;
- serializer-specific escaping and output contracts.

The generated value object supplies a safe formatted representation. Recreating NLog's traversal in Ark code is unnecessary and risks changing NLog's semantics.

The revised NLog integration should therefore:

- keep NLog's normal message-template parser and formatter;
- allow generated sensitive values to flow through their safe `ToString`/formatting methods;
- allow NLog's serializer to recurse normally;
- ensure `JsonLayout` treats generated values as ordinary rendered values, not as transport properties; its output must use the safe generated representation rather than the transport converter that calls `Reveal`;
- avoid `RegisterObjectTransformation` for general compliance traversal;
- retain `ComplianceLayout` only where Ark explicitly owns a final text layout and pattern scanning is enabled.

This does not claim that every custom NLog renderer is safe. It defines a smaller supported surface: Ark-configured layouts plus generated sensitive types.

### 5.2 What pattern scanning is for

Pattern scanning is the fallback for text that has lost its type information:

- legacy string arguments;
- exception messages from third-party code;
- status text and display names;
- custom text produced inside an Ark-owned layout;
- values supplied by integrations that cannot preserve the generated type.

Pattern scanning is not the primary mechanism for generated sensitive values. It should not be required to make a generated `EmailAddress` safe.

The current scoped `ComplianceLayout` direction is consistent with this boundary. It should remain limited to affected Ark-owned layouts rather than becoming a universal NLog output interceptor.

### 5.3 OTel and other integrations

The same rule should apply outside NLog:

- preserve generated sensitive values until the integration's normal formatting boundary where possible;
- use their safe formatting surface;
- pattern-scan untyped text at the exporter boundary when explicitly configured;
- do not recursively clone arbitrary activity/tag object graphs.

The OTel processor should be reviewed against this rule. Its primary job should be normalization at the supported exporter boundary, not a second general-purpose object redactor.

## 6. Exception handling must preserve exception contracts

### 6.1 Current problem

The current `ComplianceExceptionLayoutRenderer` replaces the exception message and `ToString()` output with `***ARKPII***` whenever compliance is enabled. This loses useful diagnostics even when the message contains no personal data.

It also conflates separate NLog formats:

- `Message`;
- `ToString`;
- `Data`;
- `StackTrace`.

Those formats have different diagnostic contracts and should not share one blanket replacement.

### 6.2 Proposed behavior

| Exception output | Proposed behavior |
|---|---|
| `Message` | Render the original message, then pattern-scan it when exception scanning is enabled |
| `Data` | Preserve the data layout contract; scan rendered data when explicitly enabled, otherwise follow the configured data policy |
| `StackTrace` | Use native NLog stack-trace rendering; do not replace it with the marker |
| `ToString` | Do not use as the default substitute for message rendering; if supported, preserve type/inner-exception formatting and define scanning explicitly |
| Exception type | Preserve |
| Empty/null message | Preserve native NLog behavior |

Analyzer rules should prevent application code from placing a revealed sensitive value into a newly created exception message. Runtime scanning remains necessary because exceptions can originate in libraries, transports, validation frameworks, or user input.

The preferred composition is explicit:

- `ExceptionMessage` uses a message renderer plus the exception-message scanner;
- `StackTrace` uses native `${exception:format=StackTrace}`;
- other layouts opt into a defined exception format instead of silently receiving a marker.

If a custom renderer remains, it should render through the base NLog renderer into a temporary buffer and scan only the contract requested by its `format` rather than replacing every format with one marker.

### 6.3 Default for exception scanning

The PRD should make this explicit:

- analyzer protection is always expected for application-created exception messages;
- exception pattern scanning is a runtime fallback and should be configurable;
- enabling compliance must not erase all exception messages;
- a fail-closed marker is appropriate only when a value cannot be rendered safely, not as the default for every ordinary exception.

## 7. Revised protection matrix

| Input | Compile-time protection | Normal runtime representation | Optional fallback | Must preserve |
|---|---|---|---|---|
| Generated sensitive value in message template | Analyzer and generated type | Safe `ToString`/formatting through NLog | Layout pattern scan | Message-template semantics |
| Generated sensitive value in JSON property | Analyzer and generated type | NLog serializer uses safe value representation | Layout pattern scan | JSON shape and escaping |
| Generated sensitive value in debugger | Generated debugger display | Redacted text | None | No cleartext debugger display |
| Explicit transport serialization | Analyzer and `Reveal(CompliancePurpose)` | Cleartext only at reviewed serializer adapter | None | Transport contract |
| Legacy/untyped string | Analyzer guidance | Ordinary string | Pattern scan at configured boundary | Original text when no match |
| Application exception message | Analyzer | Original message | Exception pattern scan | Exception message |
| Third-party exception message | Not guaranteed | Original message | Exception pattern scan | Exception type and message contract |
| Exception stack trace | Not a data payload | Native NLog stack trace | No blanket scanner | Stack trace format |
| Activity/tag containing generated value | Analyzer and generated type | Safe formatting at integration boundary | Pattern scan for untyped text | Tag names and exporter semantics |
| Arbitrary object graph | Not a supported redaction contract | Serializer-specific behavior | No recursive Ark clone | Serializer semantics |

## 8. Proposed API and implementation simplification

This review does not authorize the changes, but the resulting implementation should trend toward:

### Remove or narrow

- `ComplianceRedactor.Redact(object?)` recursive graph traversal;
- reflection metadata caches, graph budgets, and cycle handling used only by that traversal;
- `IRuntimeClassifiedValue` and runtime classification dispatch;
- general NLog object transformations that recreate serializer behavior;
- blanket exception marker behavior;
- separate HMAC key paths.

### Retain or clarify

- generated sensitive value interfaces needed by source generation and explicit reveal;
- serializer adapters that intentionally call `Reveal(CompliancePurpose)`;
- one immutable HMAC provider/configuration;
- pattern scanner and its bounded-cost safeguards;
- scoped `ComplianceLayout` for Ark-owned text layouts;
- analyzer diagnostics and explicit reviewed exemptions;
- native NLog stack-trace rendering.

The current work is not released, so `IRuntimeClassifiedValue` can be removed rather than preserved as a compatibility shim. No supported adapter should depend on runtime classification after this change.

## 9. Analyzer and generator implications

The analyzer should enforce the simpler invariant:

> A value that represents regulated data must be a generated sensitive value, or its use must have an explicit reviewed exemption.

That implies reviewing the following PRD rules:

- rules that require a classification attribute when the generated type itself is sufficient;
- rules that permit a sensitive value to be converted to `string` without an explicit reveal;
- rules that treat exception messages as inherently unsafe rather than as analyzer-protected text with a runtime scan fallback;
- rules that assume runtime reflection can make arbitrary DTOs compliant.

The generator should make the safe behavior unambiguous:

- every generated type is sensitive even when no classification metadata is supplied;
- `ToString`, `IFormattable`, `ISpanFormattable`, and debugger display never reveal cleartext;
- the selected redaction mode is deterministic and documented;
- HMAC mode uses the single process-level key;
- cleartext remains available only through `Reveal(CompliancePurpose)` and approved serializer adapters.

## 10. Required acceptance tests for the revised design

Before implementation is accepted, tests should demonstrate:

1. A generated sensitive value is safe through NLog message-template formatting without a runtime object redactor.
2. A generated sensitive value is safe as a nested JSON property using NLog's native serializer.
3. HMAC output is identical through `ToString`, message templates, JSON serialization, and any supported integration.
4. Missing HMAC configuration fails closed when HMAC mode is selected.
5. Pattern scanning still masks untyped legacy strings when enabled.
6. Pattern scanning remains scoped to configured Ark-owned layouts.
7. An ordinary exception message is retained when it contains no matching pattern.
8. A matching exception message is scanned rather than replaced wholesale by the marker.
9. Exception type, inner exception structure, and native stack trace remain available according to their selected NLog format.
10. Explicit `Reveal(CompliancePurpose)` remains the only supported cleartext transport path.
11. OTel tags containing generated sensitive values do not require recursive object-graph reconstruction.
12. Unsupported custom renderers and targets are documented rather than implied to be universally protected.

## 11. Migration and documentation impact

If the revised design is accepted:

- update the PRD sections on declaration, generated values, runtime redaction, exceptions, rejected approaches, and package responsibilities;
- update the runtime-redaction task acceptance to remove claims about recursive object-graph protection;
- document the single HMAC-key lifecycle and startup validation;
- document that generated value types are intrinsically sensitive;
- document which classification attributes are metadata-only;
- update NLog examples to show separate message and stack-trace layouts;
- update OTel documentation to define its supported boundary;
- add a migration note for consumers relying on blanket exception markers or runtime graph rewriting; the current work is unreleased, so no compatibility shim is required.

## 12. Decisions requested from the PR

The review accepts these decisions:

1. **Intrinsic sensitivity:** Every `[SensitiveValueObject<T>]` generated type is sensitive by definition.
2. **Classification metadata:** Personal/secret classification remains on the generated type for inventory and policy tooling, but is never required at usage sites or used for runtime sink safety.
3. **HMAC lifecycle:** The generated type's redaction setting selects the only runtime secret, the HMAC key.
4. **Runtime redactor:** Remove recursive object-graph redaction and use a dedicated `PiiScanner` for PII scanning.
5. **NLog:** Use native formatting and JSON serialization plus scoped Ark layout scanning. Transport converters that call `Reveal` must not be used by diagnostic JSON layouts.
6. **Exceptions:** Preserve exception messages and PII-scan them when enabled instead of always emitting `***ARKPII***`.
7. **Stack traces:** Keep native stack-trace rendering separate from exception-message scanning.
8. **OTel:** Narrow the processor to generated safe formatting and PII scanning of untyped strings; do not rewrite arbitrary tag graphs.
9. **Compatibility:** Remove `IRuntimeClassifiedValue`; this design is not released and does not need a compatibility shim.

## 13. Conclusion

The privacy goal is better served by a narrow, explicit model:

- generated sensitive types make accidental formatting safe;
- analyzers prevent cleartext and unauthorized reveal paths;
- one HMAC key supports the only runtime pseudonymization requirement;
- pattern scanning catches untyped text at known Ark-owned boundaries;
- exception layouts preserve useful diagnostics and scan messages instead of erasing them;
- serializers and NLog retain ownership of their own object graphs.

This is smaller, easier to audit, more compatible with NativeAOT, and less likely to diverge from the actual serializer or logging contract than a recursive runtime redactor.
