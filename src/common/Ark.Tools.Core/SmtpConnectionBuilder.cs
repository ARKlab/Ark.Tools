// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Compliance;

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
    public SmtpConnectionBuilder(
        [Secret] string smtpConnectionString)
    {
        if (string.IsNullOrWhiteSpace(smtpConnectionString))
            throw new ArgumentException("Empty connection string", nameof(smtpConnectionString));

        _parse(smtpConnectionString);
    }

    private void _parse(
        [Secret] string smtpConnectionString)
    {
        var span = smtpConnectionString.AsSpan();

        // Use MemoryExtensions.Split for efficient span-based splitting
        foreach (var segmentRange in span.Split(';'))
        {
            var segment = span[segmentRange].Trim();
            if (segment.Length == 0)
                continue;

            var equalsIndex = segment.IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex == segment.Length - 1)
                throw new FormatException();

            _processKeyValue(segment[..equalsIndex].Trim(), segment[(equalsIndex + 1)..].Trim());
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

    [Secret]
    [ComplianceReviewed("ARKPII005", "The credentials are composed back into the connection string this type exists to build; the value is transport, never a log sink, and carries [Secret] for the logging boundary.")]
    public string ConnectionString
    {
        get
        {
            // Round-trippable by contract: this value is fed back into _parse by the NLog
            // configuration path. Redaction happens at the logging boundary through [Secret].
            var port = (Port ?? 25).ToString(CultureInfo.InvariantCulture);
            var username = string.IsNullOrWhiteSpace(Username) ? string.Empty : $";Username={Username}";
            var secret = string.IsNullOrWhiteSpace(Password) ? string.Empty : $";Password={Password}";
            var from = string.IsNullOrWhiteSpace(From) ? string.Empty : $";From={From}";
            return $"Server={Server ?? "localhost"};Port={port}{username}{secret};UseSsl={UseSsl}{from}";
        }
        set
        {
            _parse(value);
        }
    }

    public string? Server { get; set; }
    public int? Port { get; set; }
    [Secret]
    public string? Username { get; set; }
    [Secret]
    public string? Password { get; set; }
    public bool UseSsl { get; set; }
    [PersonalData]
    public string? From { get; set; }
}