# Analyzers

`Ark.Tools.Compliance.Analyzers` turns misuse of classified data into build
diagnostics. Referenced automatically by `Ark.Tools.Sdk` when
`EnableArkToolsCompliance=true`, or explicitly:

```xml
<PackageReference Include="Ark.Tools.Compliance.Analyzers" PrivateAssets="all" />
```

## Diagnostic reference

| ID | Severity | Fires when |
| --- | --- | --- |
| ARKPII001 | Warning | A member/parameter whose *name* suggests personal data (`Email`, `Ssn`, `TaxId`, …) is not classified. Fix: apply a classification attribute, use a sensitive value object, or apply `[NotPersonalData("<why>")]`. |
| ARKPII002 | Error | A classified value reaches a log call (NLog `Logger`, `Microsoft.Extensions.Logging`, `BeginScope`) — as template argument, interpolation, or destructured object. |
| ARKPII003 | Error | A classified value reaches an exception message or `Exception.Data`. Exception text is logged and returned to clients. |
| ARKPII004 | Error | A classified value reaches `Activity` tags, baggage, or events (telemetry). |
| ARKPII005 | Error | Implicit cleartext formatting of a classified value — `ToString`, interpolation, concatenation — outside a declared egress. |
| ARKPII006 | Warning | Test data literal looks like *real* personal data (real email domain, checksum-valid IBAN/tax code). Use `ComplianceFakes` or RFC 2606 domains. |
| ARKPII007 | Error | A classified member of a `[SqlDataPolicy]` type has no `[SqlColumnPolicy]` — "unmasked by omission" is the failure this rule prevents. |
| ARKPII008 | Warning | `[ComplianceReviewed]` lacks a reason or is past its `Expires` date. |
| ARKPII009 | Warning | `[NotPersonalData]` justification is missing or boilerplate. |
| ARKPII010 | Error | Classification on a member the pipeline cannot redact (`object`, `dynamic`, delegate). |
| ARKPII011 | Error | Classified value passed to a banned formatting sink (`Console.*`, `Debug.*`, `Trace.*`, `StringBuilder.Append`). |
| ARKPII012 | Warning | Contract exposes personal data over a transport without `[PersonalDataEgress(Purpose = …)]`. |
| ARKPII013 | Warning | Project logs through `Microsoft.Extensions.Telemetry` without calling `AddArkRedaction()`. |
| ARKPII020 | Error | `ArkComplianceSurface.txt` drift — the classified surface changed vs the committed baseline. |
| ARKPII021 | Error | A classified member was removed or its protection weakened vs the baseline. |
| ARKPII201–204, 207 | Error | Generator misuse: unsupported value type, invalid struct declaration, cleartext `ToString()`, invalid hook signature, invalid SQL policy mapping. |

Errors are deliberate and strict: suppression is one pragma away and visible in
review, a leak is invisible.

## Extending the lexicon (taxonomy of PII-looking names)

`ARKPII001`'s name heuristic reads term files from analyzer `AdditionalFiles`.
The defaults ship in the package as `ComplianceLexicon.Ark.txt`. Any additional
file whose name **starts with `ComplianceLexicon` and ends with `.txt`** is merged
in — later files win, so you can extend *and* override:

```xml
<ItemGroup>
  <AdditionalFiles Include="ComplianceLexicon.MyApp.txt" />
</ItemGroup>
```

File format — one case-insensitive identifier term per line:

```
# Comments start with '#'.
CustomerCode        # exact identifier match
Fiscal*             # trailing '*' = prefix match (FiscalCode, FiscalId, ...)
-Mail               # leading '-' removes a default term
```

## Extending the sinks

`ARKPII002/004/011` sink methods come from `ComplianceSinks.Ark.txt`. Same
composition rule: any `AdditionalFiles` entry named `ComplianceSinks*.txt` is
merged after the defaults.

```xml
<ItemGroup>
  <AdditionalFiles Include="ComplianceSinks.MyApp.txt" />
</ItemGroup>
```

File format — a documentation-comment method ID prefix and the diagnostic to
report, separated by `;`:

```
# Guard our own audit trail writer like a log sink.
M:MyApp.Audit.AuditWriter.Write*;ARKPII002

# Our metrics helper is a telemetry sink.
M:MyApp.Telemetry.Metrics.Tag*;ARKPII004

# Remove a default sink after review.
-M:System.Text.StringBuilder.Append*;ARKPII011
```

## The compliance surface baseline

With `EnableArkToolsCompliance=true`, every project generates an inventory of its
classified members and verifies it against a committed baseline file,
`ArkComplianceSurface.txt`, in the project directory. The file doubles as GDPR
Art. 30 processing-record evidence:

```
COMPLIANCE-SURFACE 1
CLASSIFIED	Ark.Reference.Core.Common.Dto.Book.V1.Create	Author	Ark:PersonalData	Gets or initializes the author of the Book	System.Text.Json
```

Workflow:

1. Add/change/remove a classified member → build fails with `ARKPII020`
   (surface changed) or `ARKPII021` (protection weakened).
2. Review, then accept by copying the current snapshot over the baseline:

   ```bash
   cp obj/Debug/net8.0/ArkComplianceSurface.current.txt ArkComplianceSurface.txt
   ```

3. Commit — the privacy change is now a visible line in the PR diff.

While doing a large refactor you can temporarily skip verification with
`<ArkComplianceSurfaceUpdating>true</ArkComplianceSurfaceUpdating>` (or
`-p:ArkComplianceSurfaceUpdating=true`); regenerate and re-commit the baseline
before merging. To exclude a single project from surface tracking set
`<ArkComplianceSurfaceEnabled>false</ArkComplianceSurfaceEnabled>`.

## Escape hatches

All explicit, all greppable, in order of preference:

```csharp
// 1. Reviewed exception at the call site, recorded in the inventory, expires loudly.
[ComplianceReviewed("ARKPII002", "Ticket ARK-1234: support runbook needs the masked local part.",
                    Expires = "2027-01-01")]
private void _logSupportContext(Customer c) { … }

// 2. Standard pragma, for one line.
#pragma warning disable ARKPII002 // support runbook, ARK-1234
_logger.Debug(CultureInfo.InvariantCulture, "ctx {Email}", masked);
#pragma warning restore ARKPII002
```

```ini
# 3. Project-wide severity override in .editorconfig (discouraged, review-visible).
#    'suggestion' is not useful — a diagnostic that doesn't fail the build is one
#    nobody reads. Turn a rule off or leave it on.
dotnet_diagnostic.ARKPII001.severity = none
```

And the master switch: `EnableArkToolsCompliance=false` (the default while the
analyzers are beta) turns the whole layer off for a project.
