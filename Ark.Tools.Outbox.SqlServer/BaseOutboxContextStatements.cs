using System;

namespace Ark.Tools.Outbox.SqlServer
{
    internal class BaseOutboxContextStatements
    {
        internal BaseOutboxContextStatements(IOutboxContextSqlConfig config)
        {
            var schema = config.SchemaName ?? "dbo";
            var table = config.TableName;
            var full = $"[{schema}].[{table}]";
            Insert = $@"
                    INSERT INTO {full}
                    (
                          [Headers]
                        , [Body]
                    )
                    VALUES
                    (
                          @pHeaders
                        , @pBody
                    )
                    ";

            PeekLock = (int messageCount) => $@"
                    ;WITH batch AS (
                        SELECT TOP ({messageCount}) *
                        FROM {full}
                        ORDER BY Id DESC)
                    DELETE FROM batch
                    WITH (READPAST, ROWLOCK, READCOMMITTEDLOCK)
                    OUTPUT
                          DELETED.[Headers]
                        , DELETED.[Body]
                    ";

            Count = $@"
                    SELECT COUNT(*) as [Count] 
                    FROM {full}
                    ";

            Clear = $@"
                    DELETE FROM {full}
                    ";

            CreateTable = $@"
                    IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{schema}')
                    BEGIN
	                    EXEC('CREATE SCHEMA {schema}')
                    END

                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{schema}' AND TABLE_NAME = '{table}')
                        CREATE TABLE {full} (
                            [Id] [bigint] IDENTITY(1,1) NOT NULL,
	                        [Headers] [nvarchar](MAX) NOT NULL,
	                        [Body] [varbinary](MAX) NOT NULL,
                            CONSTRAINT [PK_{table}] PRIMARY KEY CLUSTERED 
                            (
	                            [Id] ASC
                            )
                        )
  
                    ";

        }

        public string Insert { get; }
        public Func<int, string> PeekLock { get; }
        public string Count { get; }
        public string Clear { get; }
        public string CreateTable { get; }

    }

}
