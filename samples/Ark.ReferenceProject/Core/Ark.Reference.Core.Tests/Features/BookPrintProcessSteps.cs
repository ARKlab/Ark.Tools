using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Reference.Core.Tests.Init;

using AwesomeAssertions;

using Reqnroll;

namespace Ark.Reference.Core.Tests.Features
{
    [Binding]
    public sealed class BookPrintProcessSteps
    {
        private readonly TestClient _client;
        private readonly TestHost _testHost;
        private readonly BookSteps _bookSteps;
        private readonly string _controllerName = "bookprintprocess";

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
            GivenIHaveCreatedABookPrintProcessWithShouldFail(false);
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
        }

        [When(@"I create a book print process for that book")]
        public void WhenICreateABookPrintProcessForThatBook()
        {
            var request = new BookPrintProcess.V1.Create
            {
                BookId = _bookSteps.Current!.Id,
                ShouldFail = false
            };

            _client.PostAsJson(_controllerName, request);

            // Capture result if successful for subsequent steps
            if (_client.LastStatusCodeIsSuccess())
            {
                Current = _client.ReadAs<BookPrintProcess.V1.Output>();
            }
        }

        [When(@"I try to create another book print process for that book")]
        public void WhenITryToCreateAnotherBookPrintProcessForThatBook()
        {
            WhenICreateABookPrintProcessForThatBookWithShouldFail(false);
        }

        [Given(@"I create a book print process for that book")]
        public void GivenICreateABookPrintProcessForThatBook()
        {
            WhenICreateABookPrintProcessForThatBook();
            _client.ThenTheRequestSucceded();
        }

        [When(@"I create a book print process for that book with ShouldFail (.*)")]
        public void WhenICreateABookPrintProcessForThatBookWithShouldFail(bool shouldFail)
        {
            var request = new BookPrintProcess.V1.Create
            {
                BookId = _bookSteps.Current!.Id,
                ShouldFail = shouldFail
            };

            _client.PostAsJson(_controllerName, request);

            // Capture result if successful for subsequent steps
            if (_client.LastStatusCodeIsSuccess())
            {
                Current = _client.ReadAs<BookPrintProcess.V1.Output>();
            }
        }

        [When(@"I retrieve the print process status")]
        public void WhenIRetrieveThePrintProcessStatus()
        {
            _client.Get($"{_controllerName}/{Current!.BookPrintProcessId}");
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