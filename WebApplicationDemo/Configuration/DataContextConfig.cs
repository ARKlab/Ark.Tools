using Ark.Tools.Outbox.SqlServer;

namespace WebApplicationDemo.Configuration
{
    public class DataContextConfig : IDataContextConfig
    {
        public string SqlConnectionString => @"Data Source=(localdb)\MSSQLLocalDB;Integrated Security=True;Persist Security Info=False;Pooling=True;MultipleActiveResultSets=True;Connect Timeout=60;Encrypt=False;TrustServerCertificate=True;";

        public string TableName => "People";

        public string SchemaName => "";
    }
}
