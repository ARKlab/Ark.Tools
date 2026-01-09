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
        var split = smtpConnectionString
            .Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Select(x => x.Split('=')
                          .Select(y => y.Trim())
                          .ToArray()
                   )
            .ToArray()
            ;

        if (split.Any(x => x.Length != 2))
            throw new FormatException();

        foreach (var pair in split)
        {
            switch (pair[0].ToUpperInvariant())
            {
                case "SERVER":
                    {
                        this.Server = pair[1];
                        break;
                    }

                case "PORT":
                    {
                        this.Port = Int32.Parse(pair[1], CultureInfo.InvariantCulture);
                        break;
                    }

                case "USERNAME":
                    {
                        this.Username = pair[1];
                        break;
                    }

                case "PASSWORD":
                    {
                        this.Password = pair[1];
                        break;
                    }

                case "USESSL":
                    {
                        this.UseSsl = bool.Parse(pair[1]);
                        break;
                    }

                case "FROM":
                    {
                        this.From = pair[1];
                        break;
                    }
            }
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