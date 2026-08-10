// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Sample.Tests.Hooks;

/// <summary>Names the phases that order the sample's Reqnroll hooks.</summary>
public static class HooksOrder
{
    /// <summary>Initializes shared Reqnroll infrastructure.</summary>
    public const int TestInfrastructure = -20;

    /// <summary>Creates the SQL database schema.</summary>
    public const int DatabaseSetup = -10;

    /// <summary>Resets SQL data before a scenario.</summary>
    public const int DatabaseReset = -10;

    /// <summary>Creates the scenario application composition.</summary>
    public const int ApplicationSetup = 0;

    /// <summary>Attaches scenario-owned external service mocks.</summary>
    public const int ExternalServiceSetup = 5;

    /// <summary>Starts the scenario Rebus receiver.</summary>
    public const int RebusReceiver = 10;

    /// <summary>Stops Rebus before the application is disposed.</summary>
    public const int RebusCleanup = int.MaxValue - 2;

    /// <summary>Detaches scenario-owned external service mocks.</summary>
    public const int ExternalServiceCleanup = int.MaxValue - 1;

    /// <summary>Disposes the scenario application composition.</summary>
    public const int ApplicationCleanup = int.MaxValue;
}
