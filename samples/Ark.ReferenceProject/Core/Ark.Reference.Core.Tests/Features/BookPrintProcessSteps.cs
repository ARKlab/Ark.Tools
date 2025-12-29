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
        private readonly BookSteps _bookSteps;
        private readonly string _controllerName = "bookprintprocess";
        private int? _currentPrintProcessId;

        public BookPrintProcess.V1.Output? Current { get; private set; }

        public BookPrintProcessSteps(TestClient client, TestHost testHost, BookSteps bookSteps)
        {
            _client = client;
            _testHost = testHost;
            _bookSteps = bookSteps;
        }

        [Given(@"I have created a book print process for that book")]
        public void GivenIHaveCreatedABookPrintProcessForThatBook()
        {
            var request = new BookPrintProcess.V1.Create
            {
                BookId = _bookSteps.Current!.Id,
                ShouldFail = false
            };

            _client.PostAsJson(_controllerName, request);
            _client.ThenTheRequestSucceded();
                
            Current = _client.ReadAs<BookPrintProcess.V1.Output>();
            _currentPrintProcessId = Current.BookPrintProcessId;
        }

        [Given(@"I have created a book print process for that book with ShouldFail (.*)")]
        public void GivenIHaveCreatedABookPrintProcessWithShouldFail(bool shouldFail)
        {
            var request = new BookPrintProcess.V1.Create
            {
                BookId = _bookSteps.Current!.Id,
                ShouldFail = shouldFail
            };

            _client.PostAsJson(_controllerName, request);
            _client.ThenTheRequestSucceded();
                
            Current = _client.ReadAs<BookPrintProcess.V1.Output>();
            _currentPrintProcessId = Current.BookPrintProcessId;
        }

        [When(@"I try to create another book print process for that book")]
        public void WhenITryToCreateAnotherBookPrintProcessForThatBook()
        {
            var request = new BookPrintProcess.V1.Create
            {
                BookId = _bookSteps.Current!.Id,
                ShouldFail = false
            };

            _client.PostAsJson(_controllerName, request);
        }

        [When(@"I retrieve the print process status")]
        public void WhenIRetrieveThePrintProcessStatus()
        {
            _client.Get($"{_controllerName}/{_currentPrintProcessId}");
        }

        [Then(@"the business rule violation code is ""(.*)""")]
        public void ThenTheBusinessRuleViolationCodeIs(string expectedCode)
        {
            var problemDetails = _client.ReadAs<ProblemDetails>();
            problemDetails.Should().NotBeNull();
            problemDetails.Title.Should().Contain(expectedCode);
        }

        [Then(@"the print process is")]
        public void ThenTheBookPrintProcessIs(Table table)
        {
            _client.ThenTheRequestSucceded();
            Current = _client.ReadAs<BookPrintProcess.V1.Output>();
            
            table.CompareToInstance(Current);
        }

        [Then(@"the print process has error details")]
        public void ThenThePrintProcessHasErrorDetails()
        {
            _client.ThenTheRequestSucceded();
            Current = _client.ReadAs<BookPrintProcess.V1.Output>();
            
            Current.Should().NotBeNull();
            Current!.Status.Should().Be(BookPrintProcessStatus.Error);
            Current.Progress.Should().BeLessThan(1.0);
            Current.ErrorMessage.Should().NotBeNullOrEmpty();
        }
    }
}
