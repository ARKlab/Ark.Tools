using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Reference.Core.Tests.Init;
using Ark.Tools.Core;

using AwesomeAssertions;

using Flurl;

using Microsoft.AspNetCore.Mvc;

using Reqnroll;
using Reqnroll.Assist;

using System;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Tests.Features
{
    [Binding]
    public sealed class BookPrintProcessSteps
    {
        private readonly TestClient _client;
        private readonly TestHost _testHost;
        private readonly string _controllerName = "bookprintprocess";
        private int? _currentBookId;
        private int? _currentPrintProcessId;
        private BookPrintProcess.V1.Output? _currentPrintProcess;

        public BookPrintProcessSteps(TestClient client, TestHost testHost)
        {
            _client = client;
            _testHost = testHost;
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

            _client.PostAsJson(_controllerName, request);
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
                
            var response = _client.ReadAs<BookPrintProcess.V1.Output>();
            _currentPrintProcessId = response.BookPrintProcessId;
        }

        [When(@"I try to create another book print process for that book")]
        public void WhenITryToCreateAnotherBookPrintProcessForThatBook()
        {
            var request = new BookPrintProcess.V1.Create
            {
                BookId = _currentBookId!.Value,
                ShouldFail = false
            };

            _client.PostAsJson(_controllerName, request);
        }

        [When(@"I retrieve the print process status")]
        public void WhenIRetrieveThePrintProcessStatus()
        {
            _client.Get($"{_controllerName}/{_currentPrintProcessId}");
        }

        [When(@"I wait background bus to idle and outbox to be empty")]
        public async Task WhenIWaitBackgroundBusToIdleAndOutboxToBeEmpty()
        {
            await _testHost.ThenIWaitBackgroundBusToIdleAndOutboxToBeEmpty().ConfigureAwait(false);
        }

        [Then(@"the print process should be created with status ""(.*)""")]
        public void ThenThePrintProcessShouldBeCreatedWithStatus(string status)
        {
            _client.ThenTheRequestSucceded();
            _currentPrintProcess = _client.ReadAs<BookPrintProcess.V1.Output>();
            _currentPrintProcessId = _currentPrintProcess.BookPrintProcessId;
            
            _currentPrintProcess.Status.ToString().Should().Be(status);
        }

        [Then(@"the print process progress should be (.*)")]
        public void ThenThePrintProcessProgressShouldBe(double expectedProgress)
        {
            if (_currentPrintProcess == null && _client.LastStatusCodeIsSuccess())
            {
                _currentPrintProcess = _client.ReadAs<BookPrintProcess.V1.Output>();
            }
            
            _currentPrintProcess.Should().NotBeNull();
            _currentPrintProcess!.Progress.Should().BeApproximately(expectedProgress, 0.01);
        }

        [Then(@"the business rule violation code is ""(.*)""")]
        public void ThenTheBusinessRuleViolationCodeIs(string expectedCode)
        {
            var problemDetails = _client.ReadAs<ProblemDetails>();
            problemDetails.Should().NotBeNull();
            problemDetails.Title.Should().NotBeNullOrEmpty();
            problemDetails.Extensions.Should().ContainKey("errorCode");
            problemDetails.Extensions["errorCode"]?.ToString().Should().Contain(expectedCode);
        }

        [Then(@"the print process is")]
        public void ThenTheBookPrintProcessIs(Table table)
        {
            _client.ThenTheRequestSucceded();
            _currentPrintProcess = _client.ReadAs<BookPrintProcess.V1.Output>();
            
            table.CompareToInstance(_currentPrintProcess);
        }

        [Then(@"the print process has error details")]
        public void ThenThePrintProcessHasErrorDetails()
        {
            _client.ThenTheRequestSucceded();
            _currentPrintProcess = _client.ReadAs<BookPrintProcess.V1.Output>();
            
            _currentPrintProcess.Should().NotBeNull();
            _currentPrintProcess!.Status.Should().Be(BookPrintProcessStatus.Error);
            _currentPrintProcess.Progress.Should().BeLessThan(1.0);
            _currentPrintProcess.ErrorMessage.Should().NotBeNullOrEmpty();
        }
    }
}
