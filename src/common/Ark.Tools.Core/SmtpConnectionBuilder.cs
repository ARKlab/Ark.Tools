// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Globalization;

namespace Ark.Tools;

public class SmtpConnectionBuilder
{
    public SmtpConnectionBuilder()
    { }

    /// <summary>
    /// Smtp connection string
    /// </summary>
    /// <remarks>
    /// es. Server=smtp.sendgrid.net;Port=587;Username=gnegnegne;Password=nonlosai;UseSsl=true
    /// </remarks>
    public SmtpConnectionBuilder(string smtpConnectionString)
    {
        if (string.IsNullOrWhiteSpace(smtpConnectionString))
            throw new ArgumentException("Empty connection string", nameof(smtpConnectionString));

        _parse(smtpConnectionString);
    }

    private void _parse(string smtpConnectionString)
    {
        var span = smtpConnectionString.AsSpan();

        // Split by semicolons using Span
        int start = 0;
        int semicolonIndex;
        while ((semicolonIndex = span[start..].IndexOf(';')) >= 0)
        {
            var segment = span.Slice(start, semicolonIndex).Trim();
            if (segment.Length > 0)
            {
                var equalsIndex = segment.IndexOf('=');
                if (equalsIndex < 0)
                    throw new FormatException();

                _processKeyValue(segment[..equalsIndex].Trim(), segment[(equalsIndex + 1)..].Trim());
            }
            start += semicolonIndex + 1;
        }

        // Add the last segment
        var lastSegment = span[start..].Trim();
        if (lastSegment.Length > 0)
        {
            var equalsIndex = lastSegment.IndexOf('=');
            if (equalsIndex < 0)
                throw new FormatException();

            _processKeyValue(lastSegment[..equalsIndex].Trim(), lastSegment[(equalsIndex + 1)..].Trim());
        }
    }

    private void _processKeyValue(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {
        if (key.Equals("SERVER", StringComparison.OrdinalIgnoreCase))
        {
            this.Server = value.ToString();
        }
        else if (key.Equals("PORT", StringComparison.OrdinalIgnoreCase))
        {
            this.Port = Int32.Parse(value, CultureInfo.InvariantCulture);
        }
        else if (key.Equals("USERNAME", StringComparison.OrdinalIgnoreCase))
        {
            this.Username = value.ToString();
        }
        else if (key.Equals("PASSWORD", StringComparison.OrdinalIgnoreCase))
        {
            this.Password = value.ToString();
        }
        else if (key.Equals("USESSL", StringComparison.OrdinalIgnoreCase))
        {
            this.UseSsl = bool.Parse(value);
        }
        else if (key.Equals("FROM", StringComparison.OrdinalIgnoreCase))
        {
            this.From = value.ToString();
        }
    }

    public string ConnectionString
    {
        get
        {
            return $"Server={Server};Port={Port};Username={Username};Password={Password};UseSsl={UseSsl}" + (!string.IsNullOrWhiteSpace(From) ? $";From={From}" : string.Empty);
        }
        set
        {
            _parse(value);
        }
    }

    public string? Server { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; }
    public string? From { get; set; }
}