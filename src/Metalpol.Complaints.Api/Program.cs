using System.Text.Json.Serialization;
using Metalpol.Complaints.Api;
using Metalpol.Complaints.Application.Orchestration;
using Metalpol.Complaints.Application.Ports;
using Metalpol.Complaints.Application.Review;
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

app.MapGet("/health", () => Results.Text("OK"));

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
            var result = await orchestrator.StartIntakeAsync(email, cancellationToken);
            var complaint = await repository.GetByIdAsync(result.ComplaintId, cancellationToken);

            return complaint is null
                ? Results.Problem("Complaint intake completed but complaint record was not found.")
                : Results.Created(
                    $"/api/complaints/{complaint.Id.Value}",
                    ApiContractMapper.ToIntakeResponse(complaint));
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
            CancellationToken cancellationToken) =>
        {
            var complaints = await repository.ListAsync(cancellationToken);

            return Results.Ok(ApiContractMapper.ToDashboardResponse(complaints));
        })
    .WithName("GetDashboardKpis")
    .WithTags("Dashboard");

app.Run();

public partial class Program;
