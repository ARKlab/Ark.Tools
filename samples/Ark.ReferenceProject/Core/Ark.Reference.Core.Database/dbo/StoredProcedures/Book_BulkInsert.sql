CREATE PROCEDURE [dbo].[Book_BulkInsert]
    @Books [dbo].[BookBulkInsertType] READONLY
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Book]
    (
          [Title]
        , [Author]
        , [Genre]
        , [ISBN]
        , [Description]
        , [AuditId]
    )
    OUTPUT
          INSERTED.[Id]
        , INSERTED.[Title]
        , INSERTED.[Author]
        , INSERTED.[Genre]
        , INSERTED.[ISBN]
        , INSERTED.[Description]
        , INSERTED.[AuditId]
    SELECT
          [Title]
        , [Author]
        , [Genre]
        , [ISBN]
        , [Description]
        , [AuditId]
    FROM @Books;
END
