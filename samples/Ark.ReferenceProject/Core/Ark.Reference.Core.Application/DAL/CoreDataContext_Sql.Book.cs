using Ark.Reference.Common;
using Ark.Reference.Common.Services.Audit;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Sql.SqlServer;

using Dapper;

using NodaTime;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using static Dapper.SqlMapper;

namespace Ark.Reference.Core.Application.DAL
{
    public partial class CoreDataContext_Sql
    {
        private const string _schemaBook = "dbo";
        private const string _tableBook = "Book";

        public async Task<Book.V1.Output?> ReadBookByIdAsync(int id, CancellationToken ctk = default)
        {
            _logger.Trace(CultureInfo.InvariantCulture, "ReadBookByIdAsync called");

            var (data, _) = await ReadBookByFiltersAsync(new BookSearchQueryDto.V1()
            {
                Id = [id],
                Limit = 1
            }, ctk).ConfigureAwait(false);

            _logger.Trace(CultureInfo.InvariantCulture, "ReadBookByIdAsync ended");

            return data.SingleOrDefault();
        }

        public async Task<(IEnumerable<Book.V1.Output> data, int count)> ReadBookByFiltersAsync(BookSearchQueryDto.V1 query, CancellationToken ctk = default)
        {
            _logger.Trace(CultureInfo.InvariantCulture, "ReadBookByFiltersAsync called");

            var sortFields = query.Sort.CompileSorts(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                {"id", "E.[Id]" },
                {"title", "E.[Title]" },
                {"author", "E.[Author]" },
            }, "E.[Id] DESC");

            var parameters = new
            {
                @Id = query.Id,
                @Title = query.Title,
                @Author = query.Author,
                @Genre = query.Genre?.Select(x => x.ToString()),
                @Skip = query.Skip,
                @Limit = query.Limit
            };

            var cmdText = $@"
                SELECT 
                      E.[Id]
                    , E.[Title]
                    , E.[Author]
                    , E.[Genre]
                    , E.[ISBN]
                    , E.[Description]
                    , E.[AuditId]

                FROM [{_schemaBook}].[{_tableBook}] E

                WHERE 1 = 1
                  {(query.Id?.Length > 0 ? "AND E.[Id]     IN @Id" : "")}
                  {(query.Title?.Length > 0 ? "AND E.[Title]  IN @Title" : "")}
                  {(query.Author?.Length > 0 ? "AND E.[Author] IN @Author" : "")}
                  {(query.Genre?.Length > 0 ? "AND E.[Genre]  IN @Genre" : "")}
            "
            .AsSqlServerPagedQuery(sortFields);

            var cmd = new CommandDefinition(cmdText, parameters, transaction: Transaction, cancellationToken: ctk);

            var (data, count) = await Connection.ReadPagedAsync<BookView>(cmd).ConfigureAwait(false);

            var d = data.Select(s => s.ToOutput());

            _logger.Trace(CultureInfo.InvariantCulture, "ReadBookByFiltersAsync ended");

            return (d, count);
        }

        public async Task<int> InsertBookAsync(Book.V1.Output entity, CancellationToken ctk = default)
        {
            _logger.Trace(CultureInfo.InvariantCulture, "InsertBookAsync called");

            var parameters = new
            {
                @Title = entity.Title,
                @Author = entity.Author,
                @Genre = entity.Genre.ToString(),
                @ISBN = entity.ISBN,
                @Description = entity.Description,
                @AuditId = CurrentAudit?.AuditId,
            };

            var cmdText = $@"
                INSERT INTO [{_schemaBook}].[{_tableBook}] (
                      [Title]
                    , [Author]
                    , [Genre]
                    , [ISBN]
                    , [Description]
                    , [AuditId]
                )
                OUTPUT INSERTED.Id
                VALUES (
                      @Title
                    , @Author
                    , @Genre
                    , @ISBN
                    , @Description
                    , @AuditId
                )
            ";

            var cmd = new CommandDefinition(
                cmdText,
                parameters,
                transaction: Transaction,
                cancellationToken: ctk
            );

            var id = await Connection.ExecuteScalarAsync<int>(cmd).ConfigureAwait(false);

            _logger.Trace(CultureInfo.InvariantCulture, "InsertBookAsync ended");

            return id;
        }

        public async Task PutBookAsync(Book.V1.Output entity, CancellationToken ctk = default)
        {
            _logger.Trace(CultureInfo.InvariantCulture, "PutBookAsync called");

            var parameters = new
            {
                @Id = entity.Id,
                @Title = entity.Title,
                @Author = entity.Author,
                @Genre = entity.Genre.ToString(),
                @ISBN = entity.ISBN,
                @Description = entity.Description,
                @AuditId = CurrentAudit?.AuditId,
            };

            var query = @$"
                UPDATE  [{_schemaBook}].[{_tableBook}]

                SET
                      [Title] = @Title
                    , [Author] = @Author
                    , [Genre] = @Genre
                    , [ISBN] = @ISBN
                    , [Description] = @Description
                    , [AuditId] = @AuditId 

                WHERE 1=1
                AND  [Id] = @Id
            ";

            var cmd = new CommandDefinition(
                query,
                parameters,
                transaction: Transaction,
                cancellationToken: ctk
            );

            await Connection.ExecuteAsync(cmd).ConfigureAwait(false);

            _logger.Trace(CultureInfo.InvariantCulture, "PutBookAsync ended");
        }

        public async Task DeleteBookAsync(int id, CancellationToken ctk = default)
        {
            _logger.Trace(CultureInfo.InvariantCulture, "DeleteBookAsync called");

            var parameters = new
            {
                @Id = id
            };

            var cmdText = $@"
                DELETE FROM [{_schemaBook}].[{_tableBook}]
                WHERE 1 = 1 
                    AND [Id] = @Id
            ";

            var cmd = new CommandDefinition(
                cmdText,
                parameters,
                transaction: Transaction,
                cancellationToken: ctk
            );

            await Connection.ExecuteAsync(cmd).ConfigureAwait(false);

            _logger.Trace(CultureInfo.InvariantCulture, "DeleteBookAsync ended");
        }

        public async Task<(AuditedEntityDto<Book.V1.Output>? pre, AuditedEntityDto<Book.V1.Output>? cur)> ReadBookAuditAsync(Guid auditId, CancellationToken ctk = default)
        {
            var param = new
            {
                @AuditId = auditId,
            };

            var cmd = new CommandDefinition(
                $@"
                    SELECT 
                          F.[Id]
                        , F.[Title]
                        , F.[Author]
                        , F.[Genre]
                        , F.[ISBN]
                        , F.[Description]
                        , F.[AuditId]
                        , F.[SysStartTime]
                        , F.[SysEndTime]

                    FROM 
                        [{_schemaBook}].[{_tableBook}] FOR SYSTEM_TIME ALL F

                    INNER JOIN 
                        [{_schemaBook}].[{_tableBook}] FOR SYSTEM_TIME ALL R
                        ON 1=1
                            AND F.[Id] = R.[Id]

                    WHERE 1=1
                            AND R.[AuditId] = @AuditId
                ",
                param, transaction: Transaction, cancellationToken: ctk
             );

            var data = await Connection.QueryAsync<BookView>(cmd).ConfigureAwait(false);

            var resTable = data
                .Select(s => new AuditedEntityDto<Book.V1.Output>()
                {
                    Entity = s.ToOutput(),
                    SysStartTime = s.SysStartTime!.Value,
                    SysEndTime = s.SysEndTime!.Value
                })
                .ToList();

            var cur = resTable.FirstOrDefault(w => w.Entity!.AuditId == auditId);
            var pre = resTable.FirstOrDefault(w => w.Entity!.AuditId != auditId);

            return (pre, cur);
        }

        #region Private view
        private sealed record BookView
        {
            public int Id { get; set; }
            public string? Title { get; set; }
            public string? Author { get; set; }
            public string? Genre { get; set; }
            public string? ISBN { get; set; }
            public string? Description { get; set; }

            public Guid AuditId { get; set; }
            public Instant? SysStartTime { get; set; }
            public Instant? SysEndTime { get; set; }

            public Book.V1.Output ToOutput()
            {
                return new Book.V1.Output
                {
                    Id = Id,
                    Title = Title,
                    Author = Author,
                    Genre = string.IsNullOrEmpty(Genre) ? null : Enum.Parse<BookGenre>(Genre),
                    ISBN = ISBN,
                    Description = Description,
                    AuditId = AuditId,
                };
            }
        }
        #endregion
    }
}
