using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    public interface IRECDataContextConfig : ISQLConnectionString, IOutboxContextSqlConfig
    {
        //public string SQLConnectionString { get; }
    }
}
