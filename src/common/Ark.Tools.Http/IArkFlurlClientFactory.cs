using Flurl.Http.Configuration;

namespace Ark.Tools.Http;

public interface IArkFlurlClientFactory
{
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    IFlurlClient Get(string baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoftJson = null);

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    IFlurlClient Get(Uri baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoftJson = null);
}