﻿using Ark.Tools.Outbox.Rebus;
using Ark.Tools.Solid;

using Core.Service.API.Messages;
using Core.Service.API.Requests;
using Core.Service.Application.DAL;
using Core.Service.Common.Dto;
using Core.Service.Common.Enum;

using EnsureThat;

using Rebus.Bus;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Service.Application.Handlers.Requests
{
    public class Ping_CreateAndSendMsgRequestHandler : IRequestHandler<Ping_CreateAndSendMsgRequest.V1, Ping.V1.Output>
    {
        private readonly Func<ICoreDataContext> _coreDataContext;
        private readonly IContextProvider<ClaimsPrincipal> _userContext;
        private readonly IBus _bus;

        public Ping_CreateAndSendMsgRequestHandler(
              Func<ICoreDataContext> coreDataContext
              , IContextProvider<ClaimsPrincipal> userContext
              , IBus bus
)
        {
            EnsureArg.IsNotNull(coreDataContext, nameof(coreDataContext));
            EnsureArg.IsNotNull(userContext, nameof(userContext));

            _coreDataContext = coreDataContext;
            _userContext = userContext;
            _bus = bus;
        }

        public Ping.V1.Output Execute(Ping_CreateAndSendMsgRequest.V1 request)
        {
            return ExecuteAsync(request).GetAwaiter().GetResult();
        }

        public async Task<Ping.V1.Output> ExecuteAsync(Ping_CreateAndSendMsgRequest.V1 request, CancellationToken ctk = default)
        {
            using var ctx = _coreDataContext();

            await ctx.EnsureAudit(AuditKind.Ping, _userContext.GetUserId(), "Create a new Ping", ctk);

            var createPingData = new Ping.V1.Output()
            {
                Name = request.Data.Name,
                Type = request.Data.Type,
                Code = $"PING_CODE_{request.Data.Name}"
            };

            var id = await ctx.InsertPingAsync(createPingData, ctk);

            var entity = await ctx.ReadPingByIdAsync(id, ctk);

            using var scope = _bus.Enlist(ctx);

            await _bus.Send(new Ping_ProcessMessage.V1()
            {
                Id = id,
            });

            await scope.CompleteAsync();

            ctx.Commit();

            return entity;
        }
    }
}