CREATE TYPE [dbo].[BookBulkInsertType] AS TABLE
(
    [Title] NVARCHAR(200) NOT NULL,
    [Author] NVARCHAR(100) NOT NULL,
    [Genre] VARCHAR(50) NULL,
    [ISBN] VARCHAR(20) NULL,
    [Description] NVARCHAR(MAX) NULL,
    [AuditId] UNIQUEIDENTIFIER NOT NULL
)
