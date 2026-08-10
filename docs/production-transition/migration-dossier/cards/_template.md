---
card_id: MIG-CARD-AREA-NNN
title: gameplay domain 제목으로 교체
domain: coverage의 gameplay_area와 일치하는 값으로 교체
status: draft
coverage: partial
migration_readiness: blocked
as_of_commit: 40자리 clean commit으로 교체
supersedes: none
depends_on: []
---

# 카드 제목

## Scope와 non-goals

이 카드가 다루는 gameplay 의미와 제외 범위를 적는다. 클라이언트 구현 구조, 런타임
타입, asset, 연출, 실행 환경과 이식 대상의 구현 선택은 기록하지 않는다.

## Gameplay rule statements

| rule_id | 게임플레이 의미 | rule_status | 적용 범위 | 참조 |
|---|---|---|---|---|
| `AREA-RULE-001` | 독립적으로 검토할 수 있는 규칙 하나 | `unknown` | 조건과 mode | 활성 spec 경로 또는 기존 claim ID |

`rule_status`는 `intended | incidental | unknown | conflict`만 사용한다. 한 행에는
주장 하나만 둔다. 출처가 충돌하면 승자를 추정하지 않고 모든 출처와 `conflict`를
기록한다.

## Authoritative state 후보와 invariants

규칙을 해석하는 데 필요한 gameplay-visible state와 invariant만 기록한다. 구현
handle 대신 안정적인 domain 용어를 사용한다.

## Logical inputs, validation과 atomic effects

| input_id | 의도 또는 match event | 전제조건 | 허용 outcome | 거절 outcome | rule_status |
|---|---|---|---|---|---|
| `AREA-INPUT-001` | 입력 의미 | 전제조건 | 의미론적 결과 | 의미론적 거절 | `unknown` |

## Ordering, timing, numeric과 randomness 의미

출처가 명시한 인과 순서, clock, deadline과 동시 사건 규칙만 적는다. 빠진 순서는
추정하지 않고 열린 질문으로 둔다.

## Boundary 및 acceptance cases

| case_id | Given | When | Then | rule_status | 공백 | 참조 |
|---|---|---|---|---|---|---|
| `AREA-CASE-001` | 경계 전제조건 | 사건 또는 입력 | 기대 gameplay outcome 또는 `unknown` | `unknown` | 필요한 결정 | 활성 spec 경로 또는 기존 claim ID |

## Mode와 content variants

| variant_id | 조건 | 의미 차이 | rule_status | 참조 |
|---|---|---|---|---|
| `AREA-VARIANT-001` | Mode 또는 ruleset 조건 | 차이 또는 `unknown` | `unknown` | 활성 spec 경로 또는 기존 claim ID |

## Dependencies

- 다른 카드가 필요하면 front matter의 `depends_on`과 같은 카드 ID를 적는다.
- Readiness를 막는 결정은 `decisions.md`의 기존 `decision_id`만 적는다.
- 의존성이 없으면 `none`이라고 적는다.

## Open decisions

| question_id | decision_id | 질문 | 현재 근거 | 준비도 영향 |
|---|---|---|---|---|
| `AREA-Q-001` | `MIG-DEC-AREA-001` | 해결되지 않은 gameplay 질문 | 출처가 확인하는 것과 확인하지 못하는 것 | Freeze를 막는 이유 |

## References

활성 `docs/spec/*` 문서 또는 기존 `SRC-*`, `BASE-*`, `TRN-*`, `ENG-*` claim ID만
허용한다. Handoff summary는 탐색용 지도일 뿐 진실원으로 인용하지 않는다.

## Readiness checklist

- [ ] 범위와 비목표가 명확하다.
- [ ] 모든 규칙이 `intended`, `incidental`, `unknown`, `conflict`를 구분한다.
- [ ] Boundary와 mode 변형이 완전하거나 명시적 gap으로 남아 있다.
- [ ] References가 `as_of_commit`에서 유효하다.
- [ ] 클라이언트 전용 구현·연출 정보가 없다.
- [ ] 중대 공백이 해결되거나 freeze 공백으로 담당자 승인을 받았다.
- [ ] 담당자 검토가 `review-ledger.md`에 기록됐다.
