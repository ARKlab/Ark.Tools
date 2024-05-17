using Ark.Tools.Outbox;

using System;
using System.Threading.Tasks;
using System.Threading;

using WebApplicationDemo.Dto;
using Ark.Tools.Sql;

namespace WebApplicationDemo.Application
{
    public interface ISqlDataContext : IAsyncDisposable, IOutboxContextAsync, ISqlContextAsync //should it be ioutboxcontextasync??? SqlContextAsyncFactory<ISqlContextAsync>
    {
        Task<Person?> ReadFirstEntityAsync(CancellationToken ctk = default);
    }
}
