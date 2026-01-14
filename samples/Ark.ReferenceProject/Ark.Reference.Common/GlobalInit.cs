using Ark.Tools.Nodatime;
using Ark.Tools.Sql.SqlServer;

namespace Ark.Reference.Common;

public static class GlobalInit
{
    public static void InitStatics()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        NodaTimeDapperSqlServer.Setup();
        NodeTimeConverter.Register();
    }
}