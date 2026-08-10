---
card_id: MIG-CARD-CORE-001
title: 핵심 전투 생명주기
domain: Match lifecycle와 terminal
status: review_requested
coverage: partial
migration_readiness: blocked
as_of_commit: 2d35df0680ce97d29b78101120cb9fae63c5a8ad
supersedes: none
depends_on: []
---

# 핵심 전투 생명주기

## Scope와 non-goals

Actor가 전투에 들어온 뒤 direct attack, lethal damage 또는 endpoint breach를 거쳐
match terminal에 이르는 최소 gameplay 인과를 다룬다. 활성 spec에서 문서화된
demo 의도와 명시적 공백만 기록한다. 이식 대상의 실행 순서, 수치 표현,
ruleset 값, 통신 계약, score 산식과 mode 설계는 선택하지 않는다.

이 카드는 담당자 검토 전이며, 첫 행동 시점·동시 사건 우선순위·mode별 terminal·
terminal 이후 finality라는 중대 공백이 남아 있으므로 `migration_readiness`는
`blocked`다.

## Gameplay rule statements

| rule_id | 게임플레이 의미 | rule_status | 적용 범위 | 참조 |
|---|---|---|---|---|
| `CORE-RULE-001` | Direct-attack range 경계에 정확히 놓인 target은 range 안이다. 정사각 격자에서는 접근 완료와 direct-attack 가능성이 같은 `max(abs(dx), abs(dy))` 거리 의미를 쓴다. | `intended` | 격자 기반 direct attack과 attack-range 접근 | `docs/spec/aggro-standoff/README.md` |
| `CORE-RULE-002` | Readiness, target, range와 행동 조건을 만족하면 attack이 시작된다. 다음 readiness는 attack 시작에서 기산한다. 판정 지연 중에는 새 attack을 시작하지 않고, direct hit의 target과 range는 판정 시점에 다시 확인한다. | `intended` | Direct attack; 고정 방향 trajectory는 비범위 | `docs/spec/attack-hit-delay/README.md` |
| `CORE-RULE-003` | Attacker는 문서화된 engagement 상태에서만 attack을 시작한다. Readiness 진행은 그 상태 밖에서도 계속되므로 다른 조건을 만족하면 eligible 상태 도달 직후 시작할 수 있다. | `intended` | Engagement-state 계약을 쓰는 attacker | `docs/spec/enemy-ai-fsm/3a_attack_consume.md` |
| `CORE-RULE-004` | Lethal removal과 endpoint breach는 다른 outcome이다. Breach는 breach total을 증가시키고 actor를 active set에서 제거하지만 kill outcome은 만들지 않는다. | `intended` | Main battle 흐름 | `docs/spec/battle-score-formula/README.md` |
| `CORE-RULE-005` | 이미 예정된 arrival가 남지 않고 active attacker도 없을 때만 현재 wave interval이 clear다. Future wave가 남으면 현재 interval이 비어도 final all-clear는 아니다. | `intended` | Main wave 흐름 | `docs/spec/nextwave-clear-attention/README.md`; `docs/spec/nextwave-clear-attention/0_interwave_clear_contract.md` |
| `CORE-RULE-006` | 문서화된 main mode에서는 cumulative breach limit 도달이 defeat, 예정·active attacker 전부 소진이 victory, timer 만료까지 생존도 victory다. | `intended` | Main mode | `docs/spec/battle-score-formula/README.md`; `docs/spec/nextwave-clear-attention/README.md` |
| `CORE-RULE-007` | 같은 terminal outcome의 중복 확정은 막아야 한다는 의도는 일치하지만, result 흐름 시작 뒤 gameplay state가 계속 변하는지는 활성 spec끼리 충돌한다. | `conflict` | Post-terminal lifecycle | `docs/spec/score-tally-sequence/README.md`; `docs/spec/result-screen-lobby-exit/README.md` |
| `CORE-RULE-008` | Endless variant는 timer로 끝난다는 문서와 early all-clear를 유지한다는 문서가 함께 있어 intended terminal set을 확정할 수 없다. | `conflict` | Endless variant | `docs/spec/endless-mode/README.md`; `docs/spec/endless-mode/2_bridge_mode_awareness.md` |
| `CORE-RULE-009` | Defender는 배치가 확정된 뒤 author-defined deployment delay 동안 attack ready가 아니며, delay가 `0`이면 다른 attack 조건을 만족하는 즉시 시작할 수 있다. 이 규칙은 spawned attacker의 initial readiness를 정하지 않는다. | `intended` | 배치된 defender의 첫 attack readiness | `docs/spec/attack-hit-delay/2_deploy_delay.md` |

## Authoritative state 후보와 invariants

- `active attacker`: combat 또는 path progression에 계속 참여할 수 있는 attacker.
- `pending arrival`: 이미 예정됐지만 아직 active하지 않은 attacker.
- `future wave`: 현재 battle progression에 아직 예정되지 않은 wave.
- `breach total`: 현재 match에서 누적된 endpoint 도달 횟수.
- `breach limit`: main mode에서 breach total을 defeat로 바꾸는 threshold.
- `attack readiness`: 모든 행동 조건을 만족했을 때 새 attack을 시작할 수 있는 상태.
- `terminal outcome`: 한 번 확정된 victory 또는 defeat.

문서화된 invariant는 다음과 같다.

- Breach는 kill이 아니며 kill outcome을 만들지 않는다.
- Pending arrival 또는 active attacker가 남으면 current wave interval도 final
  all-clear도 아니다.
- 둘은 없지만 future wave가 남으면 inter-wave clear일 수 있으나 final
  all-clear는 아니다. Future wave, pending arrival와 active attacker가 모두
  없을 때만 main-mode final all-clear를 판정한다.
- Endpoint 도달 한 번은 breach total을 한 번 증가시킨다. 그 증가가 적용 가능한 main-mode
  limit에 도달할 때만 즉시 defeat다.
- 검토한 근거에는 완전한 post-terminal state-freeze invariant가 없다.

## Logical inputs, validation과 atomic effects

| input_id | 의도 또는 match event | 전제조건 | 허용 outcome | 거절 outcome | rule_status |
|---|---|---|---|---|---|
| `CORE-INPUT-001` | Direct attack 시작 | Eligible engagement 상태, ready attack, eligible target, inclusive range, 미해결 이전 attack 없음 | Attack 시작과 다음 readiness 기산 | Attack 시작 없음 | `intended` |
| `CORE-INPUT-002` | Direct attack 판정 | 이전 attack이 시작됐고 판정 지연이 끝났으며 eligible target이 range 안에 남음 | Direct-hit outcome 판정 | Direct-hit outcome 없음 | `intended` |
| `CORE-INPUT-003` | Endpoint 도달 | Actor가 active path participant로 endpoint에 도달 | Breach 한 번 기록 후 active set에서 제거 | 해당 actor의 kill outcome은 기록하지 않음 | `intended` |
| `CORE-INPUT-004` | Terminal outcome 확정 | 문서화된 terminal 조건 성립, 이전 outcome 없음 | Victory 또는 defeat 한 번 확정 | 중복 확정 | `intended` |

## Ordering, timing, numeric과 randomness 의미

- Attack readiness는 hit 판정 시점이 아니라 attack 시작에서 기산한다.
- 판정이 지연된 attack은 이전 판정이 끝나기 전에 새 attack을 시작할 수 없다.
- Direct-hit target과 range는 판정 시점에 다시 확인한다.
- 새로 spawn된 actor의 첫 행동 가능 시점은 문서화되지 않았다.
- Defender의 첫 attack readiness는 배치 후 author-defined delay로 정해진다.
  Spawned attacker의 initial readiness는 문서화되지 않았다.
- 한 logical step의 lethal damage와 endpoint 도달 우선순위는 문서화되지 않았다.
- 동시에 성립한 defeat, all-clear, timer 조건의 우선순위는 문서화되지 않았다.

## Boundary 및 acceptance cases

| case_id | Given | When | Then | rule_status | 공백 | 참조 |
|---|---|---|---|---|---|---|
| `CORE-CASE-001` | Actor가 아직 active하지 않다. | Actor가 logical step 도중 spawn된다. | 첫 행동 가능 시점은 `unknown`이다. | `unknown` | First eligibility 계약이 없다. | `ENG-003`; `TRN-004` |
| `CORE-CASE-002` | 다른 attack 조건을 만족하고 target이 direct-attack range 경계에 정확히 있다. | Attack 가능성을 판정한다. | Target은 range 안이며 attack할 수 있다. | `intended` | Migration 대상의 numeric conversion은 이 card의 비범위다. | `docs/spec/aggro-standoff/README.md` |
| `CORE-CASE-003` | 같은 actor에 lethal damage와 endpoint 도달이 한 logical step에 모두 성립할 수 있다. | 두 outcome을 판정한다. | Outcome은 `unknown`이다. | `unknown` | Intended arbitration rule이 없다. | `ENG-003`; `TRN-004`; `TRN-013` |
| `CORE-CASE-004` | 마지막 active attacker가 제거됐지만 pending arrival 또는 future wave가 남아 있다. | All-clear를 판정한다. | Match는 final all-clear가 아니다. | `intended` | 다른 mode의 clear 조건은 별도다. | `docs/spec/nextwave-clear-attention/README.md` |
| `CORE-CASE-005` | Attacker가 attack을 막 시작했다. | 다음 readiness를 계산한다. | Readiness는 즉시 기산하지만 이전 판정이 끝나기 전에는 새 attack을 시작하지 않는다. | `intended` | Spawned attacker의 첫 attack 전 readiness는 별도 `unknown`이다. | `docs/spec/attack-hit-delay/README.md` |
| `CORE-CASE-006` | Actor 하나가 applicable main-mode breach limit 미만에서 endpoint에 도달한다. | Breach를 적용한다. | Breach total이 한 번 증가하고 다른 terminal 조건이 없다면 match는 계속된다. | `intended` | 동시 terminal arbitration은 `unknown`이다. | `docs/spec/battle-score-formula/README.md` |
| `CORE-CASE-007` | Main mode에서 future wave, pending arrival, active attacker가 모두 없다. | All-clear를 판정한다. | Victory다. | `intended` | 다른 mode 범위는 `unknown`이다. | `docs/spec/nextwave-clear-attention/README.md` |
| `CORE-CASE-008` | Main mode에서 defeat가 확정되지 않았다. | Timer가 만료된다. | Survival victory다. | `intended` | 동시에 성립한 all-clear 또는 defeat와의 우선순위는 `unknown`이다. | `docs/spec/battle-score-formula/README.md` |
| `CORE-CASE-009` | Terminal outcome이 이미 확정됐다. | 같은 outcome 확정 또는 이후 gameplay 변화가 시도된다. | 중복 outcome은 거절되지만 이후 gameplay state 변화 가능 여부는 `unknown`이다. | `conflict` | Post-terminal lifecycle 문서가 충돌한다. | `docs/spec/score-tally-sequence/README.md`; `docs/spec/result-screen-lobby-exit/README.md` |
| `CORE-CASE-010` | Defender의 배치가 확정됐고 author-defined deployment delay가 정해져 있다. | 첫 attack readiness를 판정한다. | Delay가 남아 있으면 ready가 아니며, `0`이면 다른 attack 조건을 만족하는 즉시 ready일 수 있다. | `intended` | Spawned attacker의 initial readiness에는 적용하지 않는다. | `docs/spec/attack-hit-delay/2_deploy_delay.md` |

## Mode와 content variants

| variant_id | 조건 | 의미 차이 | rule_status | 참조 |
|---|---|---|---|---|
| `CORE-VARIANT-001` | Main mode | Cumulative breach defeat, all-clear victory, timer survival victory가 문서화돼 있다. | `intended` | `docs/spec/battle-score-formula/README.md`; `docs/spec/nextwave-clear-attention/README.md` |
| `CORE-VARIANT-002` | Endless variant | Breach는 defeat를 만들지 않는다. Timer-only와 early all-clear 유지 문서가 충돌한다. | `conflict` | `docs/spec/endless-mode/README.md`; `docs/spec/endless-mode/2_bridge_mode_awareness.md` |
| `CORE-VARIANT-003` | 그 밖의 mode 또는 direct-entry 경로 | 이 card는 terminal 차이를 확정하지 않는다. | `unknown` | `BASE-001`; `BASE-006` |

## Dependencies

- 다른 dossier 카드 의존성: `none`
- Readiness 차단 decision: `MIG-DEC-CORE-001`부터 `MIG-DEC-CORE-012`까지
- Coverage 연결: `MIG-AREA-001`, `MIG-AREA-002`, `MIG-AREA-004`,
  `MIG-AREA-005`, `MIG-AREA-006`, `MIG-AREA-007`, `MIG-AREA-012`,
  `MIG-AREA-013`

## Open decisions

| question_id | decision_id | 질문 | 현재 근거 | 준비도 영향 |
|---|---|---|---|---|
| `CORE-Q-001` | `MIG-DEC-CORE-009` | 새로 spawn된 actor는 언제 처음 행동할 수 있는가? | 검토한 출처는 이후 행동 조건만 정의하고 first eligibility를 정의하지 않는다. | Spawn과 첫 행동 의미의 complete coverage를 막는다. |
| `CORE-Q-002` | `MIG-DEC-CORE-010` | 한 logical step에 lethal damage와 endpoint 도달이 모두 성립하면 무엇이 우선하는가? | 두 outcome이 다르다는 근거는 있지만 arbitration 근거는 없다. | Kill, breach, score의 결정 가능한 의미를 막는다. |
| `CORE-Q-003` | `MIG-DEC-CORE-011` | Spawned attacker가 행동 eligibility를 얻었을 때 첫 attack readiness는 어떤 상태인가? | Defender deployment delay와 이후 cadence는 문서화됐지만 spawned attacker의 initial readiness는 문서화되지 않았다. | Portable first-attack 계약을 막는다. |
| `CORE-Q-004` | `MIG-DEC-CORE-008` | Endless variant의 intended terminal set은 무엇인가? | Main-mode 출처는 일치하지만 endless 출처는 충돌한다. | Endless mode coverage 완료를 막는다. |
| `CORE-Q-005` | `MIG-DEC-CORE-012` | 여러 terminal 조건이 동시에 성립하면 어떤 단일 outcome을 선택하는가? | 검토한 출처에는 total priority가 없다. | 단일 final outcome 계약을 막는다. |
| `CORE-Q-006` | `MIG-DEC-CORE-007` | Terminal outcome 확정 뒤 어떤 gameplay state가 더 변할 수 있는가? | 활성 spec이 progression 정지 여부에서 충돌한다. | Final state와 late-event 의미를 막는다. |

## References

- `docs/spec/aggro-standoff/README.md`
- `docs/spec/attack-hit-delay/README.md`
- `docs/spec/attack-hit-delay/2_deploy_delay.md`
- `docs/spec/enemy-ai-fsm/3a_attack_consume.md`
- `docs/spec/battle-score-formula/README.md`
- `docs/spec/nextwave-clear-attention/README.md`
- `docs/spec/nextwave-clear-attention/0_interwave_clear_contract.md`
- `docs/spec/score-tally-sequence/README.md`
- `docs/spec/result-screen-lobby-exit/README.md`
- `docs/spec/endless-mode/README.md`
- `docs/spec/endless-mode/2_bridge_mode_awareness.md`
- `BASE-001`
- `BASE-006`
- `ENG-003`
- `ENG-006`
- `TRN-001`
- `TRN-004`
- `TRN-010`
- `TRN-013`

## Readiness checklist

- [x] 범위와 비목표가 명확하다.
- [x] `intended`, `unknown`, `conflict`를 구분했다.
- [x] 현재 core boundary를 추정 없이 기록했다.
- [x] Mode conflict와 미포함 mode를 명시했다.
- [x] References가 `as_of_commit`에서 유효하다.
- [x] 클라이언트 전용 구현·연출 정보를 제외했다.
- [ ] `MIG-DEC-CORE-001`부터 `MIG-DEC-CORE-006`까지 각각 owner disposition을 받았다.
- [ ] `MIG-DEC-CORE-007` post-terminal finality를 결정하거나 명시적으로 보류했다.
- [ ] `MIG-DEC-CORE-008` Endless terminal set을 결정하거나 명시적으로 보류했다.
- [ ] `MIG-DEC-CORE-009` spawn first-action eligibility를 결정하거나 명시적으로 보류했다.
- [ ] `MIG-DEC-CORE-010` lethal-versus-endpoint arbitration을 결정하거나 명시적으로 보류했다.
- [ ] `MIG-DEC-CORE-011` spawned-attacker initial readiness를 결정하거나 명시적으로 보류했다.
- [ ] `MIG-DEC-CORE-012` simultaneous terminal arbitration을 결정하거나 명시적으로 보류했다.
- [x] Inter-wave clear와 final all-clear를 future wave 유무로 구분했다.
- [x] `CORE-Q-001`부터 `CORE-Q-006`까지 중앙 decision에 연결했다.
- [ ] Deferred 또는 unresolved decision이 readiness blocker로 남아 있다.
- [ ] 담당자 검토가 `review-ledger.md`에 기록됐다.
