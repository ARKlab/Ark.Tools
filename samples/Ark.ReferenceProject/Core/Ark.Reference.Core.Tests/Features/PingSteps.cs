using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Tests.Init;
using Ark.Tools.Core;

using AwesomeAssertions;

using Flurl;

using Reqnroll;
using Reqnroll.Assist;


namespace Ark.Reference.Core.Tests.Features;

[Binding]
sealed class PingSteps
{
    //** Common section **
    private readonly TestClient _client;
    private readonly string _controllerName = "ping";

    //** Entity related private section **
    private Ping.V1.Output? _output;
    private readonly Dictionary<string, int> _entityNameId = new(System.StringComparer.Ordinal);

    public PingSteps(TestClient client)
    {
        _client = client;
    }

    //** PING *****************************
    [Given(@"I make a request to ping")]
    public void GivenIMakeARquestToPing()
    {
        _client.Get($"{_controllerName}/test");
    }

    [Then(@"the response is '(.*)'")]
    public void ThenTheResponseIs(string expected)
    {
        var res = _client.ReadAs<string>();

        expected.Should().Be(res);
    }


    //** PING DEEP CHECK *****************************

    [Given(@"I make a request to '([^']*)' ping")]
    public void GivenIMakeARequestToPing(string nome)
    {
        _client.Get($"{_controllerName}/test/{nome}");
    }

    [Then(@"the ping response is")]
    public void ThenThePingResponseIs(Table table)
    {
        var res = _client.ReadAs<Ping.V1.Output>();

        _output = res;
        PingMatched(res, table);
    }


    //** CREATE ********************************************************************
    [When("I create a single Ping with")]
    public void WhenICreateASinglePingWith(Table table)
    {
        var body = table.CreateInstance<Ping.V1.Create>();
        _client.PostAsJson($"{_controllerName}/", body);

        if (_client.LastResponse.ResponseMessage.IsSuccessStatusCode)
        {
            var c = _client.ReadAs<Ping.V1.Output?>();
            _output = c;
            if (c?.Name != null)
                _entityNameId.TryAdd(c.Name, c.Id);
        }
    }

    [When(@"I create multiple Ping with")]
    public void WhenICreateMultiplePingWith(Table table)
    {
        var entities = table.CreateSet<Ping.V1.Create>();

        foreach (var e in entities)
        {
            _client.PostAsJson($"{_controllerName}", e);
            _client.ThenTheRequestSucceded();

            if (_client.LastStatusCodeIsSuccess())
            {
                var res = _client.ReadAs<Ping.V1.Output?>();
                if (res?.Name != null)
                    _entityNameId.Add(res.Name, res.Id);
            }
        }
    }

    //** GET ********************************************************************
    [When(@"I request the Ping '([^']*)' by id")]
    public void WhenIRequestThePingById(string name)
    {
        var pingId = _entityNameId[name];
        _client.Get($"{_controllerName}/{pingId}");
    }

    [When(@"I request the Ping by")]
    public void WhenIRequestThePingBy(Table table)
    {
        var searchQuery = table.CreateInstance<PingSearchQueryDto.V1>();

        _client.Get($"{_controllerName}".SetQueryParams(searchQuery));
    }

    //** UPDATE ******************************************************************
    [When(@"I update using '(PATCH|PUT)' the Ping '([^']*)' with")]
    public void WhenIUpdatePutThePingWith(string method, string name, Table table)
    {
        var pingId = _entityNameId[name];

        var updateData = table.CreateInstance<Ping.V1.Update>();

        _updatePing(method, pingId, updateData);

        if (_client.LastStatusCodeIsSuccess())
        {
            var res = _client.ReadAs<Ping.V1.Output?>();
            if (res?.Name != null)
                _entityNameId.TryAdd(res.Name, res.Id);
        }
    }

    [When(@"I try to update using '([^']*)' a Ping with")]
    public void WhenITryToUpdateUsingAPingByID(string method, Table table)
    {
        var updateData = table.CreateInstance<Ping.V1.Update>();

        _updatePing(method, updateData.Id, updateData);
    }

    private void _updatePing(string method, int pingId, Ping.V1.Update payload)
    {
        switch (method)
        {
            case "PUT":
                _client.PutAsJson(
                    $"{_controllerName}/{pingId}",
                    payload
                );
                break;

            case "PATCH":
                _client.PatchAsJson(
                    $"{_controllerName}/{pingId}",
                    payload
                );
                break;
        }
    }


    //** DELETE ******************************************************************

    [When(@"I delete the Ping '([^']*)' by id")]
    public void WhenIDeleteThePingById(string name)
    {
        var idToDelete = _entityNameId[name];
        _client.Delete($"{_controllerName}/{idToDelete}");
    }

    //** THEN ********************************************************************
    [Then(@"the Ping response should match")]
    public void ThenThePingResponseShouldMatch(Table table)
    {
        var res = _client.ReadAs<Ping.V1.Output?>();
        _output = res;
        ThenThePingResponseShouldBe(table);
    }

    [Then(@"the stored Ping response should be")]
    public void ThenThePingResponseShouldBe(Table table)
    {
        PingMatched(_output, table);
    }

    [Then(@"the Ping response count should be (.*)")]
    public void ThenThePingResponseCountShouldBe(int count)
    {
        var pagedRes = _client.ReadAs<PagedResult<Ping.V1.Output>>();
        pagedRes.Count.Should().Be(count);
    }

    //** MATCH **********************************************************************
    public static void PingMatched(Ping.V1.Output? res, Table table)
    {
        var expected = table.CreateInstance<Ping.V1.Output>();

        res.Should().BeEquivalentTo(expected,
            options => options
                .Excluding(c => c.Id)
                .Excluding(c => c.AuditId)
        );
    }



    //** MESSAGE ********************************************************************
    [When("I create a single Ping And SendMsg with")]
    public void WhenICreateASinglePingAndSendMsgWith(Table table)
    {
        var body = table.CreateInstance<Ping.V1.Create>();
        _client.PostAsJson($"{_controllerName}/message", body);

        if (_client.LastResponse.ResponseMessage.IsSuccessStatusCode)
        {
            var c = _client.ReadAs<Ping.V1.Output?>();
            _output = c;
            if (c?.Name != null)
                _entityNameId.TryAdd(c.Name, c.Id);
        }
    }
}