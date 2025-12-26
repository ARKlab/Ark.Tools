using Ark.Reference.Core.Common.Dto;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ark.Reference.Core.Application.Handlers
{
    /// <summary>
    /// Simple in-memory store for Book entities (for demonstration purposes)
    /// </summary>
    internal static class InMemoryBookStore
    {
        private static readonly Dictionary<int, Book.V1.Output> _books = new();
        private static int _nextId = 1;
        private static readonly System.Threading.Lock _lock = new();

        public static Book.V1.Output Create(Book.V1.Create data)
        {
            lock (_lock)
            {
                var id = _nextId++;
                var book = new Book.V1.Output
                {
                    Id = id,
                    Title = data.Title,
                    Author = data.Author,
                    Genre = data.Genre,
                    ISBN = data.ISBN,
                    Description = $"Book created: {data.Title} by {data.Author}",
                    AuditId = Guid.NewGuid()
                };
                _books[id] = book;
                return book;
            }
        }

        public static Book.V1.Output? GetById(int id)
        {
            lock (_lock)
            {
                return _books.TryGetValue(id, out var book) ? book : null;
            }
        }

        public static (IEnumerable<Book.V1.Output> data, int count) GetByFilters(
            int[]? ids,
            string[]? titles,
            string[]? authors,
            Core.Common.Enum.BookGenre[]? genres,
            int skip,
            int limit)
        {
            lock (_lock)
            {
                IEnumerable<Book.V1.Output> query = _books.Values;

                if (ids?.Length > 0)
                    query = query.Where(b => ids.Contains(b.Id));

                if (titles?.Length > 0)
                    query = query.Where(b => b.Title != null && titles.Any(t => b.Title.Contains(t, StringComparison.OrdinalIgnoreCase)));

                if (authors?.Length > 0)
                    query = query.Where(b => b.Author != null && authors.Any(a => b.Author.Contains(a, StringComparison.OrdinalIgnoreCase)));

                if (genres?.Length > 0)
                    query = query.Where(b => b.Genre.HasValue && genres.Contains(b.Genre.Value));

                var totalCount = query.Count();
                var data = query.Skip(skip).Take(limit).ToList();

                return (data, totalCount);
            }
        }

        public static Book.V1.Output? Update(int id, Book.V1.Update data)
        {
            lock (_lock)
            {
                if (!_books.TryGetValue(id, out var existing))
                    return null;

                var updated = existing with
                {
                    Title = data.Title,
                    Author = data.Author,
                    Genre = data.Genre,
                    ISBN = data.ISBN,
                    Description = $"Book updated: {data.Title} by {data.Author}",
                    AuditId = Guid.NewGuid()
                };

                _books[id] = updated;
                return updated;
            }
        }

        public static bool Delete(int id)
        {
            lock (_lock)
            {
                return _books.Remove(id);
            }
        }
    }
}
