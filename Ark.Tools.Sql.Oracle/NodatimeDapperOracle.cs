using Ark.Tools.Nodatime.Dapper;

using Microsoft.Data.SqlClient;

using Oracle.ManagedDataAccess.Client;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.Sql.Oracle
{
    public static class NodatimeDapperOracle
    {
        public static void Setup()
        {
            NodaTimeDapper.Setup();
        }
    }
}
