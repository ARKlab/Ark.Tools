using Ark.Tools.Outbox.SqlServer;

namespace Core.Service.Application.Config
{
    public interface ICoreDataContextConfig : IOutboxContextSqlConfig
    {
        public string SQLConnectionString { get; }
    }
}
