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
      `PathBase`, unless the application opts out.
- [x] Invalid or multiple values produce a client error before downstream
      middleware runs.
- [x] No configured prefix or known proxy/network list is required.
- [ ] Prefix routing and generated links work end to end.
- [ ] Full solution build and tests pass with zero warnings.
