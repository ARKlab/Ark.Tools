/*
 Pre-Deployment Script Template							
--------------------------------------------------------------------------------------
 This file contains SQL statements that will be executed before the build script.	
 Use SQLCMD syntax to include a file in the pre-deployment script.			
 Example:      :r .\myfile.sql								
 Use SQLCMD syntax to reference a variable in the pre-deployment script.		
 Example:      :setvar TableName MyTable							
               SELECT * FROM [$(TableName)]					
--------------------------------------------------------------------------------------
*/

SET XACT_ABORT ON

-- :setvar MyPath .

BEGIN TRANSACTION

GO
CREATE PROCEDURE RemoveSchemaBinding
    @ObjectName VARCHAR(MAX),
	@ObjectType VARCHAR(MAX)
AS
BEGIN
    DECLARE @PositionShemaBinding INT
    DECLARE @Command NVARCHAR(MAX)

    SELECT @Command = OBJECT_DEFINITION(OBJECT_ID(@ObjectName,@ObjectType));

	IF @Command IS NOT NULL 
	BEGIN

		SET @PositionShemaBinding = CHARINDEX('WITH SCHEMABINDING', @Command)

		IF NOT @PositionShemaBinding = 0 
		BEGIN
			-- WITH SCHEMA BINDING IS PRESENT... Let's remove it !
			SET @Command = STUFF(@Command, CHARINDEX('WITH SCHEMABINDING', @Command), LEN('WITH SCHEMABINDING'), '');
			SET @Command = STUFF(@Command, CHARINDEX('CREATE', @Command), LEN('CREATE'), 'ALTER');

			EXECUTE sp_executesql @Command
		END
	END
END

GO




DROP PROCEDURE RemoveSchemaBinding


-- ROLLBACK

COMMIT