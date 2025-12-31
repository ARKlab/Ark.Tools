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
        
        // Handle empty input
        if (string.IsNullOrWhiteSpace(content))
        {
            return new SinkDto
            {
                SourceId = _sourceId,
                Records = []
            };
        }
        
        using var reader = new StringReader(content);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            HeaderValidated = null, // Don't validate headers - allow extra columns
            PrepareHeaderForMatch = args => args.Header.ToLowerInvariant() // Case-insensitive matching
        };
        
        using var csv = new CsvReader(reader, config);

        csv.Context.RegisterClassMap<SinkRecordMap>();

        List<SinkRecord> records;
        try
        {
            records = csv.GetRecords<SinkRecord>().ToList();
        }
        catch (CsvHelper.CsvHelperException ex) when (ex.InnerException is FormatException formatEx)
        {
            // Unwrap FormatException from CsvHelperException for invalid data types
            throw formatEx;
        }
        catch (CsvHelper.CsvHelperException ex) when (ex.InnerException?.InnerException is FormatException formatEx2)
        {
            // Unwrap FormatException that's nested deeper (e.g., through TypeConverterException)
            throw formatEx2;
        }
        catch (CsvHelper.CsvHelperException ex) when (ex.InnerException is CsvHelper.MissingFieldException)
        {
            // Handle missing required fields with a clear error message
            var row = ex.Context?.Parser?.Row ?? 0;
            throw new TransformException($"Invalid CSV format at line {row}: expected at least 3 columns", ex);
        }
        catch (CsvHelper.CsvHelperException ex)
        {
            throw new TransformException($"CSV parsing error: {ex.Message}", ex);
        }

        return new SinkDto
        {
            SourceId = _sourceId,
            Records = records
        };
    }

    /// <summary>
    /// CsvHelper ClassMap for mapping CSV columns to SinkRecord properties.
    /// Maps required columns by name/index and collects additional columns into Properties.
    /// </summary>
    private sealed class SinkRecordMap : ClassMap<SinkRecord>
    {
        public SinkRecordMap()
        {
            // Map required columns by name (case-insensitive due to PrepareHeaderForMatch)
            Map(m => m.Id).Name("id").Index(0);
            Map(m => m.Name).Name("name").Index(1);
            Map(m => m.Value).Name("value").Index(2);
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
