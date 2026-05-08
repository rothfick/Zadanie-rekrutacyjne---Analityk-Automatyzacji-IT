# Runbook demo MVP

Ten runbook prowadzi przez uruchomienie lokalnego API i wysłanie przykładowych reklamacji do mock endpointu Microsoft 365 / Exchange. Scenariusze używają wyłącznie danych fikcyjnych z katalogu `samples/`.

## Uruchomienie API

Z katalogu głównego repozytorium:

```bash
dotnet run --project src/Metalpol.Complaints.Api --urls http://127.0.0.1:5058
```

Sprawdzenie health check:

```bash
curl -i http://127.0.0.1:5058/health
```

Oczekiwany wynik:

```text
HTTP/1.1 200 OK
OK
```

## Panel demo w przeglądarce

Po uruchomieniu API otwórz:

```text
http://127.0.0.1:5058/
```

Panel "Metalpol Complaint Automation Control Center" jest lekkim UI opartym na tych samych endpointach co API. Umożliwia:

- sprawdzenie health check API,
- wyczyszczenie stanu demo przez `POST /api/demo/reset`,
- odświeżenie KPI z `GET /api/dashboard/kpis`,
- wybór scenariusza z `samples/scenarios`,
- wysłanie payloadu do `POST /api/mock/exchange/messages`,
- podejrzenie szczegółów reklamacji,
- wyświetlenie timeline'u eventów,
- wykonanie mock human review przez `POST /api/complaints/{id}/review/approve`.

UI nie zastępuje API ani skryptów. To statyczna warstwa demo korzystająca z deterministycznych mocków.

Najkrótsza ścieżka UI:

1. Otwórz `http://127.0.0.1:5058/`.
2. Wybierz `Happy path: wada wizualna` i kliknij `Przetwórz scenariusz`.
3. Sprawdź wynik: status, Jira Complaint, AI confidence i następny krok procesu.
4. Sprawdź `Pipeline` oraz `Timeline`: AI extraction, SAP ERP verification, Jira Cloud Complaint i eventy procesu.
5. W sekcji `Human review` kliknij `Potwierdź wadę / ConfirmDefect`, żeby utworzyć mock Jira Correction.
6. Odśwież KPI.
7. Jeżeli trzeba powtórzyć ten sam scenariusz od początku, kliknij `Reset demo`.

## Szybkie demo skryptem

macOS/Linux:

```bash
chmod +x scripts/demo.sh
BASE_URL=http://127.0.0.1:5058 ./scripts/demo.sh
```

PowerShell (`pwsh`):

```powershell
$env:BASE_URL = "http://127.0.0.1:5058"
./scripts/demo.ps1
```

Na macOS/Linux można uruchomić skrypt bez wchodzenia do sesji PowerShell:

```bash
BASE_URL=http://127.0.0.1:5058 pwsh ./scripts/demo.ps1
```

Oba skrypty zakładają domyślnie `http://127.0.0.1:5058`. Adres można zmienić przez `BASE_URL`, np.:

```bash
BASE_URL=http://127.0.0.1:5058 ./scripts/demo.sh
```

Jeżeli port `5058` jest zajęty, uruchom API na innym porcie i przekaż ten adres do skryptu. Dla UI unikaj portu `5060`, ponieważ Chromium traktuje go jako port blokowany. Bezpieczny przykład:

```bash
dotnet run --project src/Metalpol.Complaints.Api --urls http://127.0.0.1:5080
BASE_URL=http://127.0.0.1:5080 ./scripts/demo.sh
```

## Wysyłanie scenariuszy

Każdy scenariusz można wysłać bezpośrednio jako payload JSON:

```bash
curl -sS -X POST http://127.0.0.1:5058/api/mock/exchange/messages \
  -H "Content-Type: application/json" \
  --data @samples/scenarios/happy-path-visual-defect.json
```

W odpowiedzi API zwraca m.in. `complaintId`, `status`, `orderNumber`, `defectCategory`, `aiConfidence`, `jiraComplaintKey` i informację, czy wymagany jest human review.

Do podejrzenia szczegółów:

```bash
curl -sS http://127.0.0.1:5058/api/complaints/{complaintId}
```

Do podejrzenia timeline'u:

```bash
curl -sS http://127.0.0.1:5058/api/complaints/{complaintId}/timeline
```

Do KPI:

```bash
curl -sS http://127.0.0.1:5058/api/dashboard/kpis
```

## Oczekiwany wynik skryptu na świeżym API

Skrypt `scripts/demo.sh` używa domyślnie `happy-path-visual-defect.json`. Na świeżo uruchomionej instancji API, z pustym stanem in-memory, oczekiwany przebieg jest następujący:

1. `GET /health` zwraca `OK`.
2. `POST /api/mock/exchange/messages` zwraca status `ResponseDrafted`, `defectCategory = Visual`, `jiraComplaintKey = COMPLAINT-1001`, `humanReviewRequired = false`.
3. Timeline zawiera m.in. `EmailReceived`, `AttachmentsStored`, `ComplaintParsed`, `DefectClassified`, `CustomerMatched`, `OrderVerified`, `BatchVerified`, `JiraComplaintCreated`, `ResponseDrafted`.
4. `POST /api/complaints/{complaintId}/review/approve` z decyzją `ConfirmDefect` zwraca status `CorrectionCreated` i `correctionIssueKey = CORRECTION-2001`.
5. `GET /api/dashboard/kpis` pokazuje przykładowo `totalComplaints = 1`, `correctionsCreated = 1`, `jiraIssueCreationSuccessRatePercent = 100`, `sapVerificationFailureRatePercent = 0`.

Stan MVP jest przechowywany w pamięci procesu. Jeżeli ten sam scenariusz zostanie wysłany drugi raz bez restartu API, zadziała idempotencja po `sourceMessageId`: API zwróci `200 OK`, `duplicate = true` i istniejącą reklamację, nie utworzy drugiego Jira Cloud `Complaint`, a timeline może zawierać `DuplicateLinked`.

Przykładowy skrócony output skryptu:

```text
Metalpol AI Complaint Automation Demo

1. Sending sample email: happy-path-visual-defect.json
Created complaint: CMP-SCENARIO-HAPPY-VISUAL-DEFECT
Status: ResponseDrafted
Jira Complaint: COMPLAINT-1001
AI category: Visual
AI confidence: 0.9
Human review required: False
SAP order verified: True
Batch verified: True

2. Timeline:
- EmailReceived
- AttachmentsStored
- ComplaintParsed
- DefectClassified
- CustomerMatched
- OrderVerified
- BatchVerified
- JiraComplaintCreated
- ResponseDrafted

3. Approving complaint as confirmed defect...
Status after approval: CorrectionCreated
Correction ticket created: CORRECTION-2001

4. KPI snapshot:
Total complaints: 1
Human review required: 0
Corrections created: 1
Average first response draft time: mocked
Jira creation success rate: 100%
SAP verification failure rate: 0%
```

## Co pokazuje demo

Demo pokazuje działający przepływ procesu, timeline i KPI:

- timeline eventów per reklamacja łączy Event Storming z kodem,
- KPI endpoint odpowiada na problem braku widoczności operacyjnej,
- prompt injection scenario pokazuje realne ryzyka LLM,
- idempotencja po `sourceMessageId` chroni przed duplikowaniem pracy,
- low confidence kieruje sprawę do `HumanReviewRequired`,
- approval tworzy `Correction`, więc widać końcówkę procesu jakościowego.

## Edge case'y obsłużone w UI

UI jest celowo prostą warstwą demonstracyjną, ale zabezpiecza kilka typowych pułapek:

- Jeżeli przeglądarka ma zapisany stary `complaintId` w `localStorage`, a API zostało zrestartowane, UI czyści wybór i pokazuje neutralny empty state zamiast blokującego błędu.
- Przyciski human review są nieaktywne, dopóki nie ma wybranej reklamacji, oraz po stanie końcowym takim jak `CorrectionCreated` albo `Closed`.
- `Reset demo` czyści stan in-memory i liczniki mock Jira, więc można bez restartu API pokazać świeży przebieg tego samego scenariusza.
- Scenariusz `duplicate-message.json` najlepiej uruchomić po `happy-path-visual-defect.json`, bo dopiero wtedy widać brak drugiego Jira Cloud `Complaint`.
- Szczegóły techniczne timeline'u są zwinięte, żeby najpierw widoczny był flow procesu, a nie surowy JSON.
- KPI należy interpretować jako snapshot stanu in-memory procesu demo, nie jako produkcyjny dashboard historyczny.

## Scenariusze demo

| Plik | Komenda curl | Oczekiwany wynik | Co pokazuje biznesowo |
| --- | --- | --- | --- |
| `happy-path-visual-defect.json` | `curl -sS -X POST http://127.0.0.1:5058/api/mock/exchange/messages -H "Content-Type: application/json" --data @samples/scenarios/happy-path-visual-defect.json` | `status = ResponseDrafted`, `defectCategory = Visual`, utworzony `jiraComplaintKey` | Standardowy przepływ bez ręcznego przepisywania danych: email, załączniki, AI extraction, SAP ERP, Jira Cloud, draft odpowiedzi. |
| `missing-order-number.json` | `curl -sS -X POST http://127.0.0.1:5058/api/mock/exchange/messages -H "Content-Type: application/json" --data @samples/scenarios/missing-order-number.json` | `status = HumanReviewRequired`, `missingFields` zawiera `orderNumber`, brak Jira Cloud `Complaint` | Brak kluczowego pola nie jest zgadywany przez AI; system kieruje sprawę do wyjaśnienia z klientem. |
| `dimensional-defect-low-confidence.json` | `curl -sS -X POST http://127.0.0.1:5058/api/mock/exchange/messages -H "Content-Type: application/json" --data @samples/scenarios/dimensional-defect-low-confidence.json` | `status = HumanReviewRequired`, `defectCategory = Dimensional`, `aiConfidence < 0.85` | Niska pewność klasyfikacji uruchamia kontrolę człowieka, mimo że order i batch są poprawne. |
| `sap-order-not-found.json` | `curl -sS -X POST http://127.0.0.1:5058/api/mock/exchange/messages -H "Content-Type: application/json" --data @samples/scenarios/sap-order-not-found.json` | `status = HumanReviewRequired`, SAP ERP order not found, brak Jira Cloud `Complaint` | System nie tworzy ticketu operacyjnego na podstawie niezweryfikowanego zamówienia. |
| `prompt-injection-attempt.json` | `curl -sS -X POST http://127.0.0.1:5058/api/mock/exchange/messages -H "Content-Type: application/json" --data @samples/scenarios/prompt-injection-attempt.json` | `status = HumanReviewRequired`, `promptInjectionDetected = true` w szczegółach reklamacji | Treść maila jest traktowana jako niezaufane dane; podejrzane instrukcje obniżają confidence i wymagają review. |
| `duplicate-message.json` | `curl -sS -X POST http://127.0.0.1:5058/api/mock/exchange/messages -H "Content-Type: application/json" --data @samples/scenarios/duplicate-message.json` | Po wcześniejszym wysłaniu `happy-path-visual-defect.json`: `200 OK`, `duplicate = true`, ta sama reklamacja, bez drugiego Jira Cloud `Complaint` | Idempotencja po `sourceMessageId`; ponowne przysłanie maila nie dubluje pracy w Jira Cloud. |
| `logistics-complaint.json` | `curl -sS -X POST http://127.0.0.1:5058/api/mock/exchange/messages -H "Content-Type: application/json" --data @samples/scenarios/logistics-complaint.json` | `status = ResponseDrafted`, `defectCategory = Logistics` | Kontrolowana taksonomia pozwala raportować reklamacje logistyczne oddzielnie od jakościowych. |
| `material-defect-requires-correction.json` | `curl -sS -X POST http://127.0.0.1:5058/api/mock/exchange/messages -H "Content-Type: application/json" --data @samples/scenarios/material-defect-requires-correction.json` | `status = ResponseDrafted`, `defectCategory = Material`; po approval powstaje `CORRECTION-*` | AI przygotowuje sprawę, ale Correction powstaje dopiero po decyzji człowieka. |

## Approval dla scenariusza material defect

Po wysłaniu `material-defect-requires-correction.json` skopiuj `complaintId` z odpowiedzi i wykonaj:

```bash
curl -sS -X POST http://127.0.0.1:5058/api/complaints/{complaintId}/review/approve \
  -H "Content-Type: application/json" \
  -d '{
    "reviewer": "service.specialist",
    "decision": "ConfirmDefect",
    "notes": "Wada materiałowa potwierdzona w review."
  }'
```

Oczekiwany wynik:

```text
status = CorrectionCreated
correctionIssueKey = CORRECTION-2001
```

Ten etap pokazuje najważniejszą zasadę rozwiązania: AI wspiera ekstrakcję, klasyfikację i draft, ale nie podejmuje finalnej decyzji reklamacyjnej.

## Kolejność rekomendowana w demo

1. `happy-path-visual-defect.json`
2. `duplicate-message.json`
3. `missing-order-number.json`
4. `dimensional-defect-low-confidence.json`
5. `sap-order-not-found.json`
6. `prompt-injection-attempt.json`
7. `logistics-complaint.json`
8. `material-defect-requires-correction.json`
9. Approval `ConfirmDefect` dla material defect
10. `GET /api/dashboard/kpis`

Taka kolejność pokazuje pełny przekrój problemów CEO: opóźnienia, ręczne przepisywanie, niespójne kategorie, brak metryk, brak integracji SAP ERP/Jira Cloud/Microsoft 365 / Exchange oraz konieczność kontroli człowieka przy ryzykownych przypadkach.

## Walidacja lokalna

Standardowa walidacja:

```bash
dotnet restore Metalpol.Complaints.sln
dotnet build Metalpol.Complaints.sln
dotnet test Metalpol.Complaints.sln
```

Jeżeli Playwright Chromium nie jest jeszcze zainstalowany lokalnie:

```bash
pwsh tests/Metalpol.Complaints.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

Ręczna walidacja:

1. API startuje bez błędów:

   ```bash
   dotnet run --project src/Metalpol.Complaints.Api --urls http://127.0.0.1:5058
   ```

2. UI ładuje się pod `http://127.0.0.1:5058/`.
3. `GET /health` zwraca `OK`, a badge w UI pokazuje `API OK`.
4. Scenariusz `Happy path: wada wizualna` przechodzi przez intake, mock AI triage, SAP ERP, Jira Cloud i response draft.
5. `ConfirmDefect` tworzy mock Jira Correction.
6. Timeline pokazuje eventy od `EmailReceived` do `CorrectionTicketCreated`.
7. KPI odświeżają się po przetworzeniu scenariusza.
8. Skrypt demo działa:

   ```bash
   BASE_URL=http://127.0.0.1:5058 ./scripts/demo.sh
   ```

GitHub Actions wykonuje restore, build, instalację Chromium dla Playwright i `dotnet test --no-build`, więc E2E UI jest częścią standardowej bramki jakości.
