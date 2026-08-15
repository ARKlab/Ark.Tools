// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using NodaTime;

using ProtoBuf;
using ProtoBuf.Meta;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Ark.Tools.Nodatime.Protobuf.Tests;

/// <summary>
/// Round-trip tests proving the NodaTime protobuf surrogates preserve the semantics of each type.
/// </summary>
[TestClass]
public sealed class NodaTimeSurrogateTests
{
    [ProtoContract]
    private sealed class Wrapper
    {
        [ProtoMember(1)]
        public LocalDate Date { get; set; }

        [ProtoMember(2)]
        public LocalDateTime DateTime { get; set; }

        [ProtoMember(3)]
        public OffsetDateTime OffsetDateTime { get; set; }

        [ProtoMember(9)]
        public ZonedDateTime ZonedDateTime { get; set; }

        [ProtoMember(4)]
        public NodaTime.Period? Period { get; set; }

        [ProtoMember(5)]
        public Instant Instant { get; set; }

        [ProtoMember(6)]
        public Duration Duration { get; set; }

        [ProtoMember(7)]
        public LocalTime LocalTime { get; set; }

        [ProtoMember(8)]
        public IsoDayOfWeek IsoDayOfWeek { get; set; }
    }

    private static RuntimeTypeModel _newModel()
    {
        var model = RuntimeTypeModel.Create();
        model.AddNodaTimeSurrogates();
        model.Add(typeof(Wrapper), true);
        return model;
    }

    [TestMethod]
    public void Roundtrips_all_supported_types()
    {
        var model = _newModel();

        var original = new Wrapper
        {
            Date = new LocalDate(2024, 2, 29),
            DateTime = new LocalDate(2024, 2, 29).At(new LocalTime(13, 45, 30).PlusNanoseconds(123_456_789)),
            // Offset intentionally not the machine local offset, to prove it survives serialization.
            OffsetDateTime = new OffsetDateTime(
                new LocalDateTime(2024, 2, 29, 13, 45, 30),
                Offset.FromHoursAndMinutes(5, 30)),
            ZonedDateTime = new LocalDateTime(2024, 2, 29, 13, 45, 30)
                .InZoneLeniently(DateTimeZoneProviders.Tzdb["Europe/Rome"]),
            Period = NodaTime.Period.FromYears(1) + NodaTime.Period.FromMonths(2) + NodaTime.Period.FromDays(10)
                + NodaTime.Period.FromHours(2) + NodaTime.Period.FromMinutes(30),
            Instant = Instant.FromUtc(2024, 2, 29, 13, 45, 30).PlusNanoseconds(123_456_789),
            Duration = Duration.FromDays(2) + Duration.FromNanoseconds(987_654_321),
            LocalTime = new LocalTime(13, 45, 30).PlusNanoseconds(123_456_789),
            IsoDayOfWeek = IsoDayOfWeek.Thursday,
        };

        var clone = _protoRoundtrip(model, original);

        clone.Date.Should().Be(original.Date);
        clone.DateTime.Should().Be(original.DateTime);
        clone.OffsetDateTime.Should().Be(original.OffsetDateTime);
        clone.OffsetDateTime.Offset.Should().Be(Offset.FromHoursAndMinutes(5, 30), "the offset must be preserved");
        clone.ZonedDateTime.LocalDateTime.Should().Be(original.ZonedDateTime.LocalDateTime);
        clone.ZonedDateTime.Zone.Id.Should().Be("Europe/Rome");
        clone.Period.Should().Be(original.Period);
        clone.Instant.Should().Be(original.Instant);
        clone.Duration.Should().Be(original.Duration);
        clone.LocalTime.Should().Be(original.LocalTime);
        clone.IsoDayOfWeek.Should().Be(original.IsoDayOfWeek);
    }

    [TestMethod]
    public void Native_types_use_common_proto_shapes()
    {
        var schema = _newModel().GetSchema(typeof(Wrapper));

        schema.Should().Contain("Timestamp");
        schema.Should().Contain("Duration");
        schema.Should().Contain("Date");
        schema.Should().Contain("TimeOfDay");
        schema.Should().Contain("DayOfWeek");
    }

    [TestMethod]
    public void OffsetDateTime_preserves_negative_offset_and_instant()
    {
        var model = _newModel();

        var value = new Wrapper
        {
            OffsetDateTime = new OffsetDateTime(
                new LocalDateTime(2023, 11, 5, 1, 30, 0),
                Offset.FromHours(-8)),
        };

        var clone = _protoRoundtrip(model, value);

        clone.OffsetDateTime.Should().Be(value.OffsetDateTime);
        clone.OffsetDateTime.Offset.Should().Be(Offset.FromHours(-8));
        clone.OffsetDateTime.ToInstant().Should().Be(value.OffsetDateTime.ToInstant());
    }

    [TestMethod]
    public void Period_is_encoded_as_iso_string()
    {
        PeriodSurrogate? surrogate = NodaTime.Period.FromYears(1) + NodaTime.Period.FromMonths(2) + NodaTime.Period.FromDays(10);

        surrogate!.Value.Should().Be("P1Y2M10D");

        NodaTime.Period? back = surrogate;
        back.Should().Be(NodaTime.Period.FromYears(1) + NodaTime.Period.FromMonths(2) + NodaTime.Period.FromDays(10));
    }

    [TestMethod]
    public void LocalDate_carries_no_time_component()
    {
        LocalDateSurrogate surrogate = new LocalDate(2024, 7, 2);

        surrogate.Year.Should().Be(2024);
        surrogate.Month.Should().Be(7);
        surrogate.Day.Should().Be(2);

        LocalDate back = surrogate;
        back.Should().Be(new LocalDate(2024, 7, 2));
    }

    private static Wrapper _protoRoundtrip(RuntimeTypeModel model, Wrapper value)
    {
        using var ms = new MemoryStream();
        model.Serialize(ms, value);
        ms.Position = 0;
        return (Wrapper)model.Deserialize(ms, null, typeof(Wrapper));
    }
}
