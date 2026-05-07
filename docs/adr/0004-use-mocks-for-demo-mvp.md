# 0004: Use mocks for demo MVP

## Context

MVP ma pokazać proces end-to-end, architekturę i granice automatyzacji AI. Prawdziwe integracje z Microsoft 365 / Exchange, SAP ERP, Jira Cloud, PostgreSQL customer DB i Azure Blob Storage wymagałyby sekretów, konfiguracji i dostępu do systemów, których nie powinno być w publicznym repozytorium.

Demo musi być łatwe do uruchomienia i deterministyczne.

## Decision

W MVP używamy mocków dla Microsoft 365 / Exchange, SAP ERP, Jira Cloud, PostgreSQL customer DB, Azure Blob Storage i AI triage. Mocki implementują jawne kontrakty integracyjne, zwracają przewidywalne dane i umożliwiają pokazanie happy path oraz edge case'ów.

Kontrakty są opisane w dokumentacji, aby było jasne, jak mock można zastąpić adapterem produkcyjnym.

## Consequences

- Demo działa lokalnie bez sekretów i zewnętrznych zależności.
- Testy mogą być deterministyczne.
- Pełny pipeline działa bez konfigurowania cudzych systemów.
- MVP nie dowodzi produkcyjnej gotowości integracji.
- Trzeba jasno pokazać różnicę między mockiem a kontraktem produkcyjnym.

## Alternatives considered

- Prawdziwe integracje: bardziej realistyczne, ale zbyt kosztowne i ryzykowne dla publicznego MVP.
- Tylko dokumentacja bez MVP: prostsze, ale słabiej pokazuje myślenie techniczne.
- Hardcoded demo bez kontraktów: szybkie, ale słabe architektonicznie i trudne do dalszego rozwoju.
