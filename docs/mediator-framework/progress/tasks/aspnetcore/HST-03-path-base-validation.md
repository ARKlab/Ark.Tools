# HST-03 — Strict path-base configuration

**Category**: aspnetcore · **Scope**: FRAMEWORK + SAMPLE
**Depends on**: HSD-01, HSD-04

## Problem

Prefixed deployments need `PathBase`, but request-controlled forwarding headers
must not select it.

## Steps

1. Include one configured application path prefix in the default Ark startup
   profile, with an explicit application opt-out.
2. Normalize and reject values that are not strict absolute path-only prefixes,
   including trailing slash, query, fragment, authority, backslash, control
   character, empty/dot segments and encoded slash, backslash or dot segments.
3. Fail startup for invalid configuration and set `PathBase` from the validated
   value only.
4. Test routing, generated OpenAPI server paths, documentation links and redirects
   under a prefix, plus each rejected input class.

## Outcomes

- Prefixed deployment works without trusting request metadata or proxy identity.

## Acceptance

- [ ] Invalid prefixes fail startup with an actionable message.
- [ ] The default profile accepts every valid configured prefix, unless the
      application opts out.
- [ ] Request headers cannot alter the configured base path.
- [ ] Prefix routing and generated links work end to end.
- [ ] Full solution build and tests pass with zero warnings.
