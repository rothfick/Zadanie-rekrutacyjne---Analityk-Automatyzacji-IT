# Event Storming AS-IS

## Cel dokumentu

Ten dokument modeluje obecny proces obsługi reklamacji w Metalpolu. Perspektywa AS-IS pokazuje, że głównym ograniczeniem nie jest brak AI, tylko ręczne łączenie kilku systemów przez specjalistę serwisu.

W obecnym procesie człowiek działa jako integrator między Microsoft 365 / Exchange, Excelem, Jira Cloud, SAP ERP, klientem i działem jakości. To powoduje opóźnienia, błędy przepisywania, niespójną klasyfikację oraz brak wiarygodnych metryk.

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

## Diagram AS-IS

```mermaid
flowchart TD
    customer["Actor<br/>Customer"]:::actor
    sendEmail["Command<br/>Send complaint e-mail"]:::command
    emailSent["Domain Event<br/>ComplaintEmailSent"]:::event
    exchange["External System<br/>Microsoft 365 / Exchange"]:::system
    riskSpam["Hotspot / Risk<br/>Spam or delayed e-mail"]:::hotspot

    specialist["Actor<br/>Service specialist"]:::actor
    readEmail["Command<br/>Read e-mail manually"]:::command
    emailRead["Domain Event<br/>ComplaintEmailReadManually"]:::event
    emailDocument["Read Model / Document<br/>E-mail: photos, order number, description PL/EN"]:::readModel

    copyToExcel["Command<br/>Copy complaint data to Excel"]:::command
    excelRegister["Read Model / Document<br/>Rejestr Reklamacji 2026.xlsx"]:::readModel
    dataCopied["Domain Event<br/>ComplaintDataCopiedToExcel"]:::event
    riskCopy["Hotspot / Risk<br/>Manual copy errors"]:::hotspot
    riskExcel["Hotspot / Risk<br/>Excel as weak system of record"]:::hotspot

    categorize["Command<br/>Categorize defect subjectively"]:::command
    defectCategorized["Domain Event<br/>DefectCategorizedManually"]:::event
    taxonomy["Policy / Business Rule<br/>Wizualna / wymiary / materiał / logistyka"]:::policy
    riskCategory["Hotspot / Risk<br/>Subjective categorization"]:::hotspot

    createComplaint["Command<br/>Create Jira Cloud Complaint ticket"]:::command
    jira["External System<br/>Jira Cloud"]:::system
    complaintCreated["Domain Event<br/>JiraComplaintCreated"]:::event

    checkSap["Command<br/>Check order and batch in SAP ERP"]:::command
    sap["External System<br/>SAP ERP"]:::system
    orderChecked["Domain Event<br/>OrderCheckedInSap"]:::event
    batchChecked["Domain Event<br/>BatchCheckedInSap"]:::event
    riskDisconnected["Hotspot / Risk<br/>Disconnected SAP ERP / Jira Cloud / Excel"]:::hotspot

    prepareReply["Command<br/>Prepare and send customer reply"]:::command
    replyPolicy["Policy / Business Rule<br/>Average response around 2 days from submission"]:::policy
    customerReplied["Domain Event<br/>CustomerReplySent"]:::event
    riskBacklog["Hotspot / Risk<br/>Backlog 2-3 days"]:::hotspot
    riskSla["Hotspot / Risk<br/>No automated SLA visibility"]:::hotspot

    defectConfirmedPolicy["Policy / Business Rule<br/>If defect confirmed"]:::policy
    createCorrection["Command<br/>Create Jira Cloud Correction ticket"]:::command
    quality["Actor<br/>Quality department"]:::actor
    correctionCreated["Domain Event<br/>CorrectionTicketCreated"]:::event

    riskMetrics["Hotspot / Risk<br/>No reliable metrics"]:::hotspot

    customer --> sendEmail --> emailSent --> exchange --> riskSpam --> readEmail
    specialist --> readEmail --> emailRead --> emailDocument
    emailDocument --> copyToExcel --> dataCopied --> excelRegister
    dataCopied --> riskCopy
    excelRegister --> riskExcel

    excelRegister --> categorize
    taxonomy --> categorize
    specialist --> categorize --> defectCategorized --> riskCategory

    defectCategorized --> createComplaint --> jira --> complaintCreated
    complaintCreated --> checkSap
    specialist --> checkSap --> sap
    sap --> orderChecked --> batchChecked --> riskDisconnected

    batchChecked --> prepareReply
    replyPolicy --> prepareReply
    specialist --> prepareReply --> customerReplied
    prepareReply --> riskBacklog
    prepareReply --> riskSla

    batchChecked --> defectConfirmedPolicy --> createCorrection --> jira --> correctionCreated --> quality
    excelRegister --> riskMetrics
    jira --> riskMetrics
    sap --> riskMetrics

    classDef event fill:#ffcc80,stroke:#ef6c00,color:#1f1f1f
    classDef command fill:#90caf9,stroke:#1565c0,color:#1f1f1f
    classDef actor fill:#fff59d,stroke:#f9a825,color:#1f1f1f
    classDef system fill:#d7ccc8,stroke:#5d4037,color:#1f1f1f
    classDef policy fill:#ce93d8,stroke:#6a1b9a,color:#1f1f1f
    classDef readModel fill:#a5d6a7,stroke:#2e7d32,color:#1f1f1f
    classDef hotspot fill:#ef9a9a,stroke:#c62828,color:#1f1f1f
```

## Sekwencja procesu AS-IS

1. Klient wysyła e-mail z opisem reklamacji, zdjęciem wady i numerem zamówienia. Wiadomość może być po polsku albo po angielsku.
2. E-mail trafia do skrzynki `reklamacje@metalpol.pl`, ale część wiadomości wpada do spamu albo jest czytana z opóźnieniem.
3. Specjalista serwisu ręcznie czyta wiadomość i interpretuje dane z treści e-maila oraz załączników.
4. Specjalista ręcznie przepisuje dane do pliku Excel `Rejestr Reklamacji 2026.xlsx`.
5. Specjalista subiektywnie przypisuje kategorię wady: `wizualna`, `wymiary`, `materiał` albo `logistyka`.
6. Specjalista tworzy ticket `Complaint` w Jira Cloud.
7. Specjalista sprawdza zamówienie i batch w SAP ERP.
8. Specjalista przygotowuje odpowiedź do klienta. Średni czas odpowiedzi wynosi około 2 dni od zgłoszenia.
9. Jeżeli wada zostanie potwierdzona, specjalista tworzy ticket `Correction` w Jira Cloud dla działu jakości.

## Hotspoty i ryzyka

| Hotspot | Gdzie występuje | Skutek biznesowy |
|---|---|---|
| Spam lub opóźniony e-mail | Wejście przez Microsoft 365 / Exchange | Reklamacja może zostać obsłużona z opóźnieniem albo pominięta |
| Błędy ręcznego przepisywania | E-mail -> Excel -> Jira Cloud / SAP ERP | Błędny numer zamówienia, batch, dane klienta lub kategoria |
| Excel jako słaby system of record | Rejestr reklamacji | Brak kontroli wersji, brak audytu zdarzeń, trudny centralny status |
| Subiektywna kategoryzacja | Klasyfikacja wady przez specjalistę | Niespójne raportowanie przyczyn reklamacji |
| Brak wiarygodnych metryk | Excel, Jira Cloud i SAP ERP jako rozproszone źródła | Brak jasnego SLA, backlogu, trendów i obciążenia zespołu |
| Rozłączone SAP ERP / Jira Cloud / Excel | Walidacja orderu, batcha i ticketów | Specjalista musi ręcznie przenosić kontekst między systemami |
| Backlog 2-3 dni | Wysoki wolumen reklamacji | Wydłużony czas odpowiedzi i mniejsza przewidywalność pracy |
| Brak automatycznej widoczności SLA | Cały proces | Management widzi problem dopiero po fakcie |

## Komentarz procesowy

Proces traci czas na wejściu, bo skrzynka e-mail nie jest kontrolowanym mechanizmem intake. Jeżeli wiadomość trafi do spamu albo zostanie przeczytana później, dalsze kroki procesu nie mogą się rozpocząć. Brak automatycznego monitoringu oznacza, że opóźnienie jest wykrywane dopiero wtedy, gdy ktoś ręcznie sprawdzi skrzynkę albo klient ponowi kontakt.

Proces traci jakość danych w momentach ręcznego przepisywania i subiektywnej klasyfikacji. Ten sam e-mail jest interpretowany przez człowieka, przepisywany do Excela, odtwarzany w Jira Cloud i weryfikowany w SAP ERP. Każde przejście między narzędziami zwiększa ryzyko pomyłki oraz utrudnia późniejsze odtworzenie, skąd pochodziła konkretna informacja.

Proces traci widoczność, ponieważ nie ma jednego timeline'u reklamacji ani spójnego event store. Excel, Jira Cloud i SAP ERP przechowują różne fragmenty sprawy, ale nie dają pełnego obrazu SLA, backlogu, czasu pierwszej odpowiedzi, typów wad ani problematycznych partii produkcyjnych. W praktyce management nie ma bieżącego dashboardu, a specjalista serwisu jest jedyną osobą, która zna pełny kontekst operacyjny konkretnej reklamacji.
