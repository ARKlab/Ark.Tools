; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ARKCORE001 | Usage | Error | Evolvable enum backing type mismatch
ARKCORE002 | Usage | Error | Evolvable enum requires NOT_SET
ARKCORE003 | Usage | Error | Evolvable enum names must be unique
ARKCORE004 | Usage | Warning | Evolvable enum uses every value available in its backing type
ARKCORE005 | Exception handling | Error | Preserve the caught exception as the inner exception
ARKCORE006 | Exception handling | Error | Capture the caught exception before throwing a replacement
