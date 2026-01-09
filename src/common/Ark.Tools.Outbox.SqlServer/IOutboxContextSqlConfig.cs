
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Outbox.SqlServer(net10.0)', Before:
namespace Ark.Tools.Outbox.SqlServer
{
    public interface IOutboxContextSqlConfig
    {
        string TableName { get; }
        string SchemaName { get; }
    }
=======
namespace Ark.Tools.Outbox.SqlServer;

public interface IOutboxContextSqlConfig
{
    string TableName { get; }
    string SchemaName { get; }
>>>>>>> After
    namespace Ark.Tools.Outbox.SqlServer;

    public interface IOutboxContextSqlConfig
    {
        string TableName { get; }
        string SchemaName { get; }
    }