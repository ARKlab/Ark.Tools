using Flurl.Http;
using Flurl.Http.Configuration;

using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Http(net10.0)', Before:
namespace Ark.Tools.Http
{
    public interface IArkFlurlClientFactory
    {
        IFlurlClient Get(string baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoftJson = null);

        IFlurlClient Get(Uri baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoftJson = null);
    }


=======
namespace Ark.Tools.Http;

public interface IArkFlurlClientFactory
{
    IFlurlClient Get(string baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoftJson = null);

    IFlurlClient Get(Uri baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoftJson = null);
>>>>>>> After
    namespace Ark.Tools.Http;

    public interface IArkFlurlClientFactory
    {
        IFlurlClient Get(string baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoftJson = null);

        IFlurlClient Get(Uri baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoftJson = null);
    }