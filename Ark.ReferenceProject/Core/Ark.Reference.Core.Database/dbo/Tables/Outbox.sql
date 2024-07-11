CREATE TABLE [dbo].[Outbox] 
(
    [Id] [bigint] IDENTITY(1,1) NOT NULL,
	[Headers] [nvarchar](MAX) NOT NULL,
	[Body] [varbinary](MAX) NOT NULL,

    CONSTRAINT [PK_Outbox] PRIMARY KEY CLUSTERED 
    (
	    [Id] ASC
    )
)
