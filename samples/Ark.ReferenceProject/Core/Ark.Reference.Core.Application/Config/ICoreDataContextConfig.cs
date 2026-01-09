using Ark.Tools.Outbox.SqlServer;
using Ark.Tools.Sql;

namespace Ark.Reference.Core.Application.Config;

public interface ICoreDataContextConfig : IOutboxContextSqlConfig, ISqlContextConfig
{
}