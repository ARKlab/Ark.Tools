CREATE PROCEDURE [dbo].[Book_BulkInsert]
    @Books [dbo].[BookBulkInsertType] READONLY
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Book]
    (
        [Id],
        [Title],
        [Author],
        [Genre],
        [ISBN],
        [Description]
    )
    OUTPUT
        INSERTED.[Id],
        INSERTED.[Title],
        INSERTED.[Author],
        INSERTED.[Genre],
        INSERTED.[ISBN],
        INSERTED.[Description],
        INSERTED.[RowVersion] AS [ETag]
    SELECT
        [Id],
        [Title],
        [Author],
        [Genre],
        [ISBN],
        [Description]
    FROM @Books;
END
