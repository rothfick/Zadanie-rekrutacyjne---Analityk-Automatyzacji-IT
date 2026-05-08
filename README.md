# Metalpol AI Complaint Automation

Metalpol AI Complaint Automation to case study automatyzacji obsługi reklamacji dla fikcyjnego producenta komponentów metalowych. Repozytorium łączy analizę biznesową, Event Storming, dokumentację architektury, guardraile dla AI oraz uruchamialne demo MVP w .NET. AI wspiera ekstrakcję danych, klasyfikację, streszczenie i przygotowanie szkicu odpowiedzi; za walidację oraz końcowe decyzje biznesowe odpowiadają deterministyczna logika procesu i człowiek.

## Co Zawiera Repozytorium

- Dokumentację biznesową i procesową dla przypadku obsługi reklamacji w Metalpol.
- Modele Event Storming AS-IS oraz TO-BE.
- Specyfikację techniczną rozwiązania z granicami integracji i trade-offami.
- Projekt automatyzacji AI skupiony na triage, ekstrakcji, wsparciu draftów i guardrailach.
- Kontrakty integracyjne dla Microsoft 365 / Exchange, SAP ERP, Jira Cloud, PostgreSQL customer DB i Azure Blob Storage.
- Maszynę stanów reklamacji, katalog edge case'ów oraz model KPI/reportingu.
- Architecture Decision Records w [docs/adr](docs/adr).
- Uruchamialne mock MVP w .NET z Minimal API, orchestrator, modelem domenowym, deterministycznymi adapterami, scenariuszami demo i testami.
- Statyczny demo UI serwowany przez API.

Terminologia: dokumentacja biznesowa używa polskiego terminu **reklamacja**. Kod, namespace'y, kontrakty API, statusy, eventy i typy issue w Jira używają angielskiego terminu **Complaint**.

## Jak Uruchomić Lokalnie

Wymagany SDK: .NET zgodny z target framework projektu (`net10.0`).

```bash
dotnet restore Metalpol.Complaints.sln
dotnet build Metalpol.Complaints.sln
dotnet test Metalpol.Complaints.sln
dotnet run --project src/Metalpol.Complaints.Api --urls http://127.0.0.1:5058
```

Demo w shellu:

```bash
chmod +x scripts/demo.sh
BASE_URL=http://127.0.0.1:5058 ./scripts/demo.sh
```

Demo w PowerShell:

```powershell
$env:BASE_URL = "http://127.0.0.1:5058"
./scripts/demo.ps1
```

## Jak Otworzyć UI

Po uruchomieniu API otwórz:

```text
http://127.0.0.1:5058/
```

UI pozwala wybrać przykładową reklamację, uruchomić pipeline automatyzacji, przejrzeć szczegóły reklamacji i eventy timeline, wykonać human review, utworzyć mock Jira Correction oraz odświeżyć karty KPI. Ten sam przepływ jest dostępny przez endpointy API i skrypty.

## Scenariusze Demo

Payloady scenariuszy znajdują się w [samples/scenarios](samples/scenarios). Każdy scenariusz można wysłać bezpośrednio:

```bash
curl -sS -X POST http://127.0.0.1:5058/api/mock/exchange/messages \
  -H "Content-Type: application/json" \
  --data @samples/scenarios/happy-path-visual-defect.json
```

| Scenariusz | Oczekiwany wynik | Cel |
| --- | --- | --- |
| [happy-path-visual-defect.json](samples/scenarios/happy-path-visual-defect.json) | `ResponseDrafted`, Jira Cloud `Complaint` | Standardowy przepływ bez ręcznego przepisywania danych. |
| [missing-order-number.json](samples/scenarios/missing-order-number.json) | `HumanReviewRequired`, bez Jira Cloud `Complaint` | AI nie wymyśla krytycznych danych. |
| [dimensional-defect-low-confidence.json](samples/scenarios/dimensional-defect-low-confidence.json) | `HumanReviewRequired` | Niska pewność kieruje sprawę do człowieka. |
| [sap-order-not-found.json](samples/scenarios/sap-order-not-found.json) | `HumanReviewRequired`, niezgodność w SAP ERP | Jira Cloud `Complaint` nie jest tworzony bez walidacji zamówienia. |
| [prompt-injection-attempt.json](samples/scenarios/prompt-injection-attempt.json) | `PromptInjectionDetected` | Treść maila klienta jest traktowana jako niezaufane wejście. |
| [duplicate-message.json](samples/scenarios/duplicate-message.json) | `200 OK`, `duplicate = true`, istniejąca reklamacja bez drugiego Jira Cloud `Complaint` | Idempotencja po `sourceMessageId`. |
| [logistics-complaint.json](samples/scenarios/logistics-complaint.json) | `Logistics` | Kontrolowana taksonomia wad wspiera raportowanie. |
| [material-defect-requires-correction.json](samples/scenarios/material-defect-requires-correction.json) | Correction po akceptacji | AI przygotowuje kontekst, a człowiek potwierdza decyzję jakościową. |

## Przegląd Architektury

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

- [Domain](src/Metalpol.Complaints.Domain) - agregat `Complaint`, statusy, value objects i domain events.
- [Application](src/Metalpol.Complaints.Application) - orkiestracja, porty, DTO i przepływ human review.
- [Infrastructure](src/Metalpol.Complaints.Infrastructure) - deterministyczne fake adapters i dane przykładowe.
- [API](src/Metalpol.Complaints.Api) - Minimal API dla mock intake, szczegółów reklamacji, timeline, review approval, demo reset i KPI.
- [Demo UI](src/Metalpol.Complaints.Api/wwwroot) - statyczny interfejs serwowany przez API.
- [Tests](tests/Metalpol.Complaints.Tests) - testy jednostkowe, API smoke tests, edge case'y i Playwright UI E2E.

## Co Jest Mockowane I Dlaczego

Demo MVP nie używa prawdziwych credentiali, danych klientów ani zewnętrznych kont. Zależności zewnętrzne są reprezentowane przez deterministyczne adaptery:

- Microsoft 365 / Exchange - mock endpoint `POST /api/mock/exchange/messages`.
- SAP ERP - przykładowe zamówienia i partie z [samples/sap](samples/sap).
- Jira Cloud - fake keys, np. `COMPLAINT-*` i `CORRECTION-*`.
- Azure Blob Storage - fake URI załączników, bez prawdziwych SAS tokens.
- PostgreSQL customer DB - read-only przykładowi klienci z [samples/customers](samples/customers).
- AI triage - deterministyczny parser/classifier, dzięki któremu testy i demo są powtarzalne.

Mocki utrzymują demo bez credentiali i pozwalają uruchomić je lokalnie, zachowując jednocześnie kontrakty integracyjne oraz granice odpowiedzialności systemów.

## Kluczowe Zasady Projektowe

- Najpierw klarowność biznesowa: każdy mechanizm automatyzacji mapuje się na problem procesu albo KPI.
- AI obsługuje niepewność języka; deterministyczna logika systemu kontroluje stan procesu, walidację, integracje, audyt i odpowiedzialność.
- SAP ERP pozostaje źródłem prawdy dla zamówień i partii.
- Jira Cloud pozostaje operacyjnym systemem workflow dla ticketów `Complaint` i `Correction`.
- Complaint orchestrator odpowiada za stan procesu, idempotencję i audit timeline.
- Human review jest wymagany przy brakujących danych, niskiej pewności, niezgodności SAP, podejrzeniu duplikatu, prompt injection i przypadkach wysokiego ryzyka.
- Porty integracyjne są provider-neutral i nie zależą od SDK dostawców w warstwie Application.
- MVP jest mock implementation zaprojektowaną do pokazania wykonalności procesu, a nie wdrożeniem produkcyjnym.

Szczegóły decyzji architektonicznych:

- [ADR 0001 - Event-driven orchestration](docs/adr/0001-use-event-driven-orchestration.md)
- [ADR 0002 - LLM for triage, not final decisions](docs/adr/0002-use-llm-for-triage-not-final-decisions.md)
- [ADR 0003 - Human-in-the-loop for risky cases](docs/adr/0003-keep-human-in-the-loop-for-risky-cases.md)
- [ADR 0004 - Mocks for demo MVP](docs/adr/0004-use-mocks-for-demo-mvp.md)

## Testy I Walidacja

```bash
dotnet restore Metalpol.Complaints.sln
dotnet build Metalpol.Complaints.sln
dotnet test Metalpol.Complaints.sln
```

`dotnet test` obejmuje testy Playwright UI E2E. Na świeżej maszynie Chromium może wymagać instalacji po pierwszym buildzie:

```bash
pwsh tests/Metalpol.Complaints.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

Jeśli na macOS brakuje `pwsh`, można zainstalować PowerShell przez Homebrew:

```bash
brew install powershell
```

GitHub Actions w [.github/workflows/ci.yml](.github/workflows/ci.yml) uruchamia restore, build, instalację Chromium i `dotnet test`.

## Mapa Repozytorium

- [docs/01-business-context.md](docs/01-business-context.md) - kontekst biznesowy, interesariusze, problemy i zakres.
- [docs/02-as-is-event-storming.md](docs/02-as-is-event-storming.md) - model obecnego procesu manualnego.
- [docs/03-to-be-event-storming.md](docs/03-to-be-event-storming.md) - model proponowanego procesu automatyzacji.
- [docs/04-solution-specification.md](docs/04-solution-specification.md) - architektura rozwiązania i komponenty.
- [docs/05-ai-automation-design.md](docs/05-ai-automation-design.md) - użycie AI, JSON schema, progi confidence i guardraile.
- [docs/06-integration-contracts.md](docs/06-integration-contracts.md) - kontrakty integracyjne i obsługa awarii.
- [docs/07-state-machine-and-edge-cases.md](docs/07-state-machine-and-edge-cases.md) - stany, przejścia i edge case'y.
- [docs/08-kpis-and-reporting.md](docs/08-kpis-and-reporting.md) - definicje KPI i management dashboard.
- [docs/09-roadmap-tradeoffs.md](docs/09-roadmap-tradeoffs.md) - roadmapa etapowa i trade-offy architektoniczne.
- [docs/10-demo-runbook.md](docs/10-demo-runbook.md) - instrukcje demo dla API, UI i skryptów.
- [docs/adr/README.md](docs/adr/README.md) - indeks ADR.
- [samples/scenarios](samples/scenarios) - przykładowe payloady reklamacji.
- [scripts/demo.sh](scripts/demo.sh) i [scripts/demo.ps1](scripts/demo.ps1) - uruchamialne skrypty demo.
- [tests/Metalpol.Complaints.Tests](tests/Metalpol.Complaints.Tests) - zautomatyzowany zestaw testów.
