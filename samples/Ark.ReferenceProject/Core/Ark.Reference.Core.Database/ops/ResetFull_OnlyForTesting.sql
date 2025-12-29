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
		
		-- Turn off system versioning for all tables first
		ALTER TABLE [dbo].[BookPrintProcess] SET (SYSTEM_VERSIONING = OFF)
		ALTER TABLE [dbo].[Ping] SET (SYSTEM_VERSIONING = OFF)
		ALTER TABLE [dbo].[Book] SET (SYSTEM_VERSIONING = OFF)

		-- Note: Using DELETE FROM (instead of TRUNCATE) for tables that are referenced by 
		-- FOREIGN KEY constraints or that reference other tables with FK constraints.
		-- Even when deleting from child tables first, SQL Server still blocks TRUNCATE 
		-- operations on parent tables if FK constraints exist. DELETE FROM works with FK constraints.
		-- However, history tables can be TRUNCATED as they have no FK constraints and are not
		-- directly referenced - they're only managed by system versioning.
		
		-- Delete from child tables before parent tables (BookPrintProcess references Book)
		DELETE FROM [dbo].[BookPrintProcess]
		TRUNCATE TABLE [dbo].[BookPrintProcessHistory]

		DELETE FROM [dbo].[Ping]
		TRUNCATE TABLE [dbo].[PingHistory]

		DELETE FROM [dbo].[Book]
		TRUNCATE TABLE [dbo].[BookHistory]

		-- Turn system versioning back on for all tables
		ALTER TABLE [dbo].[BookPrintProcess] SET  ( SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[BookPrintProcessHistory], DATA_CONSISTENCY_CHECK = OFF ) )
		ALTER TABLE [dbo].[Ping] SET  ( SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[PingHistory], DATA_CONSISTENCY_CHECK = OFF ) )
		ALTER TABLE [dbo].[Book] SET  ( SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[BookHistory], DATA_CONSISTENCY_CHECK = OFF ) )

		EXEC [ops].[InitConfig] @initConfig

		COMMIT TRANSACTION


	END
RETURN 0
