; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ARKMSG001 | Ark.Tools.MediatorFramework | Error | Duplicate messaging network member
ARKMSG002 | Ark.Tools.MediatorFramework | Error | Messaging network member is not a participant
ARKMSG003 | Ark.Tools.MediatorFramework | Error | Contract has multiple messaging kinds
ARKMSG004 | Ark.Tools.MediatorFramework | Error | Participant belongs to multiple networks
ARKMSG005 | Ark.Tools.MediatorFramework | Error | Message has multiple processors
ARKMSG006 | Ark.Tools.MediatorFramework | Error | Event has multiple publishers
ARKMSG007 | Ark.Tools.MediatorFramework | Info | Messaging contract is unwired
ARKMSG008 | Ark.Tools.MediatorFramework | Error | Messaging subscription cannot be satisfied
ARKMSG009 | Ark.Tools.MediatorFramework | Error | Subscriber cannot deserialize publisher protocol
ARKMSG010 | Ark.Tools.MediatorFramework | Error | Default serializer is not supported
ARKMSG011 | Ark.Tools.MediatorFramework | Error | Messaging capability is not declared
ARKMSG012 | Ark.Tools.MediatorFramework | Error | Contract belongs to multiple networks
ARKMSG013 | Ark.Tools.MediatorFramework | Error | Invalid participant identity
ARKMSG014 | Ark.Tools.MediatorFramework | Error | Duplicate participant identity
ARKMSG015 | Ark.Tools.MediatorFramework | Error | Reserved participant identity
ARKMSG017 | Ark.Tools.MediatorFramework | Error | Invalid messaging retry policy
ARKMSG018 | Ark.Tools.MediatorFramework | Error | Invalid event contract
ARKMSG019 | Ark.Tools.MediatorFramework | Error | Non-normalized contract name
ARKMSG020 | Ark.Tools.MediatorFramework | Error | Duplicate messaging contract name
ARKMSG021 | Ark.Tools.MediatorFramework | Error | Duplicate messaging contract alias
ARKMSG022 | Ark.Tools.MediatorFramework | Error | Messaging contract alias collision
ARKMSG023 | Ark.Tools.MediatorFramework | Error | Messaging declaring type must be a non-nested, non-generic partial class
ARKMSG024 | Ark.Tools.MediatorFramework | Error | Messaging network must not be static
ARKMSG025 | Ark.Tools.MediatorFramework | Error | MessagePack contract shape is missing
ARKMSG026 | Ark.Tools.MediatorFramework | Error | Google.Protobuf contract shape is missing
ARKMF021 | Ark.Tools.MediatorFramework | Error | Contract has multiple Solid kinds
