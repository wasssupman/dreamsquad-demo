# dreamcatcher-trigger-gates — 트리거 조합: 사건 × 동적 술어 게이트

상태: 구현 완료 2026-07-25 (units 0~3 커밋 7a41668e · 850a957c · c5bc1f47 · dbda0cc7 + e2e 7c57bb31) — 사용자 체감(연출) 확인 대기

검증: EditMode 1286+게이트 5건 green · PlayMode 회귀 7/7 · **게이트 e2e 2/2 배치 자율 실행**(궁지폭발: 만피 피격 카운트 0 → HP 20% 2피격 발동·인접 정확히 −20 / 처형타: 24% 대상 피해 비율 ≈×2). 전 과정 콘솔 경고 0.

## 상위 목표

드림캐쳐 트리거를 **사건(edge) × 동적 술어 게이트(level)** 의 직교 조합으로 확장한다. "HP 30% 이하일 때 피격마다" 같은 조합을 kind 폭발 없이 **게이트 필드 축**으로 표현한다. 조합은 데이터, 어휘만 코드.

v1 범위 (2026-07-25 사용자 결정 + critic 리뷰 반영): 게이트 어휘 = **HpBelow** · 주어 = **Self + EventTarget** · **평가 배선은 v1 카드가 쓰는 2경로만** · 카드 2장(궁지폭발·처형타).

## 작업 단위

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| `0_ondamagedn_generalize.md` | 선행 부채 | OnDamagedN payload 개통 — **DamagedCounter 위드닝** (Units 소유 유지, DcTriggerSlot 통합 아님 — critic CRITICAL 반영) |
| `1_gate_axis.md` | 코어 | 게이트 축 — 정의 계층 append + 순수함수(판정·조합유효성) + bake 거절 + 2경로 평가 배선 |
| `2_cornered_card.md` | 카드 | 궁지폭발 — Self×HpBelow(30%) × OnDamagedN × SelfTileAoe |
| `3_execution_card.md` | 카드 | 처형타 — EventTarget×HpBelow(25%) × AttackN × HeavyStrike |
| `4_handoff_summary.md` | (종료 시) | 인계 요약 |

## Feature-wide 계약

- **맥락 경계 (critic CRITICAL 반영)**: OnDamagedN 의 counter/발동은 **Units 소유 `DamagedCounter` 버퍼 안에서 완결**된다 — DcTriggerSlot(Combat, 쓰기=Combat 시스템만)로 통합하지 않는다. DamagedCounter 가 분리된 원래 이유(피격 쓰기 = Units)를 유지하고 payload 필드만 위드닝.
- **게이트 경계 원칙**: 게이트 술어는 ECS sim 이 unmanaged 로 읽을 수 있는 상태만 (Health/Shield/스택/sim 시간). Mono 상태(코스트·각성)는 불가 — 필요 시 브리지 주입 싱글턴 별도 결정.
- **정의 계층 append-only**: `DcTriggerSpec` 에 `gate`(DcGateKind{None, HpBelow})·`gateSubject`(Self, EventTarget)·`gateValue`(fraction 0~1) append. 기존 카드 gate=None 무손상. **필드 축은 전 트리거 균일(데이터), 평가 코드는 배선 조합만(아래).**
- **v1 배선 조합 = 2개**: ① `OnDamagedN × Self`(궁지폭발) ② `AttackN × EventTarget`(처형타). **그 외 gate≠None 은 전부 bake loud 거절** — 미사용 라이브 경로를 만들지 않는다(SelfWarmupBuff 유령 enum 전례). 새 조합은 카드가 생길 때 배선+테스트와 함께 개방.
- **조합 유효성은 순수 함수 1곳**: `(trigger, gate, subject) → bool` 순수 static 이 배선 표의 단일 source of truth — bake 가드가 소비하고 EditMode 가 퇴화 조합(OnDeath×HpBelow, HealthThreshold×gate, None×gate, EventTarget×OnKill 등) 거절을 어서션으로 고정.
- **게이트 판정 순수 함수**: `DcTrigger` 기존 family 에 `Pass(gate, value, subjectHp, subjectMaxHp)` 추가 (신규 클래스 아님 — critic LOW). subject 소멸/DeadTag 처리는 caller 책임(게이트 실패 취급). **판정은 현재값/현재 max 기준**(HealthThreshold 의 스폰 스냅샷과 다름 — 명시 계약).
- **판정 시점 = pre-damage**: AttackN×EventTarget 게이트는 그 공격의 데미지가 적용되기 **전** HP 기준(bestTarget 스냅샷 — ApplyCcToTarget 계약 6 동일 원칙).
- **HeavyStrike 합성 불변식 (critic HIGH)**: AttackN 게이트는 pre-scan(`WouldFire ∧ Pass`)과 counter 루프(`if(Pass) Tick`)가 **같은 프레임·같은 subject·같은 입력**으로 평가되어 결과가 반드시 일치한다. 등가성 EditMode 테스트 1건 필수 (period>1 게이트 강공의 잠복 버그 지점).
- **카운트 게이트**: counter 는 게이트 통과 사건만 센다 (`if(Pass){ if(Tick()) fire; }` 조립 — Pass·Tick 각각 순수 유지, EditMode 는 Pass 경계와 Tick 계수를 따로 커버).
- **회복 시 counter 유지**: 게이트 상태 이탈/복귀에 counter 리셋 없음 — 래치 상태 불요.
- **슬롯당 게이트 1개**: 복수 ∧ 는 후일 append.
- **실행 재사용**: OnDamagedN×SelfTileAoe 는 ShieldBreakEvents 큐 재사용 (DamageApplicationSystem 이 이미 enqueue 하는 큐 — boundary 추가 0). struct 주석에 채널 공유 명시 + 드레인 로그에 origin(실드파열/피격트리거) 구분 (critic MED).
- **문안**: 게이트 접두는 **배선 조합만** 지원 — Self "HP {v}% 이하일 때 ", EventTarget "HP {v}% 이하인 적에게 ". 조립 위치는 트리거 문안 함수(TryFormatTrigger)로 고정, 골든 1건. description = formatter 정확 미러.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설·생성→렌더 경로 변경 없음.

## 후속 후보

- **미배선 게이트 조합 개방** — Self×AttackN(저체력 공세)·Self×OnKill(위기 처치)·Self×OnShieldBreak·Self×PeriodicTimer(디펜더 개방 시)·EventTarget×OnShieldBreak. 각각 쓰는 카드 + 배선 + 테스트를 한 묶음으로. EventTarget×OnDamagedN 은 한 프레임 다중 source 의 subject 선정 규칙(KillAttribution 전례) 결정도 필요
- SelfHpAbove(만전형) — 부등호 하나, 카드 기획 생기면
- 시간·실드 보유·스택 게이트 / 대상 CC·스택 게이트 / 복수 게이트 ∧ / Mono 상태 게이트(브리지 주입 결정 필요)
- 부착 축 조합(가디언∧1코) — 발동 게이트와 레이어가 다른 정적 술어 ∧ (2026-07-25 논외 처리)
