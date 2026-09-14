# Configuration reference

Everything a consumer can set, in one place.

## MSBuild properties

| Property | Default | Meaning |
| --- | --- | --- |
| `EnableArkToolsCompliance` | `false` (beta) | Master opt-in. With `Ark.Tools.Sdk`: adds the analyzer package, the globalconfig severities, the lexicon/sinks files, and the surface baseline gate. Also gates the SQL policy targets and the surface generator in any project referencing the respective packages. |
| `ArkComplianceMode` | derived | Internal build state used to validate the compliance opt-in invariant. **Do not set.** It is derived from `EnableArkToolsCompliance` (`Enforce`/`Off`); the build errors on any manual value. |
| `ArkComplianceSurfaceEnabled` | `true` iff compliance enabled | Per-project opt-out of compliance-surface generation/verification. |
| `ArkComplianceSqlOutputPath` | `$(IntermediateOutputPath)ArkCompliance.Sql` | Where `Ark.Tools.Compliance.Sql` writes the `policy-*.compliance.sql` scripts. Database projects usually point it at a stable path under their own `obj\`. |

## MSBuild items

| Item | Metadata | Meaning |
| --- | --- | --- |
| `ArkComplianceSqlToken` | `Value` | Build-time substitution of a `$(Token)` SQLCMD variable in generated SQL policy scripts (e.g. `ComplianceSchema`, `ComplianceLabel`). Values are validated and escaped for their SQL context. Tokens you do not substitute remain SQLCMD variables for deployment time. |
| `AdditionalFiles` (`ComplianceLexicon*.txt`) | — | Extra PII-name lexicon terms for `ARKPII001`. Merged after the defaults; `-Term` removes, `Term*` prefix-matches. |
| `AdditionalFiles` (`ComplianceSinks*.txt`) | — | Extra guarded sinks, `M:<doc-id-prefix>*;<ARKPII id>` per line; `-M:` removes a default sink. |
| `AdditionalFiles` (`ArkComplianceSurface.txt`) | — | The committed surface baseline. Added automatically when the file exists in the project directory. |

## MSBuild targets (Ark.Tools.Compliance.Sql)

| Target | When | Purpose |
| --- | --- | --- |
| `ArkGenerateComplianceSql` | `AfterTargets="CoreCompile"`, only when `EnableArkToolsCompliance=true` | Expands generator output into `policy-*.compliance.sql` files under `$(ArkComplianceSqlOutputPath)`, applying `@(ArkComplianceSqlToken)`. Outputs the `@(ArkComplianceSqlScript)` item. Stale files are deleted. |
| `ArkGenerateComplianceSqlStandalone` | invoked explicitly | Entry point for database projects: depends on `Compile`, so it works on a clean build. Invoke via `<MSBuild Projects="…" Targets="ArkGenerateComplianceSqlStandalone" …/>`. |

## Environment variables

| Variable | Used by | Meaning |
| --- | --- | --- |
| `ARK_TOOLS_COMPLIANCE_HMAC_KEY` | `ArkRedaction.Hmac` value objects, `ArkHmacRedactor` | HMAC-SHA256 key for stable redacted correlation digests. Without it, HMAC rendering fails closed to `***`. |

## Runtime options

| API | Meaning |
| --- | --- |
| `ComplianceRedactionOptions.PiiScan` | `PiiScanMode.Off` (default) or `MessageAndProperties` — the NLog last-resort text scan. |
| `ComplianceRedactionOptions.HmacKey` | Programmatic HMAC key for `ComplianceRedactor`. |
| `NLogConfigurer.WithComplianceRedaction(o => …)` | Override redaction defaults in the Ark NLog chain. |
| `NLogConfigurer.WithoutComplianceRedaction()` | Explicit opt-out of the NLog redaction wrapper. |
| `IServiceCollection.AddArkRedaction()` | Register Ark redactors with `Microsoft.Extensions.Compliance.Redaction` and enable log redaction. |

## .editorconfig

Per-rule severity overrides use the standard mechanism and are visible in review:

```ini
dotnet_diagnostic.ARKPII001.severity = none
dotnet_diagnostic.ARKPII006.severity = error
```

Prefer `[ComplianceReviewed]` or a pragma at the call site over project-wide
downgrades — see [Analyzers — escape hatches](analyzers.md#escape-hatches).
