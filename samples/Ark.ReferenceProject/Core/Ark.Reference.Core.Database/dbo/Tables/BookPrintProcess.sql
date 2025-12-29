CREATE TABLE [dbo].[BookPrintProcess]
(
[BookPrintProcessId] INT IDENTITY(1,1) NOT NULL,
[BookId] INT NOT NULL,
[Progress] FLOAT NOT NULL DEFAULT 0,
[Status] VARCHAR(50) NOT NULL,
[ErrorMessage] NVARCHAR(MAX) NULL,
[ShouldFail] BIT NOT NULL DEFAULT 0,

[AuditId] UNIQUEIDENTIFIER NOT NULL,

    [SysStartTime] DATETIME2 (7) GENERATED ALWAYS AS ROW START NOT NULL,
    [SysEndTime] DATETIME2 (7) GENERATED ALWAYS AS ROW END NOT NULL,  
PERIOD FOR SYSTEM_TIME ([SysStartTime], [SysEndTime]),

    CONSTRAINT [BookPrintProcess_PK] PRIMARY KEY CLUSTERED ([BookPrintProcessId] ASC),
    CONSTRAINT [BookPrintProcess_Book_FK] FOREIGN KEY ([BookId]) REFERENCES [dbo].[Book]([Id])
)
WITH    
   (   
      SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[BookPrintProcessHistory]),
  DATA_COMPRESSION = PAGE
   )
