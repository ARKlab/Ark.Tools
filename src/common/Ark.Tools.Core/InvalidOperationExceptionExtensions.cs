// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Core(net10.0)', Before:
namespace Ark.Tools.Core
{
    /// <summary>
    /// Provides extension methods for InvalidOperationException using C# 14 extension members.
    /// </summary>
    public static class InvalidOperationExceptionExtensions
    {
        /// <summary>
        /// Extension members for InvalidOperationException to provide ThrowIf and ThrowUnless static methods.
        /// </summary>
        extension(InvalidOperationException)
        {
            /// <summary>
            /// Throws an InvalidOperationException if the specified condition is true.
            /// </summary>
            /// <param name="condition">The condition to evaluate.</param>
            /// <param name="message">Optional custom message for the exception.</param>
            /// <param name="conditionExpression">The expression of the condition (captured automatically).</param>
            /// <exception cref="InvalidOperationException">Thrown when condition is true.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ThrowIf(
                [DoesNotReturnIf(true)] bool condition,
                string? message = null,
                [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
            {
                if (condition)
                {
                    var errorMessage = message != null 
                        ? $"{message} (condition: {conditionExpression})"
                        : $"Condition failed: {conditionExpression}";
                    throw new InvalidOperationException(errorMessage);
                }
            }

            /// <summary>
            /// Throws an InvalidOperationException if the specified condition is false.
            /// </summary>
            /// <param name="condition">The condition to evaluate.</param>
            /// <param name="message">Optional custom message for the exception.</param>
            /// <param name="conditionExpression">The expression of the condition (captured automatically).</param>
            /// <exception cref="InvalidOperationException">Thrown when condition is false.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ThrowUnless(
                [DoesNotReturnIf(false)] bool condition,
                string? message = null,
                [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
            {
                if (!condition)
                {
                    var errorMessage = message != null 
                        ? $"{message} (condition: {conditionExpression})"
                        : $"Condition failed: {conditionExpression}";
                    throw new InvalidOperationException(errorMessage);
                }
=======
namespace Ark.Tools.Core;

/// <summary>
/// Provides extension methods for InvalidOperationException using C# 14 extension members.
/// </summary>
public static class InvalidOperationExceptionExtensions
{
    /// <summary>
    /// Extension members for InvalidOperationException to provide ThrowIf and ThrowUnless static methods.
    /// </summary>
    extension(InvalidOperationException)
    {
        /// <summary>
        /// Throws an InvalidOperationException if the specified condition is true.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="message">Optional custom message for the exception.</param>
        /// <param name="conditionExpression">The expression of the condition (captured automatically).</param>
        /// <exception cref="InvalidOperationException">Thrown when condition is true.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIf(
            [DoesNotReturnIf(true)] bool condition,
            string? message = null,
            [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
        {
            if (condition)
            {
                var errorMessage = message != null 
                    ? $"{message} (condition: {conditionExpression})"
                    : $"Condition failed: {conditionExpression}";
                throw new InvalidOperationException(errorMessage);
            }
        }

        /// <summary>
        /// Throws an InvalidOperationException if the specified condition is false.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="message">Optional custom message for the exception.</param>
        /// <param name="conditionExpression">The expression of the condition (captured automatically).</param>
        /// <exception cref="InvalidOperationException">Thrown when condition is false.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowUnless(
            [DoesNotReturnIf(false)] bool condition,
            string? message = null,
            [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
        {
            if (!condition)
            {
                var errorMessage = message != null 
                    ? $"{message} (condition: {conditionExpression})"
                    : $"Condition failed: {conditionExpression}";
                throw new InvalidOperationException(errorMessage);
>>>>>>> After


namespace Ark.Tools.Core;

/// <summary>
/// Provides extension methods for InvalidOperationException using C# 14 extension members.
/// </summary>
public static class InvalidOperationExceptionExtensions
{
    /// <summary>
    /// Extension members for InvalidOperationException to provide ThrowIf and ThrowUnless static methods.
    /// </summary>
    extension(InvalidOperationException)
    {
        /// <summary>
        /// Throws an InvalidOperationException if the specified condition is true.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="message">Optional custom message for the exception.</param>
        /// <param name="conditionExpression">The expression of the condition (captured automatically).</param>
        /// <exception cref="InvalidOperationException">Thrown when condition is true.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIf(
            [DoesNotReturnIf(true)] bool condition,
            string? message = null,
            [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
        {
            if (condition)
            {
                var errorMessage = message != null
                    ? $"{message} (condition: {conditionExpression})"
                    : $"Condition failed: {conditionExpression}";
                throw new InvalidOperationException(errorMessage);
            }
        }

        /// <summary>
        /// Throws an InvalidOperationException if the specified condition is false.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="message">Optional custom message for the exception.</param>
        /// <param name="conditionExpression">The expression of the condition (captured automatically).</param>
        /// <exception cref="InvalidOperationException">Thrown when condition is false.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowUnless(
            [DoesNotReturnIf(false)] bool condition,
            string? message = null,
            [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
        {
            if (!condition)
            {
                var errorMessage = message != null
                    ? $"{message} (condition: {conditionExpression})"
                    : $"Condition failed: {conditionExpression}";
                throw new InvalidOperationException(errorMessage);
            }
        }
    }
}