using Ark.Tools.Sql;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    public class ExternalContext : AbstractSqlContextAsync<DataSql>, IExternalContext
    {
        public int ReadData()
        {
            return 2;
            //throw new NotImplementedException();
        }
    }


    public class DataSql { }
}
