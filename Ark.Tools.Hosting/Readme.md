# Singleton Background Service

SingletonBackgroundService can be used as a replacement of [BackgroundService](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-6.0&tabs=visual-studio#backgroundservice-base-class) 
in case is required to ensure only a single Instance of the given BackgroundService is running.
This is an alternative to a [Singleton Continous Webjob](https://github.com/projectkudu/kudu/wiki/WebJobs#settingsjob-reference) which is not supported in Azure App Service Linux.

## How to use

The only external dependency of `SingletonBackgroundService` is `IDistributedLockProvider` which has to be provided.

> WARN: choose an implementaion that support HandleLoss detection to guarantee that if one instance _lose_ the Singleton Lock it get's stopped. Otherwise Singleton behaviour is not guaranteed.
> 
> DistributedLock.Azure and DistributedLock.SqlServer are **suggested**

```cs

                .ConfigureServices((ctx,s) =>
                {
                    s.AddSingleton<IDistributedLockProvider>(
                        new AzureBlobLeaseDistributedSynchronizationProvider(
                            new Azure.Storage.Blobs.BlobContainerClient(ctx.Configuration["ConnectionStrings:Storage"],"locks")));
                })
```

Then implement your `BackgroundService` based on `SingletonBackgroundService` overriding `RunAsync` instead of `ExecuteAsync`.

```cs

    internal class MyService : Ark.Tools.Hosting.SingletonBackgroundService
    {

        public MyService(IDistributedLockProvider distributedLockProvider, ILogger<RunForeverThrowsRandomly> logger, string? serviceName = null)
            : base(distributedLockProvider, logger, serviceName)
        {
        }

        protected override async Task RunAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Do stuff
                await Task.Delay(3000);
            }
        }
    }

```

Configure it as it would with a `BackgroundService` using `.AddHostedService<>`.

```cs

                .ConfigureServices((ctx,s) =>
                {
                    s.AddHostedService<MyService>();
                })

```

## Self-healing BackgroundService after Cooldown

`BackgroundService` silently stop when its `ExecuteAsync()` exits. In NET.7 Microsoft added a flag to choose between halt or shutdown the whole Host.

`SingletonBackgroundService` instead behave similary to how Singleton Continuos Webjob in Kudu would: when process exits, it get's restarted after a bit, 
potentially on another instance depending on which one acquires the lock.

If your `BackgroundService` is inerently a Periodic runner, this behaviour can be used to load-balance your instances trying to distribute the Runs across the cluster.
`CoolDown` property defines how much to `Sleep` after RunAsync exits: can be changed in the `RunAsync` or set in the ctor.

```cs

    internal class RunEvery30Sec : Ark.Tools.Hosting.SingletonBackgroundService
    {
        public RunEvery30Sec(IDistributedLockProvider distributedLockProvider, ILogger<RunEvery30Sec> logger, string? serviceName = null) 
            : base(distributedLockProvider, logger, serviceName)
        {
            Cooldown = TimeSpan.FromSeconds(30);
        }

        protected override async Task RunAsync(CancellationToken stoppingToken)
        {
            try
            {                
                _logger.LogInformation(nameof(RunEvery30Sec));
                await Task.Delay(2000, stoppingToken);
                Cooldown = TimeSpan.FromSeconds(60);
            } catch (Exception e)
            {
                Cooldown = TimeSpan.FromSeconds(10);
            }
        }
    }

```

## How to test

Use `Test.SingletonBackgroundService` project running AzureStorageEmulator or Azurite locally. Azurite on Docker is suggested.

> The "locks" container must be created. Use StorageExplorer to create the "locks" container on the local emulator or add some code to make it happen.

Launch multiple instances of the sample and observe how the two registered services behave.

Notice that:
- `RunForeverThrowsRandomly` service only runs on the first instance that was created
- `RunEvery30Sec` sometime migrates from one instance to another
- Kill the console running `RunForeverThrowsRandomly` (via X), it's going to be started on another instance after about 1min, due to Lock Lease expire
- Close the console running `RunForeverThrowsRandomly` using Ctrl+C, it's going to be started on another instance almost immediatly as the clean Shutdown release the Lock

