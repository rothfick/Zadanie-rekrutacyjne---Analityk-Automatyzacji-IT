# Event Storming TO-BE

## Cel dokumentu

Ten dokument modeluje docelowy proces obsługi reklamacji po wprowadzeniu kontrolowanej automatyzacji AI. TO-BE nie zakłada magicznego agenta podejmującego decyzje za firmę. Zakłada pipeline, w którym system odpowiada za intake, walidację, integracje, status i metryki, a AI wspiera ekstrakcję danych, klasyfikację, streszczenie i draft odpowiedzi.

Finalne decyzje reklamacyjne pozostają po stronie człowieka lub deterministycznych reguł biznesowych.

## Legenda Event Storming

```mermaid
flowchart LR
    event["Domain Event<br/>Coś istotnego już się wydarzyło"]:::event
    command["Command<br/>Polecenie lub intencja wykonania akcji"]:::command
    actor["Actor<br/>Osoba lub zespół wykonujący akcję"]:::actor
    system["External System<br/>System poza modelowanym procesem"]:::system
    policy["Policy / Business Rule<br/>Reguła decydująca o kolejnym kroku"]:::policy
    readModel["Read Model / Document<br/>Dokument, rejestr lub widok danych"]:::readModel
    hotspot["Hotspot / Risk<br/>Ryzyko, niepewność lub miejsce straty"]:::hotspot

    classDef event fill:#ffcc80,stroke:#ef6c00,color:#1f1f1f
    classDef command fill:#90caf9,stroke:#1565c0,color:#1f1f1f
    classDef actor fill:#fff59d,stroke:#f9a825,color:#1f1f1f
    classDef system fill:#d7ccc8,stroke:#5d4037,color:#1f1f1f
    classDef policy fill:#ce93d8,stroke:#6a1b9a,color:#1f1f1f
    classDef readModel fill:#a5d6a7,stroke:#2e7d32,color:#1f1f1f
    classDef hotspot fill:#ef9a9a,stroke:#c62828,color:#1f1f1f
```

## Diagram TO-BE

```mermaid
flowchart TD
    customer["Actor<br/>Customer"]:::actor
    exchange["External System<br/>Microsoft 365 / Exchange"]:::system
    webhook["Command<br/>Detect new e-mail via webhook"]:::command
    emailReceived["Domain Event<br/>EmailReceived"]:::event
    fallbackPolicy["Policy / Business Rule<br/>Fallback polling monitors missed messages"]:::policy

    fetchEmail["Command<br/>Fetch e-mail metadata and attachments"]:::command
    emailPayload["Read Model / Document<br/>Raw e-mail payload"]:::readModel

    createIntake["Command<br/>Create complaint intake record"]:::command
    complaintRecord["Read Model / Document<br/>Complaint record with central status"]:::readModel

    storeAttachments["Command<br/>Store attachments with controlled access"]:::command
    blob["External System<br/>Azure Blob Storage"]:::system
    attachmentsStored["Domain Event<br/>AttachmentsStored"]:::event
    attachmentUris["Read Model / Document<br/>Controlled attachment URIs"]:::readModel

    aiParser["Command<br/>Extract structured complaint data"]:::command
    aiComponent["External System<br/>AI triage / extraction component"]:::system
    parsedByAi["Domain Event<br/>ComplaintParsed"]:::event
    defectClassified["Domain Event<br/>DefectClassified"]:::event
    extractedData["Read Model / Document<br/>Language, order number, description,<br/>defect category, missing fields, confidence"]:::readModel

    missingPolicy["Policy / Business Rule<br/>If required fields missing,<br/>request customer clarification"]:::policy
    confidencePolicy["Policy / Business Rule<br/>If confidence below threshold,<br/>route to manual review"]:::policy
    duplicatePolicy["Policy / Business Rule<br/>If duplicate suspected,<br/>link to existing complaint"]:::policy
    duplicateLinked["Domain Event<br/>DuplicateLinked"]:::event

    matchCustomer["Command<br/>Match customer"]:::command
    customerDb["External System<br/>PostgreSQL customer DB (read-only)"]:::system
    customerMatched["Domain Event<br/>CustomerMatched"]:::event

    verifyOrder["Command<br/>Verify order in SAP ERP"]:::command
    sap["External System<br/>SAP ERP"]:::system
    orderVerified["Domain Event<br/>OrderVerified"]:::event
    sapPolicy["Policy / Business Rule<br/>If SAP ERP unavailable,<br/>retry and keep status SapVerificationPending"]:::policy
    pendingVerification["Read Model / Document<br/>Status: SapVerificationPending"]:::readModel
    sapMismatchPolicy["Policy / Business Rule<br/>If SAP ERP mismatch,<br/>route to manual review"]:::policy
    sapMismatch["Domain Event<br/>SapMismatchDetected"]:::event

    verifyBatch["Command<br/>Verify batch in SAP ERP"]:::command
    batchVerified["Domain Event<br/>BatchVerified"]:::event

    createOrUpdateJira["Command<br/>Create or update Jira Cloud Complaint"]:::command
    jira["External System<br/>Jira Cloud"]:::system
    jiraComplaint["Domain Event<br/>JiraComplaintCreated"]:::event

    generateDraft["Command<br/>Generate response draft"]:::command
    draftGenerated["Domain Event<br/>ResponseDrafted"]:::event
    responseDraft["Read Model / Document<br/>Customer response draft"]:::readModel

    reviewPolicy["Policy / Business Rule<br/>Human review required for<br/>missing data, low confidence,<br/>SAP ERP mismatch, duplicate,<br/>high-risk complaint"]:::policy
    specialist["Actor<br/>Service specialist"]:::actor
    humanReview["Command<br/>Review complaint and response draft"]:::command
    reviewRequested["Domain Event<br/>HumanReviewRequested"]:::event
    reviewCompleted["Domain Event<br/>HumanReviewCompleted"]:::event
    responseApproved["Read Model / Document<br/>Status: CustomerResponseApproved"]:::readModel

    sendResponse["Command<br/>Send approved customer response"]:::command
    responseSent["Read Model / Document<br/>Customer response sent outside MVP"]:::readModel

    defectConfirmed["Policy / Business Rule<br/>If material or dimensional defect<br/>is confirmed, create Correction"]:::policy
    createCorrection["Command<br/>Create Jira Cloud Correction ticket"]:::command
    correctionCreated["Domain Event<br/>CorrectionTicketCreated"]:::event
    quality["Actor<br/>Quality department"]:::actor

    updateMetrics["Command<br/>Update process metrics"]:::command
    dashboard["Read Model / Document<br/>KPI dashboard"]:::readModel

    customer --> exchange
    exchange --> webhook
    fallbackPolicy --> webhook
    webhook --> emailReceived --> fetchEmail --> emailPayload

    emailPayload --> createIntake --> complaintRecord
    emailPayload --> storeAttachments --> blob --> attachmentsStored --> attachmentUris
    attachmentUris --> complaintRecord

    emailPayload --> aiParser
    aiComponent --> aiParser
    aiParser --> parsedByAi --> defectClassified --> extractedData --> complaintRecord

    extractedData --> missingPolicy --> reviewRequested
    extractedData --> confidencePolicy --> reviewRequested
    extractedData --> duplicatePolicy --> duplicateLinked --> complaintRecord

    extractedData --> matchCustomer --> customerDb --> customerMatched --> complaintRecord
    customerMatched --> verifyOrder --> sap
    sapPolicy --> verifyOrder
    sap --> orderVerified --> verifyBatch
    sapPolicy --> pendingVerification --> complaintRecord
    orderVerified --> sapMismatchPolicy --> sapMismatch --> reviewRequested

    verifyBatch --> sap --> batchVerified --> complaintRecord
    batchVerified --> createOrUpdateJira --> jira --> jiraComplaint --> complaintRecord

    jiraComplaint --> generateDraft
    extractedData --> generateDraft
    generateDraft --> draftGenerated --> responseDraft
    responseDraft --> reviewPolicy --> reviewRequested

    specialist --> humanReview
    reviewRequested --> humanReview --> reviewCompleted --> responseApproved --> sendResponse --> exchange --> responseSent

    responseApproved --> defectConfirmed --> createCorrection --> jira --> correctionCreated --> quality
    responseSent --> updateMetrics
    correctionCreated --> updateMetrics
    reviewRequested --> updateMetrics
    pendingVerification --> updateMetrics
    updateMetrics --> dashboard

    classDef event fill:#ffcc80,stroke:#ef6c00,color:#1f1f1f
    classDef command fill:#90caf9,stroke:#1565c0,color:#1f1f1f
    classDef actor fill:#fff59d,stroke:#f9a825,color:#1f1f1f
    classDef system fill:#d7ccc8,stroke:#5d4037,color:#1f1f1f
    classDef policy fill:#ce93d8,stroke:#6a1b9a,color:#1f1f1f
    classDef readModel fill:#a5d6a7,stroke:#2e7d32,color:#1f1f1f
    classDef hotspot fill:#ef9a9a,stroke:#c62828,color:#1f1f1f
```

## Sekwencja procesu TO-BE

1. Microsoft 365 / Exchange wykrywa nowy e-mail przez webhook. Fallback polling monitoruje wiadomości, których webhook nie przetworzył.
2. System pobiera metadane e-maila i załączniki.
3. System tworzy rekord intake reklamacji z centralnym identyfikatorem i statusem.
4. Załączniki są zapisywane w Azure Blob Storage z kontrolowanym dostępem, a rekord reklamacji przechowuje tylko bezpieczne URI.
5. Komponent AI wyciąga strukturę danych z e-maila: język, numer zamówienia, opis, kategorię wady, brakujące pola i confidence score.
6. System dopasowuje klienta w wewnętrznej bazie klientów w trybie read-only.
7. System weryfikuje order w SAP ERP.
8. System weryfikuje batch w SAP ERP.
9. System tworzy albo aktualizuje ticket `Complaint` w Jira Cloud.
10. System generuje draft odpowiedzi do klienta.
11. System kieruje sprawę do człowieka, jeżeli brakuje danych, confidence jest niskie, SAP ERP zwraca mismatch, istnieje podejrzenie duplikatu albo reklamacja jest wysokiego ryzyka.
12. Specjalista serwisu zatwierdza i wysyła odpowiedź do klienta.
13. Jeżeli wada materiałowa albo wymiarowa zostanie potwierdzona, system tworzy ticket `Correction` w Jira Cloud dla działu jakości.
14. System aktualizuje metryki procesu i dashboard KPI.

## Polityki i reguły biznesowe

| Polityka | Warunek | Decyzja systemu | Dlaczego to ważne |
|---|---|---|---|
| Brak pól wymaganych | AI lub walidacja wykrywa brak numeru zamówienia, opisu, zdjęcia albo danych klienta | Utwórz status `HumanReviewRequired` i przygotuj draft prośby o uzupełnienie danych | Sprawa nie utknie cicho w backlogu, a klient szybko dostanie informację, czego brakuje |
| Niski confidence | Confidence ekstrakcji lub klasyfikacji spada poniżej ustalonego progu | Skieruj sprawę do manual review | AI nie podejmuje decyzji przy niskiej pewności |
| SAP ERP niedostępny | SAP ERP nie odpowiada albo przekracza limit czasowy | Wykonaj retry i oznacz sprawę jako `SapVerificationPending` | Proces zachowuje status i nie gubi kontekstu przy awarii integracji |
| SAP ERP mismatch | Numer orderu albo batch nie zgadza się z danymi SAP ERP | Skieruj sprawę do manual review | SAP ERP pozostaje źródłem prawdy dla orderów i batchy |
| Podejrzenie duplikatu | Podobny e-mail, order, batch, klient i opis już istnieją | Połącz z istniejącą reklamacją zamiast tworzyć nową | Ogranicza duplikaty w Jira Cloud i fałszywe zawyżanie metryk |
| Wada materiałowa lub wymiarowa potwierdzona | Człowiek potwierdza defekt kategorii `materiał` albo `wymiary` | Utwórz ticket `Correction` dla jakości | Dział jakości dostaje sprawy wymagające realnego działania korygującego |
| Reklamacja wysokiego ryzyka | Duży klient, wysoka wartość zamówienia, powtarzalny batch albo eskalacja | Wymagaj human review niezależnie od confidence | Chroni relację z klientem i ogranicza ryzyko błędnej automatyzacji |

## Zdarzenia w timeline TO-BE

MVP zapisuje w timeline nazwy eventów zgodne z klasami domenowymi:

- `EmailReceived`,
- `AttachmentsStored`,
- `ComplaintParsed`,
- `DefectClassified`,
- `CustomerMatched`,
- `OrderVerified`,
- `BatchVerified`,
- `SapMismatchDetected`,
- `JiraComplaintCreated`,
- `ResponseDrafted`,
- `HumanReviewRequested`,
- `HumanReviewCompleted`,
- `CustomerClarificationRequested`,
- `CorrectionTicketCreated`,
- `ComplaintClosed`,
- `ComplaintFailed`,
- `DuplicateLinked`.

Statusy takie jak `SapVerificationPending` i `CustomerResponseApproved` są stanami procesu, a nie osobnymi eventami w MVP. Wersja produkcyjna może dodać bardziej granularne zdarzenia techniczne, ale powinny być mapowane do tych samych statusów domenowych.

## Komentarz architektoniczny

AI wspiera triage i ekstrakcję, ale nie jest źródłem prawdy ani finalnym decydentem. Model może zaproponować język, kategorię wady, opis strukturalny, brakujące pola, confidence i draft odpowiedzi. Stan procesu, walidacja SAP ERP, obsługa duplikatów, tworzenie ticketów, SLA, retry oraz eskalacje są kontrolowane przez deterministyczną logikę systemu.

Taki podział odpowiedzialności zmniejsza ryzyko operacyjne. AI przyspiesza pracę specjalisty, ale to człowiek zatwierdza odpowiedź do klienta i potwierdza decyzje reklamacyjne. System natomiast zapewnia mierzalność, timeline, centralny status i spójny przepływ między Microsoft 365 / Exchange, Azure Blob Storage, PostgreSQL customer DB, SAP ERP i Jira Cloud.
