CREATE TABLE [dbo].[BookReview]
(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [BookId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] NVARCHAR(255) NOT NULL,
    [Rating] INT NOT NULL,
    [Text] NVARCHAR(2000) NOT NULL,
    [CreatedAt] DATETIME2(7) NOT NULL,
    CONSTRAINT [PK_BookReview] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_BookReview_Book] FOREIGN KEY ([BookId]) REFERENCES [dbo].[Book]([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_BookReview_Rating] CHECK ([Rating] BETWEEN 1 AND 5)
);
GO

CREATE INDEX [IX_BookReview_BookId_CreatedAt]
    ON [dbo].[BookReview] ([BookId], [CreatedAt] DESC, [Id] DESC);
