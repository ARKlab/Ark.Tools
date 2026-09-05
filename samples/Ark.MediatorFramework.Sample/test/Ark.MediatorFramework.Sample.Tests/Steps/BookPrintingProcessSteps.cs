// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Drivers;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;

using AwesomeAssertions;

using Reqnroll;
using Reqnroll.Assist;

namespace Ark.MediatorFramework.Sample.Tests.Steps;

/// <summary>Defines current-book print-process verbs for Reqnroll scenarios.</summary>
[Binding]
public sealed class BookPrintingProcessSteps
{
    private readonly BookDriver _books;
    private readonly SampleTestContext _sampleContext;
    private Exception? _exception;

    /// <summary>Initializes a new instance of the <see cref="BookPrintingProcessSteps"/> class.</summary>
    /// <param name="books">The scenario-owned book driver.</param>
    /// <param name="sampleContext">The scenario-owned application context.</param>
    public BookPrintingProcessSteps(BookDriver books, SampleTestContext sampleContext)
    {
        _books = books;
        _sampleContext = sampleContext;
    }

    /// <summary>Gets the active print process.</summary>
    public BookPrintProcessResponse? Current { get; private set; }

    /// <summary>Starts a print process for the active book.</summary>
    /// <param name="table">The print process data.</param>
    [Given("I start a book print process for the current book with")]
    public async Task GivenStartCurrentBookPrintProcess(Table table)
    {
        await StartCurrentBookPrintProcess(table).ConfigureAwait(false);
        _exception.Should().BeNull();
        Current.Should().NotBeNull();
    }

    [When("I start a book print process for the current book with")]
    public async Task StartCurrentBookPrintProcess(Table table)
    {
        var request = table.CreateInstance<CreateBookPrintProcessRequest.V1>() with { BookId = _books.Current.Id };
        _exception = await _captureAsync(async () =>
        {
            Current = await _context.DispatchRequestAsync<CreateBookPrintProcessRequest.V1, BookPrintProcessResponse>(request)
                .ConfigureAwait(false);
            return Current;
        }).ConfigureAwait(false);
    }

    [When("I concurrently start two book print processes for the current book with")]
    public async Task ConcurrentlyStartCurrentBookPrintProcesses(Table table)
    {
        var request = table.CreateInstance<CreateBookPrintProcessRequest.V1>() with { BookId = _books.Current.Id };
        var requests = new[]
        {
            _context.DispatchRequestAsync<CreateBookPrintProcessRequest.V1, BookPrintProcessResponse>(request),
            _context.DispatchRequestAsync<CreateBookPrintProcessRequest.V1, BookPrintProcessResponse>(request),
        };

        try
        {
            await Task.WhenAll(requests).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _exception = exception;
            var successfulRequests = requests.Where(static task => task.Status == TaskStatus.RanToCompletion).ToArray();
            if (successfulRequests.Length != 1)
            {
                throw new InvalidOperationException(
                    "Expected exactly one concurrent print request to succeed.",
                    exception);
            }

            Current = await successfulRequests[0].ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException("Expected one concurrent print request to fail.");
    }

    /// <summary>Loads the active print process through its query contract.</summary>
    [When("I retrieve the current book print process")]
    public async Task RetrieveCurrentBookPrintProcess()
    {
        Current.Should().NotBeNull();
        Current = await _context.DispatchQueryAsync<GetBookPrintProcessQuery.V1, BookPrintProcessResponse>(
            new GetBookPrintProcessQuery.V1 { Id = Current!.Id }).ConfigureAwait(false);
    }

    /// <summary>Seeds a running process to represent interrupted background work.</summary>
    [Given("I have a running book print process for the current book")]
    public async Task GivenRunningBookPrintProcess()
    {
        _books.HasCurrent.Should().BeTrue();
        Current = await _context.SeedRunningBookPrintProcessAsync(_books.Current.Id).ConfigureAwait(false);
    }

    /// <summary>Resumes the active process through its application request.</summary>
    [When("I resume the current book print process")]
    public async Task ResumeCurrentBookPrintProcess()
    {
        Current.Should().NotBeNull();
        Current = await _context.DispatchRequestAsync<ResumeBookPrintProcessRequest, BookPrintProcessResponse>(
            new ResumeBookPrintProcessRequest { Id = Current!.Id }).ConfigureAwait(false);
    }

    /// <summary>Cancels the active print process through its application request.</summary>
    [When("I cancel the current book print process")]
    public async Task CancelCurrentBookPrintProcess()
    {
        Current.Should().NotBeNull();
        _exception = await _captureAsync(async () =>
        {
            Current = await _context.DispatchRequestAsync<CancelBookPrintProcessRequest.V1, BookPrintProcessResponse>(
                new CancelBookPrintProcessRequest.V1 { Id = Current!.Id }).ConfigureAwait(false);
            return Current;
        }).ConfigureAwait(false);
    }

    /// <summary>Asserts that the active print process matches the supplied table.</summary>
    /// <param name="table">The expected print process data.</param>
    [Then("the current book print process is")]
    public void CurrentBookPrintProcessIs(Table table)
    {
        Current.Should().NotBeNull();
        table.CompareToInstance(Current!);
    }

    /// <summary>Asserts that the print process failed with error details.</summary>
    [Then("the current book print process has error details")]
    public void CurrentBookPrintProcessHasErrorDetails()
    {
        Current.Should().NotBeNull();
        Current!.Status.Should().Be((EvolvableEnum<BookPrintProcessStatus>)BookPrintProcessStatus.Error);
        Current.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>Asserts that the active print process was cancelled.</summary>
    [Then("the current book print process was cancelled")]
    public void CurrentBookPrintProcessWasCancelled()
    {
        _exception.Should().BeNull();
        Current.Should().NotBeNull();
        Current!.Status.Should().Be((EvolvableEnum<BookPrintProcessStatus>)BookPrintProcessStatus.Cancelled);
    }

    /// <summary>Asserts that cancellation was rejected for a terminal process.</summary>
    [Then("cancellation fails because the current book print process is terminal")]
    public void CancellationFailsBecauseCurrentBookPrintProcessIsTerminal()
    {
        _exception.Should().BeOfType<BusinessRuleViolationException>()
            .Which.BusinessRuleViolation.Should().BeOfType<BookPrintProcessCannotBeCancelledViolation>();
    }

    /// <summary>Asserts that the external notification service was called for the active process.</summary>
    [Then("the print-completion notification service was called")]
    public void PrintCompletionNotificationServiceWasCalled()
    {
        Current.Should().NotBeNull();
        _sampleContext.Application.VerifyPrintCompletionNotification(Current!);
    }

    /// <summary>Asserts the typed duplicate-print-process business-rule violation.</summary>
    [Then("the request fails because the current book is already printing")]
    public void RequestFailsBecauseCurrentBookIsAlreadyPrinting()
    {
        _exception.Should().BeOfType<BusinessRuleViolationException>()
            .Which.BusinessRuleViolation.Should().BeOfType<BookPrintingProcessAlreadyRunningViolation>();
    }

    private static async Task<Exception?> _captureAsync<T>(Func<Task<T>> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
#pragma warning disable ERP022 // Reqnroll needs the exception for a later typed assertion.
            return exception;
#pragma warning restore ERP022
        }
    }

    private ApplicationTestContext _context => _sampleContext.Application;
}
