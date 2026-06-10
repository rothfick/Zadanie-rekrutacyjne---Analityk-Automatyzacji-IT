<!-- README_PRESENTATION_START -->
<p align="center">
  <img src="https://capsule-render.vercel.app/api?type=rect&height=140&color=0:0B2545,100:2E74B5&text=Metalpol%20AI%20Complaint%20Automation&fontColor=FFFFFF&fontSize=30&fontAlignY=42&desc=AI-assisted%20complaint%20workflow%20with%20QA%20guardrails&descAlignY=68&descSize=15" alt="Metalpol AI Complaint Automation banner" />
</p>

<p align="center">
  <img alt="Case Study: AI QA" src="https://img.shields.io/badge/Case%20Study-AI%20QA-0B2545?style=for-the-badge" /> <img alt=".NET: Minimal API" src="https://img.shields.io/badge/.NET-Minimal%20API-512BD4?style=for-the-badge" /> <img alt="Testing: xUnit + Playwright" src="https://img.shields.io/badge/Testing-xUnit%20%2B%20Playwright-2E74B5?style=for-the-badge" /> <img alt="Workflow: Human Review" src="https://img.shields.io/badge/Workflow-Human%20Review-1F7A5C?style=for-the-badge" /> <img alt="Architecture: DDD + Ports" src="https://img.shields.io/badge/Architecture-DDD%20%2B%20Ports-5B5FC7?style=for-the-badge" />
</p>

<table>
  <tr><td><strong>Role signal</strong></td><td>AI QA, test strategy, workflow validation, backend orchestration</td></tr>
<tr><td><strong>What to inspect</strong></td><td><code>ComplaintIntakeOrchestrator</code>, edge-case tests, AI guardrail docs</td></tr>
<tr><td><strong>Best for</strong></td><td>Senior QA, QA AI Engineer, SDET, test strategy interviews</td></tr>
</table>

<!-- README_PRESENTATION_END -->

# Metalpol AI Complaint Automation

Portfolio case study for AI-assisted complaint intake in a regulated, operationally realistic manufacturing environment.

This repository models how a fictional automotive supplier could move from manual e-mail and spreadsheet-based complaint handling to a controlled automation workflow with AI triage, deterministic orchestration, human review, audit events, KPI visibility, and integration boundaries for systems such as Microsoft 365, SAP ERP, Jira Cloud, PostgreSQL, and Azure Blob Storage.

The goal is not to show "AI magic". The goal is to show how AI can be safely introduced into a business process where traceability, exception handling, human ownership, and system-of-record boundaries matter.

## Why This Project Exists

Metalpol receives customer complaints by e-mail. In the AS-IS process, a service specialist manually reads the message, extracts order details, classifies the defect, copies data into Excel, checks SAP, creates Jira tickets, prepares a customer reply, and escalates confirmed defects to the quality team.

That creates several risks:

- manual copy-paste errors between e-mail, Excel, SAP, and Jira;
- inconsistent complaint categorization;
- no reliable end-to-end status model;
- weak auditability;
- slow first response time;
- limited KPI visibility for management;
- no safe guardrails around AI-generated suggestions.

This project proposes and implements a demo-grade TO-BE workflow where AI supports intake and triage, while deterministic code and human review control the process.

## What This Demonstrates

For recruiters and engineering reviewers, this repository demonstrates:

- business analysis translated into a working technical MVP;
- Event Storming documentation for AS-IS and TO-BE process design;
- a layered .NET solution with API, application, domain, infrastructure, and tests;
- AI automation design with explicit boundaries and guardrails;
- human-in-the-loop workflow for risky or low-confidence cases;
- state-machine thinking for complaint lifecycle management;
- idempotency handling for duplicate messages;
- integration boundary design for Exchange, SAP, Jira, Blob Storage, and customer lookup;
- test coverage for happy paths, business rules, edge cases, and API behavior;
- CI with GitHub Actions;
- a static demo UI served by the API.

## Current Scope

This is a portfolio MVP, not a production integration.

Implemented:

- .NET Minimal API;
- complaint intake orchestration;
- domain model and value objects;
- deterministic fake adapters for external systems;
- mock AI triage component;
- human review and correction creation flow;
- timeline / audit events;
- dashboard KPI endpoint;
- static demo UI;
- sample scenario payloads;
- xUnit test suite;
- GitHub Actions build and test pipeline.

Intentionally mocked:

- Microsoft 365 / Exchange;
- SAP ERP;
- Jira Cloud;
- PostgreSQL customer database;
- Azure Blob Storage;
- real LLM / VLM provider.

Out of scope for this MVP:

- production authentication and authorization;
- real customer data;
- production observability;
- real provider credentials;
- automatic final complaint decisions made by AI.

## Architecture

The solution is organized around clear ownership boundaries:

```text
src/
  Metalpol.Complaints.Api/
    Minimal API endpoints, static demo UI, scenario catalog

  Metalpol.Complaints.Application/
    Orchestration, ports, DTOs, review workflow

  Metalpol.Complaints.Domain/
    Complaint aggregate, statuses, priorities, events, value objects

  Metalpol.Complaints.Infrastructure/
    Fake adapters, mock AI triage, in-memory repository, sample data

tests/
  Metalpol.Complaints.Tests/
    Business tests, edge case tests, API tests, contract-style assertions

docs/
  Business context, Event Storming, AI design, integration contracts,
  state machine, KPI model, roadmap, ADRs, demo runbook
```

Key design decisions:

- AI extracts, classifies, summarizes, and drafts; it does not decide.
- SAP remains the source of truth for order and batch verification.
- Jira remains the operational ticketing system.
- The orchestrator owns state transitions, idempotency, and audit events.
- Human review is required for missing data, low confidence, suspicious input, integration failures, and business-risky decisions.
- Every important state transition is represented as a domain event.

## AI Safety and QA Angle

The AI component is deliberately constrained. It is treated as an untrusted helper, not as an authority.

The project covers:

- structured extraction from unstructured e-mail text;
- controlled defect taxonomy;
- confidence thresholds;
- missing-field detection;
- prompt-injection detection scenarios;
- response draft generation with human approval;
- low-confidence routing to manual review;
- deterministic validation before external side effects;
- duplicate message handling.

This makes the repository relevant for QA Automation, AI QA, AI Assistant testing, and workflow validation roles.

## Demo Scenarios

Sample payloads live in `samples/scenarios`.

| Scenario | Expected outcome | What it proves |
|---|---|---|
| `happy-path-visual-defect.json` | Complaint is drafted and Jira Complaint is created | Standard automated intake |
| `missing-order-number.json` | Human review, no Jira issue | AI does not invent critical data |
| `dimensional-defect-low-confidence.json` | Human review | Confidence threshold works |
| `sap-order-not-found.json` | Human review, no Jira issue | SAP validation blocks invalid workflow |
| `prompt-injection-attempt.json` | Human review with prompt-injection flag | Customer input is not trusted as instructions |
| `duplicate-message.json` | Duplicate result, no second Jira issue | Idempotency by source message id |
| `logistics-complaint.json` | Controlled taxonomy category | Defect classification supports reporting |
| `material-defect-requires-correction.json` | Correction after human approval | Human decision gates quality action |

## Running Locally

Required:

- .NET SDK compatible with the solution target framework;
- PowerShell if you want to run the PowerShell demo script;
- Chromium dependencies if running Playwright-backed tests in a clean environment.

```bash
dotnet restore Metalpol.Complaints.sln
dotnet build Metalpol.Complaints.sln
dotnet test Metalpol.Complaints.sln
dotnet run --project src/Metalpol.Complaints.Api --urls http://127.0.0.1:5058
```

Open:

```text
http://127.0.0.1:5058/
```

Run a sample manually:

```bash
curl -sS -X POST http://127.0.0.1:5058/api/mock/exchange/messages \
  -H "Content-Type: application/json" \
  --data @samples/scenarios/happy-path-visual-defect.json
```

Run the shell demo:

```bash
chmod +x scripts/demo.sh
BASE_URL=http://127.0.0.1:5058 ./scripts/demo.sh
```

## Test Strategy

The test suite focuses on behavior rather than only endpoint availability.

Covered areas include:

- happy-path complaint intake;
- Jira Complaint creation after validation;
- human approval leading to Jira Correction creation;
- missing order number;
- unknown customer;
- SAP timeout and SAP mismatch cases;
- duplicate message handling;
- low AI confidence;
- prompt-injection style input;
- API not-found cases;
- API edge cases and response consistency.

The CI pipeline restores, builds, installs Playwright Chromium, and runs `dotnet test`.

## Documentation Map

Start here:

- `docs/01-business-context.md` - business problem and stakeholders;
- `docs/02-as-is-event-storming.md` - current process;
- `docs/03-to-be-event-storming.md` - target automation workflow;
- `docs/04-solution-specification.md` - solution boundaries and design;
- `docs/05-ai-automation-design.md` - where AI is allowed and where it is not;
- `docs/06-integration-contracts.md` - external system contracts;
- `docs/07-state-machine-and-edge-cases.md` - statuses and exception paths;
- `docs/08-kpis-and-reporting.md` - reporting model;
- `docs/09-roadmap-tradeoffs.md` - delivery plan and trade-offs;
- `docs/10-demo-runbook.md` - how to present the project;
- `docs/adr/` - architecture decision records.

## What To Review First

If you only have a few minutes:

1. Read this README.
2. Open `docs/05-ai-automation-design.md`.
3. Review `ComplaintIntakeOrchestrator.cs`.
4. Review `ComplaintAutomationBusinessTests.cs`.
5. Run the happy path and prompt-injection scenarios.

## Recruiter Signal

This project is intended to show a blend of:

- Lead QA / QA Automation thinking;
- AI workflow validation;
- business-process analysis;
- backend API and orchestration understanding;
- test strategy design;
- edge-case modelling;
- pragmatic architecture for regulated workflows.

It is especially relevant for roles involving QA Automation, AI QA, AI Assistant testing, process automation, test strategy, or quality leadership in product engineering teams.
