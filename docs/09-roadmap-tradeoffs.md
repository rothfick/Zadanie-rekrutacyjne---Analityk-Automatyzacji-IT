# Roadmapa i trade-offy

## Cel dokumentu

Ten dokument pokazuje realistyczną ścieżkę rozwoju rozwiązania oraz najważniejsze kompromisy architektoniczne. Roadmapa zaczyna się od walidacji procesu i danych, potem przechodzi przez MVP demonstracyjne, a dopiero później przez bardziej zaawansowane elementy AI i analityki jakości.

Założenie przewodnie: najpierw kontrolowany proces i mierzalność, potem automatyzacja coraz trudniejszych decyzji pomocniczych.

## Roadmapa

### Phase 0: discovery and data validation

Cel: potwierdzić proces, dane i definicję sukcesu przed implementacją.

Zakres:

- warsztat z serwisem, jakością, produkcją, IT i managementem,
- walidacja obecnego procesu reklamacji AS-IS,
- potwierdzenie wymaganych pól reklamacji,
- uzgodnienie taksonomii wad,
- sprawdzenie jakości historycznych maili i Excela,
- potwierdzenie dostępności danych SAP ERP: order, batch, production line,
- ustalenie baseline KPI: first response time, backlog, SLA breaches, manual touches.

Efekt: jasny zakres MVP, lista ryzyk danych i uzgodnione KPI.

### Phase 1 MVP: controlled complaint automation

Cel: pokazać end-to-end pipeline na mockach, bez prawdziwych integracji.

Zakres:

- email intake przez mock Microsoft 365 / Exchange endpoint,
- AI extraction i klasyfikacja na deterministycznym mocku,
- walidacja SAP ERP przez mock adapter,
- customer lookup przez mock PostgreSQL customer DB,
- zapis załączników jako fake Azure Blob Storage URI,
- utworzenie mock Jira Cloud `Complaint`,
- draft odpowiedzi do klienta,
- human review dla braków i niskiego confidence,
- podstawowy dashboard KPI,
- timeline eventów.

Efekt: pełny przepływ reklamacji można uruchomić lokalnie bez zewnętrznych kont i sekretów.

### Phase 2: human review UI and feedback loop

Cel: zwiększyć użyteczność procesu dla specjalisty serwisu i poprawiać jakość klasyfikacji.

Zakres:

- prosty UI do przeglądu spraw `HumanReviewRequired`,
- edycja draftu odpowiedzi,
- korekta kategorii wady,
- zapisywanie korekt jako feedback,
- raport `classification correction rate`,
- lepsza analityka brakujących danych i powodów review.

Efekt: system zaczyna uczyć organizację, które dane i klasyfikacje są problematyczne.

### Phase 3: advanced image triage and root cause analysis

Cel: wzbogacić triage o jakość zdjęć i analizę trendów jakościowych.

Zakres:

- ocena jakości zdjęć: czytelne, częściowe, niewyraźne, brak,
- sugestia prośby o lepsze zdjęcia,
- analiza trendów defect category vs batch vs production line,
- wykrywanie powtarzalnych problemów jakościowych,
- półautomatyczne odpowiedzi do klienta dla niskiego ryzyka po zatwierdzeniu reguł.

Efekt: dział jakości i produkcja dostają lepszy sygnał o źródłach problemów.

### Phase 4: predictive quality insights

Cel: przejść od raportowania reklamacji do predykcyjnego wsparcia jakości.

Zakres:

- predykcja ryzyka reklamacji dla batchy lub linii,
- korelacja z parametrami produkcyjnymi,
- alerty dla powtarzalnych anomalii,
- rekomendacje działań prewencyjnych,
- analiza wpływu działań korygujących na liczbę reklamacji.

Efekt: automatyzacja nie tylko przyspiesza obsługę reklamacji, ale pomaga ograniczać ich przyczyny.

## Trade-offy architektoniczne

### Webhook vs polling

Decyzja dla TO-BE: webhook jako podstawowy mechanizm intake, fallback polling jako zabezpieczenie.

Trade-off:

- webhook daje niski czas reakcji i wspiera KPI `time to ingest email`,
- polling jest prostszy koncepcyjnie i odporny na zgubione webhooki,
- samo polling może zwiększyć opóźnienia,
- sam webhook może przegapić wiadomość przy problemach subskrypcji.

Wniosek: hybryda webhook + fallback polling najlepiej odpowiada problemowi CEO z opóźnionymi lub pominiętymi mailami.

### Queue-based async processing vs synchronous flow

Decyzja dla produkcji: asynchroniczne przetwarzanie przez kolejkę dla intake i integracji. Dla MVP dopuszczalny uproszczony flow synchroniczny, jeżeli zachowuje event timeline.

Trade-off:

- asynchroniczność zwiększa odporność na SAP ERP, Jira Cloud i Azure Blob Storage timeouty,
- synchroniczny flow jest prostszy do demo i debugowania,
- kolejki wymagają idempotencji, retry i obserwowalności,
- synchroniczny flow łatwiej blokuje request na zewnętrznej integracji.

Wniosek: MVP może być proste, ale projekt powinien mieć eventy i statusy gotowe pod późniejszą kolejkę.

### LLM extraction vs deterministic parsing

Decyzja: LLM do ekstrakcji z nieustrukturyzowanego maila, deterministic parsing do walidacji formatów i reguł.

Trade-off:

- LLM lepiej radzi sobie z mailami PL/EN i różnymi stylami opisu,
- deterministic parsing jest przewidywalny i łatwy do testowania,
- LLM może halucynować, dlatego wynik wymaga schema validation i confidence,
- parser regułowy może być kruchy przy realnych mailach klientów.

Wniosek: LLM pomaga w triage, ale nie zastępuje walidacji SAP ERP, schema validation i human review.

### AI draft vs automatic customer response

Decyzja: AI generuje draft, człowiek zatwierdza odpowiedź.

Trade-off:

- draft skraca czas pracy specjalisty,
- automatyczna odpowiedź mogłaby jeszcze bardziej skrócić first response time,
- automatyczne wysyłanie zwiększa ryzyko złej komunikacji i obietnic biznesowych,
- human approval chroni relację z klientem i jakość decyzji.

Wniosek: w MVP i pierwszych fazach AI draft jest bezpieczniejszy niż automatyczna wysyłka.

### Mocks vs real integrations in MVP

Decyzja: mocki dla wszystkich integracji zewnętrznych.

Trade-off:

- mocki dają deterministyczne demo bez sekretów,
- realne integracje lepiej pokazałyby produkcyjne detale,
- realne systemy są trudne do bezpiecznego udostępnienia w publicznym demo,
- mocki nadal mogą pokazywać kontrakty, retry, błędy i idempotencję.

Wniosek: mocki są właściwym wyborem dla MVP demonstracyjnego, pod warunkiem że kontrakty integracyjne są jawne.

### Jira Cloud as workflow system vs custom workflow UI

Decyzja: Jira Cloud jako system workflow, custom UI tylko dla widoków specyficznych dla review, jeśli będzie potrzebny.

Trade-off:

- Jira Cloud już istnieje i jest znana zespołowi,
- custom UI może być lepszy ergonomicznie dla serwisu,
- budowa własnego workflow UI zwiększa zakres i utrzymanie,
- Jira Cloud nie jest idealnym źródłem audytu całego procesu.

Wniosek: Jira Cloud powinna obsługiwać operacyjne tickety, a orchestrator i event store powinny przechowywać centralny status oraz timeline.

### Azure Blob Storage SAS links vs copying attachments into Jira Cloud

Decyzja: załączniki w Azure Blob Storage, Jira Cloud dostaje kontrolowane linki.

Trade-off:

- Azure Blob Storage lepiej obsługuje pliki, retencję i dostęp,
- kopiowanie załączników do Jira Cloud upraszcza widok użytkownika,
- stałe linki mogą być ryzykiem bezpieczeństwa,
- SAS links muszą mieć kontrolowany czas życia i uprawnienia.

Wniosek: Azure Blob Storage jako archiwum jest bezpieczniejszy i bardziej skalowalny, a Jira Cloud powinna dostać tylko kontrolowane linki.

### No fine-tuning initially vs future fine-tuning

Decyzja: bez fine-tuningu w MVP; najpierw prompt, schema validation, deterministic tests i feedback loop.

Trade-off:

- fine-tuning może poprawić klasyfikację po zebraniu danych,
- na starcie nie ma wystarczająco jakościowych przykładów,
- fine-tuning zwiększa koszt i złożoność utrzymania,
- dobrze zaprojektowane prompting + feedback wystarczy do walidacji wartości procesu.

Wniosek: fine-tuning można rozważyć dopiero po zebraniu oznaczonych korekt człowieka i pomiarze classification correction rate.

## Zależność roadmapy od KPI

| Faza | Najważniejsze KPI |
|---|---|
| Phase 0 | baseline first response time, baseline backlog, manual touches per complaint |
| Phase 1 | time to ingest email, first response time, Jira issue creation success rate, SAP verification failure rate |
| Phase 2 | percent of complaints requiring manual review, classification correction rate, draft edit rate |
| Phase 3 | complaint count by defect category, production line, batch, image quality issue count |
| Phase 4 | trend reklamacji po działaniach korygujących, predictive quality alert precision |

Roadmapa powinna być sterowana metrykami. Jeżeli Phase 1 nie skraca first response time ani nie zmniejsza manual touches, nie ma sensu przechodzić do zaawansowanego image triage.
