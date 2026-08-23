// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

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

/// <summary>Per-delivery incoming context.</summary>
public sealed class MessagingIncomingContext
{
    /// <summary>Creates an incoming context.</summary>
    public MessagingIncomingContext(
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload)
    {
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        Payload = payload;
    }

    /// <summary>Gets received headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Gets the prepared payload.</summary>
    public ReadOnlySequence<byte> Payload { get; }

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

    /// <summary>Gets the resolved destination.</summary>
    public string Destination { get; }

    /// <summary>Gets or sets the serialized payload.</summary>
    public ReadOnlySequence<byte>? Payload { get; set; }

    /// <summary>Gets step-local state.</summary>
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

    private sealed class MessagingOutgoingHeaders : IDictionary<string, string>
    {
        private readonly IDictionary<string, string> _inner;

        public MessagingOutgoingHeaders(IDictionary<string, string> inner) => _inner = inner;
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
