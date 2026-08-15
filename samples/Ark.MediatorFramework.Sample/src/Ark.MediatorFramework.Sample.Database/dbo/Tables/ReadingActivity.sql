CREATE TABLE [dbo].[ReadingActivity]
(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [BookId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] NVARCHAR(255) NOT NULL,
    [Kind] NVARCHAR(30) NOT NULL,
    [Progress] INT NOT NULL,
    [OccurredAt] DATETIME2(7) NOT NULL,
    CONSTRAINT [PK_ReadingActivity] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ReadingActivity_Book] FOREIGN KEY ([BookId]) REFERENCES [dbo].[Book]([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_ReadingActivity_Progress] CHECK ([Progress] BETWEEN 0 AND 100)
);
GO

CREATE INDEX [IX_ReadingActivity_BookId_UserId_OccurredAt]
    ON [dbo].[ReadingActivity] ([BookId], [UserId], [OccurredAt] DESC, [Id] DESC);
