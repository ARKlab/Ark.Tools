# Generator diagnostics and troubleshooting

Mediator generators are opt-in. Add the transport package and call its generated
mapping or registration method; unrelated referenced assemblies are not scanned.
Minimal API hosts should use a partial context with
`[ArkEndpointAssembly(typeof(ContractMarker))]` and
`MapArkEndpoints<TContext>()`. The older `FromAssembly` methods remain available
for compatibility.

## Diagnostics

| IDs | Meaning | Typical resolution |
| --- | --- | --- |
| `ARKMF001`, `ARKMF005` | Attachment declarations have multiple or unsupported shapes | Use one supported `IArkAttachment` or supported collection shape |
| `ARKMF002`, `ARKMF003` | Server-owned or suspicious input properties need review | Mark server-owned properties correctly and remove unintended client binding |
| `ARKMF004` | Rebus owner queue is invalid | Set a non-blank owner queue |
| `ARKMF010`–`ARKMF013` | HTTP verb, handler, route, or contract shape is invalid | Correct the HTTP metadata and handler interface |
| `ARKMF014`, `ARKMF015`, `ARKMF019` | Rebus registration, queue, or streaming response is invalid | Register each message once, use one owner queue, and return a supported response |
| `ARKMF016` | HTTP operation names collide within a version | Change the route, contract name, or explicit operation metadata |
| `ARKMF017`, `ARKMF018` | ETag property type or multiplicity is invalid | Use one nullable/string ETag property |
| `ARKMF020` | Version prefix has no `{version}` token | Include the token in `versionPrefix` |
| `ARKMF030`–`ARKMF032` | Azure Functions MessagePack, route, or function name is invalid | Use supported bindings and unique route/name values |
| `ARKAPI001`–`ARKAPI004` | API surface baseline is missing, changed, duplicated, or malformed | Enable the feature only with one deterministic `ArkApiSurface.txt` baseline and review intentional changes |

## Fallback and compiler requirements

`ToDataTableArk` uses its reflection fallback when interception is disabled,
the call is not eligible, or the compiler does not support C# 14 interceptors.
The interceptor generator uses Roslyn language-version APIs and emits
deterministic source. The generated namespace must be listed in
`InterceptorsNamespaces`.

Generated source inspection, `dotnet build`, and `dotnet test` are acceptance
checks for generator changes. The benchmark project compares intercepted and
fallback paths; run it when changing conversion or interception behavior.
