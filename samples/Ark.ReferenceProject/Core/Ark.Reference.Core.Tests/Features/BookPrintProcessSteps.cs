using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Reference.Core.Common.Exceptions;
using Ark.Reference.Core.Tests.Support;
using Ark.Tools.Core.EntityTag;

using Reqnroll;

using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

using TechTalk.SpecFlow.Assist;

namespace Ark.Reference.Core.Tests.Features
{
    [Binding]
    public class BookPrintProcessSteps
    {
        private readonly TestContext _testContext;
        private int? _currentBookId;
        private int? _currentPrintProcessId;
        private HttpResponseMessage? _lastResponse;
        private BookPrintProcess.V1.Output? _currentPrintProcess;

        public BookPrintProcessSteps(TestContext testContext)
        {
            _testContext = testContext;
        }

        [Given(@"I have created a book with title ""(.*)"" and author ""(.*)""")]
        public async Task GivenIHaveCreatedABookWithTitleAndAuthor(string title, string author)
        {
            var request = new Book_CreateRequest
            {
                Data = new Book.V1.Create
                {
                    Title = title,
                    Author = author,
                    Genre = BookGenre.Fiction,
                    ISBN = "978-0-123456-78-9"
                }
            };

            var response = await _testContext.ApiClient.PostAsJsonAsync("/api/v1/book", request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<Book.V1.Output>(_testContext.JsonOptions).ConfigureAwait(false);
            _currentBookId = result!.Id;
        }

        [Given(@"I have created a book print process for that book")]
        [When(@"I create a book print process for that book")]
        public async Task WhenICreateABookPrintProcessForThatBook()
        {
            var request = new BookPrintProcess_CreateRequest.V1
            {
                Data = new BookPrintProcess.V1.Create
                {
                    BookId = _currentBookId!.Value,
                    ShouldFail = false
                }
            };

            _lastResponse = await _testContext.ApiClient.PostAsJsonAsync("/api/v1/bookprintprocess", request).ConfigureAwait(false);
            
            if (_lastResponse.IsSuccessStatusCode)
            {
                _currentPrintProcess = await _lastResponse.Content.ReadFromJsonAsync<BookPrintProcess.V1.Output>(_testContext.JsonOptions).ConfigureAwait(false);
                _currentPrintProcessId = _currentPrintProcess!.BookPrintProcessId;
            }
        }

        [Given(@"I have created a book print process for that book with ShouldFail (.*)")]
        public async Task GivenIHaveCreatedABookPrintProcessWithShouldFail(bool shouldFail)
        {
            var request = new BookPrintProcess_CreateRequest.V1
            {
                Data = new BookPrintProcess.V1.Create
                {
                    BookId = _currentBookId!.Value,
                    ShouldFail = shouldFail
                }
            };

            _lastResponse = await _testContext.ApiClient.PostAsJsonAsync("/api/v1/bookprintprocess", request).ConfigureAwait(false);
            _lastResponse.EnsureSuccessStatusCode();

            _currentPrintProcess = await _lastResponse.Content.ReadFromJsonAsync<BookPrintProcess.V1.Output>(_testContext.JsonOptions).ConfigureAwait(false);
            _currentPrintProcessId = _currentPrintProcess!.BookPrintProcessId;
        }

        [When(@"I try to create another book print process for that book")]
        public async Task WhenITryToCreateAnotherBookPrintProcessForThatBook()
        {
            var request = new BookPrintProcess_CreateRequest.V1
            {
                Data = new BookPrintProcess.V1.Create
                {
                    BookId = _currentBookId!.Value,
                    ShouldFail = false
                }
            };

            _lastResponse = await _testContext.ApiClient.PostAsJsonAsync("/api/v1/bookprintprocess", request).ConfigureAwait(false);
        }

        [When(@"I wait for the print process to complete")]
        public async Task WhenIWaitForThePrintProcessToComplete()
        {
            // Wait up to 35 seconds for process to complete (10 iterations * 3 seconds + buffer)
            var maxWaitTime = TimeSpan.FromSeconds(35);
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < maxWaitTime)
            {
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

                await _testContext.ApiHost.Container.WaitForOutboxAsync().ConfigureAwait(false);

                // Check status via DAL
                await using var ctx = await _testContext.ApiHost.Container.GetInstance<Application.DAL.ICoreDataContextFactory>()
                    .CreateAsync().ConfigureAwait(false);
                _currentPrintProcess = await ctx.ReadBookPrintProcessByIdAsync(_currentPrintProcessId!.Value).ConfigureAwait(false);

                if (_currentPrintProcess?.Status == BookPrintProcessStatus.Completed)
                    return;
            }

            throw new TimeoutException("Print process did not complete within expected time");
        }

        [When(@"I wait for the print process to fail")]
        public async Task WhenIWaitForThePrintProcessToFail()
        {
            // Wait up to 15 seconds for process to fail at 30%
            var maxWaitTime = TimeSpan.FromSeconds(15);
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < maxWaitTime)
            {
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

                await _testContext.ApiHost.Container.WaitForOutboxAsync().ConfigureAwait(false);

                // Check status via DAL
                await using var ctx = await _testContext.ApiHost.Container.GetInstance<Application.DAL.ICoreDataContextFactory>()
                    .CreateAsync().ConfigureAwait(false);
                _currentPrintProcess = await ctx.ReadBookPrintProcessByIdAsync(_currentPrintProcessId!.Value).ConfigureAwait(false);

                if (_currentPrintProcess?.Status == BookPrintProcessStatus.Error)
                    return;
            }

            throw new TimeoutException("Print process did not fail within expected time");
        }

        [When(@"I wait for the IFailed handler to process the error")]
        public async Task WhenIWaitForTheIFailedHandlerToProcessTheError()
        {
            // Same as waiting for failure
            await WhenIWaitForThePrintProcessToFail().ConfigureAwait(false);
        }

        [Then(@"the print process should be created with status ""(.*)""")]
        public void ThenThePrintProcessShouldBeCreatedWithStatus(string status)
        {
            _currentPrintProcess.Should().NotBeNull();
            _currentPrintProcess!.Status.ToString().Should().Be(status);
        }

        [Then(@"the print process progress should be (.*)")]
        public void ThenThePrintProcessProgressShouldBe(double expectedProgress)
        {
            _currentPrintProcess.Should().NotBeNull();
            _currentPrintProcess!.Progress.Should().BeApproximately(expectedProgress, 0.01);
        }

        [Then(@"I should get a (.*) Bad Request response")]
        public void ThenIShouldGetABadRequestResponse(int statusCode)
        {
            _lastResponse.Should().NotBeNull();
            ((int)_lastResponse!.StatusCode).Should().Be(statusCode);
        }

        [Then(@"the error should indicate ""(.*)""")]
        public async Task ThenTheErrorShouldIndicate(string ruleCode)
        {
            var content = await _lastResponse!.Content.ReadAsStringAsync().ConfigureAwait(false);
            content.Should().Contain(ruleCode);
        }

        [Then(@"the print process status should be ""(.*)""")]
        public void ThenThePrintProcessStatusShouldBe(string status)
        {
            _currentPrintProcess.Should().NotBeNull();
            _currentPrintProcess!.Status.ToString().Should().Be(status);
        }

        [Then(@"the error message should contain ""(.*)""")]
        public void ThenTheErrorMessageShouldContain(string expectedText)
        {
            _currentPrintProcess.Should().NotBeNull();
            _currentPrintProcess!.ErrorMessage.Should().NotBeNullOrEmpty();
            _currentPrintProcess!.ErrorMessage.Should().Contain(expectedText);
        }

        [Then(@"the error message should not be empty")]
        public void ThenTheErrorMessageShouldNotBeEmpty()
        {
            _currentPrintProcess.Should().NotBeNull();
            _currentPrintProcess!.ErrorMessage.Should().NotBeNullOrEmpty();
        }
    }
}
