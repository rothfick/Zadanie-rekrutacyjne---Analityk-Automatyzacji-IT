# 0001: Use event-driven orchestration

## Context

Proces reklamacyjny Metalpolu przechodzi przez kilka systemów: Microsoft 365 / Exchange, AI triage, PostgreSQL customer DB, SAP ERP, Jira Cloud, Azure Blob Storage i dashboard KPI. Obecny proces traci widoczność, bo status sprawy jest rozproszony między e-mailem, Excelem, Jira Cloud i SAP ERP.

Potrzebujemy sposobu, aby każda ważna zmiana w procesie była audytowalna i mierzalna.

## Decision

Używamy event-driven orchestration tam, gdzie zdarzenia mają znaczenie biznesowe, audytowe lub raportowe. `Complaint Orchestrator` zapisuje eventy, takie jak `EmailReceived`, `ComplaintParsed`, `DefectClassified`, `OrderVerified`, `BatchVerified`, `HumanReviewRequested`, `JiraComplaintCreated` i `CorrectionTicketCreated`.

W MVP event store może być prosty i lokalny. Ważniejsza jest semantyka zdarzeń niż produkcyjna infrastruktura.

## Consequences

- Każda reklamacja ma czytelny timeline.
- KPI mogą być liczone z eventów zamiast z ręcznych raportów.
- Łatwiej debugować przypadki brzegowe i błędy integracji.
- Implementacja jest bardziej złożona niż prosty CRUD.
- Trzeba pilnować idempotencji eventów i komend.

## Alternatives considered

- Prosty CRUD bez eventów: łatwiejszy start, ale słaby audyt i trudniejsze KPI.
- Pełny event sourcing od początku: bardzo mocny audyt, ale za duży koszt i złożoność dla MVP demonstracyjnego.
- Workflow tylko w Jira Cloud: szybkie wdrożenie operacyjne, ale brak pełnego procesu i metryk poza Jira Cloud.
