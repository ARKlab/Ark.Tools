using Flurl.Http.Configuration;
using Flurl.Http;
using System;

namespace Ark.Tools.Http
{
    public interface IArkFlurlClientFactory
    {
        IFlurlClient Get(string baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoft = null);

        IFlurlClient Get(Uri baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoft = null);
    }
}
