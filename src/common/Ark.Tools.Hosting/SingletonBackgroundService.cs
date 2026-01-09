using Medallion.Threading;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Hosting(net10.0)', Before:
namespace Ark.Tools.Hosting
{
    internal static partial class LogMessages
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Service<{ServiceName}> is trying to acquire Lock<{LockId}>")]
        public static partial void TryingToAcquireLock(this ILogger logger, string serviceName, Guid lockId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Service<{ServiceName}> has acquired Lock<{LockId}>: executing RunAsync")]
        public static partial void AcquiredLock(this ILogger logger, string serviceName, Guid lockId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Service<{ServiceName}>.RunAsync() exited with exception.")]
        public static partial void RunAsyncExited(this ILogger logger, Exception exception, string serviceName);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Service<{ServiceName}> cooldown for {Cooldown}")]
        public static partial void Cooldown(this ILogger logger, string serviceName, TimeSpan cooldown);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Service<{ServiceName}> releasing Lock<{LockId}>")]
        public static partial void ReleasingLock(this ILogger logger, string serviceName, Guid lockId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Service<{ServiceName}> failed while Acquiring or Disposing Lock<{LockId}>.")]
        public static partial void FailedAcquiringOrDisposing(this ILogger logger, Exception exception, string serviceName, Guid lockId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Service<{ServiceName}> stopped.")]
        public static partial void Stopped(this ILogger logger, string serviceName);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Service<{ServiceName}> does not support Lock loss detection. This means Singleton behaviour cannot be guaranteed. Please use a IDistributedLock implementation that support HandleLost detection.")]
        public static partial void NoHandleLossSupport(this ILogger logger, string serviceName);
    }
    /// <summary>
    /// Base class for implementing a long running <see cref="IHostedService"/> which is Singleton via DistributedLock.
    /// </summary>
    public abstract class SingletonBackgroundService : BackgroundService
    {
        private readonly IDistributedLock _lock;
        private bool _hasWarnedForHandleLoss;
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable CA1707 // Identifiers should not contain underscores
        protected ILogger<SingletonBackgroundService> _logger { get; private set; }
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore IDE1006 // Naming Styles

        /// <summary>
        /// Unique identifier of the Service. Used to compute the Singleton Lock name.
        /// </summary>
        /// <remarks>
        /// This is to be unique across the different 'Service' that use the same <see cref="IDistributedLock"/> location.
        /// </remarks>
        public string ServiceName { get; }

        /// <summary>
        /// Cooldown between <see cref="RunAsync(CancellationToken)"/> return and next lease acquire time. (default: 60sec)
        /// </summary>
        /// <remarks>
        /// When <see cref="RunAsync(CancellationToken)"/> returns after each 'Run', this is the sleep time between runs.
        /// When <see cref="RunAsync(CancellationToken)"/> returns after has been Canceled due to lease-loss, this is the sleep before this instance tries to re-acquire the lease.
        /// </remarks>
        public TimeSpan Cooldown { get; protected set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Guid MD5-derived from <see cref="ServiceName"/>
        /// </summary>
        public Guid LockId { get; }


        /// <summary>
        /// Singleton version of the <see cref="BackgroundService"/> based on Distributed Locks.
        /// </summary>
        /// <param name="distributedLockProvider">Used to create the Lock. Prefer an implementation which support Handle-loss detection.</param>
        /// <param name="logger"></param>
        /// <param name="serviceName">Unique name across the <see cref="IDistributedLockProvider"/> scope. (default: GetType().FullName)</param>
        protected SingletonBackgroundService(IDistributedLockProvider distributedLockProvider, ILogger<SingletonBackgroundService> logger, string? serviceName = null)
        {
            ServiceName = serviceName ?? this.GetType().FullName!;
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms | used only for identifier hash
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(ServiceName));
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
            LockId = new Guid(hash);

            _lock = distributedLockProvider.CreateLock(LockId.ToString("N"));
            _logger = logger;
        }

        /// <summary>
        /// This method is called when the <see cref="BackgroundService"/> successully obtain the Singleton lease/lock.
        /// The implementation should respect the CancellationToken: when Lease is lost, the token in cancelled to signal it.
        /// </summary>
        /// <param name="stoppingToken">Triggered when SingletonLock is lost or <see cref="IHostedService.StopAsync(CancellationToken)"/> is called.</param>
        /// <returns>A <see cref="Task"/> that represents the long running operations.</returns>
        /// <remarks>
        /// Differently from <see cref="BackgroundService"/>, if <see cref="RunAsync(CancellationToken)"/> exits, it may be re-called if/when the Singleton lease is re-obtained.
        /// </remarks>
        protected abstract Task RunAsync(CancellationToken stoppingToken);

        protected override sealed async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.TryingToAcquireLock(ServiceName, LockId);
                    var handle = await _lock.AcquireAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
                    await using (handle.ConfigureAwait(false))
                    {
                        _checkHandleLossSupport(handle);

                        _logger.AcquiredLock(ServiceName, LockId);

                        using var ct = CancellationTokenSource.CreateLinkedTokenSource(handle.HandleLostToken, stoppingToken);
                        try
                        {
                            await RunAsync(ct.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        { }
                        catch (Exception e)
                        {
                            _logger.RunAsyncExited(e, ServiceName);
                        }

                        // we want to keep the lock, if we still have it, while cooling down in case of RunAsync returns or throws
                        // this is required to ensure the cooldown is respected across all Instances as in "one Run every X minutes"
                        // otherwise as soon as we release the Lock another instance would Run
                        if (!ct.IsCancellationRequested)
                        {
                            _logger.Cooldown(ServiceName, Cooldown);
                            try
                            {
                                await Task.Delay(Cooldown, ct.Token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) when (ct.IsCancellationRequested)
                            { }
                        }

                        _logger.ReleasingLock(ServiceName, LockId);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception e) // either the ExecuteAsync failed or the AcquireAsync failed or its disposal (strange)
                {
                    // We want to try as much as possible to keep this Service running on an instance.
                    // Log error, sleep a bit, and restart.
                    _logger.FailedAcquiringOrDisposing(e, ServiceName, LockId);

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.Cooldown(ServiceName, Cooldown);
                        try
                        {
                            await Task.Delay(Cooldown, stoppingToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        { }
                    }
                }
            }

            _logger.Stopped(ServiceName);
        }

        private void _checkHandleLossSupport(IDistributedSynchronizationHandle handle)
        {
            if (!handle.HandleLostToken.CanBeCanceled && !_hasWarnedForHandleLoss)
            {
                _logger.NoHandleLossSupport(ServiceName);
                _hasWarnedForHandleLoss = true;
            }
=======
namespace Ark.Tools.Hosting;

internal static partial class LogMessages
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Service<{ServiceName}> is trying to acquire Lock<{LockId}>")]
    public static partial void TryingToAcquireLock(this ILogger logger, string serviceName, Guid lockId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Service<{ServiceName}> has acquired Lock<{LockId}>: executing RunAsync")]
    public static partial void AcquiredLock(this ILogger logger, string serviceName, Guid lockId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service<{ServiceName}>.RunAsync() exited with exception.")]
    public static partial void RunAsyncExited(this ILogger logger, Exception exception, string serviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Service<{ServiceName}> cooldown for {Cooldown}")]
    public static partial void Cooldown(this ILogger logger, string serviceName, TimeSpan cooldown);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Service<{ServiceName}> releasing Lock<{LockId}>")]
    public static partial void ReleasingLock(this ILogger logger, string serviceName, Guid lockId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service<{ServiceName}> failed while Acquiring or Disposing Lock<{LockId}>.")]
    public static partial void FailedAcquiringOrDisposing(this ILogger logger, Exception exception, string serviceName, Guid lockId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Service<{ServiceName}> stopped.")]
    public static partial void Stopped(this ILogger logger, string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Service<{ServiceName}> does not support Lock loss detection. This means Singleton behaviour cannot be guaranteed. Please use a IDistributedLock implementation that support HandleLost detection.")]
    public static partial void NoHandleLossSupport(this ILogger logger, string serviceName);
}
/// <summary>
/// Base class for implementing a long running <see cref="IHostedService"/> which is Singleton via DistributedLock.
/// </summary>
public abstract class SingletonBackgroundService : BackgroundService
{
    private readonly IDistributedLock _lock;
    private bool _hasWarnedForHandleLoss;
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable CA1707 // Identifiers should not contain underscores
    protected ILogger<SingletonBackgroundService> _logger { get; private set; }
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore IDE1006 // Naming Styles

    /// <summary>
    /// Unique identifier of the Service. Used to compute the Singleton Lock name.
    /// </summary>
    /// <remarks>
    /// This is to be unique across the different 'Service' that use the same <see cref="IDistributedLock"/> location.
    /// </remarks>
    public string ServiceName { get; }

    /// <summary>
    /// Cooldown between <see cref="RunAsync(CancellationToken)"/> return and next lease acquire time. (default: 60sec)
    /// </summary>
    /// <remarks>
    /// When <see cref="RunAsync(CancellationToken)"/> returns after each 'Run', this is the sleep time between runs.
    /// When <see cref="RunAsync(CancellationToken)"/> returns after has been Canceled due to lease-loss, this is the sleep before this instance tries to re-acquire the lease.
    /// </remarks>
    public TimeSpan Cooldown { get; protected set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Guid MD5-derived from <see cref="ServiceName"/>
    /// </summary>
    public Guid LockId { get; }


    /// <summary>
    /// Singleton version of the <see cref="BackgroundService"/> based on Distributed Locks.
    /// </summary>
    /// <param name="distributedLockProvider">Used to create the Lock. Prefer an implementation which support Handle-loss detection.</param>
    /// <param name="logger"></param>
    /// <param name="serviceName">Unique name across the <see cref="IDistributedLockProvider"/> scope. (default: GetType().FullName)</param>
    protected SingletonBackgroundService(IDistributedLockProvider distributedLockProvider, ILogger<SingletonBackgroundService> logger, string? serviceName = null)
    {
        ServiceName = serviceName ?? this.GetType().FullName!;
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms | used only for identifier hash
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(ServiceName));
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
        LockId = new Guid(hash);

        _lock = distributedLockProvider.CreateLock(LockId.ToString("N"));
        _logger = logger;
    }

    /// <summary>
    /// This method is called when the <see cref="BackgroundService"/> successully obtain the Singleton lease/lock.
    /// The implementation should respect the CancellationToken: when Lease is lost, the token in cancelled to signal it.
    /// </summary>
    /// <param name="stoppingToken">Triggered when SingletonLock is lost or <see cref="IHostedService.StopAsync(CancellationToken)"/> is called.</param>
    /// <returns>A <see cref="Task"/> that represents the long running operations.</returns>
    /// <remarks>
    /// Differently from <see cref="BackgroundService"/>, if <see cref="RunAsync(CancellationToken)"/> exits, it may be re-called if/when the Singleton lease is re-obtained.
    /// </remarks>
    protected abstract Task RunAsync(CancellationToken stoppingToken);

    protected override sealed async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.TryingToAcquireLock(ServiceName, LockId);
                var handle = await _lock.AcquireAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
                await using (handle.ConfigureAwait(false))
                {
                    _checkHandleLossSupport(handle);

                    _logger.AcquiredLock(ServiceName, LockId);

                    using var ct = CancellationTokenSource.CreateLinkedTokenSource(handle.HandleLostToken, stoppingToken);
                    try
                    {
                        await RunAsync(ct.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    { }
                    catch (Exception e)
                    {
                        _logger.RunAsyncExited(e, ServiceName);
                    }

                    // we want to keep the lock, if we still have it, while cooling down in case of RunAsync returns or throws
                    // this is required to ensure the cooldown is respected across all Instances as in "one Run every X minutes"
                    // otherwise as soon as we release the Lock another instance would Run
                    if (!ct.IsCancellationRequested)
                    {
                        _logger.Cooldown(ServiceName, Cooldown);
                        try
                        {
                            await Task.Delay(Cooldown, ct.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        { }
                    }

                    _logger.ReleasingLock(ServiceName, LockId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception e) // either the ExecuteAsync failed or the AcquireAsync failed or its disposal (strange)
            {
                // We want to try as much as possible to keep this Service running on an instance.
                // Log error, sleep a bit, and restart.
                _logger.FailedAcquiringOrDisposing(e, ServiceName, LockId);

                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.Cooldown(ServiceName, Cooldown);
                    try
                    {
                        await Task.Delay(Cooldown, stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    { }
                }
            }
        }

        _logger.Stopped(ServiceName);
    }

    private void _checkHandleLossSupport(IDistributedSynchronizationHandle handle)
    {
        if (!handle.HandleLostToken.CanBeCanceled && !_hasWarnedForHandleLoss)
        {
            _logger.NoHandleLossSupport(ServiceName);
            _hasWarnedForHandleLoss = true;
>>>>>>> After


namespace Ark.Tools.Hosting;

internal static partial class LogMessages
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Service<{ServiceName}> is trying to acquire Lock<{LockId}>")]
    public static partial void TryingToAcquireLock(this ILogger logger, string serviceName, Guid lockId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Service<{ServiceName}> has acquired Lock<{LockId}>: executing RunAsync")]
    public static partial void AcquiredLock(this ILogger logger, string serviceName, Guid lockId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service<{ServiceName}>.RunAsync() exited with exception.")]
    public static partial void RunAsyncExited(this ILogger logger, Exception exception, string serviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Service<{ServiceName}> cooldown for {Cooldown}")]
    public static partial void Cooldown(this ILogger logger, string serviceName, TimeSpan cooldown);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Service<{ServiceName}> releasing Lock<{LockId}>")]
    public static partial void ReleasingLock(this ILogger logger, string serviceName, Guid lockId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service<{ServiceName}> failed while Acquiring or Disposing Lock<{LockId}>.")]
    public static partial void FailedAcquiringOrDisposing(this ILogger logger, Exception exception, string serviceName, Guid lockId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Service<{ServiceName}> stopped.")]
    public static partial void Stopped(this ILogger logger, string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Service<{ServiceName}> does not support Lock loss detection. This means Singleton behaviour cannot be guaranteed. Please use a IDistributedLock implementation that support HandleLost detection.")]
    public static partial void NoHandleLossSupport(this ILogger logger, string serviceName);
}
/// <summary>
/// Base class for implementing a long running <see cref="IHostedService"/> which is Singleton via DistributedLock.
/// </summary>
public abstract class SingletonBackgroundService : BackgroundService
{
    private readonly IDistributedLock _lock;
    private bool _hasWarnedForHandleLoss;
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable CA1707 // Identifiers should not contain underscores
    protected ILogger<SingletonBackgroundService> _logger { get; private set; }
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore IDE1006 // Naming Styles

    /// <summary>
    /// Unique identifier of the Service. Used to compute the Singleton Lock name.
    /// </summary>
    /// <remarks>
    /// This is to be unique across the different 'Service' that use the same <see cref="IDistributedLock"/> location.
    /// </remarks>
    public string ServiceName { get; }

    /// <summary>
    /// Cooldown between <see cref="RunAsync(CancellationToken)"/> return and next lease acquire time. (default: 60sec)
    /// </summary>
    /// <remarks>
    /// When <see cref="RunAsync(CancellationToken)"/> returns after each 'Run', this is the sleep time between runs.
    /// When <see cref="RunAsync(CancellationToken)"/> returns after has been Canceled due to lease-loss, this is the sleep before this instance tries to re-acquire the lease.
    /// </remarks>
    public TimeSpan Cooldown { get; protected set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Guid MD5-derived from <see cref="ServiceName"/>
    /// </summary>
    public Guid LockId { get; }


    /// <summary>
    /// Singleton version of the <see cref="BackgroundService"/> based on Distributed Locks.
    /// </summary>
    /// <param name="distributedLockProvider">Used to create the Lock. Prefer an implementation which support Handle-loss detection.</param>
    /// <param name="logger"></param>
    /// <param name="serviceName">Unique name across the <see cref="IDistributedLockProvider"/> scope. (default: GetType().FullName)</param>
    protected SingletonBackgroundService(IDistributedLockProvider distributedLockProvider, ILogger<SingletonBackgroundService> logger, string? serviceName = null)
    {
        ServiceName = serviceName ?? this.GetType().FullName!;
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms | used only for identifier hash
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(ServiceName));
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
        LockId = new Guid(hash);

        _lock = distributedLockProvider.CreateLock(LockId.ToString("N"));
        _logger = logger;
    }

    /// <summary>
    /// This method is called when the <see cref="BackgroundService"/> successully obtain the Singleton lease/lock.
    /// The implementation should respect the CancellationToken: when Lease is lost, the token in cancelled to signal it.
    /// </summary>
    /// <param name="stoppingToken">Triggered when SingletonLock is lost or <see cref="IHostedService.StopAsync(CancellationToken)"/> is called.</param>
    /// <returns>A <see cref="Task"/> that represents the long running operations.</returns>
    /// <remarks>
    /// Differently from <see cref="BackgroundService"/>, if <see cref="RunAsync(CancellationToken)"/> exits, it may be re-called if/when the Singleton lease is re-obtained.
    /// </remarks>
    protected abstract Task RunAsync(CancellationToken stoppingToken);

    protected override sealed async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.TryingToAcquireLock(ServiceName, LockId);
                var handle = await _lock.AcquireAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
                await using (handle.ConfigureAwait(false))
                {
                    _checkHandleLossSupport(handle);

                    _logger.AcquiredLock(ServiceName, LockId);

                    using var ct = CancellationTokenSource.CreateLinkedTokenSource(handle.HandleLostToken, stoppingToken);
                    try
                    {
                        await RunAsync(ct.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    { }
                    catch (Exception e)
                    {
                        _logger.RunAsyncExited(e, ServiceName);
                    }

                    // we want to keep the lock, if we still have it, while cooling down in case of RunAsync returns or throws
                    // this is required to ensure the cooldown is respected across all Instances as in "one Run every X minutes"
                    // otherwise as soon as we release the Lock another instance would Run
                    if (!ct.IsCancellationRequested)
                    {
                        _logger.Cooldown(ServiceName, Cooldown);
                        try
                        {
                            await Task.Delay(Cooldown, ct.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        { }
                    }

                    _logger.ReleasingLock(ServiceName, LockId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception e) // either the ExecuteAsync failed or the AcquireAsync failed or its disposal (strange)
            {
                // We want to try as much as possible to keep this Service running on an instance.
                // Log error, sleep a bit, and restart.
                _logger.FailedAcquiringOrDisposing(e, ServiceName, LockId);

                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.Cooldown(ServiceName, Cooldown);
                    try
                    {
                        await Task.Delay(Cooldown, stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    { }
                }
            }
        }

        _logger.Stopped(ServiceName);
    }

    private void _checkHandleLossSupport(IDistributedSynchronizationHandle handle)
    {
        if (!handle.HandleLostToken.CanBeCanceled && !_hasWarnedForHandleLoss)
        {
            _logger.NoHandleLossSupport(ServiceName);
            _hasWarnedForHandleLoss = true;
        }
    }
}