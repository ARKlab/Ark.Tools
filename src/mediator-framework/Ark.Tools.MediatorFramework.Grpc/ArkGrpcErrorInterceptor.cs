// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.Core.EntityTag;
using Ark.Tools.Core;
using Ark.Tools.Authorization;

using FluentValidation;

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;

using Grpc.Core;
using Grpc.Core.Interceptors;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NLog;

using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Ark.Tools.MediatorFramework.Grpc;

/// <summary>Maps transport-agnostic failures to the gRPC rich error model.</summary>
public sealed class ArkGrpcErrorInterceptor : Interceptor
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly bool _includeExceptionDetails;

    /// <summary>Initializes the gRPC error interceptor.</summary>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="options">The exception detail options.</param>
    public ArkGrpcErrorInterceptor(
        IHostEnvironment? environment = null,
        IOptions<ArkGrpcErrorOptions>? options = null)
    {
        _includeExceptionDetails = environment?.IsDevelopment() == true
            || options?.Value.IncludeExceptionDetails == true;
    }

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
        catch (Exception exception)
        {
            throw _mapException(exception, context);
        }
    }

    /// <summary>Executes a client-streaming call and maps known application failures.</summary>
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(requestStream, context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw _mapException(exception, context);
        }
    }

    /// <summary>Executes a server-streaming call and maps known application failures.</summary>
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await continuation(request, responseStream, context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw _mapException(exception, context);
        }
    }

    /// <summary>Executes a duplex-streaming call and maps known application failures.</summary>
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await continuation(requestStream, responseStream, context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw _mapException(exception, context);
        }
    }

    private Exception _mapException(Exception exception, ServerCallContext context)
    {
        if (exception is RpcException
            || exception is OperationCanceledException && context.CancellationToken.IsCancellationRequested)
            return exception;

        if (exception is BusinessRuleViolationException businessRuleException)
        {
            var violation = businessRuleException.BusinessRuleViolation;
            var detail = new ArkBusinessRuleViolation
            {
                Type = violation.GetType().Name,
                Title = violation.Title,
                Status = violation.Status,
                Detail = violation.Detail ?? string.Empty,
            };
            detail.Extensions.Add(_getExtensions(violation));

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
            return status.ToRpcException();
        }

        if (exception is ValidationException validationException)
        {
            var status = new Google.Rpc.Status
            {
                Code = (int)Code.InvalidArgument,
                Message = "Validation failed",
            };
            var badRequest = new BadRequest();
            foreach (var failure in validationException.Errors)
            {
                badRequest.FieldViolations.Add(new BadRequest.Types.FieldViolation
                {
                    Field = failure.PropertyName,
                    Description = failure.ErrorMessage,
                });
            }
            status.Details.Add(Any.Pack(badRequest));
            return status.ToRpcException();
        }

        if (exception is PolicyAuthorizationException authorizationException)
            return _createRpcException(StatusCode.PermissionDenied, authorizationException.Message);
        if (exception is EntityTagMismatchException entityTagException)
            return _createRpcException(StatusCode.FailedPrecondition, entityTagException.Message);
        if (exception is OptimisticConcurrencyException concurrencyException)
            return _createRpcException(StatusCode.Aborted, concurrencyException.Message);

        _logger.Error(exception, CultureInfo.InvariantCulture, "Unhandled exception while processing a gRPC request.");
        var unexpectedStatus = new Google.Rpc.Status
        {
            Code = (int)StatusCode.Internal,
            Message = _includeExceptionDetails
                ? exception.Message
                : "An unexpected error occurred.",
        };
        if (_includeExceptionDetails)
        {
            unexpectedStatus.Details.Add(Any.Pack(new DebugInfo
            {
                Detail = exception.StackTrace ?? string.Empty,
            }));
        }
        return unexpectedStatus.ToRpcException();
    }

    private static RpcException _createRpcException(StatusCode statusCode, string message) =>
        new(new global::Grpc.Core.Status(statusCode, message));

    [SuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The BusinessRuleViolation base type preserves public properties for the documented client-visible contract.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072",
        Justification = "Business-rule violation properties are part of the preserved client-visible contract.")]
    private static Dictionary<string, string> _getExtensions(BusinessRuleViolation violation)
    {
        var properties = violation.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetMethod is not null
                && !property.GetMethod.IsStatic
                && property.Name is not nameof(BusinessRuleViolation.Status)
                and not nameof(BusinessRuleViolation.Title)
                and not nameof(BusinessRuleViolation.Detail)
                )
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(property => _getInheritanceDepth(property.DeclaringType))
                .First())
            .OrderBy(property => property.Name, StringComparer.Ordinal);

        return properties.ToDictionary(
            property => property.Name,
            property => JsonSerializer.Serialize(property.GetValue(violation), property.PropertyType, ArkSerializerOptions.JsonOptions),
            StringComparer.Ordinal);
    }

    private static int _getInheritanceDepth(System.Type? type)
    {
        var depth = 0;
        for (var current = type; current is not null; current = current.BaseType)
            depth++;
        return depth;
    }
}
