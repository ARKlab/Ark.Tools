; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ARKMSG001 | Ark.MediatorFramework | Error | Duplicate messaging network member
ARKMSG002 | Ark.MediatorFramework | Error | Messaging network member is not a participant
ARKMSG003 | Ark.MediatorFramework | Error | Contract has multiple messaging kinds
ARKMSG004 | Ark.MediatorFramework | Error | Participant belongs to multiple networks
ARKMSG005 | Ark.MediatorFramework | Error | Message has multiple processors
ARKMSG006 | Ark.MediatorFramework | Error | Event has multiple publishers
ARKMSG007 | Ark.MediatorFramework | Info | Messaging contract is unwired
ARKMSG008 | Ark.MediatorFramework | Error | Messaging subscription cannot be satisfied
ARKMSG009 | Ark.MediatorFramework | Error | Subscriber cannot deserialize publisher protocol
ARKMSG010 | Ark.MediatorFramework | Error | Default serializer is not supported
ARKMSG011 | Ark.MediatorFramework | Error | Messaging capability is not declared
ARKMSG012 | Ark.MediatorFramework | Error | Contract belongs to multiple networks
ARKMSG013 | Ark.MediatorFramework | Error | Invalid participant identity
ARKMSG014 | Ark.MediatorFramework | Error | Duplicate participant identity
ARKMSG015 | Ark.MediatorFramework | Error | Reserved participant identity
ARKMSG016 | Ark.MediatorFramework | Error | Event topic name is too long
ARKMSG017 | Ark.MediatorFramework | Error | Invalid messaging retry policy
ARKMSG018 | Ark.MediatorFramework | Error | Invalid event contract
ARKMSG019 | Ark.MediatorFramework | Error | Non-normalized contract name
ARKMSG020 | Ark.MediatorFramework | Error | Duplicate messaging contract name
ARKMSG021 | Ark.MediatorFramework | Error | Duplicate messaging contract alias
ARKMSG022 | Ark.MediatorFramework | Error | Messaging contract alias collision
