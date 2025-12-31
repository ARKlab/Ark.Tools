// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using System.Globalization;
using System.Text;

using Ark.ResourceWatcher.Sample.Dto;

using CsvHelper;
using CsvHelper.Configuration;

namespace Ark.ResourceWatcher.Sample.Transform;

/// <summary>
/// Transforms CSV byte content to SinkDto.
/// </summary>
public sealed class CsvTransformService
{
    private readonly string _sourceId;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvTransformService"/> class.
    /// </summary>
    /// <param name="sourceId">The source identifier for the output.</param>
    public CsvTransformService(string sourceId)
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
        
        using var reader = new StringReader(content);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null
        });

        csv.Context.RegisterClassMap<SinkRecordMap>();

        List<SinkRecord> records;
        try
        {
            records = csv.GetRecords<SinkRecord>().ToList();
        }
        catch (CsvHelper.CsvHelperException ex)
        {
            throw new TransformException($"CSV parsing error: {ex.Message}", ex);
        }
        catch (FormatException ex)
        {
            throw new TransformException($"Invalid data format: {ex.Message}", ex);
        }

        return new SinkDto
        {
            SourceId = _sourceId,
            Records = records
        };
    }

    /// <summary>
    /// CsvHelper ClassMap for mapping CSV columns to SinkRecord properties.
    /// </summary>
    private sealed class SinkRecordMap : ClassMap<SinkRecord>
    {
        public SinkRecordMap()
        {
            Map(m => m.Id).Index(0).Name("Id");
            Map(m => m.Name).Index(1).Name("Name");
            Map(m => m.Value).Index(2).Name("Value");
            // Properties field is not mapped from CSV, handled separately if needed
        }
    }
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
