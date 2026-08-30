// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Continuation-based incoming messaging step.</summary>
public interface IMessagingIncomingStep
{
    /// <summary>Processes the context and invokes the remaining pipeline.</summary>
    /// <param name="context">The incoming message context.</param>
    /// <param name="next">The remaining pipeline.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    Task ProcessAsync(MessagingIncomingContext context, Func<Task> next, CancellationToken cancellationToken);
}

/// <summary>Continuation-based outgoing messaging step.</summary>
public interface IMessagingOutgoingStep
{
    /// <summary>Processes the context and invokes the remaining pipeline.</summary>
    /// <param name="context">The outgoing message context.</param>
    /// <param name="next">The remaining pipeline.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    Task ProcessAsync(MessagingOutgoingContext context, Func<Task> next, CancellationToken cancellationToken);
}

/// <summary>Provides framework-owned mutation of reserved outgoing headers.</summary>
internal interface IMessagingFrameworkHeaders
{
    /// <summary>Sets a reserved framework header.</summary>
    /// <param name="key">The reserved header name.</param>
    /// <param name="value">The header value.</param>
    void SetReserved(string key, string value);

    /// <summary>Removes a reserved framework header.</summary>
    /// <param name="key">The reserved header name.</param>
    /// <returns><see langword="true"/> when the header was present.</returns>
    bool RemoveReserved(string key);
}

/// <summary>Per-delivery incoming context.</summary>
public sealed class MessagingIncomingContext
{
    /// <summary>Creates an incoming context.</summary>
    public MessagingIncomingContext(IReadOnlyDictionary<string, string> headers)
        : this(headers, 0, default)
    {
    }

    /// <summary>Creates an incoming context with delivery metadata.</summary>
    public MessagingIncomingContext(
        IReadOnlyDictionary<string, string> headers,
        int deliveryCount = 0,
        CancellationToken cancellationToken = default)
    {
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        ArgumentOutOfRangeException.ThrowIfNegative(deliveryCount);
        DeliveryCount = deliveryCount;
        CancellationToken = cancellationToken;
    }

    /// <summary>Gets received headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Gets the native delivery count, or zero when not supplied by a host.</summary>
    public int DeliveryCount { get; }

    /// <summary>Gets the cancellation token for this delivery.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Gets step-local state.</summary>
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
}

/// <summary>Per-send outgoing context.</summary>
public sealed class MessagingOutgoingContext
{
    /// <summary>Creates an outgoing context.</summary>
    public MessagingOutgoingContext(
        IDictionary<string, string> headers,
        string destination)
    {
        ArgumentNullException.ThrowIfNull(headers);
        Headers = new MessagingOutgoingHeaders(headers);
        ArgumentException.ThrowIfNullOrEmpty(destination);
        Destination = destination;
    }

    /// <summary>Gets mutable, validated headers.</summary>
    public IDictionary<string, string> Headers { get; }

    internal void _setReservedHeader(string key, string value)
    {
        ((IMessagingFrameworkHeaders)Headers).SetReserved(key, value);
    }

    /// <summary>Gets the resolved destination.</summary>
    public string Destination { get; }

    /// <summary>Gets step-local state.</summary>
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

    private sealed class MessagingOutgoingHeaders : IDictionary<string, string>, IMessagingFrameworkHeaders
    {
        private readonly IDictionary<string, string> _inner;

        public MessagingOutgoingHeaders(IDictionary<string, string> inner) => _inner = inner;
        public void SetReserved(string key, string value)
        {
            if (!MessagingHeadersGuard.IsReserved(key))
                throw new ArgumentException("Only reserved headers can use the framework mutation path.", nameof(key));
            _inner[key] = value;
        }
        public bool RemoveReserved(string key)
        {
            if (!MessagingHeadersGuard.IsReserved(key))
                throw new ArgumentException("Only reserved headers can use the framework mutation path.", nameof(key));
            return _inner.Remove(key);
        }
        public string this[string key]
        {
            get => _inner[key];
            set { MessagingHeadersGuard.ThrowIfReserved(key); _inner[key] = value; }
        }
        public ICollection<string> Keys => _inner.Keys;
        public ICollection<string> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool IsReadOnly => false;
        public void Add(string key, string value) { MessagingHeadersGuard.ThrowIfReserved(key); _inner.Add(key, value); }
        public void Add(KeyValuePair<string, string> item) => Add(item.Key, item.Value);
        public void Clear()
        {
            if (_inner.Keys.Any(MessagingHeadersGuard.IsReserved))
                throw new InvalidOperationException("Reserved messaging headers cannot be removed.");
            _inner.Clear();
        }
        public bool Contains(KeyValuePair<string, string> item) => _inner.Contains(item);
        public bool ContainsKey(string key) => _inner.ContainsKey(key);
        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _inner.GetEnumerator();
        public bool Remove(string key) { MessagingHeadersGuard.ThrowIfReserved(key); return _inner.Remove(key); }
        public bool Remove(KeyValuePair<string, string> item) { MessagingHeadersGuard.ThrowIfReserved(item.Key); return _inner.Remove(item); }
        public bool TryGetValue(string key, out string value) => _inner.TryGetValue(key, out value!);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>Stable positions in the messaging pipeline.</summary>
public enum MessagingPipelineStage
{
    /// <summary>Before incoming payload deserialization.</summary>
    BeforeDeserialize,
    /// <summary>After incoming payload deserialization.</summary>
    AfterDeserialize,
    /// <summary>After handler dispatch.</summary>
    AfterDispatch,
    /// <summary>Before outgoing serialization.</summary>
    BeforeSerialize,
    /// <summary>Before transport send.</summary>
    BeforeSend,
    /// <summary>After incoming settlement.</summary>
    AfterSettlement
}
