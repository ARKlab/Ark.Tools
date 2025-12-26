CREATE PROCEDURE [ops].[ResetFull_OnlyForTesting]
	  @areYouReallySure bit = 0
	, @resetConfig bit = 0
	, @initConfig bit = 0
	, @resetProfileCalendar bit = 0
AS
	SET XACT_ABORT ON
	SET NOCOUNT ON

	IF @areYouReallySure = 1
	BEGIN

		BEGIN TRANSACTION
		
	    -- Ping
		ALTER TABLE [dbo].[Ping] SET (SYSTEM_VERSIONING = OFF)
		TRUNCATE TABLE [dbo].[Ping]
		TRUNCATE TABLE [dbo].[PingHistory]
		ALTER TABLE [dbo].[Ping] SET  ( SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[PingHistory], DATA_CONSISTENCY_CHECK = OFF ) )

		-- Book
		ALTER TABLE [dbo].[Book] SET (SYSTEM_VERSIONING = OFF)
		TRUNCATE TABLE [dbo].[Book]
		TRUNCATE TABLE [dbo].[BookHistory]
		ALTER TABLE [dbo].[Book] SET  ( SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[BookHistory], DATA_CONSISTENCY_CHECK = OFF ) )

		EXEC [ops].[InitConfig] @initConfig

		COMMIT TRANSACTION


	END
RETURN 0
