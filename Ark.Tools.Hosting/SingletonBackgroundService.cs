using Medallion.Threading;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Hosting
{
    /// <summary>
    /// Base class for implementing a long running <see cref="IHostedService"/> which is Singleton via DistributedLock.
    /// </summary>
    public abstract class SingletonBackgroundService : BackgroundService
    {
        private readonly IDistributedLock _lock;
        private bool _hasWarnedForHandleLoss;
#pragma warning disable IDE1006 // Naming Styles
        protected ILogger<SingletonBackgroundService> _logger { get; private set; }
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
            using var md5 = MD5.Create();
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(ServiceName));
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

        protected override sealed async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogDebug("Service<{ServiceName}> is trying to acquire Lock<{LockId}>", ServiceName, LockId);
                    await using var handle = await _lock.AcquireAsync(cancellationToken: cancellationToken);
                    _checkHandleLossSupport(handle);

                    _logger.LogInformation("Service<{ServiceName}> has acquired Lock<{LockId}>: executing " + nameof(RunAsync), ServiceName, LockId);

                    using var ct = CancellationTokenSource.CreateLinkedTokenSource(handle.HandleLostToken, cancellationToken);                    
                    try
                    {
                        await RunAsync(ct.Token);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) 
                    {}
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Service<{ServiceName}>.RunAsync() exited with exception.", ServiceName);
                    }

                    // we want to keep the lock, if we still have it, while cooling down in case of RunAsync returns or throws
                    // this is required to ensure the cooldown is respected across all Instances as in "one Run every X minutes"
                    // otherwise as soon as we release the Lock another instance would Run
                    if (!ct.IsCancellationRequested)
                    {
                        _logger.LogDebug("Service<{ServiceName}> cooldown for {Cooldown}", ServiceName, Cooldown);
                        try
                        {
                            await Task.Delay(Cooldown, ct.Token);
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        { }
                    }

                    _logger.LogDebug("Service<{0}> releasing Lock<{1}>", ServiceName, LockId);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
                catch (Exception e) // either the ExecuteAsync failed or the AcquireAsync failed or its disposal (strange)
                {
                    // We want to try as much as possible to keep this Service running on an instance.
                    // Log error, sleep a bit, and restart.
                    _logger.LogError(e, "Service<{ServiceName}> failed while Acquiring or Disposing Lock<{LockId}>.", ServiceName, LockId);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogDebug("Service<{ServiceName}> cooldown for {Cooldown}", ServiceName, Cooldown);
                        try
                        {
                            await Task.Delay(Cooldown, cancellationToken);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        { }
                    }
                }
            }

            _logger.LogDebug("Service<{ServiceName}> stopped.", ServiceName);
        }

        private void _checkHandleLossSupport(IDistributedSynchronizationHandle handle)
        {
            if (!handle.HandleLostToken.CanBeCanceled && !_hasWarnedForHandleLoss)
            {
                _logger.LogWarning("Service<{ServiceName}> does not support Lock loss detection. This means Singleton behaviour cannot be guaranteed. Please use a IDistributedLock implementation that support HandleLost detection.", ServiceName);
                _hasWarnedForHandleLoss = true;
            }
        }
    }
}