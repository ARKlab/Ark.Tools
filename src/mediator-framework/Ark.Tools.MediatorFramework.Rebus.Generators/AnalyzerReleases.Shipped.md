; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 7.0.0

### New Rules

Rule ID | Category | Severity | Notes
---------|----------|----------|-------
ARKMF004 | Rebus | Error | Invalid owner queue
ARKMF011 | Ark.Tools.MediatorFramework | Error | Unsupported handler kind
ARKMF014 | Ark.Tools.MediatorFramework | Error | Duplicate Rebus registration
ARKMF015 | Ark.Tools.MediatorFramework | Error | Conflicting Rebus owner queues
ARKMF019 | Ark.Tools.MediatorFramework | Error | Unsupported streaming response
ARKMF057 | Ark.Tools.MediatorFramework | Error | Use a valid Rebus participant host binding
ARKMSG025 | Ark.Tools.MediatorFramework | Error | MessagePack contract shape is missing
ARKMSG026 | Ark.Tools.MediatorFramework | Error | Google.Protobuf contract shape is missing
