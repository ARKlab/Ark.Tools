using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Core;

using AwesomeAssertions;

using Flurl;

using Reqnroll;
using Reqnroll.Assist;

using System.Collections.Generic;

namespace Ark.Reference.Core.Tests.Features
{
    [Binding]
    sealed class BookSteps
    {
        private readonly TestClient _client;
        private readonly string _controllerName = "book";

        private Book.V1.Output? _output;
        private readonly Dictionary<string, int> _entityNameId = new(System.StringComparer.Ordinal);

        public Book.V1.Output? Current { get; private set; }

        public BookSteps(TestClient client)
        {
            _client = client;
        }

        //** CREATE ********************************************************************
        [Given("I create a book with")]
        public void GivenICreateABookWith(Table table)
        {
            var body = table.CreateInstance<Book.V1.Create>();
            _client.PostAsJson($"{_controllerName}/", body);
            _client.ThenTheRequestSucceded();

            Current = _client.ReadAs<Book.V1.Output?>();
            _output = Current;
            if (Current?.Title != null)
                _entityNameId.TryAdd(Current.Title, Current.Id);
        }

        [When("I create a single Book with")]
        public void WhenICreateASingleBookWith(Table table)
        {
            var body = table.CreateInstance<Book.V1.Create>();
            _client.PostAsJson($"{_controllerName}/", body);

            if (_client.LastResponse.ResponseMessage.IsSuccessStatusCode)
            {
                var c = _client.ReadAs<Book.V1.Output?>();
                _output = c;
                Current = c;
                if (c?.Title != null)
                    _entityNameId.TryAdd(c.Title, c.Id);
            }
        }

        [When(@"I create multiple Book with")]
        public void WhenICreateMultipleBookWith(Table table)
        {
            var entities = table.CreateSet<Book.V1.Create>();

            foreach (var e in entities)
            {
                _client.PostAsJson($"{_controllerName}", e);
                _client.ThenTheRequestSucceded();

                if (_client.LastStatusCodeIsSuccess())
                {
                    var res = _client.ReadAs<Book.V1.Output?>();
                    if (res?.Title != null)
                        _entityNameId.Add(res.Title, res.Id);
                }
            }
        }

        //** GET ********************************************************************
        [When(@"I request the Book '([^']*)' by id")]
        public void WhenIRequestTheBookById(string name)
        {
            _entityNameId.TryGetValue(name, out var id).Should().BeTrue();

            _client.Get($"{_controllerName}/{id}");

            if (_client.LastStatusCodeIsSuccess())
            {
                _output = _client.ReadAs<Book.V1.Output?>();
            }
        }

        [When(@"I request the Book by")]
        public void WhenIRequestTheBookBy(Table table)
        {
            var url = new Url($"{_controllerName}");

            foreach (var row in table.Rows)
            {
                foreach (var kv in row)
                {
                    url.SetQueryParam(kv.Key, kv.Value);
                }
            }

            _client.Get(url.ToString());
        }

        //** UPDATE ********************************************************************
        [When(@"I update the Book '([^']*)' with")]
        public void WhenIUpdateTheBookWith(string name, Table table)
        {
            _entityNameId.TryGetValue(name, out var id).Should().BeTrue();

            var body = table.CreateInstance<Book.V1.Update>();

            _client.PutAsJson($"{_controllerName}/{id}", body);

            if (_client.LastStatusCodeIsSuccess())
            {
                _output = _client.ReadAs<Book.V1.Output?>();
                if (_output?.Title != null)
                    _entityNameId.TryAdd(_output.Title, _output.Id);
            }
        }

        [When(@"I try to update a Book with")]
        public void WhenITryToUpdateABookWith(Table table)
        {
            var body = table.CreateInstance<Book.V1.Update>();

            _client.PutAsJson($"{_controllerName}/{body.Id}", body);

            if (_client.LastStatusCodeIsSuccess())
            {
                _output = _client.ReadAs<Book.V1.Output?>();
            }
        }

        //** DELETE ********************************************************************
        [When(@"I delete the Book '([^']*)' by id")]
        public void WhenIDeleteTheBookById(string name)
        {
            _entityNameId.TryGetValue(name, out var id).Should().BeTrue();

            _client.Delete($"{_controllerName}/{id}");
        }

        //** ASSERTIONS ****************************************************************
        [Then(@"the stored Book response should be")]
        public void ThenTheStoredBookResponseShouldBe(Table table)
        {
            BookMatched(_output, table);
        }

        [Then(@"the Book response should match")]
        public void ThenTheBookResponseShouldMatch(Table table)
        {
            BookMatched(_output, table);
        }

        [Then(@"the Book response count should be (.*)")]
        public void ThenTheBookResponseCountShouldBe(int expectedCount)
        {
            var res = _client.ReadAs<PagedResult<Book.V1.Output>>();
            res.Should().NotBeNull();
            res!.Count.Should().Be(expectedCount);
        }

        private static void BookMatched(Book.V1.Output? output, Table table)
        {
            output.Should().NotBeNull();
            var expected = table.CreateInstance<Book.V1.Output>();

            if (!string.IsNullOrWhiteSpace(expected.Title))
                output!.Title.Should().Be(expected.Title);

            if (!string.IsNullOrWhiteSpace(expected.Author))
                output!.Author.Should().Be(expected.Author);

            if (expected.Genre.HasValue && expected.Genre.Value != Common.Enum.BookGenre.NotSet)
                output!.Genre.Should().Be(expected.Genre);

            if (!string.IsNullOrWhiteSpace(expected.ISBN))
                output!.ISBN.Should().Be(expected.ISBN);
        }
    }
}
