# Metalpol AI Complaint Automation

Repozytorium przedstawia rozwiązanie dla przypadku Metalpol: automatyzację obsługi reklamacji z zachowaniem kontroli procesu, audytu i decyzji człowieka. Zakres obejmuje analizę biznesową, modele Event Storming AS-IS/TO-BE, specyfikację techniczną, decyzje architektoniczne, KPI oraz uruchamialne mock MVP w .NET. AI wspiera ekstrakcję danych, klasyfikację i draft odpowiedzi, ale nie podejmuje finalnej decyzji reklamacyjnej.

## Zakres repozytorium

- Dokumentacja biznesowa i procesowa: kontekst, problemy CEO, KPI, AS-IS i TO-BE.
- Specyfikacja rozwiązania: architektura, integracje, state machine, edge cases i roadmapa.
- ADR-y opisujące decyzje techniczne wraz z trade-offami.
- Uruchamialne mock MVP w .NET: API, orchestrator, domain model, fake SAP ERP, Jira Cloud, Azure Blob Storage, PostgreSQL customer DB oraz deterministyczny mock AI triage.
- Statyczny UI "Control Center" serwowany przez API.
- Scenariusze demo w JSON i skrypty uruchamiające pełny przepływ reklamacji.
- Testy automatyczne pokrywające happy path, human review, integracje mockowane, edge cases, API, KPI i UI E2E.

## Konwencja terminologiczna

W dokumentacji biznesowej używany jest polski termin **reklamacja**. W kodzie, nazwach projektów, endpointach, statusach, eventach i typach Jira używany jest angielski termin **Complaint**, zgodnie z konwencją techniczną repozytorium.

## Quickstart

Wymagany jest .NET SDK zgodny z target framework projektu (`net10.0`).

```bash
dotnet restore Metalpol.Complaints.sln
dotnet build Metalpol.Complaints.sln
dotnet test Metalpol.Complaints.sln
dotnet run --project src/Metalpol.Complaints.Api --urls http://127.0.0.1:5058
```

Po starcie API:

```text
http://127.0.0.1:5058/
```

W drugim terminalu można uruchomić skrypt demo:

```bash
chmod +x scripts/demo.sh
BASE_URL=http://127.0.0.1:5058 ./scripts/demo.sh
```

PowerShell:

```powershell
$env:BASE_URL = "http://127.0.0.1:5058"
./scripts/demo.ps1
```

## CI validation

GitHub Actions workflow w [.github/workflows/ci.yml](.github/workflows/ci.yml) wykonuje standardową bramkę jakości: restore, build, instalację Chromium dla Playwright oraz `dotnet test` z testami E2E UI.

Lokalnie, na świeżej maszynie, po pierwszym buildzie może być potrzebne:

```bash
pwsh tests/Metalpol.Complaints.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

## Demo

UI pod `http://127.0.0.1:5058/` pozwala przejść przez główny przepływ:

1. wybór scenariusza z `samples/scenarios`,
2. intake reklamacji przez mock Microsoft 365 / Exchange endpoint,
3. mock AI triage,
4. walidacja SAP ERP i PostgreSQL customer DB,
5. utworzenie Jira Cloud `Complaint`,
6. response draft,
7. timeline eventów,
8. human review i opcjonalne utworzenie Jira Cloud `Correction`,
9. odświeżenie KPI.

Oryginalne API i skrypty pozostają dostępne niezależnie od UI.

## Scenariusze demo

Scenariusze znajdują się w [samples/scenarios](samples/scenarios). Każdy payload można wysłać na:

```bash
curl -sS -X POST http://127.0.0.1:5058/api/mock/exchange/messages \
  -H "Content-Type: application/json" \
  --data @samples/scenarios/happy-path-visual-defect.json
```

| Scenariusz | Oczekiwany efekt | Co pokazuje |
| --- | --- | --- |
| [happy-path-visual-defect.json](samples/scenarios/happy-path-visual-defect.json) | `ResponseDrafted`, Jira Cloud `Complaint` | Pełny przepływ bez ręcznego przepisywania danych. |
| [missing-order-number.json](samples/scenarios/missing-order-number.json) | `HumanReviewRequired`, brak Jira Cloud `Complaint` | AI nie zgaduje krytycznych danych. |
| [dimensional-defect-low-confidence.json](samples/scenarios/dimensional-defect-low-confidence.json) | `HumanReviewRequired` | Niska pewność klasyfikacji kieruje sprawę do człowieka. |
| [sap-order-not-found.json](samples/scenarios/sap-order-not-found.json) | `HumanReviewRequired`, SAP ERP mismatch | Jira Cloud `Complaint` nie powstaje bez walidacji zamówienia. |
| [prompt-injection-attempt.json](samples/scenarios/prompt-injection-attempt.json) | `PromptInjectionDetected` | Treść maila jest niezaufanym wejściem. |
| [duplicate-message.json](samples/scenarios/duplicate-message.json) | Ten sam complaint bez drugiego Jira Cloud `Complaint` | Idempotencja po `sourceMessageId`. |
| [logistics-complaint.json](samples/scenarios/logistics-complaint.json) | `Logistics` | Kontrolowana taksonomia wspiera raportowanie. |
| [material-defect-requires-correction.json](samples/scenarios/material-defect-requires-correction.json) | Correction dopiero po approval | AI przygotowuje sprawę, człowiek decyduje. |

## Architecture overview

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

Warstwy kodu:

- [Domain](src/Metalpol.Complaints.Domain) - aggregate `Complaint`, statusy, value objects i domain events.
- [Application](src/Metalpol.Complaints.Application) - orchestrator, porty integracyjne, DTO i flow human review.
- [Infrastructure](src/Metalpol.Complaints.Infrastructure) - deterministyczne fake adaptery i sample data.
- [API](src/Metalpol.Complaints.Api) - Minimal API dla mock Microsoft 365 / Exchange, complaint details, timeline, review approval, reset demo i KPI.
- [Demo UI](src/Metalpol.Complaints.Api/wwwroot) - statyczny panel procesu.
- [Tests](tests/Metalpol.Complaints.Tests) - testy jednostkowe, smoke tests API, edge cases i E2E UI.

## Kluczowe decyzje projektowe

- AI służy do ekstrakcji, klasyfikacji, streszczenia i draftu odpowiedzi, ale nie podejmuje finalnej decyzji reklamacyjnej.
- Stan procesu, walidacja SAP ERP, tworzenie Jira Cloud, retry/idempotencja i audyt są kontrolowane deterministycznie przez aplikację.
- Human-in-the-loop jest wymagany przy brakach danych, niskiej pewności, SAP ERP mismatch, podejrzeniu duplikatu, prompt injection i przypadkach wysokiego ryzyka.
- Integracje są opisane przez provider-neutral ports, bez zależności od SDK dostawców w warstwie Application.
- Event log i timeline są projektowane od początku, bo bez mierzalności automatyzacja nie rozwiązuje problemu zarządczego.

Pełne uzasadnienia są w [ADR](docs/adr/README.md):

- [ADR 0001 - Event-driven orchestration](docs/adr/0001-use-event-driven-orchestration.md)
- [ADR 0002 - LLM for triage, not final decisions](docs/adr/0002-use-llm-for-triage-not-final-decisions.md)
- [ADR 0003 - Human-in-the-loop for risky cases](docs/adr/0003-keep-human-in-the-loop-for-risky-cases.md)
- [ADR 0004 - Mocks for demo MVP](docs/adr/0004-use-mocks-for-demo-mvp.md)

## Co jest mockowane i dlaczego

MVP nie używa prawdziwych danych ani sekretów. Wszystkie zewnętrzne zależności są zastąpione deterministycznymi adapterami:

- Microsoft 365 / Exchange - wejście przez mock endpoint `POST /api/mock/exchange/messages`.
- SAP ERP - sample orders i batches z [samples/sap](samples/sap).
- Jira Cloud - fake keys `COMPLAINT-*` i `CORRECTION-*`.
- Azure Blob Storage - fake URI dla załączników, bez realnych SAS tokenów.
- PostgreSQL customer DB - read-only sample customers z [samples/customers](samples/customers).
- AI triage - deterministyczny parser/klasyfikator, aby testy i demo były powtarzalne.

Mocki utrzymują demo lokalne, bezpieczne i niezależne od zewnętrznych kont, a jednocześnie pokazują kontrakty integracyjne i granice odpowiedzialności.

## Co dalej w produkcji

- Discovery z klientem: potwierdzenie danych, wolumenów, SLA, taksonomii wad i ról decyzyjnych.
- Podłączenie realnych adapterów Microsoft Graph, SAP ERP, Jira Cloud, PostgreSQL customer DB i Azure Blob Storage za istniejącymi portami.
- Kolejka asynchroniczna, retry/backoff, observability, alerting i monitoring nieprzetworzonych maili.
- UI dla human review z feedback loopiem do jakości klasyfikacji.
- Rozbudowa dashboardu o trendy po liniach, partiach, kategoriach, SLA i korekty klasyfikacji.
- Dopiero po zebraniu danych: ocena fine-tuningu lub innych metod poprawy modelu.

## Dokumentacja

- [01 - Business context](docs/01-business-context.md)
- [02 - AS-IS Event Storming](docs/02-as-is-event-storming.md)
- [03 - TO-BE Event Storming](docs/03-to-be-event-storming.md)
- [04 - Solution specification](docs/04-solution-specification.md)
- [05 - AI automation design](docs/05-ai-automation-design.md)
- [06 - Integration contracts](docs/06-integration-contracts.md)
- [07 - State machine and edge cases](docs/07-state-machine-and-edge-cases.md)
- [08 - KPIs and reporting](docs/08-kpis-and-reporting.md)
- [09 - Roadmap and trade-offs](docs/09-roadmap-tradeoffs.md)
- [10 - Demo runbook](docs/10-demo-runbook.md)
- [ADR index](docs/adr/README.md)

## Testy i przykłady

`dotnet test` uruchamia także E2E testy UI w Playwright. Jeżeli Chromium nie jest jeszcze zainstalowany lokalnie dla Playwright, wykonaj po pierwszym buildzie:

```bash
pwsh tests/Metalpol.Complaints.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

- [Test project](tests/Metalpol.Complaints.Tests)
- [API edge cases](tests/Metalpol.Complaints.Tests/ApiEdgeCaseTests.cs)
- [Automation business tests](tests/Metalpol.Complaints.Tests/ComplaintAutomationBusinessTests.cs)
- [Automation edge cases](tests/Metalpol.Complaints.Tests/ComplaintAutomationEdgeCaseTests.cs)
- [Review edge cases](tests/Metalpol.Complaints.Tests/ComplaintReviewEdgeCaseTests.cs)
- [UI E2E tests](tests/Metalpol.Complaints.Tests/UiE2ETests.cs)
- [Mock AI tests](tests/Metalpol.Complaints.Tests/MockAiTriageServiceTests.cs)
- [Sample scenarios](samples/scenarios)
- [Sample SAP data](samples/sap)
- [Sample customer data](samples/customers)
