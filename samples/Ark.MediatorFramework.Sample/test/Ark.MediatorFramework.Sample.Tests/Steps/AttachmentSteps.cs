// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Hooks;

using Ark.Tools.Core;

using AwesomeAssertions;

using Reqnroll;
using Reqnroll.Assist;

namespace Ark.MediatorFramework.Sample.Tests.Steps;

/// <summary>Defines table data used to create and compare greeting-card attachments.</summary>
public sealed record GreetingCardTable
{
    /// <summary>Gets the attachment file name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the attachment MIME content type.</summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>Gets the UTF-8 attachment content.</summary>
    public string Content { get; init; } = string.Empty;
}

/// <summary>Defines application-contract steps for greeting-card attachments.</summary>
[Binding]
public sealed class AttachmentSteps
{
    private readonly SampleTestContext _sampleContext;
    private Guid _id;
    private UploadResponse? _upload;
    private UploadBatchResponse? _batch;
    private IArkAttachment? _attachment;
    private Exception? _exception;

    /// <summary>Initializes a new instance of the <see cref="AttachmentSteps"/> class.</summary>
    /// <param name="sampleContext">The scenario's direct application context.</param>
    public AttachmentSteps(SampleTestContext sampleContext)
    {
        _sampleContext = sampleContext;
    }

    /// <summary>Uploads one greeting card defined by a table.</summary>
    /// <param name="table">The greeting-card data.</param>
    [When("I upload a greeting card with")]
    public async Task UploadGreetingCard(Table table)
    {
        var card = table.CreateInstance<GreetingCardTable>();
        _id = Guid.NewGuid();
        _upload = await Context.DispatchRequestAsync<UploadGreetingCardRequest, UploadResponse>(
            new UploadGreetingCardRequest
            {
                Id = _id,
                Label = card.Name,
                Attachment = CreateAttachment(card),
            }).ConfigureAwait(false);
    }

    /// <summary>Uploads greeting cards defined by table rows.</summary>
    /// <param name="table">The greeting-card data.</param>
    [When("I upload greeting cards with")]
    public async Task UploadGreetingCards(Table table)
    {
        var cards = table.CreateSet<GreetingCardTable>().ToArray();
        _id = Guid.NewGuid();
        _batch = await Context.DispatchRequestAsync<UploadGreetingCardsRequest, UploadBatchResponse>(
            new UploadGreetingCardsRequest
            {
                Id = _id,
                Attachments = cards.Select(CreateAttachment).ToArray(),
            }).ConfigureAwait(false);
    }

    /// <summary>Loads the active greeting card through its public query contract.</summary>
    [When("I retrieve the current greeting card")]
    public async Task RetrieveGreetingCard()
    {
        _attachment = await Context.DispatchQueryAsync<GetDocumentQuery, IArkAttachment>(
            new GetDocumentQuery { Id = _id }).ConfigureAwait(false);
    }

    /// <summary>Attempts to load an unknown greeting card.</summary>
    [When("I retrieve an unknown greeting card")]
    public async Task RetrieveUnknownGreetingCard()
    {
        try
        {
            _attachment = await Context.DispatchQueryAsync<GetDocumentQuery, IArkAttachment>(
                new GetDocumentQuery { Id = Guid.NewGuid() }).ConfigureAwait(false);
            _exception = null;
        }
        catch (Exception exception)
        {
#pragma warning disable ERP022 // Reqnroll needs the exception for the later assertion.
            _exception = exception;
#pragma warning restore ERP022
        }
    }

    /// <summary>Asserts the metadata and byte count reported by a single upload.</summary>
    /// <param name="table">The expected upload response.</param>
    [Then("the greeting card upload is")]
    public void GreetingCardUploadIs(Table table)
    {
        _upload.Should().NotBeNull();
        table.CompareToInstance(_upload!);
    }

    /// <summary>Asserts the metadata and UTF-8 content of the active greeting card.</summary>
    /// <param name="table">The expected greeting-card data.</param>
    [Then("the current greeting card is")]
    public async Task CurrentGreetingCardIs(Table table)
    {
        _attachment.Should().NotBeNull();
        await using var stream = _attachment!.OpenRead();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
        var card = new GreetingCardTable
        {
            Name = _attachment.Name,
            ContentType = _attachment.ContentType,
            Content = await reader.ReadToEndAsync().ConfigureAwait(false),
        };
        table.CompareToInstance(card);
    }

    /// <summary>Asserts the file names returned by a batch upload.</summary>
    /// <param name="table">The expected greeting-card data.</param>
    [Then("the greeting card batch contains")]
    public void GreetingCardBatchContains(Table table)
    {
        _batch.Should().NotBeNull();
        var expectedNames = table.CreateSet<GreetingCardTable>().Select(card => card.Name);
        _batch!.Names.Should().Equal(expectedNames);
    }

    /// <summary>Asserts the typed missing-document result.</summary>
    [Then("the document query fails because the greeting card is missing")]
    public void DocumentQueryFailsBecauseGreetingCardIsMissing()
    {
        _exception.Should().BeOfType<EntityNotFoundException>();
    }

    private static ArkAttachment CreateAttachment(GreetingCardTable card)
    {
        var content = Encoding.UTF8.GetBytes(card.Content);
        return new ArkAttachment(card.Name, card.ContentType, () => new MemoryStream(content, writable: false));
    }

    private ApplicationTestContext Context => _sampleContext.Application;
}
