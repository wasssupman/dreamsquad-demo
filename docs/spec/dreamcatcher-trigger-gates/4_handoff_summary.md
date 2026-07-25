# 4 — handoff summary

## Commit

- `7a41668e` unit 0 — OnDamagedN payload 개통 (DamagedCounter 위드닝)
- `850a957c` unit 1 — 게이트 축 (DcGateKind/gateSubject/gateValue + 순수함수 + bake 거절 + 2경로 배선)
- `c5bc1f47` unit 2 — 궁지폭발 카드
- `dbda0cc7` unit 3 — 처형타 카드
- `7c57bb31` 게이트 e2e PlayMode 테스트 2건
- `378c792a` 폭발 킬 owner 귀속 통일(궁지폭발/실드폭발=host — 투트랙 리뷰 B-M1, 사용자 결정)
- spec: `c03f12b4`(rev 2 — critic 반영 이력 포함) · `cb36c074`(투트랙 리뷰 반영 docs)

## Implemented

- 트리거가 **사건 kind × 게이트 필드(gate/gateSubject/gateValue)** 로 직교 분해됨 — 조합은 데이터, 어휘만 코드
- v1 배선 = OnDamagedN×Self · AttackN×EventTarget 뿐. 배선 표의 단일 SoT = `DcTrigger.GateComboSupported` (bake 가드+EditMode 어서션이 소비)
- OnDamagedN 은 DamagedCounter(Units 소유) **위드닝**으로 payload 개통 — NextAttackDoubleFire(기존)·SelfTileAoe(ShieldBreakEvents 큐 공유, fromDamagedTrigger origin)
- 카운트 게이트: `if(GatePass){Tick}` — 통과 사건만 카운트, 회복 시 유지
- HeavyStrike 합성 불변식: pre-scan `WouldFire∧GatePass` == 루프 `if(GatePass)Tick` (동일 bestTarget·pre-damage HP)
- 카드 2장 (코드 0줄): 궁지폭발(HP 30%↓ 2피격마다 반경1 폭발 20) · 처형타(HP 25%↓ 적에게 피해 ×2)
- CardText 게이트 접두 직교 합성 + 미러

## Key Files

- `Scripts/Battle/Combat/DcTrigger.cs` — GatePass/GateComboSupported (+DcTriggerTests 게이트 4종)
- `Scripts/Battle/Units/DamagedCounter.cs` + `DamageApplicationSystem.cs` — OnDamagedN 발동/디스패치/Self 게이트
- `Scripts/Battle/Combat/AttackSystem.cs` — AttackN 게이트 2지점(pre-scan·루프)
- `Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — bake 배선 검증
- `Tests/PlayMode/DreamcatcherGateE2ETest.cs` · `DreamcatcherDamagedTriggerTest.cs`(unit 0 회귀 핀)

## Verified

- EditMode 전체 green (게이트 순수함수·조합표·pre-scan 등가성·문안 골든 포함)
- PlayMode: 킬임계/온히트/전투데미지/피격핀 7/7 + 게이트 e2e 2/2 (배치 자율)
- 콘솔 에러/경고 0. `DreamcatcherEffectTest.CardBuffs` 는 main 사전 실패(별건, memory 기록)

## Notes (되돌리면 안 되는 것)

- **DamagedCounter 를 DcTriggerSlot 로 합치지 말 것** — Units 소유가 존재 이유(교차-맥락 쓰기 금지, critic CRITICAL)
- 게이트 판정 시점: AttackN=pre-damage(대상), OnDamagedN=post-damage(자신 newHp) — 사건 성질 차이가 근거
- 미배선 조합 개방은 카드+배선+테스트 한 묶음으로 (GateComboSupported 가 gate)
- 라이브 e2e 는 에디터 포커스에 취약 — **배치 PlayMode 테스트가 정석** (드레인은 StartBattle(_running) 요구)

## Follow-up

- 사용자 Play 체감 확인 (연출/수치감 — 카드 2장 덱 투입)
- README 후속 후보: 미배선 조합 개방 · SelfHpAbove(만전형) · 시간/실드/스택 게이트 · 부착 축 조합(가디언∧1코)
- 실아트 2장 (guid 유지 교체)
