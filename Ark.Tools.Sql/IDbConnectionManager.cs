using System.Data;

namespace Ark.Tools.Sql
{
    public interface IDbConnectionManager
    {
        IDbConnection Get(string connectionString);
    }
}
