// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Tests.Init;
using Ark.Tools.Compliance;

using AwesomeAssertions;

using Dapper;

using Microsoft.Data.SqlClient;

using NLog;

using Reqnroll;

namespace Ark.Reference.Core.Tests.Features;

[Binding]
public sealed class ComplianceSteps
{
    private readonly TestClient _client;

    public ComplianceSteps(TestClient client)
    {
        _client = client;
    }

    [Then("the API response contains the cleartext author")]
    public void ThenTheApiResponseContainsTheCleartextAuthor()
    {
        var response = _client.ReadAsString();
        response.Should().Contain(ComplianceFakes.PersonName());
        response.Should().NotContain(ArkErasingRedactor.Marker);
    }

    [Then("the compliance log and span contain only the masked author")]
    public void ThenTheComplianceLogAndSpanContainOnlyTheMaskedAuthor()
    {
        LogManager.Flush(TimeSpan.FromSeconds(2));

        TestHost._logs._getMessages()
            .Should()
            .Contain(message => message.Contains("Creating book with author", StringComparison.Ordinal)
                && message.Contains(ArkErasingRedactor.Marker, StringComparison.Ordinal))
            .And.NotContain(message => message.Contains(ComplianceFakes.PersonName(), StringComparison.Ordinal));

        TestHost._telemetry._getSpans()
            .Should()
            .Contain(span => span.Name == "book.create"
                && span.Tags.TryGetValue("ark.compliance.book.author", out var value)
                && value == ArkErasingRedactor.Marker)
            .And.NotContain(span => span.Tags.Values.Contains(ComplianceFakes.PersonName(), StringComparer.Ordinal));
    }

    [Then("the database masks the author for a low-privilege reader")]
    public async Task ThenTheDatabaseMasksTheAuthorForALowPrivilegeReader()
    {
        var bookId = _client.ReadAs<Book.V1.Output>().Id;
        var masked = await _readAuthorAs("ComplianceMaskedReader", bookId).ConfigureAwait(false);
        var privileged = await _readAuthorAs("CompliancePrivilegedReader", bookId).ConfigureAwait(false);

        masked.Should().NotBe(ComplianceFakes.PersonName());
        masked.Should().NotBeNullOrWhiteSpace();
        privileged.Should().Be(ComplianceFakes.PersonName());
    }

    private static async Task<string?> _readAuthorAs(string userName, int bookId)
    {
        await using var connection = new SqlConnection(TestHost.DBConfig.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await connection.ExecuteAsync("EXECUTE AS USER = @UserName", new { UserName = userName }).ConfigureAwait(false);
        try
        {
            return await connection.QuerySingleAsync<string?>(
                "SELECT [Author] FROM [dbo].[Book] WHERE [Id] = @BookId",
                new { BookId = bookId }).ConfigureAwait(false);
        }
        finally
        {
            await connection.ExecuteAsync("REVERT").ConfigureAwait(false);
        }
    }
}
