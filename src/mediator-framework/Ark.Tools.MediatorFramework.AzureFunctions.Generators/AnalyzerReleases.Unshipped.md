; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ARKMF030 | Ark.Tools.MediatorFramework | Error | MessagePack is not supported by Azure Functions
ARKMF031 | Ark.Tools.MediatorFramework | Error | Duplicate Azure Functions route
ARKMF032 | Ark.Tools.MediatorFramework | Error | Duplicate Azure Functions name
ARKMF033 | Ark.Tools.MediatorFramework | Error | Multiple Functions messaging hosts
ARKMF034 | Ark.Tools.MediatorFramework | Error | Invalid Functions messaging participant
ARKMF035 | Ark.Tools.MediatorFramework | Error | Functions messaging participant has no network
ARKMF036 | Ark.Tools.MediatorFramework | Error | Functions messaging participant has multiple networks
ARKMF037 | Ark.Tools.MediatorFramework | Info | Functions messaging participant is sender-only
ARKMF038 | Ark.Tools.MediatorFramework | Error | Functions messaging trigger binding is not implemented
ARKMF039 | Ark.Tools.MediatorFramework | Error | Functions messaging subscription has no publisher
ARKMF040 | Ark.Tools.MediatorFramework | Info | Storage Queue host settings are not inspectable
ARKMF041 | Ark.Tools.MediatorFramework | Warning | Invalid Storage Queue message encoding
ARKMF042 | Ark.Tools.MediatorFramework | Warning | Invalid Storage Queue maximum dequeue count
ARKMF043 | Ark.Tools.MediatorFramework | Warning | Invalid Storage Queue visibility timeout
ARKMF044 | Ark.Tools.MediatorFramework | Error | Storage Queue consumer has no retry policy
