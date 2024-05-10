using Ark.Tools.Sql;

using Dapper;

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    public interface IPublicOfferContextConfig
    {
        string PublicOfferConnectionString { get; set; }
    }

    public interface IPublicOfferContext : IAsyncDisposable { }
    public class PublicOfferSql {}
    public class PublicOfferContext_Sql : /*AbstractSqlContextAsync<PublicOfferSql>,*/ IPublicOfferContext
    {

        public IDbConnection Connection { get; set; }
        public IDbTransaction Transaction { get; set; }

        public PublicOfferContext_Sql(IPublicOfferContextConfig config, IDbConnectionManager connectionManager, IDbConnection connection, IDbTransaction transaction)
            //: base(connectionManager.Get(config.PublicOfferConnectionString))
        {
            Connection = connection;
            Transaction = transaction;
        }

        public async Task<(int records, int totalCount)> ReadUnitsPagedAsync()
        {
            var ctk = new CancellationToken();

            //await CreateAsync(IsolationLevel.ReadCommitted, ctk);


            var cmd = new CommandDefinition("", transaction: Transaction, commandTimeout: 120, cancellationToken: ctk);

            using var multi = await Connection.QueryMultipleAsync(cmd);
            var res = (await multi.ReadAsync<int>()).AsList();
            var t = await multi.ReadFirstAsync<int>();




            return (2, 1);
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            GC.SuppressFinalize(this);

            throw new NotImplementedException();
        }
    }
}
