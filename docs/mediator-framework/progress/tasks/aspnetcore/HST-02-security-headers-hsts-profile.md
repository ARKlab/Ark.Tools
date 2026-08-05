# HST-02 — Security headers and HSTS defaults

**Category**: aspnetcore · **Scope**: FRAMEWORK + SAMPLE
**Depends on**: HSD-01, HSD-03

## Problem

Minimal API consumers do not receive the security-header and HSTS defaults
provided by the existing Ark startup libraries.

## Steps

1. Add the accepted optional Ark startup helper, reusing the current WebApiCommon
   security-header and HSTS behavior.
2. Define named API, Scalar/Swagger and gRPC-reflection policies.
3. Cover success, errors, documentation, not-found responses and HSTS with
   TestServer assertions.
4. Keep direct host composition without the Ark helper supported.

## Outcomes

- Consumers can opt into one Ark-default security profile without adopting MVC.

## Acceptance

- [x] The `Server` header is removed and expected security headers are present.
- [x] HSTS is enabled consistently with the existing Ark startup.
- [x] TLS-free hosting is not presented as a supported deployment.
- [x] Full solution build and tests pass with zero warnings.
