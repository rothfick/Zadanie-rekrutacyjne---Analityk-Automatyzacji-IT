using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Metalpol.Complaints.Application.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Metalpol.Complaints.Tests;

public sealed class ApiSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpointReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetStringAsync("/health");

        Assert.Equal("OK", response);
    }

    [Fact]
    public async Task StaticDemoUiAndScenarioEndpointsAreAvailable()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/");
        Assert.Contains("Metalpol Complaint Automation Control Center", html);

        var scenarios = await client.GetFromJsonAsync<JsonElement[]>("/api/demo/scenarios");
        Assert.NotNull(scenarios);
        Assert.Contains(scenarios!, scenario => scenario.GetProperty("id").GetString() == "happy-path-visual-defect");

        var scenarioPayload = await client.GetStringAsync("/api/demo/scenarios/happy-path-visual-defect");
        Assert.Contains("sourceMessageId", scenarioPayload);
        Assert.Contains("attachments", scenarioPayload);
    }

    [Fact]
    public async Task ComplaintApiSupportsIntakeDetailsTimelineReviewAndKpis()
    {
        using var client = _factory.CreateClient();
        var messageId = $"api-message-{Guid.NewGuid():N}";
        var email = new
        {
            sourceMessageId = messageId,
            from = "quality@automotive-pl.example",
            subject = "Complaint ORDER-1001",
            body = "We found scratches and paint damage. Batch BATCH-1001. Please verify.",
            attachments = new[]
            {
                new IncomingAttachmentDto("att-001", "photo.jpg", "image/jpeg", 2048)
            }
        };

        var intakeResponse = await client.PostAsJsonAsync("/api/mock/exchange/messages", email);
        Assert.Equal(HttpStatusCode.Created, intakeResponse.StatusCode);

        using var intakeJson = await JsonDocument.ParseAsync(await intakeResponse.Content.ReadAsStreamAsync());
        var complaintId = intakeJson.RootElement.GetProperty("complaintId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(complaintId));
        Assert.Equal("ResponseDrafted", intakeJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("ORDER-1001", intakeJson.RootElement.GetProperty("orderNumber").GetString());
        Assert.Equal("COMPLAINT-1001", intakeJson.RootElement.GetProperty("jiraComplaintKey").GetString());

        var details = await client.GetFromJsonAsync<JsonElement>($"/api/complaints/{complaintId}");
        Assert.Equal(complaintId, details.GetProperty("complaintId").GetString());
        Assert.Equal("Visual", details.GetProperty("defectCategory").GetString());

        var timeline = await client.GetFromJsonAsync<JsonElement[]>($"/api/complaints/{complaintId}/timeline");
        Assert.NotNull(timeline);
        Assert.Contains(timeline!, item => item.GetProperty("eventName").GetString() == "JiraComplaintCreated");

        var reviewResponse = await client.PostAsJsonAsync(
            $"/api/complaints/{complaintId}/review/approve",
            new
            {
                reviewer = "service.specialist",
                decision = "ConfirmDefect",
                notes = "Defect confirmed during demo review."
            });
        reviewResponse.EnsureSuccessStatusCode();

        using var reviewJson = await JsonDocument.ParseAsync(await reviewResponse.Content.ReadAsStreamAsync());
        Assert.Equal("CorrectionCreated", reviewJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("CORRECTION-2001", reviewJson.RootElement.GetProperty("correctionIssueKey").GetString());

        var kpis = await client.GetFromJsonAsync<JsonElement>("/api/dashboard/kpis");
        Assert.True(kpis.GetProperty("totalComplaints").GetInt32() >= 1);
        Assert.True(kpis.GetProperty("correctionsCreated").GetInt32() >= 1);

        var resetResponse = await client.PostAsync("/api/demo/reset", null);
        resetResponse.EnsureSuccessStatusCode();

        var resetKpis = await client.GetFromJsonAsync<JsonElement>("/api/dashboard/kpis");
        Assert.Equal(0, resetKpis.GetProperty("totalComplaints").GetInt32());
        Assert.Equal(0, resetKpis.GetProperty("correctionsCreated").GetInt32());
    }
}
