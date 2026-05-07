# 0003: Keep human in the loop for risky cases

## Context

Nie wszystkie reklamacje są równe. Część ma brakujące dane, niskie confidence klasyfikacji, SAP ERP mismatch, podejrzenie duplikatu albo wysokie ryzyko biznesowe. Automatyczne przejście dalej mogłoby pogorszyć relację z klientem albo wygenerować błędną decyzję.

Potrzebujemy granicy, przy której system przestaje automatyzować i przekazuje sprawę człowiekowi.

## Decision

Wprowadzamy stan `HumanReviewRequired`. Trafiają do niego sprawy z brakującymi danymi, confidence poniżej progu, SAP ERP mismatch, podejrzeniem duplikatu, nieznanym klientem, prompt injection albo oznaczeniem high-risk.

Specjalista zatwierdza odpowiedź do klienta i decyzję o dalszym kroku. AI może przygotować draft, ale nie wysyła go samodzielnie.

## Consequences

- Automatyzacja jest bezpieczniejsza i bardziej kontrolowana.
- Specjalista skupia się na wyjątkach, a nie na przepisywaniu danych.
- Część spraw nadal wymaga pracy ręcznej.
- Trzeba mierzyć `percent of complaints requiring manual review`, aby wiedzieć, czy automatyzacja realnie zmniejsza tarcie.

## Alternatives considered

- Automatyczne odpowiedzi bez zatwierdzenia: szybsze, ale ryzykowne komunikacyjnie i biznesowo.
- Ręczny review każdej sprawy: bezpieczne, ale ogranicza wartość automatyzacji.
- Review tylko przy niskim confidence: niewystarczające, bo ryzyko może wynikać też z SAP ERP mismatch, duplikatu lub wartości klienta.
