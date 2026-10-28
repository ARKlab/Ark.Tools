// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.ResourceWatcher.Sample.Dto;

using CsvHelper;

namespace Ark.ResourceWatcher.Sample.Transform;

/// <summary>
/// Transforms CSV byte content to SinkDto.
/// </summary>
public sealed class MyTransformService
{
    private readonly string _sourceId;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyTransformService"/> class.
    /// </summary>
    /// <param name="sourceId">The source identifier for the output.</param>
    public MyTransformService(string sourceId)
    {
        _sourceId = sourceId;
    }

    /// <summary>
    /// Transforms CSV byte content to SinkDto.
    /// </summary>
    /// <param name="input">The CSV data as a byte array.</param>
    /// <returns>The transformed SinkDto.</returns>
    public SinkDto Transform(byte[] input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var content = Encoding.UTF8.GetString(input);

        // Handle empty input
        if (string.IsNullOrWhiteSpace(content))
        {
            return new SinkDto
            {
                SourceId = _sourceId,
                Records = []
            };
        }

        // Header names are matched ignoring case, and extra columns are allowed
        var options = new CsvReaderOptions
        {
            Separator = ',',
            HasHeader = true,
            TrimWhitespace = true,
            MissingField = CsvMissingFieldHandling.Throw,
            MalformedData = CsvMalformedDataHandling.Throw,
            IncludeValuesInExceptions = false, // keep raw field values out of logged exceptions
        };

        using var csv = CsvReader.FromText(content, options);

        List<SinkRecord> records;
        try
        {
            records = csv.GetRecords<SinkRecord>().ToList();
        }
        catch (Exception ex) when (ex is CsvFormatException or KeyNotFoundException)
        {
            throw new TransformException($"CSV parsing error: {ex.Message}", ex);
        }

        return new SinkDto
        {
            SourceId = _sourceId,
            Records = records
        };
    }
}

/// <summary>
/// CsvHelper record map for mapping CSV columns to SinkRecord properties.
/// </summary>
[CsvRecordMap(typeof(SinkRecord))]
[SuppressMessage("ErrorHandling", "ERP022:Unobserved exception in a generic exception handler", Justification = "The CsvHelper-generated mapper catches exceptions only to find the failing member; the original error is still thrown.")]
internal sealed partial class SinkRecordMap
{
    [CsvColumn("id")]
    public string Id { get; set; } = "";

    [CsvColumn("name")]
    public string Name { get; set; } = "";

    [CsvColumn("value")]
    public decimal Value { get; set; }
}

/// <summary>
/// Exception thrown when transformation fails.
/// </summary>
public sealed class TransformException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransformException"/> class.
    /// </summary>
    public TransformException() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransformException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public TransformException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransformException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TransformException(string message, Exception innerException) : base(message, innerException)
    {
    }
}