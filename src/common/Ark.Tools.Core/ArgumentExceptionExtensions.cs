// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Ark.Tools.Core;

/// <summary>
/// Provides extension methods for ArgumentException using C# 14 extension members.
/// </summary>
public static class ArgumentExceptionExtensions
{
    /// <summary>
    /// Extension members for ArgumentException to provide ThrowIf and ThrowUnless static methods.
    /// </summary>
    extension(ArgumentException)
    {
        /// <summary>
        /// Throws an ArgumentException if the specified condition is true.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="message">Optional custom message for the exception.</param>
        /// <param name="paramName">The name of the parameter that caused the exception.</param>
        /// <param name="conditionExpression">The expression of the condition (captured automatically).</param>
        /// <exception cref="ArgumentException">Thrown when condition is true.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIf(
            [DoesNotReturnIf(true)] bool condition,
            string? message = null,
            string? paramName = null,
            [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
        {
            if (condition)
            {
                var errorMessage = message != null
                    ? $"{message} (condition: {conditionExpression})"
                    : $"Condition failed: {conditionExpression}";
                throw new ArgumentException(errorMessage, paramName);
            }
        }

        /// <summary>
        /// Throws an ArgumentException if the specified condition is false.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="message">Optional custom message for the exception.</param>
        /// <param name="paramName">The name of the parameter that caused the exception.</param>
        /// <param name="conditionExpression">The expression of the condition (captured automatically).</param>
        /// <exception cref="ArgumentException">Thrown when condition is false.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowUnless(
            [DoesNotReturnIf(false)] bool condition,
            string? message = null,
            string? paramName = null,
            [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
        {
            if (!condition)
            {
                var errorMessage = message != null
                    ? $"{message} (condition: {conditionExpression})"
                    : $"Condition failed: {conditionExpression}";
                throw new ArgumentException(errorMessage, paramName);
            }
        }
    }
}