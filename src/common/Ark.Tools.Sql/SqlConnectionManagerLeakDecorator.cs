
namespace Ark.Tools.Sql;

/// <summary>
/// <see cref="IDbConnectionManager"/> decorator to track connection leaks.
/// </summary>
/// <remarks>
/// Potential leak are printed to System.Diagnostics.Debug output. Search for "Suspected connection leak with origin:" text.
/// </remarks>
/// <example>
/// <code>
/// <![CDATA[
/// // enable SqlConnectionManagerLeakDecorator when debugger is attached
/// #if DEBUG
/// if (Debugger.IsAttached)
///     _container.RegisterDecorator<IDbConnectionManager, SqlConnectionManagerLeakDecorator>();
/// #endif
/// ]]>
/// </code>
/// </example>
public class SqlConnectionManagerLeakDecorator : IDbConnectionManager
{
    private readonly IDbConnectionManager _inner;

    public SqlConnectionManagerLeakDecorator(IDbConnectionManager inner)
    {
        _inner = inner;
    }

    public DbConnection Get(string connectionString)
    {
        var cnn = _inner.Get(connectionString);
#pragma warning disable CA2000 // Dispose objects before losing scope
#pragma warning disable CA1806 // Do not ignore method results
        new ConnectionLeakWatcher(cnn);
#pragma warning restore CA1806 // Do not ignore method results
#pragma warning restore CA2000 // Dispose objects before losing scope
        return cnn;
    }

    public async Task<DbConnection> GetAsync(string connectionString, CancellationToken ctk = default)
    {
        var cnn = await _inner.GetAsync(connectionString, ctk).ConfigureAwait(false);
#pragma warning disable CA2000 // Dispose objects before losing scope
#pragma warning disable CA1806 // Do not ignore method results
        new ConnectionLeakWatcher(cnn);
#pragma warning restore CA1806 // Do not ignore method results
#pragma warning restore CA2000 // Dispose objects before losing scope
        return cnn;
    }

    /// <summary>
    /// This class can help identify db connection leaks (connections that are not closed after use).
    /// Usage:
    /// connection = new SqlConnection(..);
    /// connection.Open()
    /// #if DEBUG
    /// new ConnectionLeakWatcher(connection);
    /// #endif
    /// That's it. Don't store a reference to the watcher. It will make itself available for garbage collection
    /// once it has fulfilled its purpose. Watch the visual studio debug output for details on potentially leaked connections.
    /// Note that a connection could possibly just be taking its time and may eventually be closed properly despite being flagged by this class.
    /// So take the output with a pinch of salt.
    /// </summary>
    public sealed class ConnectionLeakWatcher : IDisposable
    {
        private readonly Timer? _timer;

        //Store reference to connection so we can unsubscribe from state change events
        [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Only used to track leakage. The StateChange is used to trick GC and track proper dispose.")]
        private DbConnection? _connection = null;

        private static int _idCounter;
        private readonly int _connectionId = ++_idCounter;

        public ConnectionLeakWatcher(DbConnection connection)
        {
            _connection = connection;
            StackTrace = Environment.StackTrace;

            connection.StateChange += _connectionOnStateChange;
            System.Diagnostics.Trace.TraceInformation("Connection opened " + _connectionId);

            _timer = new Timer(x =>
            {
                //The timeout expired without the connection being closed. Write to debug output the stack trace of the connection creation to assist in pinpointing the problem
                System.Diagnostics.Trace.TraceError("Suspected connection leak with origin: {0}{1}{0}Connection id: {2}", Environment.NewLine, StackTrace, _connectionId);
                //That's it - we're done. Clean up by calling Dispose.
                Dispose();
            }, null, 30000, Timeout.Infinite);
        }

        private void _connectionOnStateChange(object sender, StateChangeEventArgs stateChangeEventArgs)
        {
            //Connection state changed. Was it closed?
            if (stateChangeEventArgs.CurrentState == ConnectionState.Closed)
            {
                //That's it - we're done. Clean up by calling Dispose.
                Dispose();
            }
        }

        public string StackTrace { get; set; }

        #region Dispose
        private bool _isDisposed;

        public void Dispose()
        {
            if (_isDisposed) return;

            _timer?.Dispose();

            if (_connection != null)
            {
                _connection.StateChange -= _connectionOnStateChange;
                _connection = null;
            }

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

#pragma warning disable MA0055 // Do not use finalizer
        ~ConnectionLeakWatcher()
#pragma warning restore MA0055 // Do not use finalizer
        {
            Dispose();
        }
        #endregion
    }
}