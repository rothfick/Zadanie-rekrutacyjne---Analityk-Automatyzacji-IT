# Metalpol AI Complaint Automation

Metalpol AI Complaint Automation is a complaint handling automation case study for a fictional metal components manufacturer. The repository combines business analysis, Event Storming, architecture documentation, AI guardrails and a runnable .NET demo MVP. AI supports extraction, classification, summary and response drafting; deterministic process logic and human review remain responsible for validation and final business decisions.

## What This Repository Contains

- Business and process documentation for the Metalpol complaint handling case.
- AS-IS and TO-BE Event Storming models.
- Technical solution specification with integration boundaries and trade-offs.
- AI automation design focused on triage, extraction, draft support and guardrails.
- Integration contracts for Microsoft 365 / Exchange, SAP ERP, Jira Cloud, PostgreSQL customer DB and Azure Blob Storage.
- Complaint state machine, edge case catalogue and KPI/reporting model.
- Architecture Decision Records in [docs/adr](docs/adr).
- Runnable .NET mock MVP with Minimal API, orchestrator, domain model, deterministic fake adapters, sample scenarios and tests.
- Static demo UI served by the API.

Terminology: business documentation uses the Polish term **reklamacja**. Code, namespaces, API contracts, statuses, events and Jira issue types use the English term **Complaint**.

## How To Run Locally

Required SDK: .NET matching the project target framework (`net10.0`).

```bash
dotnet restore Metalpol.Complaints.sln
dotnet build Metalpol.Complaints.sln
dotnet test Metalpol.Complaints.sln
dotnet run --project src/Metalpol.Complaints.Api --urls http://127.0.0.1:5058
```

Shell demo:

```bash
chmod +x scripts/demo.sh
BASE_URL=http://127.0.0.1:5058 ./scripts/demo.sh
```

PowerShell demo:

```powershell
$env:BASE_URL = "http://127.0.0.1:5058"
./scripts/demo.ps1
```

## How To Open The UI

After starting the API, open:

```text
http://127.0.0.1:5058/
```

The UI lets a demo user select a sample complaint, run the automation pipeline, inspect complaint details and timeline events, perform human review, create a mock Jira Correction and refresh KPI cards. The same flow remains available through API endpoints and scripts.

## Demo Scenarios

Scenario payloads are stored in [samples/scenarios](samples/scenarios). Any scenario can be posted directly:

```bash
curl -sS -X POST http://127.0.0.1:5058/api/mock/exchange/messages \
  -H "Content-Type: application/json" \
  --data @samples/scenarios/happy-path-visual-defect.json
```

| Scenario | Expected result | Purpose |
| --- | --- | --- |
| [happy-path-visual-defect.json](samples/scenarios/happy-path-visual-defect.json) | `ResponseDrafted`, Jira Cloud `Complaint` | Standard flow without manual data copying. |
| [missing-order-number.json](samples/scenarios/missing-order-number.json) | `HumanReviewRequired`, no Jira Cloud `Complaint` | AI does not invent critical data. |
| [dimensional-defect-low-confidence.json](samples/scenarios/dimensional-defect-low-confidence.json) | `HumanReviewRequired` | Low confidence routes the case to a human. |
| [sap-order-not-found.json](samples/scenarios/sap-order-not-found.json) | `HumanReviewRequired`, SAP ERP mismatch | Jira Cloud `Complaint` is not created without order validation. |
| [prompt-injection-attempt.json](samples/scenarios/prompt-injection-attempt.json) | `PromptInjectionDetected` | Customer email body is treated as untrusted input. |
| [duplicate-message.json](samples/scenarios/duplicate-message.json) | Existing complaint without a second Jira Cloud `Complaint` | Idempotency by `sourceMessageId`. |
| [logistics-complaint.json](samples/scenarios/logistics-complaint.json) | `Logistics` | Controlled defect taxonomy supports reporting. |
| [material-defect-requires-correction.json](samples/scenarios/material-defect-requires-correction.json) | Correction after approval | AI prepares context; a human confirms the quality decision. |

## Architecture Overview

```text
Microsoft 365 / Exchange webhook
  -> Complaint Intake API
  -> Complaint Orchestrator
  -> Mock AI Triage
  -> PostgreSQL customer DB + SAP ERP validation
  -> Azure Blob Storage attachment archive
  -> Jira Cloud Complaint
  -> Response draft
  -> Human review
  -> Jira Cloud Correction
  -> KPI read model
```

Code layers:

- [Domain](src/Metalpol.Complaints.Domain) - `Complaint` aggregate, statuses, value objects and domain events.
- [Application](src/Metalpol.Complaints.Application) - orchestration, ports, DTOs and human review flow.
- [Infrastructure](src/Metalpol.Complaints.Infrastructure) - deterministic fake adapters and sample data.
- [API](src/Metalpol.Complaints.Api) - Minimal API for mock intake, complaint details, timeline, review approval, demo reset and KPI.
- [Demo UI](src/Metalpol.Complaints.Api/wwwroot) - static UI served by the API.
- [Tests](tests/Metalpol.Complaints.Tests) - unit tests, API smoke tests, edge cases and Playwright UI E2E.

## What Is Mocked And Why

The demo MVP does not use real credentials, customer data or external accounts. External dependencies are represented by deterministic adapters:

- Microsoft 365 / Exchange - mock endpoint `POST /api/mock/exchange/messages`.
- SAP ERP - sample orders and batches from [samples/sap](samples/sap).
- Jira Cloud - fake keys such as `COMPLAINT-*` and `CORRECTION-*`.
- Azure Blob Storage - fake attachment URIs, no real SAS tokens.
- PostgreSQL customer DB - read-only sample customers from [samples/customers](samples/customers).
- AI triage - deterministic parser/classifier so tests and demos remain repeatable.

Mocks keep the demo credential-free and locally runnable while preserving integration contracts and ownership boundaries.

## Key Design Principles

- Business clarity first: each automation mechanism maps to a process problem or KPI.
- AI supports language uncertainty; deterministic system logic controls process state, validation, integrations, audit and responsibility.
- SAP ERP remains source of truth for orders and batches.
- Jira Cloud remains the operational workflow system for `Complaint` and `Correction` tickets.
- The complaint orchestrator owns process state, idempotency and audit timeline.
- Human review is required for missing data, low confidence, SAP mismatch, duplicate suspicion, prompt injection and high-risk cases.
- Integration ports are provider-neutral and do not depend on vendor SDKs in the Application layer.
- The MVP is a mock implementation designed to demonstrate feasibility, not a production deployment.

Architecture decision details:

- [ADR 0001 - Event-driven orchestration](docs/adr/0001-use-event-driven-orchestration.md)
- [ADR 0002 - LLM for triage, not final decisions](docs/adr/0002-use-llm-for-triage-not-final-decisions.md)
- [ADR 0003 - Human-in-the-loop for risky cases](docs/adr/0003-keep-human-in-the-loop-for-risky-cases.md)
- [ADR 0004 - Mocks for demo MVP](docs/adr/0004-use-mocks-for-demo-mvp.md)

## Test And Validation Commands

```bash
dotnet restore Metalpol.Complaints.sln
dotnet build Metalpol.Complaints.sln
dotnet test Metalpol.Complaints.sln
```

`dotnet test` includes Playwright UI E2E tests. On a fresh machine, Chromium may need to be installed after the first build:

```bash
pwsh tests/Metalpol.Complaints.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

GitHub Actions in [.github/workflows/ci.yml](.github/workflows/ci.yml) runs restore, build, Chromium installation and `dotnet test`.

## Repository Map

- [docs/01-business-context.md](docs/01-business-context.md) - business context, stakeholders, problems and scope.
- [docs/02-as-is-event-storming.md](docs/02-as-is-event-storming.md) - current manual process model.
- [docs/03-to-be-event-storming.md](docs/03-to-be-event-storming.md) - proposed automated process model.
- [docs/04-solution-specification.md](docs/04-solution-specification.md) - solution architecture and components.
- [docs/05-ai-automation-design.md](docs/05-ai-automation-design.md) - AI usage, JSON schema, confidence thresholds and guardrails.
- [docs/06-integration-contracts.md](docs/06-integration-contracts.md) - integration contracts and failure handling.
- [docs/07-state-machine-and-edge-cases.md](docs/07-state-machine-and-edge-cases.md) - states, transitions and edge cases.
- [docs/08-kpis-and-reporting.md](docs/08-kpis-and-reporting.md) - KPI definitions and management dashboard.
- [docs/09-roadmap-tradeoffs.md](docs/09-roadmap-tradeoffs.md) - phased roadmap and architectural trade-offs.
- [docs/10-demo-runbook.md](docs/10-demo-runbook.md) - API, UI and script demo instructions.
- [docs/adr/README.md](docs/adr/README.md) - ADR index.
- [samples/scenarios](samples/scenarios) - sample complaint payloads.
- [scripts/demo.sh](scripts/demo.sh) and [scripts/demo.ps1](scripts/demo.ps1) - runnable demo scripts.
- [tests/Metalpol.Complaints.Tests](tests/Metalpol.Complaints.Tests) - automated test suite.
