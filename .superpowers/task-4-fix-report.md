# Task 4 Fix Report

## Changes

- Added a regression test proving `ARKPII020` preserves a `#line`-mapped path and line.
- Changed compliance surface location serialization to use Roslyn's mapped line span.
- Retained the syntax-tree path fallback when the mapped path is empty.

## Verification

- Red: focused test failed because the diagnostic path was `null`.
- Green: focused test passed (1/1).
- Compliance test project passed (54/54).
- Full solution build succeeded with 0 warnings and 0 errors.
- Automated code review was unavailable because its configured model was not present; CodeQL timed out.

## Commit

- `fix(Compliance): preserve mapped diagnostics`
