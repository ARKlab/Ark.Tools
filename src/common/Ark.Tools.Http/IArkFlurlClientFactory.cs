using Flurl.Http;
using Flurl.Http.Configuration;

using System;

namespace Ark.Tools.Http
{
    public interface IArkFlurlClientFactory
    {
        IFlurlClient Get(string baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoftJson = null);

        IFlurlClient Get(Uri baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoftJson = null);
    }
}