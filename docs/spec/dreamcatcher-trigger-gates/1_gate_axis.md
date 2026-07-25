# 1 — 게이트 축: 정의 계층 + 순수함수 + bake 거절 + v1 2경로 배선 (rev 2)

## 목적

사건 트리거에 동적 술어 게이트를 직교 필드 축으로 추가한다. **rev 2**: 필드 축은 전 트리거 균일(데이터), **평가 코드 배선은 v1 카드가 쓰는 2경로만** — ① OnDamagedN×Self ② AttackN×EventTarget. 그 외 gate≠None 조합은 전부 bake loud 거절 (critic HIGH: 미사용 라이브 경로 금지).

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcGateKind { None, HpBelow }`, `DcGateSubject { Self, EventTarget }`, `DcTriggerSpec.gate/gateSubject/gateValue` append (ECS-free 유지)
- `Assets/_Project/Scripts/Battle/Combat/DcTrigger.cs` — 기존 family 에 추가 (신규 클래스 아님):
  - `Pass(gate, value, subjectHp, subjectMaxHp) → bool` — 게이트 판정 순수 함수 (경계: 정확히 30% = 이하 통과)
  - `GateComboSupported(trigger, gate, subject) → bool` — **배선 표의 단일 source of truth** (v1: 위 2조합만 true)
- `Assets/_Project/Scripts/Battle/Combat/DcTriggerSlot.cs` — gate/gateSubject/gateValue append (AttackN 용, Combat 소유 불변)
- `Assets/_Project/Scripts/Battle/Units/DamagedCounter.cs` — gate/gateValue append (OnDamagedN×Self 용, subject 는 Self 고정이라 필드 생략 가능 — 구현 재량)
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — bake: `GateComboSupported` false 면 경고+skip, true 면 슬롯/카운터에 게이트 번역
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — ① HeavyStrike pre-scan ② counter 루프 두 곳에 게이트 평가 (아래 불변식). bestTarget Health RO lookup 없으면 추가
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — DamagedCounter 발동에 Self 게이트 평가 (자기 Health 는 그 자리 값)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs` — TryFormatTrigger 에 게이트 접두 (배선 조합만): Self `"HP {v}% 이하일 때 "` / EventTarget `"HP {v}% 이하인 적에게 "`
- EditMode 테스트

## 구현 계약

- **HeavyStrike 합성 불변식 (critic HIGH)**: pre-scan 판정 = `WouldFire(counter, period) ∧ Pass(gate, …, bestTargetHp)`, counter 루프 = `if (Pass(…same input…)) { fired = Tick(…) }`. 두 지점은 같은 프레임·같은 bestTarget·같은 HP 스냅샷을 사용해 결과가 반드시 일치. **등가성 EditMode 테스트 필수** — 게이트 통과/실패 × period 1/3 매트릭스로 pre-scan 예측 == 루프 발동 확인 (unit 3 카드는 period 1 이라 이 seam 을 못 건드림).
- **판정 시점 = pre-damage**: AttackN×EventTarget 은 이번 공격 데미지 적용 전 HP (critic MED). 판정은 현재값/현재 max (스폰 스냅샷 아님).
- **카운트 게이트 조립**: `if(Pass){ if(Tick()) fire; }` — 게이트 실패 사건은 counter 무변화. Pass·Tick 은 각각 순수라 EditMode 가 따로 커버, 조립 seam 만 e2e (unit 2).
- **subject 소멸 처리**: bestTarget 이 Null/파괴/DeadTag 면 게이트 실패 취급(발동·카운트 없음) — caller 책임.

## 완료 기준

- [ ] compile + EditMode 전체 green
- [ ] EditMode 신규: ① `Pass` 경계값(29.9/30.0/30.1%) ② `GateComboSupported` 표 — 배선 2조합 true + 퇴화 조합(OnDeath×HpBelow, HealthThreshold×gate, None×gate, EventTarget×OnKill, 미배선 조합) false 어서션 (critic MED: 수동 확인 아닌 어서션 고정) ③ HeavyStrike pre-scan 등가성
- [ ] CardText 게이트 접두 골든 1건 (배선 조합)
- [ ] 기존 카드(gate=None) 무회귀 — PlayMode 드림캐쳐 스위트 green
