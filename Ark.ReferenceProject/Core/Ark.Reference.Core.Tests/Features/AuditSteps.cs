using Ark.Tools.Core;

using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;

using FluentAssertions;

using Ark.Reference.Common.Dto;
using Ark.Reference.Common.Services.Audit;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;

namespace Ark.Reference.Core.Tests.Features
{
    [Binding]
    internal class AuditSteps
    {
        private readonly TestClient _client;
        private AuditDto<AuditKind>? _lastAudit;
        private AuditRecordReturn<JsonElement?>? _changes;

        public AuditSteps(TestClient client)
        {
            _client = client;
        }

        [When(@"I get the last audit for '(.*)'")]
        public void WhenIGetTheLastAuditFor(string auditKind)
        {
            _client.Get($"audit?limit=10&auditKinds={auditKind}");

            var results = _client.ReadAs<PagedResult<AuditDto<AuditKind>>>();

            _lastAudit = results.Data.OrderByDescending(x => x.SysStartTime).FirstOrDefault();
        }

        [Then(@"the audit record has")]
        public void ThenTheAuditHas(Table table)
        {
            table.CompareToInstance(_lastAudit);
        }

        [When(@"I get the list of changes for this audit")]
        public void WhenIGetTheChangesForThisAudit()
        {
            _client.Get($"audit/{_lastAudit?.AuditId}/changes");

            _changes = _client.ReadAs<AuditRecordReturn<JsonElement?>>();
        }

        [Then(@"the list of changes contains (.*) records")]
        public void ThenTheChangesContainsRecords(int count)
        {
            Assert.AreEqual(count, _changes?.Changes?.Count());
        }

        
        [Then(@"the (current|previous) Ping audit is")]
        public void ThenTheCurrentContractAuditIs(string choice, Table table)
        {
            var changes = _changes?.Changes?.First();

            var expected = table.CreateInstance<Ping.V1.Output>();

            Ping.V1.Output? res;
            if (choice == "current")
            {
                res = changes?.Cur?.ToObject<AuditedEntityDto<Ping.V1.Output>>()?.Entity;
            }
            else
            {
                res = changes?.Pre?.ToObject<AuditedEntityDto<Ping.V1.Output>>()?.Entity;
            }

            res.Should().BeEquivalentTo(expected, options => options
                .Excluding(p => p.Id)
                .Excluding(p => p.AuditId)
            );
        }
        
        public class AuditRecordReturn<TAuditObject>
        {
            public IEnumerable<Changes<TAuditObject>.V1>? Changes { get; set; }
        }
    }
}
