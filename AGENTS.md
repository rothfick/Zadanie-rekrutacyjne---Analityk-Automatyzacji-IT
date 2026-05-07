# AGENTS.md

## Cel repozytorium

To repozytorium opisuje i demonstruje rozwiązanie dla przypadku biznesowego Metalpol: automatyzację obsługi reklamacji w firmie produkcyjnej.

Najważniejszy cel: pokazać dojrzałe myślenie analityczne, procesowe i techniczne, a nie maksymalną ilość kodu.

Repozytorium powinno zawierać:

- klarowną analizę biznesową i procesową,
- diagramy Event Storming dla stanu AS-IS i TO-BE,
- specyfikację techniczną rozwiązania,
- uruchamialny mock MVP w .NET pokazujący pipeline automatyzacji reklamacji,
- profesjonalny, neutralny względem dostawcy projekt automatyzacji AI.

## Język i styl

- Dokumentację, opisy procesów, uzasadnienia decyzji i materiały biznesowe pisz po polsku.
- Nazwy techniczne, kod, namespace'y, API, klasy, testy, commity i identyfikatory pisz po angielsku.
- Stawiaj na czytelność biznesową: krótkie sekcje, konkretne decyzje, jasne powiązanie problemu z rozwiązaniem.
- Nie dodawaj metadanych, sformułowań wskazujących na wygenerowanie przez narzędzia AI ani oznaczeń automatycznego współautorstwa w commitach, komentarzach, nagłówkach i README.

## Priorytety pracy

1. Najpierw jasność biznesowa i procesowa.
2. Potem spójny model TO-BE i techniczna specyfikacja.
3. Dopiero potem kod MVP, tylko w zakresie potrzebnym do demonstracji pipeline'u.
4. Kod ma wspierać opis rozwiązania, nie zastępować analizy.

Każda proponowana automatyzacja musi wynikać z konkretnego problemu biznesowego albo KPI, np. skrócenia czasu pierwszej odpowiedzi, redukcji pracy manualnej, poprawy kompletności danych, lepszej kontroli SLA lub zwiększenia spójności decyzji.

## Zasady projektowania rozwiązania AI

- AI pełni rolę komponentu triage, ekstrakcji danych i przygotowania draftu odpowiedzi.
- AI nie jest finalnym decydentem w procesie reklamacyjnym.
- Decyzje końcowe, akceptacje, odrzucenia i eskalacje powinny pozostawać pod kontrolą człowieka lub deterministycznych reguł biznesowych.
- Projektuj provider-neutral: nie uzależniaj architektury od jednego dostawcy LLM, chmury lub konkretnej usługi.
- Integracje zewnętrzne muszą być mockowane.
- Nie commituj sekretów, tokenów, kluczy API, connection stringów ani prawdziwych danych klientów.
- Dane przykładowe powinny być fikcyjne i bezpieczne do publicznej prezentacji.

## Decyzje architektoniczne

Każda istotna decyzja architektoniczna powinna zawierać:

- kontekst i problem,
- wybraną decyzję,
- alternatywy,
- trade-offy,
- konsekwencje dla biznesu, utrzymania lub testowalności.

Nie przedstawiaj automatyzacji jako celu samego w sobie. Zawsze pokazuj, jaki problem usuwa i jaki efekt biznesowy może dać.

## MVP .NET

MVP powinno być uruchamialnym mockiem pipeline'u obsługi reklamacji. Preferowany zakres:

- przyjęcie zgłoszenia reklamacyjnego,
- ekstrakcja danych z treści zgłoszenia,
- klasyfikacja / triage,
- walidacja kompletności danych,
- propozycja następnego kroku lub draft odpowiedzi,
- przekazanie sprawy do człowieka w przypadku ryzyka, braku danych lub niskiej pewności.

W kodzie:

- używaj angielskich nazw klas, metod, namespace'ów, endpointów i testów,
- zachowaj prostą strukturę projektu, łatwą do szybkiego przeglądu,
- mockuj LLM, CRM, ERP, e-mail, storage i inne integracje zewnętrzne,
- utrzymuj zachowanie LLM w testach deterministyczne,
- preferuj czytelne typy i jawne przepływy zamiast nadmiarowych abstrakcji.

## Testy i jakość

- Przed commitem uruchom odpowiednie testy.
- Jeśli testów jeszcze nie ma albo zmiana dotyczy tylko dokumentacji, wyjaśnij to w opisie pracy lub komunikacie przed commitem.
- Testy komponentów AI/mock LLM muszą być deterministyczne i powtarzalne.
- Pokrywaj testami logikę decyzyjną, routing, walidację danych i przypadki eskalacji do człowieka.
- Nie testuj rzeczy przez prawdziwe integracje zewnętrzne.

## Commitowanie

- Używaj Conventional Commits, np. `docs: add process overview`, `feat: add complaint triage pipeline`, `test: cover missing-data escalation`.
- Commit messages pisz po angielsku.
- Przed commitem sprawdź `git status` i upewnij się, że nie dodajesz przypadkowych plików.
- Nie commituj sekretów, lokalnych konfiguracji, plików tymczasowych, build artifactów ani danych produkcyjnych.

## Standard repozytorium

Repozytorium ma być:

- czytelne biznesowo, procesowo i technicznie,
- wolne od zależności od konkretnego dostawcy AI,
- nastawione na realną wartość dla procesu reklamacyjnego Metalpol,
- łatwe do uruchomienia lokalnie bez sekretów i zewnętrznych kont,
- wystarczająco szczegółowe, aby zespół techniczny mógł zaplanować dalszą implementację.
