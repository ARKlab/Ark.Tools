# HST-06 — NLog process boundary

**Category**: aspnetcore · **Scope**: MINIMAL API SAMPLE + AZURE FUNCTIONS SAMPLE
**Depends on**: HSD-08, HSD-09

## Problem

The Minimal API and Azure Functions samples do not register Ark NLog hosting or
guarantee that startup failures are logged, flushed and returned as process
failures.

## Steps

1. Use the smallest testable async-main shape and register Ark NLog before host
   build in both samples.
2. Wrap construction, configuration, build, startup and `RunAsync`.
3. Emit one structured fatal event with `CultureInfo.InvariantCulture`, provide a
   console fallback, and never interpolate NLog messages.
4. Flush/shut down NLog in `finally` and preserve a non-zero failure result.
5. Test failures before and after build with an in-memory target or isolated process.

## Outcomes

- The sample demonstrates the Ark logging standard and preserves startup failures.

## Acceptance

- [x] Framework logging reaches NLog.
- [x] Each startup failure produces one flushed fatal event.
- [x] Startup failure cannot exit with code zero.
- [x] Full solution build and tests pass with zero warnings.
