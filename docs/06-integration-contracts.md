# Kontrakty integracyjne

## Cel dokumentu

Ten dokument opisuje kontrakty integracyjne dla docelowego procesu obsługi reklamacji w Metalpolu. W MVP wszystkie integracje są mockowane, ale kontrakty powinny być widoczne i wystarczająco precyzyjne, aby zespół .NET mógł zaplanować adaptery produkcyjne.

Kontrakty nie są pełną dokumentacją dostawców. Są roboczym modelem wymiany danych, odpowiedzialności, błędów i decyzji projektowych dla tego procesu.

## Zasady wspólne dla adapterów

- Adaptery izolują system od szczegółów dostawców zewnętrznych.
- Orchestrator komunikuje się z integracjami przez porty aplikacyjne, nie przez SDK dostawcy.
- Każde wywołanie powinno mieć `correlationId` i `complaintId`, jeżeli reklamacja już istnieje.
- Operacje tworzące lub aktualizujące dane muszą być idempotentne.
- Retry może być stosowany tylko wtedy, gdy nie grozi utworzeniem duplikatów.
- Sekrety, tokeny i connection stringi nie mogą znajdować się w kodzie ani repozytorium.
- Błędy integracyjne muszą być widoczne w timeline reklamacji i KPI.

## Microsoft 365 / Exchange

### Purpose

Microsoft 365 / Exchange jest kanałem wejścia reklamacji. Adapter ma wykrywać nowe wiadomości, pobierać metadane, treść i załączniki oraz zapewnić fallback polling dla wiadomości, które nie zostały obsłużone przez webhook.

### New email notification

Szkic webhook notification:

```http
POST /api/integrations/exchange/notifications
Content-Type: application/json
X-Correlation-Id: corr-2026-0001
```

```json
{
  "subscriptionId": "sub-complaints-mailbox",
  "tenantId": "metalpol-tenant",
  "mailbox": "reklamacje@metalpol.pl",
  "changeType": "created",
  "resource": "/users/reklamacje@metalpol.pl/messages/AAMkAG...",
  "messageId": "AAMkAG...",
  "receivedAt": "2026-05-07T09:15:00Z"
}
```

Response:

```http
202 Accepted
```

```json
{
  "accepted": true,
  "messageId": "AAMkAG...",
  "status": "queued"
}
```

Required fields:

- `subscriptionId`,
- `tenantId`,
- `mailbox`,
- `changeType`,
- `messageId`,
- `receivedAt`.

### Message fetch

Szkic żądania adaptera:

```http
GET /mock/graph/users/reklamacje@metalpol.pl/messages/AAMkAG...
Authorization: Bearer <token-from-secret-store>
X-Correlation-Id: corr-2026-0001
```

Response:

```json
{
  "messageId": "AAMkAG...",
  "internetMessageId": "<mail-001@customer.example>",
  "from": {
    "email": "quality.manager@customer.example",
    "name": "Quality Manager"
  },
  "subject": "Reklamacja MP-2026-1042",
  "receivedAt": "2026-05-07T09:15:00Z",
  "bodyText": "Dzień dobry, zgłaszamy rysy na elementach z zamówienia MP-2026-1042...",
  "languageHint": null,
  "hasAttachments": true,
  "attachments": [
    {
      "attachmentId": "att-001",
      "fileName": "defect-photo-1.jpg",
      "contentType": "image/jpeg",
      "sizeBytes": 2450000
    }
  ]
}
```

Required fields:

- `messageId`,
- `from.email`,
- `subject`,
- `receivedAt`,
- `bodyText`,
- `attachments[]` when `hasAttachments = true`.

### Attachments fetch

Szkic żądania:

```http
GET /mock/graph/users/reklamacje@metalpol.pl/messages/AAMkAG.../attachments/att-001/content
Authorization: Bearer <token-from-secret-store>
X-Correlation-Id: corr-2026-0001
```

Response metadata:

```json
{
  "attachmentId": "att-001",
  "fileName": "defect-photo-1.jpg",
  "contentType": "image/jpeg",
  "sizeBytes": 2450000,
  "contentRef": "mock://exchange/messages/AAMkAG/attachments/att-001"
}
```

W MVP `contentRef` może wskazywać na fixture zamiast prawdziwych bajtów pliku.

### Fallback polling

Szkic żądania:

```http
GET /mock/graph/users/reklamacje@metalpol.pl/messages?folder=inbox,junk&receivedAfter=2026-05-07T09:00:00Z
Authorization: Bearer <token-from-secret-store>
X-Correlation-Id: corr-polling-2026-0001
```

Response:

```json
{
  "messages": [
    {
      "messageId": "AAMkAG...",
      "receivedAt": "2026-05-07T09:15:00Z",
      "folder": "inbox",
      "alreadyProcessed": false
    },
    {
      "messageId": "AAMkSPAM...",
      "receivedAt": "2026-05-07T09:17:00Z",
      "folder": "junk",
      "alreadyProcessed": false
    }
  ],
  "nextDeltaToken": "delta-001"
}
```

### Errors

| Error | Znaczenie | Decyzja systemu |
|---|---|---|
| `401 Unauthorized` | Brak lub nieważny token | Nie retry bez odświeżenia tokenu; alert techniczny |
| `403 Forbidden` | Brak uprawnień do skrzynki | Oznacz integrację jako niedostępną; alert |
| `404 Not Found` | Wiadomość lub załącznik nie istnieje | Zapisz `EmailFetchFailed`; skieruj do manual review |
| `409 Conflict` | Wiadomość już przetworzona | Zwróć istniejący `complaintId` |
| `429 Too Many Requests` | Throttling | Retry zgodnie z `Retry-After` |
| `5xx` | Błąd usługi | Retry z backoffem, potem `HumanReviewRequired` |

### Retry behavior

- Webhook powinien tylko przyjąć event i szybko zwrócić `202 Accepted`.
- Pobieranie wiadomości i załączników może być ponawiane dla `429` i `5xx`.
- Nie ponawiać automatycznie `403` i błędów walidacji.
- Fallback polling powinien okresowo sprawdzać inbox i spam/junk.

### Idempotency strategy

- Klucz idempotencji: `tenantId + mailbox + messageId`.
- Alternatywny klucz pomocniczy: `internetMessageId`.
- Ten sam e-mail nie może utworzyć drugiej reklamacji.
- Fallback polling musi sprawdzać, czy `messageId` już ma przypisany `complaintId`.

### Security considerations

- Uprawnienia tylko do skrzynki reklamacyjnej.
- Tokeny tylko z bezpiecznej konfiguracji, nigdy w repozytorium.
- Walidacja `subscriptionId` i tajnego `clientState` dla webhooka w produkcji.
- Nie logować pełnej treści maila w logach technicznych.
- Załączniki traktować jako niezaufane pliki.

### Mock implementation approach for demo

- `POST /api/mock/exchange/messages` przyjmuje fixture e-maila.
- Adapter zwraca deterministyczne wiadomości z `samples/emails`.
- Scenariusze mock: poprawny mail, brak orderu, spam/delayed, duplikat, brak załączników.
- Fallback polling może być symulowany przez osobny fixture z `folder = junk`.

## SAP ERP

### Purpose

SAP ERP jest źródłem prawdy dla zamówień, batchy i danych produkcyjnych. Adapter SAP ERP ma tylko odczytywać i walidować dane. Orchestrator nie może nadpisywać danych SAP ERP.

Rate limit dla API: `100 req/min`.

### GET /api/v1/orders/{id}

Request:

```http
GET /api/v1/orders/MP-2026-1042
Authorization: Bearer <token-from-secret-store>
X-Correlation-Id: corr-2026-0001
```

Response:

```json
{
  "orderId": "MP-2026-1042",
  "customerId": "CUST-1001",
  "status": "delivered",
  "createdAt": "2026-04-12",
  "deliveredAt": "2026-04-28",
  "batchIds": ["B-2026-07-19"],
  "productionLine": "LINE-2"
}
```

Required fields:

- `orderId`,
- `customerId`,
- `status`,
- `batchIds`,
- `productionLine` if available.

### GET /api/v1/batches/{id}

Request:

```http
GET /api/v1/batches/B-2026-07-19
Authorization: Bearer <token-from-secret-store>
X-Correlation-Id: corr-2026-0001
```

Response:

```json
{
  "batchId": "B-2026-07-19",
  "orderIds": ["MP-2026-1042"],
  "productionLine": "LINE-2",
  "operatorId": "OP-018",
  "producedAt": "2026-04-20T14:30:00Z",
  "qualityStatus": "released"
}
```

Required fields:

- `batchId`,
- `orderIds`,
- `productionLine`,
- `qualityStatus`.

### Errors

| Error | Znaczenie | Decyzja systemu |
|---|---|---|
| `400 Bad Request` | Niepoprawny format orderu lub batcha | Bez retry; manual review |
| `404 Not Found` | Order lub batch nie istnieje | `SapMismatchDetected`; manual review |
| `409 Conflict` | Dane order/batch niespójne | Manual review |
| `429 Too Many Requests` | Przekroczony limit 100 req/min | Retry po `Retry-After`; kolejka |
| `503 Service Unavailable` | SAP ERP chwilowo niedostępny | Retry z backoffem; status `SapVerificationPending` |
| Timeout | Brak odpowiedzi | Retry z backoffem; potem `SapVerificationPending` |

### Retry behavior

- Retry tylko dla `429`, `503` i timeoutów.
- Stosować exponential backoff i respektować `Retry-After`.
- Nie retry dla `400`, `404` i walidacyjnego mismatch.
- Orchestrator powinien ograniczać liczbę zapytań, aby nie przekroczyć `100 req/min`.

### Idempotency strategy

- Operacje SAP ERP są read-only, więc są naturalnie idempotentne.
- Wynik walidacji powinien być zapisany w timeline z `complaintId`.
- Cache per complaint może ograniczyć ponowne odpytywanie tego samego orderu i batcha.

### Security considerations

- Dostęp tylko read-only.
- Tokeny i certyfikaty poza repozytorium.
- Nie logować pełnych danych produkcyjnych, jeżeli nie są potrzebne do audytu reklamacji.
- Walidować format `orderId` i `batchId` przed wywołaniem SAP ERP.

### Mock implementation approach for demo

- Fixtures w `samples/sap`.
- Mock obsługuje scenariusze: valid order/batch, unknown order, batch mismatch, SAP ERP unavailable, rate limited.
- Mock może liczyć żądania, aby zasymulować limit `100 req/min`.

## Jira Cloud

### Purpose

Jira Cloud jest operacyjnym systemem workflow dla ticketów `Complaint` i `Correction`. Adapter Jira Cloud odpowiada za tworzenie, aktualizację i linkowanie ticketów, ale pełny timeline procesu pozostaje w Event Store.

### Create Complaint issue

Request:

```http
POST /mock/jira/rest/api/3/issue
Authorization: Bearer <token-from-secret-store>
Idempotency-Key: complaint-CMP-2026-0001-complaint
X-Correlation-Id: corr-2026-0001
```

```json
{
  "projectKey": "REK",
  "issueType": "Complaint",
  "externalComplaintId": "CMP-2026-0001",
  "summary": "Complaint for order MP-2026-1042 - visual defect",
  "description": "Customer reports scratches and discoloration on delivered metal components.",
  "customerId": "CUST-1001",
  "orderId": "MP-2026-1042",
  "batchId": "B-2026-07-19",
  "defectCategory": "visual",
  "attachmentLinks": [
    "https://storage.example/complaints/CMP-2026-0001/att-001?sas=temporary"
  ]
}
```

Response:

```json
{
  "issueKey": "REK-1024",
  "issueId": "10024",
  "externalComplaintId": "CMP-2026-0001",
  "status": "Open"
}
```

Required fields:

- `projectKey`,
- `issueType`,
- `externalComplaintId`,
- `summary`,
- `description`,
- `orderId` when available,
- `defectCategory`.

### Create Correction issue

Request:

```http
POST /mock/jira/rest/api/3/issue
Authorization: Bearer <token-from-secret-store>
Idempotency-Key: complaint-CMP-2026-0001-correction
X-Correlation-Id: corr-2026-0001
```

```json
{
  "projectKey": "REK",
  "issueType": "Correction",
  "externalComplaintId": "CMP-2026-0001",
  "parentComplaintIssueKey": "REK-1024",
  "summary": "Correction required for confirmed dimensional defect",
  "defectCategory": "dimensional",
  "orderId": "MP-2026-1042",
  "batchId": "B-2026-07-19",
  "productionLine": "LINE-2",
  "qualityReason": "Defect confirmed by service specialist"
}
```

Response:

```json
{
  "issueKey": "REK-1025",
  "issueId": "10025",
  "linkedComplaintIssueKey": "REK-1024",
  "status": "Open"
}
```

Required fields:

- `projectKey`,
- `issueType = Correction`,
- `externalComplaintId`,
- `parentComplaintIssueKey`,
- `defectCategory`,
- `qualityReason`.

### Update issue with links and status

Request:

```http
PATCH /mock/jira/rest/api/3/issue/REK-1024
Authorization: Bearer <token-from-secret-store>
Idempotency-Key: complaint-CMP-2026-0001-update-001
X-Correlation-Id: corr-2026-0001
```

```json
{
  "status": "Waiting for customer",
  "links": [
    {
      "type": "relates_to",
      "issueKey": "REK-1025"
    }
  ],
  "comment": "Customer clarification requested due to missing order number.",
  "externalStatus": "HumanReviewRequired"
}
```

Response:

```json
{
  "issueKey": "REK-1024",
  "status": "Waiting for customer",
  "updated": true
}
```

### Errors

| Error | Znaczenie | Decyzja systemu |
|---|---|---|
| `400 Bad Request` | Niepoprawne pola lub workflow | Bez retry; alert konfiguracyjny |
| `401 Unauthorized` | Brak autoryzacji | Bez retry do czasu odświeżenia sekretu |
| `403 Forbidden` | Brak uprawnień do projektu | Alert; manual fallback |
| `404 Not Found` | Projekt lub issue nie istnieje | Manual review / alert |
| `409 Conflict` | Duplikat po `externalComplaintId` | Pobierz istniejący issue i zapisz link |
| `429 Too Many Requests` | Rate limit Jira Cloud | Retry po `Retry-After` |
| `5xx` | Błąd Jira Cloud | Retry z backoffem |

### Retry behavior

- Retry dla `429` i `5xx`.
- Nie retry dla błędów walidacji workflow bez zmiany danych.
- Po niepewnym timeout sprawdzić, czy issue powstał po `externalComplaintId`.

### Idempotency strategy

- `externalComplaintId` jako custom field w Jira Cloud.
- `Idempotency-Key` dla operacji tworzenia i aktualizacji.
- `Complaint` może powstać tylko raz dla `complaintId`.
- `Correction` może powstać tylko raz dla potwierdzonego defektu w danej reklamacji.

### Security considerations

- Token Jira Cloud tylko z secret store.
- Minimalne uprawnienia do projektu `REK`.
- Nie wysyłać do Jira Cloud danych niepotrzebnych do workflow.
- Linki do załączników powinny być czasowe i kontrolowane.

### Mock implementation approach for demo

- Mock Jira Cloud przechowuje issue in-memory.
- Klucze ticketów generowane deterministycznie, np. `REK-1001`.
- Scenariusze mock: create success, duplicate, rate limit, validation error, correction created.
- Mock powinien zapisywać `externalComplaintId`, aby pokazać idempotencję.

## PostgreSQL customer DB (read-only)

### Purpose

PostgreSQL customer DB jest źródłem prawdy dla metadanych klienta. Adapter działa w trybie read-only i służy do dopasowania klienta do reklamacji.

### Customer lookup

Logika dopasowania:

1. Dopasowanie po `customerId`, jeżeli występuje w mailu lub SAP ERP.
2. Dopasowanie po pełnym adresie e-mail.
3. Dopasowanie po domenie e-mail, jeżeli domena jednoznacznie wskazuje klienta.
4. Brak dopasowania lub wiele dopasowań oznacza manual review.

Szkic zapytania aplikacyjnego:

```http
GET /mock/customer-db/customers:match?email=quality.manager@customer.example&customerId=CUST-1001
X-Correlation-Id: corr-2026-0001
```

Response:

```json
{
  "matchStatus": "matched",
  "matchConfidence": "exact",
  "customer": {
    "customerId": "CUST-1001",
    "name": "Example Automotive GmbH",
    "primaryDomain": "customer.example",
    "languagePreference": "en",
    "riskTier": "standard"
  },
  "matchedBy": "customerId"
}
```

Required fields:

- request: at least one of `customerId`, `email`, `domain`,
- response: `matchStatus`,
- response when matched: `customer.customerId`, `customer.name`, `matchedBy`.

### Errors

| Error | Znaczenie | Decyzja systemu |
|---|---|---|
| `400 Bad Request` | Brak danych do dopasowania | Manual review |
| `404 Not Found` | Klient nie znaleziony | Manual review |
| `409 Conflict` | Wiele możliwych dopasowań | Manual review |
| `503 Service Unavailable` | Baza niedostępna | Retry; potem `CustomerMatchPending` |
| Timeout | Brak odpowiedzi | Retry z backoffem |

### Retry behavior

- Retry dla timeoutów i `503`.
- Nie retry dla braku dopasowania lub wielu dopasowań.
- Po kilku nieudanych próbach status `CustomerMatchPending`.

### Idempotency strategy

- Operacja read-only.
- Wynik dopasowania zapisać w rekordzie reklamacji z timestampem.
- Ponowne wywołanie dla tych samych danych nie może zmienić statusu bez nowego eventu.

### Security considerations

- Konto techniczne tylko read-only.
- Nie logować pełnych danych klienta poza potrzebnym kontekstem reklamacji.
- Dostęp do danych klienta powinien być ograniczony do adaptera.
- W MVP używać fikcyjnych danych klientów.

### Mock implementation approach for demo

- Fixtures w `samples/customers`.
- Scenariusze: exact customer id match, email match, domain match, ambiguous domain, not found.
- Mock powinien zwracać stabilne dane dla testów deterministycznych.

## Azure Blob Storage

### Purpose

Azure Blob Storage jest archiwum załączników reklamacyjnych. Rekord reklamacji przechowuje tylko metadane i kontrolowane URI, a nie binarne pliki.

### Upload / archive attachments

Request:

```http
PUT /mock/blob/complaints/CMP-2026-0001/attachments/att-001
Content-Type: image/jpeg
X-Correlation-Id: corr-2026-0001
```

Metadata:

```json
{
  "complaintId": "CMP-2026-0001",
  "attachmentId": "att-001",
  "fileName": "defect-photo-1.jpg",
  "contentType": "image/jpeg",
  "sizeBytes": 2450000,
  "sourceMessageId": "AAMkAG..."
}
```

Response:

```json
{
  "blobUri": "https://storage.example/complaints/CMP-2026-0001/attachments/att-001",
  "stored": true,
  "checksum": "sha256:mocked-checksum",
  "retentionUntil": "2027-05-07T00:00:00Z"
}
```

Required fields:

- `complaintId`,
- `attachmentId`,
- `fileName`,
- `contentType`,
- `sizeBytes`,
- binary content or mock `contentRef`.

### Temporary SAS links

Request:

```http
POST /mock/blob/complaints/CMP-2026-0001/attachments/att-001/sas
X-Correlation-Id: corr-2026-0001
```

```json
{
  "purpose": "jira-comment",
  "expiresInMinutes": 60,
  "permission": "read"
}
```

Response:

```json
{
  "temporaryUrl": "https://storage.example/complaints/CMP-2026-0001/attachments/att-001?sas=mock-token",
  "expiresAt": "2026-05-07T10:15:00Z"
}
```

### Retention and access control

Proponowane zasady:

- załączniki przechowywane według polityki retencji reklamacji,
- dostęp tylko dla procesu reklamacyjnego, serwisu i jakości,
- tymczasowe linki wygasają automatycznie,
- linki do Jira Cloud powinny być czasowe albo kontrolowane,
- usunięcie załącznika powinno być osobnym audytowalnym zdarzeniem.

### Errors

| Error | Znaczenie | Decyzja systemu |
|---|---|---|
| `400 Bad Request` | Niepoprawne metadane pliku | Manual review |
| `401 Unauthorized` | Brak autoryzacji | Alert techniczny |
| `403 Forbidden` | Brak uprawnień do kontenera | Alert techniczny |
| `413 Payload Too Large` | Załącznik za duży | Poproś klienta o inny sposób dostarczenia pliku |
| `415 Unsupported Media Type` | Nieobsługiwany typ pliku | Manual review |
| `409 Conflict` | Załącznik już istnieje | Zwróć istniejący `blobUri` |
| `5xx` | Błąd storage | Retry z backoffem |

### Retry behavior

- Retry dla `5xx` i timeoutów.
- Nie retry dla `413` i `415` bez zmiany danych.
- Po timeout sprawdzić, czy blob istnieje po `complaintId + attachmentId`.

### Idempotency strategy

- Klucz idempotencji: `complaintId + attachmentId`.
- Ponowny upload tego samego załącznika powinien zwrócić istniejący `blobUri`.
- Checksum może wykrywać różną zawartość dla tego samego `attachmentId`.

### Security considerations

- Brak publicznych, stałych linków do załączników.
- SAS tylko tymczasowy, z minimalnym zakresem uprawnień.
- Kontrola typów plików i limitów rozmiaru.
- Skanowanie antywirusowe w produkcji.
- Nie przechowywać danych produkcyjnych w repozytorium.

### Mock implementation approach for demo

- Mock nie zapisuje prawdziwych plików binarnych.
- Zwraca deterministyczne `mock://blob/...` albo fikcyjne HTTPS URI.
- Scenariusze: upload success, duplicate upload, unsupported file type, file too large.
- Metadane załączników mogą pochodzić z `samples/emails`.

## Cross-integration error handling

| Scenariusz | Status reklamacji | Event w timeline | Co widzi specjalista |
|---|---|---|---|
| Microsoft 365 / Exchange fetch failed | `HumanReviewRequired` albo `Failed` | `ComplaintFailed` lub `HumanReviewRequested` | Mail wymaga ręcznego sprawdzenia |
| SAP ERP unavailable | `SapVerificationPending` albo `HumanReviewRequired` | `SapMismatchDetected` przy przekroczeniu retry | Sprawa czeka na walidację orderu/batcha albo wymaga ręcznej weryfikacji |
| SAP ERP mismatch | `HumanReviewRequired` | `SapMismatchDetected` | Order lub batch wymaga ręcznej weryfikacji |
| Jira Cloud create failed | `HumanReviewRequired` albo `Failed` | `ComplaintFailed` | Sprawa istnieje, ale ticket wymaga ponowienia |
| PostgreSQL customer DB not matched | `HumanReviewRequired` | `HumanReviewRequested` | Trzeba wybrać albo doprecyzować klienta |
| Azure Blob Storage upload failed | `HumanReviewRequired` albo `Failed` | `ComplaintFailed` | Dane maila są, ale zdjęcia wymagają ponowienia zapisu |

## Why mocks in MVP

Mocki są świadomą decyzją projektową, nie skrótem ukrywającym integracje.

- Brak prawdziwych poświadczeń: repozytorium nie powinno zawierać sekretów, tokenów ani connection stringów.
- Brak zewnętrznych zależności: demo powinno działać lokalnie bez dostępu do Microsoft 365 / Exchange, SAP ERP, Jira Cloud, PostgreSQL customer DB i Azure Blob Storage.
- Deterministyczne demo: te same przykładowe maile muszą zawsze dawać te same eventy, statusy i KPI.
- Widoczne kontrakty: nawet przy mockach widać requesty, response'y, błędy, retry, idempotencję i ownership danych.
- Czytelne granice odpowiedzialności: mocki pokazują proces i architekturę bez potrzeby konfigurowania cudzych systemów.

Docelowo każdy mock powinien implementować ten sam port aplikacyjny co przyszły adapter produkcyjny. Dzięki temu MVP pokazuje proces end-to-end, a jednocześnie nie uzależnia projektu od realnych integracji.
