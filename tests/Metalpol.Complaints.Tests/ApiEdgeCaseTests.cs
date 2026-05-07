using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Metalpol.Complaints.Application.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Metalpol.Complaints.Tests;

public sealed class ApiEdgeCaseTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiEdgeCaseTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MissingComplaintEndpointsReturnNotFound()
    {
        using var client = _factory.CreateClient();
        var complaintId = $"CMP-MISSING-{Guid.NewGuid():N}";

        var details = await client.GetAsync($"/api/complaints/{complaintId}");
        var timeline = await client.GetAsync($"/api/complaints/{complaintId}/timeline");
        var review = await client.PostAsJsonAsync(
            $"/api/complaints/{complaintId}/review/approve",
            new
            {
                reviewer = "service.specialist",
                decision = "ConfirmDefect",
                notes = "Reviewed."
            });

        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, timeline.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, review.StatusCode);
    }

    [Fact]
    public async Task MissingOrderScenarioReturnsHumanReviewWithoutJiraComplaint()
    {
        using var client = _factory.CreateClient();
        var messageId = $"api-missing-order-{Guid.NewGuid():N}";

        var intakeResponse = await client.PostAsJsonAsync(
            "/api/mock/exchange/messages",
            CreateRequest(
                messageId,
                "Complaint without order",
                "The delivered components have scratches and paint damage, but the order number is not available."));
        Assert.Equal(HttpStatusCode.Created, intakeResponse.StatusCode);

        using var intakeJson = await JsonDocument.ParseAsync(await intakeResponse.Content.ReadAsStreamAsync());
        var complaintId = intakeJson.RootElement.GetProperty("complaintId").GetString();

        Assert.Equal("HumanReviewRequired", intakeJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, intakeJson.RootElement.GetProperty("orderNumber").ValueKind);
        Assert.Equal(JsonValueKind.Null, intakeJson.RootElement.GetProperty("jiraComplaintKey").ValueKind);
        Assert.True(intakeJson.RootElement.GetProperty("humanReviewRequired").GetBoolean());

        var details = await client.GetFromJsonAsync<JsonElement>($"/api/complaints/{complaintId}");
        var missingFields = details.GetProperty("missingFields").EnumerateArray().Select(item => item.GetString()).ToArray();

        Assert.Contains("orderNumber", missingFields);
        Assert.Contains("Missing required fields", details.GetProperty("humanReviewReason").GetString());
    }

    [Fact]
    public async Task DuplicateMessagePostReturnsExistingComplaintAndAddsTimelineEvent()
    {
        using var client = _factory.CreateClient();
        var messageId = $"api-duplicate-{Guid.NewGuid():N}";
        var request = CreateRequest(
            messageId,
            "Complaint ORDER-1001",
            "We found scratches and paint damage. Batch BATCH-1001.");

        var first = await client.PostAsJsonAsync("/api/mock/exchange/messages", request);
        var second = await client.PostAsJsonAsync("/api/mock/exchange/messages", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        using var firstJson = await JsonDocument.ParseAsync(await first.Content.ReadAsStreamAsync());
        using var secondJson = await JsonDocument.ParseAsync(await second.Content.ReadAsStreamAsync());
        var complaintId = firstJson.RootElement.GetProperty("complaintId").GetString();

        Assert.Equal(complaintId, secondJson.RootElement.GetProperty("complaintId").GetString());
        Assert.Equal("COMPLAINT-1001", firstJson.RootElement.GetProperty("jiraComplaintKey").GetString());
        Assert.Equal("COMPLAINT-1001", secondJson.RootElement.GetProperty("jiraComplaintKey").GetString());

        var timeline = await client.GetFromJsonAsync<JsonElement[]>($"/api/complaints/{complaintId}/timeline");

        Assert.NotNull(timeline);
        Assert.Equal(1, timeline!.Count(item => item.GetProperty("eventName").GetString() == "JiraComplaintCreated"));
        Assert.Contains(timeline, item => item.GetProperty("eventName").GetString() == "DuplicateLinked");
    }

    [Fact]
    public async Task RequestMoreInfoReviewMovesComplaintToMissingDataWithoutCorrection()
    {
        using var client = _factory.CreateClient();
        var intake = await PostScenarioAsync(
            client,
            $"api-request-more-info-{Guid.NewGuid():N}",
            "Complaint without order",
            "The package arrived damaged, but we do not have the order number yet.");
        var complaintId = intake.GetProperty("complaintId").GetString();

        var review = await client.PostAsJsonAsync(
            $"/api/complaints/{complaintId}/review/approve",
            new
            {
                reviewer = "service.specialist",
                decision = "RequestMoreInfo",
                notes = "Ask customer for order number and clearer photos."
            });
        review.EnsureSuccessStatusCode();

        using var reviewJson = await JsonDocument.ParseAsync(await review.Content.ReadAsStreamAsync());
        var details = await client.GetFromJsonAsync<JsonElement>($"/api/complaints/{complaintId}");

        Assert.Equal("MissingData", reviewJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, reviewJson.RootElement.GetProperty("correctionIssueKey").ValueKind);
        Assert.Equal(JsonValueKind.Null, details.GetProperty("correctionTicketKey").ValueKind);
        Assert.Contains("Ask customer", details.GetProperty("humanReviewReason").GetString());
    }

    [Fact]
    public async Task ConfirmDefectReviewIsIdempotentAtApiLevel()
    {
        using var client = _factory.CreateClient();
        var intake = await PostScenarioAsync(
            client,
            $"api-confirm-idempotent-{Guid.NewGuid():N}",
            "Complaint ORDER-1001",
            "We found scratches and paint damage. Batch BATCH-1001.");
        var complaintId = intake.GetProperty("complaintId").GetString();

        var first = await ConfirmDefectAsync(client, complaintId);
        var second = await ConfirmDefectAsync(client, complaintId);

        Assert.Equal("CorrectionCreated", first.GetProperty("status").GetString());
        Assert.Equal("CorrectionCreated", second.GetProperty("status").GetString());
        Assert.Equal("CORRECTION-2001", first.GetProperty("correctionIssueKey").GetString());
        Assert.Equal("CORRECTION-2001", second.GetProperty("correctionIssueKey").GetString());
    }

    private static async Task<JsonElement> PostScenarioAsync(
        HttpClient client,
        string messageId,
        string subject,
        string body)
    {
        var response = await client.PostAsJsonAsync(
            "/api/mock/exchange/messages",
            CreateRequest(messageId, subject, body));
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> ConfirmDefectAsync(HttpClient client, string? complaintId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/complaints/{complaintId}/review/approve",
            new
            {
                reviewer = "service.specialist",
                decision = "ConfirmDefect",
                notes = "Confirmed during API test."
            });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        return document.RootElement.Clone();
    }

    private static object CreateRequest(string messageId, string subject, string body)
    {
        return new
        {
            sourceMessageId = messageId,
            from = "quality@automotive-pl.example",
            subject,
            body,
            attachments = new[]
            {
                new IncomingAttachmentDto("att-001", "photo.jpg", "image/jpeg", 2048)
            }
        };
    }
}
