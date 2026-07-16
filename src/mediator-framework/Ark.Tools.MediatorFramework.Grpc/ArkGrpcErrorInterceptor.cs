// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.Authorization;

using FluentValidation;

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;

using Grpc.Core;
using Grpc.Core.Interceptors;

using System.Reflection;
using System.Text.Json;

namespace Ark.Tools.MediatorFramework.Grpc;

/// <summary>Maps transport-agnostic failures to the gRPC rich error model.</summary>
public sealed class ArkGrpcErrorInterceptor : Interceptor
{
    /// <summary>Executes a unary call and maps known application failures to rich statuses.</summary>
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
                Detail = violation.Detail ?? string.Empty,
            };
            detail.Extensions.Add(GetExtensions(violation));

            var status = new Google.Rpc.Status
            {
                Code = (int)Code.FailedPrecondition,
                Message = violation.Title,
            };
            status.Details.Add(new Any
            {
                TypeUrl = "type.googleapis.com/ark.mediator.ArkBusinessRuleViolation",
                Value = detail.ToByteString(),
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
        catch (PolicyAuthorizationException exception)
        {
            throw new RpcException(new global::Grpc.Core.Status(StatusCode.PermissionDenied, exception.Message));
        }
    }

    [SuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Business rule payloads are application-defined and intentionally serialized using the shared Ark JSON options.")]
    private static Dictionary<string, string> GetExtensions(BusinessRuleViolation violation)
    {
        var properties = violation.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.Name is not nameof(BusinessRuleViolation.Status)
                and not nameof(BusinessRuleViolation.Title)
                and not nameof(BusinessRuleViolation.Detail)
                && property.GetMethod is not null);

        return properties.ToDictionary(
            property => property.Name,
            property => JsonSerializer.Serialize(property.GetValue(violation), property.PropertyType, ArkSerializerOptions.JsonOptions),
            StringComparer.Ordinal);
    }
}
