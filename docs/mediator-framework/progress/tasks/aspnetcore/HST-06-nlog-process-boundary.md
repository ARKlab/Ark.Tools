# HST-06 — NLog process boundary

**Category**: aspnetcore · **Scope**: SAMPLE
**Depends on**: HSD-08, HSD-09

## Problem

The sample does not register Ark NLog hosting or guarantee that startup failures
are logged, flushed and returned as process failures.

## Steps

1. Use the smallest testable async-main shape and register `ConfigureNLog` before
   host build.
2. Wrap construction, configuration, build, startup and `RunAsync`.
3. Emit one structured fatal event with `CultureInfo.InvariantCulture`, provide a
   console fallback, and never interpolate NLog messages.
4. Flush/shut down NLog in `finally` and preserve a non-zero failure result.
5. Test failures before and after build with an in-memory target or isolated process.

## Outcomes

- The sample demonstrates the Ark logging standard and preserves startup failures.

## Acceptance

- [ ] Framework logging reaches NLog.
- [ ] Each startup failure produces one flushed fatal event.
- [ ] Startup failure cannot exit with code zero.
- [ ] Full solution build and tests pass with zero warnings.
