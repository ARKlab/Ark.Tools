# HST-05 — Default response compression

**Category**: aspnetcore · **Scope**: FRAMEWORK + SAMPLE
**Depends on**: HSD-06

## Problem

Minimal API hosting omits the accepted Ark Brotli/Gzip HTTPS compression default.

## Steps

1. Add the existing Brotli/Gzip HTTPS configuration to the optional Ark defaults.
2. Keep gRPC compression enabled when the gRPC stack supports it.
3. Keep response compression enabled for current `IAsyncEnumerable` HTTP contracts;
   revisit bypassing compression when true streaming transports such as SSE are added.
4. Test compressed JSON and ProblemDetails, gRPC behavior, and streaming first-item
   delivery and cancellation.
5. Document the accepted BREACH trade-off.

## Outcomes

- Non-streaming responses use the established Ark compression default without
  delaying streamed items.

## Acceptance

- [x] Brotli/Gzip over HTTPS is enabled by the Ark defaults.
- [x] gRPC compression remains available.
- [x] Streaming items are delivered without compressor buffering delays.
- [x] Full solution build and tests pass with zero warnings.
