CREATE TABLE [dbo].[BookPrintProcess]
(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [BookId] UNIQUEIDENTIFIER NOT NULL,
    [Progress] FLOAT NOT NULL,
    [Status] NVARCHAR(30) NOT NULL,
    [IsActive] BIT NOT NULL,
    [ErrorMessage] NVARCHAR(400) NULL,
    [ShouldFail] BIT NOT NULL,
    CONSTRAINT [PK_BookPrintProcess] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_BookPrintProcess_Book] FOREIGN KEY ([BookId]) REFERENCES [dbo].[Book]([Id])
);

GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_BookPrintProcess_ActiveBook]
    ON [dbo].[BookPrintProcess] ([BookId])
    WHERE [IsActive] = 1;
