// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using FluentValidation;

using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.MediatorFramework;
using Ark.Tools.SystemTextJson;

using Google.Protobuf.WellKnownTypes;
using Google.Rpc;

using Grpc.Core;
using Grpc.Core.Interceptors;

using System.Diagnostics.CodeAnalysis;

namespace Ark.MediatorFramework.Sample.WebInterface;

/// <summary>Maps transport-agnostic validation failures to the gRPC rich error model.</summary>
public sealed class GrpcErrorInterceptor : Interceptor
{
    /// <summary>Executes a unary call and adds field violations to validation failures.</summary>
    /// <typeparam name="TRequest">The request message type.</typeparam>
    /// <typeparam name="TResponse">The response message type.</typeparam>
    /// <param name="request">The request message.</param>
    /// <param name="context">The server call context.</param>
    /// <param name="continuation">The next unary handler.</param>
    /// <returns>The handler response.</returns>
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context).ConfigureAwait(false);
        }
        catch (BusinessRuleViolationException exception)
        {
            var violation = exception.BusinessRuleViolation;
            var detail = new ArkBusinessRuleViolation
            {
                Type = violation.GetType().Name,
                Title = violation.Title,
                Status = violation.Status,
                PayloadJson = Convert.ToBase64String(SerializeViolation(violation)),
            };
            using var stream = new MemoryStream();
            ProtoBuf.Serializer.Serialize(stream, detail);

            var status = new Google.Rpc.Status
            {
                Code = (int)Code.FailedPrecondition,
                Message = violation.Title,
            };
            status.Details.Add(new Any
            {
                TypeUrl = "type.googleapis.com/ark.mediator.ArkBusinessRuleViolation",
                Value = Google.Protobuf.ByteString.CopyFrom(stream.ToArray()),
            });
            throw status.ToRpcException();
        }
        catch (ValidationException exception)
        {
            var status = new Google.Rpc.Status
            {
                Code = (int)Code.InvalidArgument,
                Message = "Validation failed",
            };

            var badRequest = new BadRequest();
            foreach (var failure in exception.Errors)
            {
                badRequest.FieldViolations.Add(new BadRequest.Types.FieldViolation
                {
                    Field = failure.PropertyName,
                    Description = failure.ErrorMessage,
                });
            }

            status.Details.Add(Any.Pack(badRequest));
            throw status.ToRpcException();
        }

        [SuppressMessage(
            "Trimming",
            "IL2026",
            Justification = "Business rule payloads are application-defined and intentionally serialized using the shared Ark JSON options.")]
        private static byte[] SerializeViolation(BusinessRuleViolation violation)
        {
            return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                violation,
                violation.GetType(),
                ArkSerializerOptions.JsonOptions);
        }
    }
}
