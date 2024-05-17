using Microsoft.Data.SqlClient;

namespace WebApplicationDemo
{
    public class InitializeDb : IInitializeDb 
    { 
        // Need to replace this with a DB initialization
        public InitializeDb() 
        {            
            var cs = @"Data Source=(localdb)\MSSQLLocalDB;Integrated Security=True;Persist Security Info=False;Pooling=True;MultipleActiveResultSets=True;Connect Timeout=60;Encrypt=False;TrustServerCertificate=True;";

            var queryTxt = @"IF OBJECT_ID('People', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[People]
                (
                    ID [int] NOT NULL,
                    FirstName [varchar](50) NOT NULL,
                    LastName [varchar](50) NULL
                    PRIMARY KEY (ID)
                ); 
            END
            ELSE
                BEGIN
                TRUNCATE TABLE [dbo].[People]
            END

            INSERT INTO [dbo].[People] (ID, FirstName, LastName) VALUES (9, 'John', 'Doe')
            ";

            using (var conn = new SqlConnection(cs))
            {
                conn.Open();
                using (var cmd = new SqlCommand(queryTxt, conn))
                {
                    cmd.ExecuteNonQuery();
                }
                conn.Close();
            }
        }
    }
}
