using Ark.Tools.AspNetCore.NestedStartup;

namespace ProblemDetailsSample;

public sealed class PreviewArea : IArea { }
public sealed class PrivateArea : IArea { }
public sealed class PublicArea : IArea { }

public static class ProblemDetailsSampleConstants
{
    public static readonly string[] PublicVersions = [
         "1.0"
    ];

    public static readonly string[] PrivateVersions = [
         "1.0"
    ];

    public static readonly string[] PreviewVersions = [
         "1.0"
    ];

    public static readonly string EnforceEmptyPayload = string.Empty;
}