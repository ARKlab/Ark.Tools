// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf.Meta;

namespace Ark.Tools.Nodatime.Protobuf;

/// <summary>
/// Registration helpers wiring the NodaTime surrogates into a protobuf-net
/// <see cref="RuntimeTypeModel"/> so NodaTime types serialize over protobuf and code-first gRPC.
/// </summary>
public static class Ex
{
    /// <summary>
    /// Registers the NodaTime surrogates on the given protobuf-net model.
    /// Calling it more than once is a no-op for already-registered types.
    /// </summary>
    /// <param name="model">The protobuf-net runtime type model to configure.</param>
    /// <returns>The same <paramref name="model"/> for chaining.</returns>
    public static RuntimeTypeModel AddNodaTimeSurrogates(this RuntimeTypeModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        _setSurrogate<LocalDate, LocalDateSurrogate>(model);
        _setSurrogate<Instant, InstantSurrogate>(model);
        _setSurrogate<Duration, DurationSurrogate>(model);
        _setSurrogate<LocalTime, LocalTimeSurrogate>(model);
        _setSurrogate<IsoDayOfWeek, IsoDayOfWeekSurrogate>(model);
        _setSurrogate<LocalDateTime, LocalDateTimeSurrogate>(model);
        _setSurrogate<OffsetDateTime, OffsetDateTimeSurrogate>(model);
        _setSurrogate<Period, PeriodSurrogate>(model);

        return model;
    }

    private static void _setSurrogate<TNodaTime, TSurrogate>(RuntimeTypeModel model)
    {
        // Avoid re-registering (SetSurrogate throws if the type is already fully configured).
        if (model.IsDefined(typeof(TNodaTime)))
            return;

        model.Add(typeof(TNodaTime), false).SetSurrogate(typeof(TSurrogate));
    }
}
