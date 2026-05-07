# Kontekst biznesowy

## Cel dokumentu

Ten dokument opisuje kontekst biznesowy automatyzacji obsługi reklamacji w firmie Metalpol. Jego zadaniem jest pokazanie, jaki problem operacyjny rozwiązujemy, kto korzysta z procesu, jakie są obecne ograniczenia oraz jak cele automatyzacji mapują się na realne bóle biznesowe.

Automatyzacja nie jest tu celem samym w sobie. Celem jest skrócenie czasu obsługi reklamacji, ograniczenie pracy manualnej, poprawa spójności klasyfikacji, udostępnienie metryk procesu oraz zmniejszenie ryzyka operacyjnego wynikającego z ręcznego łączenia kilku systemów.

## Kontekst firmy

Metalpol Sp. z o.o. to fikcyjny producent komponentów metalowych dla branży automotive. Firma ma około 180 pracowników, trzy hale produkcyjne oraz dział serwisu posprzedażowego odpowiedzialny za obsługę reklamacji klientów.

Reklamacje wpływają pocztą e-mail na adres `reklamacje@metalpol.pl`. Klienci przesyłają opis problemu, numer zamówienia oraz zdjęcia wady. Zgłoszenia mogą być napisane po polsku albo po angielsku.

Dostępne systemy w obecnym środowisku:

- Microsoft 365 / Exchange jako kanał wejścia dla reklamacji,
- Excel jako ręczny rejestr reklamacji,
- Jira Cloud jako system ticketowy dla reklamacji i korekt jakościowych,
- SAP ERP jako źródło danych o zamówieniach i partiach,
- wewnętrzna baza klientów PostgreSQL w trybie read-only,
- Azure Blob Storage jako archiwum zdjęć wad.

Wolumen procesu:

- średnio około 600 reklamacji miesięcznie,
- sezonowe szczyty do około 2000 reklamacji miesięcznie,
- typowy e-mail ma około 150 słów i od 1 do 3 zdjęć,
- większość reklamacji można ocenić na podstawie danych z SAP, danych operatora, batcha i parametrów produkcji.

## Obecny proces w skrócie

Obecnie proces reklamacji jest oparty na ręcznej pracy specjalisty serwisu. Klient wysyła e-mail z opisem, numerem zamówienia i zdjęciami. Specjalista czyta wiadomość, przepisuje dane do Excela, kategoryzuje wadę, tworzy zgłoszenie `Complaint` w Jira, sprawdza zamówienie i batch w SAP, a następnie przygotowuje odpowiedź do klienta.

Jeżeli wada zostanie potwierdzona, specjalista tworzy dodatkowy ticket korygujący dla działu jakości w Jira jako `Correction`.

Najważniejszy problem procesu AS-IS: człowiek pełni rolę integratora systemów. Exchange, Excel, Jira, SAP, baza klientów, zdjęcia i dział jakości nie tworzą jednego spójnego przepływu. Status reklamacji, metryki i decyzje są rozproszone między narzędziami oraz ręcznymi czynnościami.

## Użytkownicy i interesariusze

| Rola | Potrzeba w procesie | Wpływ automatyzacji |
|---|---|---|
| Klient | Szybkie potwierdzenie przyjęcia reklamacji, jasna komunikacja, informacja o brakujących danych | Krótszy czas pierwszej odpowiedzi, mniej zgłoszeń zagubionych lub opóźnionych |
| Specjalista serwisu | Sprawne przyjęcie sprawy, komplet danych, mniej przepisywania, jasny status | Automatyczny intake, draft odpowiedzi, routing spraw wymagających decyzji człowieka |
| Dział jakości | Informacja o potwierdzonych wadach i sprawach wymagających korekty | Spójne tworzenie ticketów `Correction` po zatwierdzeniu |
| CEO / management | Widoczność backlogu, SLA, trendów reklamacji i problematycznych partii | Dashboard KPI oparty o zdarzenia procesu |
| Produkcja / operacje | Informacja, które partie, linie lub parametry produkcyjne generują problemy | Korelacja reklamacji z orderem, batchem i linią produkcyjną |
| IT / zespół integracyjny | Kontrolowany, utrzymywalny przepływ między systemami | API adapters, mocki w MVP, jasne kontrakty integracyjne i centralny status |

## Problemy biznesowe

### Opóźnienia

- Około 40% maili trafia do spamu albo jest czytane z opóźnieniem.
- Średni czas odpowiedzi do klienta wynosi około 2 dni od zgłoszenia.
- Przy większym wolumenie backlog może rosnąć do 2-3 dni.

Skutek biznesowy: klient długo czeka na reakcję, a firma traci kontrolę nad SLA i priorytetyzacją spraw.

### Praca manualna

- Jeden specjalista obsługuje 30-80 reklamacji dziennie.
- Dane z maila są ręcznie przepisywane do Excela.
- Specjalista ręcznie przechodzi między Exchange, Excelem, Jira i SAP.

Skutek biznesowy: czas specjalisty jest zużywany na przenoszenie danych, a nie na ocenę reklamacji i kontakt z klientem.

### Niespójna klasyfikacja

- Kategorie wad są oceniane subiektywnie.
- Ten sam typ wady może zostać oznaczony jako `wizualna`, `wymiary`, `materiał` albo `logistyka`, zależnie od specjalisty.

Skutek biznesowy: raportowanie przyczyn reklamacji jest niewiarygodne, a dział jakości i produkcja dostają niespójny obraz problemów.

### Brak metryk

- Brakuje bieżącej informacji o liczbie reklamacji, typach wad i źródłach problemów.
- Nie wiadomo, które linie produkcyjne, partie lub typy zamówień generują najwięcej reklamacji.
- SLA i czas pierwszej odpowiedzi nie są mierzone w spójnym miejscu.

Skutek biznesowy: management nie ma operacyjnego dashboardu do podejmowania decyzji i kontroli obciążenia zespołu.

### Rozłączone systemy

- SAP i Jira nie komunikują się bezpośrednio.
- Excel pełni rolę ręcznego łącznika między systemami.
- Zdjęcia wad, dane reklamacji, statusy i ticket jakościowy nie są zarządzane jako jeden proces.

Skutek biznesowy: status sprawy jest trudny do odtworzenia, a każda reklamacja wymaga wielu manualnych kroków.

### Ryzyko operacyjne

- Mail może zostać pominięty, trafić do spamu albo zostać przeczytany z opóźnieniem.
- Ręczne przepisywanie zwiększa ryzyko błędów w numerze zamówienia, batchu lub kategorii wady.
- Brak kompletności danych może zostać zauważony dopiero późno w procesie.
- Brak centralnego timeline'u utrudnia audyt sprawy.

Skutek biznesowy: proces zależy od pamięci i dokładności pojedynczych osób, a nie od kontrolowanego mechanizmu operacyjnego.

## Mapowanie problemów na mechanizmy TO-BE i KPI

| Problem CEO | Mechanizm w TO-BE | KPI |
|---|---|---|
| 40% maili trafia do spamu lub jest czytane z opóźnieniem | Microsoft Graph webhook + fallback polling + monitoring nieprzetworzonych maili | Time to ingest, missed messages |
| Jeden specjalista obsługuje 30-80 reklamacji dziennie | Automatyczny intake, draft odpowiedzi, routing przypadków | Throughput per specialist, backlog |
| Niespójna kategoryzacja wad | Kontrolowana taksonomia + AI classification + confidence + review | Classification accuracy, manual correction rate |
| Brak metryk | Event store + dashboard KPI | SLA breaches, average first response time |
| SAP, Jira i Excel nie komunikują się | Orchestrator + API adapters + centralny status reklamacji | Manual touches per complaint |
| 2 dni do odpowiedzi | Auto draft + szybka walidacja SAP | First response time |
| Brak informacji o liniach i partiach generujących problemy | Korelacja order / batch / production line | Complaints per line, complaints per batch |

## Cele automatyzacji

1. Skrócić czas od wpływu maila do utworzenia sprawy reklamacyjnej.
2. Ograniczyć ręczne przepisywanie danych między Exchange, Excelem, Jira i SAP.
3. Ujednolicić klasyfikację wad przez kontrolowaną taksonomię i wynik klasyfikacji z confidence.
4. Wykrywać brakujące dane wcześniej, zanim sprawa utknie w ręcznej obsłudze.
5. Generować draft odpowiedzi do klienta, aby specjalista mógł szybciej przejść do merytorycznej weryfikacji.
6. Zachować finalną decyzję po stronie człowieka lub deterministycznych reguł biznesowych.
7. Zbudować timeline zdarzeń dla każdej reklamacji, aby ułatwić audyt i analizę procesu.
8. Udostępnić KPI dla managementu, serwisu, jakości i produkcji.

## Granice zakresu MVP

### W zakresie MVP

- Mock endpoint przyjmujący wiadomość reklamacyjną z Exchange.
- Utworzenie rekordu reklamacji.
- Mock AI triage wyciągający `orderId`, język, opis, kategorię wady, confidence i brakujące pola.
- Dopasowanie klienta przez mock customer DB.
- Sprawdzenie zamówienia i batcha przez mock SAP.
- Zapis załączników jako fake URI w mock Blob Storage.
- Utworzenie mock `Complaint` w Jira.
- Wygenerowanie draftu odpowiedzi.
- Przekazanie sprawy do `HumanReviewRequired`, gdy confidence jest niskie albo brakuje danych.
- Endpoint zatwierdzenia przez człowieka, po którym system tworzy mock `Correction`.
- Timeline eventów i podstawowe KPI procesu.

### Poza zakresem MVP

- Produkcyjna integracja z Microsoft Graph, SAP, Jira, PostgreSQL i Azure Blob Storage.
- Prawdziwy model LLM, prompt tuning, fine-tuning lub ocena jakości modelu na dużym zbiorze danych.
- Automatyczne podejmowanie finalnych decyzji reklamacyjnych.
- Pełny panel UI dla specjalisty serwisu, działu jakości lub managementu.
- Zaawansowane role, uprawnienia, SSO i audyt bezpieczeństwa.
- Migracja historycznych danych z Excela.
- Produkcyjne monitorowanie, retry policy, dead-letter queue i alerting.
- Obsługa rzeczywistych danych klientów, sekretów i plików produkcyjnych.

## Założenia

- Metalpol jest firmą fikcyjną, a wszystkie dane w repozytorium powinny być przykładowe.
- Źródłem prawdy dla zamówień i batchy pozostaje SAP.
- Źródłem prawdy dla decyzji reklamacyjnej pozostaje człowiek oraz uzgodnione reguły biznesowe.
- AI jest komponentem pomocniczym do ekstrakcji, klasyfikacji, streszczania i przygotowania draftów.
- W MVP wszystkie integracje zewnętrzne są mockowane.
- Zachowanie mock AI w testach powinno być deterministyczne.
- Projekt ma być neutralny względem dostawcy AI i chmury.

## Pytania otwarte do walidacji z klientem

- Jakie SLA obowiązuje dla pierwszej odpowiedzi, potwierdzenia przyjęcia i finalnego rozstrzygnięcia reklamacji?
- Jaka jest docelowa taksonomia wad i kto jest właścicielem jej zmian?
- Jaki próg confidence powinien kierować sprawę do ręcznego przeglądu?
- Jakie dane są minimalnie wymagane do przyjęcia reklamacji?
- Jak obsługiwać reklamacje bez numeru zamówienia albo z niepoprawnym batchem?
- Czy kategorie `wizualna`, `wymiary`, `materiał` i `logistyka` są wystarczające, czy wymagają podkategorii?
- Jak wygląda obecny workflow ticketów `Complaint` i `Correction` w Jira?
- Jakie dane o linii produkcyjnej i parametrach produkcji są dostępne przez SAP?
- Kto zatwierdza draft odpowiedzi do klienta i kiedy można go wysłać?
- Jak długo przechowywać zdjęcia wad i kto powinien mieć do nich dostęp?
- Jak mierzyć baseline KPI przed wdrożeniem automatyzacji?
- Jakie są wymagania dotyczące monitoringu nieprzetworzonych maili, fallback polling i alertów?
