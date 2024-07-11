using Ark.Tools.Outbox.SqlServer;

namespace Ark.Reference.Core.Application.Config
{
    public interface ICoreDataContextConfig : IOutboxContextSqlConfig
    {
        public string SQLConnectionString { get; }
    }
}
