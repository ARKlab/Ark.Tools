namespace Ark.Tools.Outbox.SqlServer;

public interface IOutboxContextSqlConfig
{
    string TableName { get; }
    string SchemaName { get; }
}