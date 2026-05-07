# KPI i raportowanie

## Cel dokumentu

Ten dokument definiuje, jak mierzyć efekt automatyzacji obsługi reklamacji w Metalpolu. Metryki są dobrane tak, aby pokazać poprawę procesu, a nie aktywność samego narzędzia.

Najważniejsza zasada: KPI muszą odpowiadać na pytania biznesowe zarządu, serwisu, jakości i produkcji. Jeżeli metryka nie pomaga podjąć decyzji operacyjnej, nie powinna być eksponowana na głównym dashboardzie.

## Główne pytania biznesowe

- Czy reklamacje są przyjmowane szybko i bez pominiętych wiadomości?
- Czy klient otrzymuje pierwszą odpowiedź szybciej niż w procesie AS-IS?
- Czy backlog jest pod kontrolą, szczególnie w sezonowych szczytach wolumenu?
- Które kategorie wad, linie produkcyjne i batche generują najwięcej reklamacji?
- Ile spraw wymaga ręcznego przeglądu i dlaczego?
- Czy integracje z Jira Cloud i SAP ERP działają stabilnie?
- Czy klasyfikacja AI jest użyteczna, mierzalna i korygowalna przez człowieka?

## Mapa problemów biznesowych do KPI

| Zgłoszony problem CEO | Root cause | Proponowany mechanizm w TO-BE | KPI mierzący poprawę | Oczekiwany wpływ biznesowy |
|---|---|---|---|---|
| 40% maili trafia do spamu lub jest czytane z opóźnieniem | Brak aktywnego intake, fallback polling i monitoringu wiadomości bez dalszego przetworzenia | Microsoft Graph webhook, fallback polling, event `EmailReceived`, alert dla wiadomości bez rekordu reklamacji | Time to ingest email, missed messages, SLA breach count | Reklamacje szybciej trafiają do procesu, a ryzyko pominięcia maila jest mierzalne |
| Odpowiedź do klienta trwa średnio około 2 dni | Dane są ręcznie zbierane z maila, Excela, SAP ERP i Jira Cloud przed przygotowaniem odpowiedzi | Automatyczny intake, ekstrakcja danych, walidacja SAP ERP / PostgreSQL customer DB, draft odpowiedzi | First response time, time to ingest email, percent of complaints requiring manual review | Krótszy czas pierwszej reakcji i mniej oczekiwania klienta na podstawową informację |
| Specjalista obsługuje 30-80 reklamacji dziennie | Powtarzalne czynności manualne blokują czas specjalisty | Orchestrator, automatyczne tworzenie sprawy, mock Jira Cloud `Complaint`, routing wyjątków | Backlog size, average resolution time, Jira issue creation success rate | Większa przepustowość zespołu i mniejszy backlog przy wzroście wolumenu |
| Kategoryzacja wad jest niespójna | Brak wspólnej taksonomii, brak confidence i brak śledzenia korekt | Kontrolowana taksonomia, AI classification, confidence, review i korekta klasyfikacji | Complaint count by defect category, AI extraction confidence distribution, classification correction rate | Lepsze dane dla jakości i produkcji, mniej sporów o interpretację typu wady |
| Brak metryk dla zarządu | Excel, Jira Cloud i SAP ERP pokazują fragmenty procesu, ale nie wspólny timeline | Event store, centralny status reklamacji, dashboard KPI | SLA breach count, first response time, average resolution time, backlog size | Zarząd widzi trend, ryzyko i skuteczność procesu bez ręcznego raportowania |
| SAP ERP, Jira Cloud i Excel nie komunikują się | Specjalista przenosi kontekst między systemami ręcznie | API adapters, centralny complaint id, timeline integracyjny | Jira issue creation success rate, SAP verification failure rate, manual touches per complaint | Mniej błędów operacyjnych i większa przewidywalność procesu |
| Brak informacji o partiach i liniach produkcyjnych | Dane reklamacji nie są konsekwentnie łączone z orderem, batchem i production line | Walidacja order/batch w SAP ERP, dołączenie production line do rekordu reklamacji | Complaint count by production line, complaint count by batch, SAP verification failure rate | Produkcja szybciej identyfikuje problematyczne partie i linie |
| Dużo spraw wymaga ręcznego doprecyzowania | Braki w danych wejściowych są wykrywane późno | Walidacja pól wymaganych, `missingFields`, status `HumanReviewRequired`, draft prośby o uzupełnienie | Percent of complaints requiring manual review, average resolution time, SLA breach count | Sprawy niekompletne są szybciej separowane od spraw gotowych do obsługi |

## Definicje KPI

| KPI | Definicja | Jak mierzyć | Źródło zdarzeń / danych | Po co mierzyć |
|---|---|---|---|---|
| Time to ingest email | Czas od pojawienia się wiadomości w skrzynce do utworzenia rekordu reklamacji | `ComplaintCreatedAt - EmailReceivedAt` | `EmailReceived`, status `Received` / `IntakeQueued` | Wykrywa opóźnienia intake i problemy z webhookiem / pollingiem |
| First response time | Czas od otrzymania maila do wysłania pierwszej odpowiedzi lub draftu gotowego do review | `FirstResponseDraftedAt - EmailReceivedAt` | `EmailReceived`, `ResponseDrafted` | Mierzy realną poprawę widoczną dla klienta |
| Average resolution time | Średni czas od otrzymania reklamacji do zamknięcia lub utworzenia korekty | Średnia z `ResolvedAt - EmailReceivedAt` | `EmailReceived`, `ComplaintClosed`, `CorrectionTicketCreated` | Pokazuje pełną skuteczność procesu, nie tylko intake |
| Backlog size | Liczba aktywnych reklamacji bez finalnego statusu | Count spraw w statusach otwartych | Aktualny status reklamacji | Kontroluje obciążenie zespołu i ryzyko opóźnień |
| SLA breach count | Liczba spraw, które przekroczyły ustalony próg SLA | Count spraw po terminie dla pierwszej odpowiedzi lub rozwiązania | Status reklamacji, konfiguracja SLA | Umożliwia zarządowi szybkie wykrycie ryzyka operacyjnego |
| Complaint count by defect category | Liczba reklamacji według kategorii wady | Grupowanie po `defectCategory` | `DefectClassified`, korekty klasyfikacji | Pokazuje główne typy problemów jakościowych |
| Complaint count by production line | Liczba reklamacji według linii produkcyjnej | Grupowanie po `productionLine` | `OrderVerified`, dane SAP ERP | Wskazuje linie wymagające analizy jakościowej |
| Complaint count by batch | Liczba reklamacji według partii produkcyjnej | Grupowanie po `batchId` | `BatchVerified`, dane SAP ERP | Pomaga wykrywać problematyczne partie |
| Percent of complaints requiring manual review | Udział spraw skierowanych do człowieka | `HumanReviewRequired / total complaints * 100%` | `HumanReviewRequested`, status reklamacji | Mierzy skalę wyjątków i jakość danych wejściowych |
| AI extraction confidence distribution | Rozkład confidence dla ekstrakcji i klasyfikacji | Histogram lub percentyle confidence | `ComplaintParsed`, `DefectClassified` | Pokazuje, gdzie AI wspiera proces stabilnie, a gdzie wymaga review |
| Classification correction rate | Udział spraw, w których człowiek zmienił kategorię sugerowaną przez AI | `ClassificationCorrected / classified complaints * 100%` | `DefectClassified`, `ClassificationCorrected` | Mierzy użyteczność klasyfikacji i jakość taksonomii |
| Jira issue creation success rate | Odsetek udanych prób utworzenia ticketów Jira Cloud | `JiraIssueCreated / JiraIssueCreationRequested * 100%` | `JiraComplaintCreated`, `CorrectionTicketCreated`, błędy integracji | Kontroluje stabilność integracji ticketowej |
| SAP verification failure rate | Odsetek spraw, w których order lub batch nie przeszedł walidacji | `SapMismatchDetected / SapVerificationRequested * 100%` | `OrderVerified`, `BatchVerified`, `SapMismatchDetected` | Wykrywa problemy z jakością danych wejściowych albo dostępnością SAP ERP |

## Minimalny model danych dla raportowania

Dashboard KPI powinien bazować na zdarzeniach procesu, a nie na ręcznym Excelu. Minimalny zestaw pól dla raportowania:

- `complaintId`,
- `receivedAt`,
- `createdAt`,
- `firstResponseDraftedAt`,
- `resolvedAt`,
- `status`,
- `language`,
- `customerId`,
- `orderId`,
- `batchId`,
- `productionLine`,
- `defectCategory`,
- `aiExtractionConfidence`,
- `missingFields`,
- `requiresManualReview`,
- `jiraComplaintKey`,
- `jiraCorrectionKey`,
- `sapVerificationStatus`,
- `slaBreached`.

W MVP te dane mogą być przechowywane w pamięci lub prostym mock event store. W wersji produkcyjnej powinny trafić do trwałego event store albo bazy operacyjnej, z której dashboard odczytuje agregaty.

## Dashboard zarządczy

### Widok dzienny CEO

CEO powinien codziennie widzieć stan operacyjny procesu:

- ile nowych reklamacji wpłynęło dzisiaj,
- ile reklamacji czeka w backlogu,
- ile spraw przekroczyło SLA,
- średni `time to ingest email`,
- średni `first response time`,
- ile spraw wymaga ręcznego przeglądu,
- czy wystąpiły błędy tworzenia ticketów Jira Cloud albo walidacji SAP ERP.

Ten widok odpowiada na pytanie: czy proces działa dzisiaj i czy trzeba interweniować operacyjnie.

### Widok tygodniowy CEO

Co tydzień CEO powinien widzieć trendy i miejsca przeciążenia:

- trend liczby reklamacji dzień po dniu,
- backlog na koniec każdego dnia,
- top kategorie wad,
- top linie produkcyjne według liczby reklamacji,
- top batche według liczby reklamacji,
- odsetek spraw wymagających manual review,
- classification correction rate,
- SAP verification failure rate,
- Jira issue creation success rate.

Ten widok odpowiada na pytanie: gdzie proces albo produkcja generują powtarzalne problemy.

### Widok miesięczny CEO

Co miesiąc CEO powinien widzieć efekt biznesowy automatyzacji:

- porównanie first response time do baseline AS-IS,
- porównanie average resolution time do baseline AS-IS,
- trend SLA breach count,
- zmiana backlog size przy podobnym wolumenie reklamacji,
- udział spraw obsłużonych bez dodatkowego ręcznego doprecyzowania,
- najczęstsze kategorie wad,
- linie i batche wymagające działań jakościowych,
- stabilność integracji Jira Cloud i SAP ERP.

Ten widok odpowiada na pytanie: czy automatyzacja realnie poprawia obsługę reklamacji i gdzie inwestować w kolejne usprawnienia.

## Praktyczne zasady interpretacji KPI

- Wysoki `time to ingest email` oznacza problem z intake, webhookiem, pollingiem lub filtrowaniem skrzynki.
- Wysoki `first response time` oznacza, że automatyczny draft albo walidacja danych nie skracają jeszcze pracy specjalisty.
- Rosnący `backlog size` przy stabilnym wolumenie oznacza problem z przepustowością albo zbyt dużo spraw trafiających do ręcznego przeglądu.
- Wysoki `percent of complaints requiring manual review` może oznaczać niską jakość maili klientów, zbyt wysoki próg confidence albo zbyt słabą ekstrakcję danych.
- Wysoki `classification correction rate` oznacza problem z taksonomią, regułami klasyfikacji albo jakością danych wejściowych.
- Wysoki `SAP verification failure rate` może oznaczać błędne numery zamówień w mailach albo problem po stronie integracji SAP ERP.
- Niski `Jira issue creation success rate` oznacza ryzyko operacyjne, bo sprawy mogą nie trafiać do Jira Cloud mimo poprawnego intake.

## Granice raportowania w MVP

W MVP dashboard KPI powinien pokazać mechanikę pomiaru, nie pełne raportowanie produkcyjne. W zakresie MVP wystarczy:

- endpoint `GET /api/dashboard/kpis`,
- agregaty liczone na podstawie mock eventów,
- kilka scenariuszy demonstracyjnych pokazujących reklamacje kompletne, niekompletne, o niskim confidence i z błędem walidacji SAP ERP,
- czytelne wartości KPI po uruchomieniu demo.

Poza zakresem MVP pozostają:

- produkcyjne hurtownie danych,
- BI dashboard w Power BI albo innym narzędziu,
- alerting on-call,
- retencja danych produkcyjnych,
- audyt dostępu do raportów.
