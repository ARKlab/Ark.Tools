using Ark.Reference.Common.Services.Audit;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Core;
using Ark.Tools.Outbox;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.DAL
{
    public interface ICoreDataContext : IOutboxAsyncContext, IAsyncContext, IAuditDataContext<AuditKind>
    {
        #region Ping
        Task<Ping.V1.Output?> ReadPingByIdAsync(
              int id
            , CancellationToken ctk = default
        );

        Task<(IEnumerable<Ping.V1.Output> data, int count)> ReadPingByFiltersAsync(
              PingSearchQueryDto.V1 query
            , CancellationToken ctk = default
        );

        Task<int> InsertPingAsync(
              Ping.V1.Output entity
            , CancellationToken ctk = default
        );

        Task PutPingAsync(
            Ping.V1.Output entity
            , CancellationToken ctk = default
        );

        Task PatchPingAsync(
            Ping.V1.Output entity
            , CancellationToken ctk = default
        );

        Task DeletePingAsync(
            int id
            , CancellationToken ctk = default
        );

        Task<(AuditedEntityDto<Ping.V1.Output>? pre, AuditedEntityDto<Ping.V1.Output>? cur)> ReadPingAuditAsync(
            Guid auditId,
            CancellationToken ctk = default
        );
        #endregion

        #region Book
        Task<Book.V1.Output?> ReadBookByIdAsync(
              int id
            , CancellationToken ctk = default
        );

        Task<(IEnumerable<Book.V1.Output> data, int count)> ReadBookByFiltersAsync(
              BookSearchQueryDto.V1 query
            , CancellationToken ctk = default
        );

        Task<int> InsertBookAsync(
              Book.V1.Output entity
            , CancellationToken ctk = default
        );

        Task PutBookAsync(
            Book.V1.Output entity
            , CancellationToken ctk = default
        );

        Task DeleteBookAsync(
            int id
            , CancellationToken ctk = default
        );

        Task<(AuditedEntityDto<Book.V1.Output>? pre, AuditedEntityDto<Book.V1.Output>? cur)> ReadBookAuditAsync(
            Guid auditId,
            CancellationToken ctk = default
        );
        #endregion

        #region BookPrintProcess
        Task<BookPrintProcess.V1.Output?> ReadBookPrintProcessByIdAsync(
            int bookPrintProcessId
            , CancellationToken ctk = default
        );

        Task<(IEnumerable<BookPrintProcess.V1.Output> data, int count)> ReadBookPrintProcessByFiltersAsync(
            BookPrintProcessSearchQueryDto.V1 query
            , CancellationToken ctk = default
        );

        Task<BookPrintProcess.V1.Output?> ReadRunningPrintProcessForBookAsync(
            int bookId
            , CancellationToken ctk = default
        );

        Task<int> PostBookPrintProcessAsync(
            BookPrintProcess.V1.Output entity
            , CancellationToken ctk = default
        );

        Task PutBookPrintProcessAsync(
            BookPrintProcess.V1.Output entity
            , CancellationToken ctk = default
        );
        #endregion
    }
}