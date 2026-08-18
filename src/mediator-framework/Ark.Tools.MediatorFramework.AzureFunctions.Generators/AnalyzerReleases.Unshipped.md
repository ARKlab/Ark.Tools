; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ARKMF030 | Ark.MediatorFramework | Error | MessagePack is not supported by Azure Functions
ARKMF031 | Ark.MediatorFramework | Error | Duplicate Azure Functions route
ARKMF032 | Ark.MediatorFramework | Error | Duplicate Azure Functions name
ARKMF033 | Ark.MediatorFramework | Error | Messaging contract owner is missing
ARKMF034 | Ark.MediatorFramework | Error | Contract is both a message and an event
ARKMF035 | Ark.MediatorFramework | Error | Messaging contract is not registered
ARKMF036 | Ark.MediatorFramework | Error | Duplicate messaging registration
ARKMF037 | Ark.MediatorFramework | Error | Participant network is missing
ARKMF038 | Ark.MediatorFramework | Error | Duplicate participant declaration
ARKMF039 | Ark.MediatorFramework | Error | Invalid participant declaration
ARKMF040 | Ark.MediatorFramework | Error | Messaging logical name is not normalized
ARKMF041 | Ark.MediatorFramework | Error | Messaging queue name is invalid
ARKMF042 | Ark.MediatorFramework | Error | Messaging name is reserved
ARKMF043 | Ark.MediatorFramework | Error | Messaging capability is missing
ARKMF044 | Ark.MediatorFramework | Error | Event contract shape is invalid
ARKMF045 | Ark.MediatorFramework | Error | Messaging name is duplicated
ARKMF046 | Ark.MediatorFramework | Error | Messaging alias conflicts with a current name
ARKMF047 | Ark.MediatorFramework | Error | Derived event topic is too long
ARKMF048 | Ark.MediatorFramework | Error | Producer declares subscriptions
ARKMF049 | Ark.MediatorFramework | Error | Contract serializer conflicts with network
