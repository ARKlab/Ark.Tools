using Rebus.Config;
using Rebus.Time;
using Rebus.Timeouts;

using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Rebus(net10.0)', Before:
namespace Ark.Tools.Rebus.Tests
{

    /// <summary>
    /// Configuration extensions for in-mem timeout manager
    /// </summary>
    public static class InMemoryTimeoutManagerExtensions
    {
        /// <summary>
        /// Configures Rebus to store timeouts in memory. Please note that this is probably not really suitable for production usage,
        /// as deferred messages will be lost in case the endpoint is restarted. Therefore, the in-mem timeout manager is probably
        /// mostly suited best to be used in automated tests.
        /// </summary>
        public static void StoreInMemoryTests(this StandardConfigurer<ITimeoutManager> configurer)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));

            configurer.Register(c => new TestsInMemoryTimeoutManager(c.Get<IRebusTime>()));
        }
=======
namespace Ark.Tools.Rebus.Tests;


/// <summary>
/// Configuration extensions for in-mem timeout manager
/// </summary>
public static class InMemoryTimeoutManagerExtensions
{
    /// <summary>
    /// Configures Rebus to store timeouts in memory. Please note that this is probably not really suitable for production usage,
    /// as deferred messages will be lost in case the endpoint is restarted. Therefore, the in-mem timeout manager is probably
    /// mostly suited best to be used in automated tests.
    /// </summary>
    public static void StoreInMemoryTests(this StandardConfigurer<ITimeoutManager> configurer)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));

        configurer.Register(c => new TestsInMemoryTimeoutManager(c.Get<IRebusTime>()));
>>>>>>> After


namespace Ark.Tools.Rebus.Tests;


/// <summary>
/// Configuration extensions for in-mem timeout manager
/// </summary>
public static class InMemoryTimeoutManagerExtensions
{
    /// <summary>
    /// Configures Rebus to store timeouts in memory. Please note that this is probably not really suitable for production usage,
    /// as deferred messages will be lost in case the endpoint is restarted. Therefore, the in-mem timeout manager is probably
    /// mostly suited best to be used in automated tests.
    /// </summary>
    public static void StoreInMemoryTests(this StandardConfigurer<ITimeoutManager> configurer)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));

        configurer.Register(c => new TestsInMemoryTimeoutManager(c.Get<IRebusTime>()));
    }
}