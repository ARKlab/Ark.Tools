﻿CREATE PROCEDURE [ops].[InitConfig]
	@reset bit = 0
AS
	SET XACT_ABORT ON
	SET NOCOUNT ON

	BEGIN TRANSACTION

	COMMIT TRANSACTION

RETURN 0