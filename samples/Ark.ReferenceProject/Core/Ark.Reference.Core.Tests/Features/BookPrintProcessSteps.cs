using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Core;

using AwesomeAssertions;

using Flurl;
using Flurl.Http;

using Reqnroll;

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Tests.Features
{
    [Binding]
    public sealed class BookPrintProcessSteps
    {
        private readonly TestClient _client;
        private readonly string _controllerName = "bookprintprocess";
        private int? _currentBookId;
        private int? _currentPrintProcessId;
        private HttpStatusCode? _lastStatusCode;
        private string? _lastErrorContent;
        private BookPrintProcess.V1.Output? _currentPrintProcess;

        public BookPrintProcessSteps(TestClient client)
        {
            _client = client;
        }

        [Given(@"I have created a book with title ""(.*)"" and author ""(.*)""")]
        public void GivenIHaveCreatedABookWithTitleAndAuthor(string title, string author)
        {
            var request = new Book.V1.Create
            {
                Title = title,
                Author = author,
                Genre = BookGenre.Fiction,
                ISBN = "978-0-123456-78-9"
            };

            _client.PostAsJson("book", request);
            _client.ThenTheRequestSucceded();
            
            var result = _client.ReadAs<Book.V1.Output>();
            _currentBookId = result.Id;
        }

        [Given(@"I have created a book print process for that book")]
        [When(@"I create a book print process for that book")]
        public void WhenICreateABookPrintProcessForThatBook()
        {
            var request = new BookPrintProcess.V1.Create
            {
                BookId = _currentBookId!.Value,
                ShouldFail = false
            };

            try
            {
                _client.PostAsJson(_controllerName, request);
                
                if (_client.LastStatusCodeIsSuccess())
                {
                    _currentPrintProcess = _client.ReadAs<BookPrintProcess.V1.Output>();
                    _currentPrintProcessId = _currentPrintProcess!.BookPrintProcessId;
                    _lastStatusCode = _client.GetLastStatusCode();
                }
                else
                {
                    _lastStatusCode = _client.GetLastStatusCode();
                    _lastErrorContent = _client.ReadAsString();
                }
            }
            catch (FlurlHttpException ex)
            {
                _lastStatusCode = (HttpStatusCode?)(ex.StatusCode ?? 0);
                _lastErrorContent = ex.GetResponseStringAsync().GetAwaiter().GetResult();
            }
        }

        [Given(@"I have created a book print process for that book with ShouldFail (.*)")]
        public void GivenIHaveCreatedABookPrintProcessWithShouldFail(bool shouldFail)
        {
            var request = new BookPrintProcess.V1.Create
            {
                BookId = _currentBookId!.Value,
                ShouldFail = shouldFail
            };

            _client.PostAsJson(_controllerName, request);
            _client.ThenTheRequestSucceded();
                
            _currentPrintProcess = _client.ReadAs<BookPrintProcess.V1.Output>();
            _currentPrintProcessId = _currentPrintProcess!.BookPrintProcessId;
        }

        [When(@"I try to create another book print process for that book")]
        public void WhenITryToCreateAnotherBookPrintProcessForThatBook()
        {
            var request = new BookPrintProcess.V1.Create
            {
                BookId = _currentBookId!.Value,
                ShouldFail = false
            };

            try
            {
                _client.PostAsJson(_controllerName, request);
                _lastStatusCode = _client.GetLastStatusCode();
                
                if (!_client.LastStatusCodeIsSuccess())
                {
                    _lastErrorContent = _client.ReadAsString();
                }
            }
            catch (FlurlHttpException ex)
            {
                _lastStatusCode = (HttpStatusCode?)(ex.StatusCode ?? 0);
                _lastErrorContent = ex.GetResponseStringAsync().GetAwaiter().GetResult();
            }
        }

        [When(@"I wait for the print process to complete")]
        public async Task WhenIWaitForThePrintProcessToComplete()
        {
            // Simplified: just wait for outbox and bus to be idle
            await Task.Delay(TimeSpan.FromSeconds(35)).ConfigureAwait(false);
        }

        [When(@"I wait for the print process to fail")]
        public async Task WhenIWaitForThePrintProcessToFail()
        {
            // Simplified: just wait for outbox and bus to be idle
            await Task.Delay(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
        }

        [When(@"I wait for the IFailed handler to process the error")]
        public async Task WhenIWaitForTheIFailedHandlerToProcessTheError()
        {
            // Same as waiting for failure
            await WhenIWaitForThePrintProcessToFail().ConfigureAwait(false);
        }

        [When(@"I retrieve the print process status")]
        public void WhenIRetrieveThePrintProcessStatus()
        {
            _client.Get($"{_controllerName}/{_currentPrintProcessId}");
            _client.ThenTheRequestSucceded();
            _currentPrintProcess = _client.ReadAs<BookPrintProcess.V1.Output>();
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
            _lastStatusCode.Should().NotBeNull();
            ((int)_lastStatusCode!.Value).Should().Be(statusCode);
        }

        [Then(@"the error should indicate ""(.*)""")]
        public void ThenTheErrorShouldIndicate(string ruleCode)
        {
            _lastErrorContent.Should().NotBeNullOrEmpty();
            _lastErrorContent.Should().Contain(ruleCode);
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
