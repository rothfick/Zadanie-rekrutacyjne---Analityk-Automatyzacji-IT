using System.Text.Json.Serialization;
using Metalpol.Complaints.Api;
using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Application.Orchestration;
using Metalpol.Complaints.Application.Ports;
using Metalpol.Complaints.Application.Review;
using Metalpol.Complaints.Domain.Events;
using Metalpol.Complaints.Domain.ValueObjects;
using Metalpol.Complaints.Infrastructure.Fakes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<InMemoryComplaintRepository>();
builder.Services.AddSingleton<IComplaintRepository>(provider => provider.GetRequiredService<InMemoryComplaintRepository>());
builder.Services.AddSingleton<InMemoryEventLog>();
builder.Services.AddSingleton<IEventLog>(provider => provider.GetRequiredService<InMemoryEventLog>());
builder.Services.AddSingleton<IBlobStorageClient, FakeBlobStorageClient>();
builder.Services.AddSingleton<IAiTriageService, MockAiTriageService>();
builder.Services.AddSingleton<ICustomerLookupService, FakeCustomerLookupService>();
builder.Services.AddSingleton<ISapClient, FakeSapClient>();
builder.Services.AddSingleton<FakeJiraClient>();
builder.Services.AddSingleton<IJiraClient>(provider => provider.GetRequiredService<FakeJiraClient>());
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IComplaintOrchestrator, ComplaintIntakeOrchestrator>();
builder.Services.AddScoped<IComplaintReviewService, ComplaintReviewService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Text("OK"));

app.MapGet(
        "/api/demo/scenarios",
        () => Results.Ok(DemoScenarioCatalog.List()))
    .WithName("ListDemoScenarios")
    .WithTags("Demo");

app.MapGet(
        "/api/demo/scenarios/{id}",
        (string id) =>
        {
            var scenario = DemoScenarioCatalog.ReadScenarioJson(id);

            return scenario is null
                ? Results.NotFound(new { error = $"Demo scenario {id} was not found." })
                : Results.Text(scenario, "application/json");
        })
    .WithName("GetDemoScenario")
    .WithTags("Demo");

app.MapPost(
        "/api/demo/reset",
        (
            InMemoryComplaintRepository repository,
            InMemoryEventLog eventLog,
            FakeJiraClient jira) =>
        {
            repository.Clear();
            eventLog.Clear();
            jira.Reset();

            return Results.Ok(new { reset = true, message = "Demo state reset." });
        })
    .WithName("ResetDemoState")
    .WithTags("Demo");

app.MapPost(
        "/api/mock/exchange/messages",
        async (
            MockExchangeMessageRequest request,
            IComplaintOrchestrator orchestrator,
            IComplaintRepository repository,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var email = request.ToIncomingEmail(clock);
            var duplicate = await repository.GetByMessageIdAsync(email.MessageId, cancellationToken) is not null;
            var result = await orchestrator.StartIntakeAsync(email, cancellationToken);
            var complaint = await repository.GetByIdAsync(result.ComplaintId, cancellationToken);

            if (complaint is null)
            {
                return Results.Problem("Complaint intake completed but complaint record was not found.");
            }

            var response = ApiContractMapper.ToIntakeResponse(complaint, duplicate);

            return duplicate
                ? Results.Ok(response)
                : Results.Created($"/api/complaints/{complaint.Id.Value}", response);
        })
    .WithName("MockExchangeMessage")
    .WithTags("Mock Exchange");

app.MapGet(
        "/api/complaints/{id}",
        async (
            string id,
            IComplaintRepository repository,
            CancellationToken cancellationToken) =>
        {
            var complaint = await repository.GetByIdAsync(new ComplaintId(id), cancellationToken);

            return complaint is null
                ? Results.NotFound(new { error = $"Complaint {id} was not found." })
                : Results.Ok(ApiContractMapper.ToDetailsResponse(complaint));
        })
    .WithName("GetComplaint")
    .WithTags("Complaints");

app.MapGet(
        "/api/complaints/{id}/timeline",
        async (
            string id,
            IComplaintRepository repository,
            IEventLog eventLog,
            CancellationToken cancellationToken) =>
        {
            var complaintId = new ComplaintId(id);
            var complaint = await repository.GetByIdAsync(complaintId, cancellationToken);
            if (complaint is null)
            {
                return Results.NotFound(new { error = $"Complaint {id} was not found." });
            }

            var timeline = await eventLog.GetTimelineAsync(complaintId, cancellationToken);

            return Results.Ok(timeline);
        })
    .WithName("GetComplaintTimeline")
    .WithTags("Complaints");

app.MapPost(
        "/api/complaints/{id}/review/approve",
        async (
            string id,
            ReviewApprovalRequest request,
            IComplaintReviewService reviewService,
            CancellationToken cancellationToken) =>
        {
            var result = await reviewService.ApproveComplaintAsync(
                new ComplaintId(id),
                request.Reviewer,
                request.Decision,
                request.Notes,
                cancellationToken);
            var response = ApiContractMapper.ToReviewResponse(result);

            if (result.Succeeded)
            {
                return Results.Ok(response);
            }

            return string.Equals(result.Error, $"Complaint {id} was not found.", StringComparison.OrdinalIgnoreCase)
                ? Results.NotFound(response)
                : Results.BadRequest(response);
        })
    .WithName("ApproveComplaintReview")
    .WithTags("Review");

app.MapGet(
        "/api/dashboard/kpis",
        async (
            IComplaintRepository repository,
            IEventLog eventLog,
            CancellationToken cancellationToken) =>
        {
            var complaints = await repository.ListAsync(cancellationToken);
            var sapVerificationFailureCount = 0;

            foreach (var complaint in complaints)
            {
                var timeline = await eventLog.GetTimelineAsync(complaint.Id, cancellationToken);
                if (timeline.Any(item => item.EventName == nameof(SapMismatchDetected)))
                {
                    sapVerificationFailureCount++;
                }
            }

            return Results.Ok(ApiContractMapper.ToDashboardResponse(complaints, sapVerificationFailureCount));
        })
    .WithName("GetDashboardKpis")
    .WithTags("Dashboard");

app.Run();

public partial class Program;
