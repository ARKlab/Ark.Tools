# HST-03 — Strict forwarded-prefix handling

**Category**: aspnetcore · **Scope**: FRAMEWORK + SAMPLE
**Depends on**: HSD-01, HSD-04

## Problem

Prefixed deployments need `PathBase`. The existing Ark startup reads an
unvalidated forwarding header, while the mediator sample has no equivalent.

## Steps

1. Handle `X-Forwarded-Prefix` by default, with an explicit application opt-out
   and no configured-prefix or known-proxy/network requirement.
2. Require exactly one header value. Strictly sanitize and reject values that are
   not absolute path-only prefixes, including root, trailing slash, query,
   fragment, scheme, authority, backslash, control character, whitespace,
   empty/dot segments and encoded slash, backslash or dot segments.
3. Prepend a valid prefix to `PathBase`. For an invalid or ambiguous header,
   reject the request before the remaining pipeline executes.
4. Test absent, valid, invalid and multiple header values; existing `PathBase`
   composition; routing; generated OpenAPI server paths; documentation links;
   redirects; opt-out; and proof that downstream middleware is not invoked for
   rejected requests.

## Outcomes

- Prefixed deployment preserves Ark forwarded-prefix behavior with strict request
  validation.

## Acceptance

- [x] The default profile accepts a valid `X-Forwarded-Prefix` and prepends it to
      `PathBase`, unless the application opts out (`ArkMinimalApiHostExtensions.cs:40-42,140-158`; no automated test).
- [x] Invalid or multiple values produce a client error before downstream
      middleware runs (`ArkMinimalApiHostExtensions.cs:146-150` returns 400 before `next()`; no automated test).
- [x] No configured prefix or known proxy/network list is required (implementation reads only the header, no config/allow-list).
- [ ] Prefix routing and generated OpenAPI paths work end to end. (no test anywhere references `X-Forwarded-Prefix`; end-to-end behavior unproven)
- [ ] Full solution build and tests pass with zero warnings.
