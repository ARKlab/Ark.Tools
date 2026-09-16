# ARKPII014: Complete the test-data compliance scan

- **Severity:** Warning
- **Component:** Compliance
- **Diagnostic message:** `Test data could not be fully scanned for personal data because a pattern exceeded the analyzer time limit; shorten or replace the literal.`

## What it checks

This diagnostic reports test data that the fixture scanner could not fully inspect within its bounded regex time limit. The analyzer reports the incomplete scan instead of silently treating the input as safe.

## How to fix it

Shorten the literal or replace it with reserved test data. If the value is generated, keep the test fixture input bounded and deterministic.

## Suppression and configuration

Use the standard `dotnet_diagnostic.ARKPII014.severity` EditorConfig setting only for an intentional exception. Prefer changing the fixture over suppressing an incomplete scan.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII014.md`.
