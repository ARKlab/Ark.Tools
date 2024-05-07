using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    public class RECDataContext_Sql : AbstractSqlContextWithOutboxAsync<string>, IRECDataContext
    {

        private readonly IRECDataContextConfig _config;
        private readonly IsolationLevel _isolation;


        public RECDataContext_Sql(IRECDataContextConfig config, DbConnection connection, DbTransaction transaction, IsolationLevel isolation)
            : base(connection, config)
        {
            _config = config;
            _isolation = isolation;
            Transaction = transaction;
        }

        public async void Dispose()
        {
            if (Transaction != null)
            {
                await Transaction.DisposeAsync();
            }
        }
    }
}
