CREATE PROCEDURE [ops].[ResetFull_OnlyForTesting]
    @areYouReallySure BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    IF @areYouReallySure = 1
    BEGIN
        DELETE FROM [dbo].[Outbox];
        DELETE FROM [dbo].[Greeting];
    END
END
