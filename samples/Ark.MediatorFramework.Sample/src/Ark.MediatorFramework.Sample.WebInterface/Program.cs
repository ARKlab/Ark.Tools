// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.WebInterface;
using Rebus.Transport.InMem;
using NLog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    var network = new InMemNetwork();
    var container = SampleComposition.BuildContainer(network);
    var startup = SampleHost.Configure(builder, container, network);

    var app = builder.Build();
    startup.Configure(app);

    await app.RunAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    LogManager.GetLogger("Main").Fatal(
        ex,
        CultureInfo.InvariantCulture,
        "Unhandled startup or host failure: {Message}",
        ex.Message);
#pragma warning disable RS0030 // Exception handler - console output for critical failures
    await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false);
#pragma warning restore RS0030
    Environment.ExitCode = 1;
}
finally
{
    LogManager.Flush(TimeSpan.FromSeconds(5));
    LogManager.Shutdown();
}

/// <summary>Entry-point marker so the sample host type is discoverable.</summary>
public sealed partial class Program
{
    private Program()
    {
    }
}
