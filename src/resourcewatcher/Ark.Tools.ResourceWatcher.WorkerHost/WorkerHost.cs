// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;
using Ark.Tools.SimpleInjector;


using NLog;

using NodaTime;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Diagnostics;

namespace Ark.Tools.ResourceWatcher.WorkerHost;


[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "suffix is appropriate here")]
public delegate void VoidEventHandler();

public abstract class WorkerHost
{
    public abstract void Start();
    public abstract void Stop();

    public abstract void RunAndBlock(CancellationToken ctk = default);
}

/// <summary>
/// A host for a classic worker that polls resources from one provider, with state-tracking and processors (outputs).
/// Generic version with type-safe extension data.
/// </summary>
/// <remarks>
/// The work can be summarized in the following pseudo-code:
/// var filter = new TQueryFilter();
/// foreach (var fc in FilterConfigurer) 
///     fc(filter);
/// var resources = provider.GetMetadata(filter);
/// resources = resources.Where(combine(predicates));
/// foreach (var r in resources)
///     var state = state.Get(r);
///     if (state.RetryCount > max) continue;
///     if (state.RetryCount == 0 &amp;&amp; r.Modified == state.Modified) continue;
///     var data = provider.GetFile(r, state.CheckSum);
///     if (data == null) continue; // checksum is same, skip
///     foreach (var w in writers)
///         w.Save(data);
/// </remarks>
/// <typeparam name="TResource">The resource data</typeparam>
/// <typeparam name="TMetadata">The resource metadata listed from a provider</typeparam>
/// <typeparam name="TQueryFilter">The filter that the provider supports</typeparam>
/// <typeparam name="TExtensions">The type of extension data. Use <see cref="VoidExtensions"/> if no extension data is needed.</typeparam>
public class WorkerHost<TResource, TMetadata, TQueryFilter, TExtensions> : WorkerHost
    where TResource : class, IResource<TMetadata, TExtensions>
    where TMetadata : class, IResourceMetadata<TExtensions>
    where TQueryFilter : class, new()
{
    private readonly List<Predicate<TMetadata>> _predicates = new() { };
    private readonly List<Action<TQueryFilter>> _configurers = new() { };

    private Container _container { get; } = new Container();

    [SuppressMessage("Design", "MA0046:Use EventHandler<T> to declare events", Justification = "Historical - Public API - Next Major")]
    private event VoidEventHandler? _onBeforeStart;

    public sealed class Dependencies
    {
        private readonly WorkerHost<TResource, TMetadata, TQueryFilter, TExtensions> _host;

        internal Dependencies(WorkerHost<TResource, TMetadata, TQueryFilter, TExtensions> host)
        {
            _host = host;
        }

        public Container Container => _host._container;

        [SuppressMessage("Design", "MA0046:Use EventHandler<T> to declare events", Justification = "Historical - Public API - Next Major")]
        [SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Historical - Public API - Next Major")]
        [SuppressMessage("Design", "CA1030:Use events where appropriate", Justification = "<Pending>")]
        public event VoidEventHandler OnBeforeStart { add { _host._onBeforeStart += value; } remove { _host._onBeforeStart -= value; } }
    }

    public WorkerHost(IHostConfig config)
    {
        _configureCommonContainer();
        _container.RegisterInstance(config);
    }

    private void _configureCommonContainer()
    {
        _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        _container.AllowResolvingFuncFactories();

        _container.RegisterSingleton<IResourceWatcherConfig, WatcherConfigProxy>();

        _container.RegisterSingleton<Watcher>();
    }

    /// <summary>
    /// Add dependencies
    /// </summary>
    /// <param name="deps">Used to register host dependencies</param>
    public void Use(Action<Dependencies>? deps = null)
    {
        deps?.Invoke(new Dependencies(this));
    }

    /// <summary>
    /// Set the data provider implementation to use
    /// </summary>
    /// <typeparam name="TDataProvider">The data provider</typeparam>
    /// <param name="deps">Used to register provider dependencies</param>
    /// <param name="lifeStyle">The lifeStyle of the DataProvider (Default: Singleton)</param>
    public void UseDataProvider<TDataProvider>(Action<Dependencies>? deps = null, Lifestyle? lifeStyle = null)
        where TDataProvider : class, IResourceProvider<TMetadata, TResource, TQueryFilter, TExtensions>
    {
        this.Use(d =>
        {
            deps?.Invoke(d);
            d.Container.Register<IResourceProvider<TMetadata, TResource, TQueryFilter, TExtensions>, TDataProvider>(lifeStyle ?? Lifestyle.Singleton);
        });
    }

    /// <summary>
    /// Append a processor to the collection. They are executed in order of insertion
    /// </summary>
    /// <typeparam name="TFileProcessor">The processor</typeparam>
    /// <param name="deps">Used to register processor dependencies</param>
    /// <param name="lifeStyle">The lifeStyle of the FileProcessor (Default: Singleton)</param>
    public void AppendFileProcessor<TFileProcessor>(Action<Dependencies>? deps = null, Lifestyle? lifeStyle = null)
        where TFileProcessor : class, IResourceProcessor<TResource, TMetadata, TExtensions>
    {
        this.Use(d =>
        {
            deps?.Invoke(d);
            d.Container.Register<TFileProcessor>(lifeStyle ?? Lifestyle.Singleton);
            d.Container.Collection.Append<IResourceProcessor<TResource, TMetadata, TExtensions>, TFileProcessor>();
        });
    }

    /// <summary>
    /// Set the state provider implementation to use
    /// </summary>
    /// <typeparam name="TStateProvider">The state provider</typeparam>
    /// <param name="deps">Used to register state provider dependencies</param>
    public void UseStateProvider<TStateProvider>(Action<Dependencies>? deps = null)
        where TStateProvider : class, IStateProvider<TExtensions>
    {
        this.Use(d =>
        {
            deps?.Invoke(d);
            d.Container.RegisterSingleton<IStateProvider<TExtensions>, TStateProvider>();
        });
    }

    /// <summary>
    /// Add a filter to the metadata filter chain used to filter the list of metadata listed before processing
    /// </summary>
    /// <param name="filter">The predicate to apply</param>
    public void AddMetadataFilter(Predicate<TMetadata> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        _predicates.Add(filter);
    }

    /// <summary>
    /// Add a filter to the metadata filter chain used to filter the list of metadata listed before processing
    /// </summary>
    /// <param name="configurer">The predicate to apply</param>
    public void AddProviderFilterConfigurer(Action<TQueryFilter> configurer)
    {
        ArgumentNullException.ThrowIfNull(configurer);

        _configurers.Add(configurer);
    }

    /// <summary>
    /// Exec a single run with additional configurer for the provider filter.
    /// </summary>
    /// <param name="filterConfigurer">Additional filter configurer to apply as last</param>
    /// <param name="ctk">Cancellation token</param>
    /// <returns></returns>
    public async Task RunOnceAsync(Action<TQueryFilter>? filterConfigurer = null, CancellationToken ctk = default)
    {
        _onInit();

        await _container.GetInstance<Watcher>().RunOnce(filterConfigurer, ctk).ConfigureAwait(false);
    }

    public override void Start()
    {
        _onInit();

        _container.GetInstance<Watcher>().Start();
    }

    public override void Stop()
    {
        _container.GetInstance<Watcher>().Stop();
    }

    public override void RunAndBlock(CancellationToken ctk = default)
    {
        Start();

        ctk.WaitHandle.WaitOne();

        Stop();
    }

    /// <summary>
    /// Called only once, before the first run
    /// </summary>
    protected virtual void _onInit()
    {
        if (_container.IsLocked) return;

        _container.Collection.Register<Predicate<TMetadata>>(_predicates);
        _container.Collection.Register<Action<TQueryFilter>>(_configurers);

        _container.Verify();

        _onBeforeStart?.Invoke();
    }

    sealed class WatcherConfigProxy : IResourceWatcherConfig
    {
        private readonly IHostConfig _config;

        public WatcherConfigProxy(IHostConfig config)
        {
            _config = config;
        }

        public string Tenant => _config.WorkerName;
        public int SleepSeconds => (int)_config.Sleep.TotalSeconds;
        public int MaxRetries => (int)_config.MaxRetries;
        public uint DegreeOfParallelism => _config.DegreeOfParallelism;
        public bool IgnoreState => _config.IgnoreState;
        public uint? SkipResourcesOlderThanDays => _config.SkipResourcesOlderThanDays;
        public Duration BanDuration => _config.BanDuration;
        public TimeSpan RunDurationNotificationLimit => _config.RunDurationNotificationLimit;
        public TimeSpan ResourceDurationNotificationLimit => _config.ResourceDurationNotificationLimit;
    }

    sealed class Watcher : ResourceWatcher<TResource, TExtensions>
    {
        private readonly IEnumerable<Action<TQueryFilter>> _filterChainBuilder;
        private readonly IEnumerable<Predicate<TMetadata>> _metadataFilterChain;
        private readonly Container _container;
        private Action<TQueryFilter>? _filter;

        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        public Watcher(
              Container container
            , IEnumerable<Action<TQueryFilter>> filterChainBuilder
            , IEnumerable<Predicate<TMetadata>> metadataFilterChain
            )
            : base(container.GetInstance<IResourceWatcherConfig>(), container.GetInstance<IStateProvider<TExtensions>>())
        {
            _filterChainBuilder = filterChainBuilder;
            _metadataFilterChain = metadataFilterChain;
            _container = container;
        }

        public async Task RunOnce(Action<TQueryFilter>? filter, CancellationToken ctk = default)
        {
            _filter = filter;
            try
            {
                await RunOnce(ctk).ConfigureAwait(false);
            }
            finally
            {
                _filter = null;
            }
        }

        protected override async Task _runOnce(RunType runType, CancellationToken ctk = default)
        {
            var scope = AsyncScopedLifestyle.BeginScope(_container);
            await using (scope.ConfigureAwait(false))
            {
                await base._runOnce(runType, ctk).ConfigureAwait(false);
            }
        }

        protected override async Task<IEnumerable<IResourceMetadata<TExtensions>>> _getResourcesInfo(CancellationToken ctk = default)
        {
            var filter = _buildFilter();
            var meta = await _container.GetInstance<IResourceProvider<TMetadata, TResource, TQueryFilter, TExtensions>>().GetMetadata(filter, ctk).ConfigureAwait(false);

            InvalidOperationException.ThrowIf(meta.Any(x => x.Modified == default && (x.ModifiedSources == null || x.ModifiedSources.Count == 0)), "At least one field between Modified and ModifiedSources must be populated");

            foreach (var p in _metadataFilterChain)
                meta = meta.Where(m => p(m));

            return meta;
        }

        private TQueryFilter _buildFilter()
        {
            var f = new TQueryFilter();
            foreach (var b in _filterChainBuilder)
                b(f);

            _filter?.Invoke(f);

            return f;
        }

        protected override async Task _processResource(ChangedStateContext<TResource, TExtensions> context, CancellationToken ctk = default)
        {
            var sw = Stopwatch.StartNew();
            var data = await context.Payload.ConfigureAwait(false);

            if (data != null)
            {
                _logger.Info(CultureInfo.InvariantCulture, "Retrived ResourceId={ResourceId} in {Elapsed}", context.Info.ResourceId, sw.Elapsed);

                foreach (var w in _container.GetAllInstances<IResourceProcessor<TResource, TMetadata, TExtensions>>())
                {
                    sw.Restart();
                    await w.Process(data, ctk).ConfigureAwait(false);
                    _logger.Info(CultureInfo.InvariantCulture, "Processed ResourceId={ResourceId} with {Name} in {Elapsed}", context.Info.ResourceId, w.GetType().Name, sw.Elapsed);
                }
            }
            else
            {
                _logger.Info(CultureInfo.InvariantCulture, "Retrived ResourceId={ResourceId} in {Elapsed} but is NULL, so nothing to do", context.Info.ResourceId, sw.Elapsed);
            }
        }

        protected override Task<TResource?> _retrievePayload(IResourceMetadata<TExtensions> info, IResourceTrackedState<TExtensions>? lastState, CancellationToken ctk = default)
        {
            return _container.GetInstance<IResourceProvider<TMetadata, TResource, TQueryFilter, TExtensions>>().GetResource((TMetadata)info, lastState, ctk);
        }
    }
}

/// <summary>
/// Non-generic proxy class for backward compatibility.
/// Inherits from <see cref="WorkerHost{TResource, TMetadata, TQueryFilter, TExtensions}"/> with <see cref="VoidExtensions"/>.
/// </summary>
/// <typeparam name="TResource">The resource data</typeparam>
/// <typeparam name="TMetadata">The resource metadata listed from a provider</typeparam>
/// <typeparam name="TQueryFilter">The filter that the provider supports</typeparam>
public class WorkerHost<TResource, TMetadata, TQueryFilter> : WorkerHost<TResource, TMetadata, TQueryFilter, VoidExtensions>
    where TResource : class, IResource<TMetadata>
    where TMetadata : class, IResourceMetadata
    where TQueryFilter : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerHost{TResource, TMetadata, TQueryFilter}"/> class.
    /// </summary>
    /// <param name="config">The host configuration</param>
    public WorkerHost(IHostConfig config) : base(config)
    {
    }
}