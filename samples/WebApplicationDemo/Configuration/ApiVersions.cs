using Asp.Versioning;

namespace WebApplicationDemo.Configuration;

public static class ApiVersions
{
    public static readonly ApiVersion V0 = new(0, 0);
    public static readonly ApiVersion V1 = new(1, 0);
    public static readonly ApiVersion V2 = new(2, 0);
    public static readonly ApiVersion V3 = new(3, 0);

    public static readonly ApiVersion[] All =
    [
        V0,
        V1,
        V2,
        V3
    ];
}