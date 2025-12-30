// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using System.Globalization;
using System.Text;

using Ark.ResourceWatcher.Sample.Dto;

namespace Ark.ResourceWatcher.Sample.Transform;

/// <summary>
/// Transforms CSV byte content to SinkDto.
/// </summary>
public sealed class CsvTransformService : ITransformService<byte[], SinkDto>
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

    /// <inheritdoc/>
    public SinkDto Transform(byte[] input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var content = Encoding.UTF8.GetString(input);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length == 0)
        {
            return new SinkDto
            {
                SourceId = _sourceId,
                Records = []
            };
        }

        // First line is header
        var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
        var records = new List<SinkRecord>();

        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            if (values.Length < 3)
            {
                throw new TransformException($"Invalid CSV format at line {i + 1}: expected at least 3 columns");
            }

            var record = new SinkRecord
            {
                Id = values[0].Trim(),
                Name = values[1].Trim(),
                Value = decimal.Parse(values[2].Trim(), CultureInfo.InvariantCulture)
            };

            // Add additional columns as properties
            if (values.Length > 3)
            {
                var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (int j = 3; j < values.Length && j < headers.Length; j++)
                {
                    properties[headers[j]] = values[j].Trim();
                }
                record = record with { Properties = properties };
            }

            records.Add(record);
        }

        return new SinkDto
        {
            SourceId = _sourceId,
            Records = records
        };
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
