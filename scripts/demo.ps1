param(
    [string]$BaseUrl = $(if ($env:BASE_URL) { $env:BASE_URL } else { "http://127.0.0.1:5058" }),
    [string]$ScenarioPath = $(if ($env:SCENARIO_PATH) { $env:SCENARIO_PATH } else { "samples/scenarios/happy-path-visual-defect.json" }),
    [string]$Reviewer = $(if ($env:REVIEWER) { $env:REVIEWER } else { "service.specialist" })
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $RepoRoot

Write-Host "Metalpol AI Complaint Automation Demo"
Write-Host "API base URL: $BaseUrl"
Write-Host ""

Write-Host "Health check:"
Invoke-RestMethod -Method Get -Uri "$BaseUrl/health"
Write-Host ""

Write-Host "1. Sending sample email: $(Split-Path -Leaf $ScenarioPath)"
$ScenarioJson = Get-Content -Raw -Path $ScenarioPath
$Intake = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/mock/exchange/messages" -ContentType "application/json" -Body $ScenarioJson

$ComplaintId = $Intake.complaintId
$Timeline = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/complaints/$ComplaintId/timeline"

$OrderVerified = [bool]($Timeline | Where-Object { $_.eventName -eq "OrderVerified" })
$BatchVerified = [bool]($Timeline | Where-Object { $_.eventName -eq "BatchVerified" })

Write-Host "Created complaint: $ComplaintId"
Write-Host "Status: $($Intake.status)"
Write-Host "Jira Complaint: $($Intake.jiraComplaintKey)"
Write-Host "AI category: $($Intake.defectCategory)"
Write-Host "AI confidence: $($Intake.aiConfidence)"
Write-Host "Human review required: $($Intake.humanReviewRequired)"
Write-Host "SAP order verified: $OrderVerified"
Write-Host "Batch verified: $BatchVerified"
Write-Host ""

Write-Host "2. Timeline:"
$Timeline | ForEach-Object { Write-Host "- $($_.eventName)" }
Write-Host ""

Write-Host "3. Approving complaint as confirmed defect..."
$ApprovalPayload = @{
    reviewer = $Reviewer
    decision = "ConfirmDefect"
    notes = "Defect confirmed during local demo review."
} | ConvertTo-Json
$Review = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/complaints/$ComplaintId/review/approve" -ContentType "application/json" -Body $ApprovalPayload
Write-Host "Status after approval: $($Review.status)"
Write-Host "Correction ticket created: $($Review.correctionIssueKey)"
Write-Host ""

Write-Host "4. KPI snapshot:"
$Kpis = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/dashboard/kpis"
Write-Host "Total complaints: $($Kpis.totalComplaints)"
Write-Host "Human review required: $($Kpis.humanReviewRequired)"
Write-Host "Corrections created: $($Kpis.correctionsCreated)"
Write-Host "Average first response draft time: mocked"
Write-Host "Jira creation success rate: $($Kpis.jiraIssueCreationSuccessRatePercent)%"
Write-Host "SAP verification failure rate: $($Kpis.sapVerificationFailureRatePercent)%"
Write-Host ""

Write-Host "Result summary:"
Write-Host "- The API receives a mock Microsoft 365 / Exchange message and creates a complaint record."
Write-Host "- Mock AI extracts order, batch, defect category and draft response deterministically."
Write-Host "- Mock SAP ERP and Jira Cloud integrations keep the demo provider-neutral and repeatable."
Write-Host "- Human approval creates a Correction ticket; AI does not make the final decision."
Write-Host "- KPI output shows how the process becomes measurable."
