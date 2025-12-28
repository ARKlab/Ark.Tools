using Ark.Reference.Common;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Sql.SqlServer;

using Dapper;

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.DAL
{
    public partial class CoreDataContext_Sql
    {
        private const string _schemaBookPrintProcess = "dbo";
        private const string _tableBookPrintProcess = "BookPrintProcess";

        public async Task<BookPrintProcess.V1.Output?> ReadBookPrintProcessByIdAsync(int bookPrintProcessId, CancellationToken ctk = default)
        {
            _logger.Trace(CultureInfo.InvariantCulture, "ReadBookPrintProcessByIdAsync called");

            var cmdText = $@"
                SELECT 
                      E.[BookPrintProcessId]
                    , E.[BookId]
                    , E.[Progress]
                    , E.[Status]
                    , E.[ErrorMessage]
                    , E.[ShouldFail]
                    , E.[AuditId]

                FROM [{_schemaBookPrintProcess}].[{_tableBookPrintProcess}] E
                WHERE E.[BookPrintProcessId] = @BookPrintProcessId
            ";

            var result = await Connection.QueryAsync<_BookPrintProcessDto>(cmdText, new { BookPrintProcessId = bookPrintProcessId }, Transaction).ConfigureAwait(false);

            _logger.Trace(CultureInfo.InvariantCulture, "ReadBookPrintProcessByIdAsync ended");

            return result.SingleOrDefault()?.ToOutput();
        }

        public async Task<BookPrintProcess.V1.Output?> ReadRunningPrintProcessForBookAsync(int bookId, CancellationToken ctk = default)
        {
            _logger.Trace(CultureInfo.InvariantCulture, "ReadRunningPrintProcessForBookAsync called");

            var cmdText = $@"
                SELECT TOP 1
                      E.[BookPrintProcessId]
                    , E.[BookId]
                    , E.[Progress]
                    , E.[Status]
                    , E.[ErrorMessage]
                    , E.[ShouldFail]
                    , E.[AuditId]

                FROM [{_schemaBookPrintProcess}].[{_tableBookPrintProcess}] E
                WHERE E.[BookId] = @BookId
                  AND E.[Status] IN (@Pending, @Running)
                ORDER BY E.[BookPrintProcessId] DESC
            ";

            var result = await Connection.QueryAsync<_BookPrintProcessDto>(cmdText, new 
            { 
                BookId = bookId,
                Pending = BookPrintProcessStatus.Pending.ToString(),
                Running = BookPrintProcessStatus.Running.ToString()
            }, Transaction).ConfigureAwait(false);

            _logger.Trace(CultureInfo.InvariantCulture, "ReadRunningPrintProcessForBookAsync ended");

            return result.SingleOrDefault()?.ToOutput();
        }

        public async Task<int> PostBookPrintProcessAsync(BookPrintProcess.V1.Output entity, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            _logger.Trace(CultureInfo.InvariantCulture, "PostBookPrintProcessAsync called");

            var cmdText = $@"
                INSERT INTO [{_schemaBookPrintProcess}].[{_tableBookPrintProcess}]
                (
                      [BookId]
                    , [Progress]
                    , [Status]
                    , [ErrorMessage]
                    , [ShouldFail]
                    , [AuditId]
                )
                OUTPUT INSERTED.[BookPrintProcessId]
                VALUES
                (
                      @BookId
                    , @Progress
                    , @Status
                    , @ErrorMessage
                    , @ShouldFail
                    , @AuditId
                )
            ";

            var id = await Connection.QuerySingleAsync<int>(cmdText, new
            {
                entity.BookId,
                entity.Progress,
                Status = entity.Status.ToString(),
                entity.ErrorMessage,
                entity.ShouldFail,
                entity.AuditId
            }, Transaction).ConfigureAwait(false);

            _logger.Trace(CultureInfo.InvariantCulture, "PostBookPrintProcessAsync ended");

            return id;
        }

        public async Task PutBookPrintProcessAsync(BookPrintProcess.V1.Output entity, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            _logger.Trace(CultureInfo.InvariantCulture, "PutBookPrintProcessAsync called");

            var cmdText = $@"
                UPDATE [{_schemaBookPrintProcess}].[{_tableBookPrintProcess}]
                SET
                      [Progress] = @Progress
                    , [Status] = @Status
                    , [ErrorMessage] = @ErrorMessage
                    , [AuditId] = @AuditId
                WHERE [BookPrintProcessId] = @BookPrintProcessId
            ";

            await Connection.ExecuteAsync(cmdText, new
            {
                entity.BookPrintProcessId,
                entity.Progress,
                Status = entity.Status.ToString(),
                entity.ErrorMessage,
                entity.AuditId
            }, Transaction).ConfigureAwait(false);

            _logger.Trace(CultureInfo.InvariantCulture, "PutBookPrintProcessAsync ended");
        }

        private sealed class _BookPrintProcessDto
        {
            public int BookPrintProcessId { get; set; }
            public int BookId { get; set; }
            public double Progress { get; set; }
            public string Status { get; set; } = string.Empty;
            public string? ErrorMessage { get; set; }
            public bool ShouldFail { get; set; }
            public Guid AuditId { get; set; }

            public BookPrintProcess.V1.Output ToOutput() => new()
            {
                BookPrintProcessId = BookPrintProcessId,
                BookId = BookId,
                Progress = Progress,
                Status = Enum.Parse<BookPrintProcessStatus>(Status),
                ErrorMessage = ErrorMessage,
                ShouldFail = ShouldFail,
                AuditId = AuditId
            };
        }
    }
}
