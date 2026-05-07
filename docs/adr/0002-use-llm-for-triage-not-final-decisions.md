# 0002: Use LLM for triage, not final decisions

## Context

Reklamacje przychodzą jako nieustrukturyzowane e-maile po polsku lub angielsku. Specjalista musi ręcznie odczytać numer zamówienia, opis, kategorię wady i brakujące dane. To powoduje opóźnienia i błędy przepisywania.

Jednocześnie finalna decyzja reklamacyjna ma konsekwencje biznesowe i nie powinna zależeć od probabilistycznego modelu.

## Decision

LLM jest używany tylko jako komponent triage: ekstrakcja danych, detekcja języka, klasyfikacja do kontrolowanej taksonomii, streszczenie i draft odpowiedzi. Model zwraca structured output, confidence i missing fields.

LLM nie potwierdza danych SAP ERP, nie tworzy ticketów Jira Cloud, nie zmienia stanu procesu i nie podejmuje finalnej decyzji reklamacyjnej.

## Consequences

- System szybciej przygotowuje sprawę dla specjalisty.
- Mniejsze ryzyko błędów niż przy automatycznej decyzji AI.
- Wynik LLM musi być walidowany przez schema i progi confidence.
- Niskie confidence oraz braki danych prowadzą do human review.
- Nadal potrzebne są deterministyczne reguły i adaptery do źródeł prawdy.

## Alternatives considered

- Tylko deterministic parsing: bezpieczniejsze, ale kruche przy naturalnych mailach klientów.
- LLM jako pełny agent decyzyjny: efektowne, ale za ryzykowne dla reklamacji i niezgodne z założeniem human-in-the-loop.
- Brak AI: najprostsze technicznie, ale nie usuwa ręcznego triage i klasyfikacji.
