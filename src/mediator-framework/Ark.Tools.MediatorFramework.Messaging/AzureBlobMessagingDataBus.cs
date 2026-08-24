// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Security.Cryptography;

using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Azure Blob implementation of the shared DataBus provider contract.</summary>
public sealed class AzureBlobMessagingDataBus : IMessagingDataBus
{
    private const string _lengthMetadataName = "amf1_length";
    private const string _sha256MetadataName = "amf1_sha256";

    private readonly BlobContainerClient _container;
    private readonly AzureBlobDataBusOptions _options;

    /// <summary>
    /// Creates an Azure Blob DataBus provider from host configuration.
    /// </summary>
    /// <param name="options">The provider options.</param>
    public AzureBlobMessagingDataBus(AzureBlobDataBusOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = _validateOptions(options);
        _container = _createContainer(_options);
    }

    /// <summary>Gets the configured minimum attachment lifetime.</summary>
    public TimeSpan MinimumAttachmentLifetime => _options.MinimumAttachmentLifetime;

    /// <summary>
    /// Validates data-plane access and the configured container at host startup.
    /// </summary>
    /// <param name="ctk">The cancellation token.</param>
    public async Task ValidateAsync(CancellationToken ctk)
    {
        if (_options.EnsureContainer)
        {
            await _container.CreateIfNotExistsAsync(cancellationToken: ctk)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            await _container.GetPropertiesAsync(cancellationToken: ctk)
                .ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException(
                "The configured Azure Blob DataBus container does not exist.",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(
        ReadOnlySequence<byte> content,
        CancellationToken ctk)
    {
        ctk.ThrowIfCancellationRequested();

        var attachmentId = Guid.NewGuid().ToString("N");
        var blob = _container.GetBlobClient(_options.Prefix + attachmentId);
        long length = 0;
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var segment in content)
        {
            ctk.ThrowIfCancellationRequested();
            sha.AppendData(segment.Span);
            length += segment.Length;
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [_lengthMetadataName] = length.ToString(CultureInfo.InvariantCulture),
            [_sha256MetadataName] = Convert.ToHexString(sha.GetHashAndReset())
        };
        var stream = await blob.OpenWriteAsync(
                overwrite: true,
                options: new BlobOpenWriteOptions
                {
                    Metadata = metadata
                },
                cancellationToken: ctk)
            .ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            foreach (var segment in content)
            {
                await stream.WriteAsync(segment, ctk).ConfigureAwait(false);
            }
        }

        return attachmentId;
    }

    /// <inheritdoc />
    public async Task<Stream> OpenReadAsync(
        string attachmentId,
        long expectedLength,
        string expectedSha256,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(attachmentId);
        ArgumentException.ThrowIfNullOrEmpty(expectedSha256);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedLength);
        var expectedHash = _parseHash(expectedSha256);
        var blob = _container.GetBlobClient(_options.Prefix + attachmentId);

        BlobProperties properties;
        try
        {
            properties = await blob.GetPropertiesAsync(cancellationToken: ctk)
                .ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw _attachmentFailure("The payload attachment is missing.", ex);
        }

        if (properties.ContentLength != expectedLength
            || !_metadataMatches(properties.Metadata, expectedLength, expectedHash))
        {
            throw _attachmentFailure(
                "The payload attachment metadata does not match the envelope.");
        }

        try
        {
            var stream = await blob.OpenReadAsync(cancellationToken: ctk)
                .ConfigureAwait(false);
            return new Sha256ValidatingReadStream(stream, expectedLength, expectedSha256);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw _attachmentFailure("The payload attachment is missing.", ex);
        }
    }

    private static AzureBlobDataBusOptions _validateOptions(
        AzureBlobDataBusOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ContainerName))
            throw new ArgumentException(
                "A Blob container name is required.",
                nameof(options));

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new ArgumentException(
                "An Azure Blob connection string is required.",
                nameof(options));

        if (options.ContainerName.Length is < 3 or > 63
            || !string.Equals(
                options.ContainerName,
                options.ContainerName.ToLowerInvariant(),
                StringComparison.Ordinal)
            || options.ContainerName.Any(
                character => !(character is >= 'a' and <= 'z'
                    or >= '0' and <= '9'
                    or '-')))
        {
            throw new ArgumentException(
                "The Blob container name must be 3-63 lowercase letters, digits, or hyphens.",
                nameof(options));
        }

        if (options.ContainerName.StartsWith('-') || options.ContainerName.EndsWith('-'))
            throw new ArgumentException(
                "The Blob container name cannot start or end with a hyphen.",
                nameof(options));

        if (options.MinimumAttachmentLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The attachment lifetime must be positive.");

        return options;
    }

    private static BlobContainerClient _createContainer(AzureBlobDataBusOptions options)
    {
        if (Uri.TryCreate(options.ConnectionString, UriKind.Absolute, out var serviceUri)
            && serviceUri.Scheme == Uri.UriSchemeHttps)
        {
            return new BlobServiceClient(serviceUri, new DefaultAzureCredential())
                .GetBlobContainerClient(options.ContainerName);
        }

        var client = new BlobServiceClient(options.ConnectionString);
        if (client.Uri.Scheme != Uri.UriSchemeHttp
            && !options.ConnectionString.Contains(
                "AccountKey",
                StringComparison.OrdinalIgnoreCase)
            && !options.ConnectionString.Contains(
                "SharedAccessSignature",
                StringComparison.OrdinalIgnoreCase))
        {
            client = new BlobServiceClient(client.Uri, new DefaultAzureCredential());
        }

        return client.GetBlobContainerClient(options.ContainerName);
    }

    private static byte[] _parseHash(string value)
    {
        try
        {
            var hash = Convert.FromHexString(value);
            if (hash.Length == 32)
                return hash;
        }
        catch (FormatException)
        {
        }

        throw _attachmentFailure("The payload attachment SHA-256 digest is invalid.");
    }

    private static bool _metadataMatches(
        IDictionary<string, string> metadata,
        long expectedLength,
        byte[] expectedHash)
    {
        if (!metadata.TryGetValue(_lengthMetadataName, out var lengthValue)
            || !long.TryParse(
                lengthValue,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var length)
            || length != expectedLength
            || !metadata.TryGetValue(_sha256MetadataName, out var hashValue))
        {
            return false;
        }

        try
        {
            var hash = Convert.FromHexString(hashValue);
            return hash.Length == expectedHash.Length
                && CryptographicOperations.FixedTimeEquals(hash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static MessagingFailFastException _attachmentFailure(
        string message,
        Exception? innerException = null)
    {
        return innerException is null
            ? new MessagingFailFastException(
                MessagingFailFastReason.AttachmentIntegrityFailure,
                message)
            : new MessagingFailFastException(
                MessagingFailFastReason.AttachmentIntegrityFailure,
                message,
                innerException);
    }
}
