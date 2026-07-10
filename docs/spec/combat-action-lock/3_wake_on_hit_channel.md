# 3 — wake-on-hit (Units→Effects 이벤트)

## 목적
피격 시 Sleep 해제(남은시간 무시). Units 는 CcEffect 를 직접 못 지우므로 이벤트로 Effects 에 요청.

## 변경 대상
- 신규 `Assets/_Project/Scripts/Battle/Effects/CcClearEvents.cs` — `CcClearRequest{entity,kind}` + `CcClearRequestsSingleton{ NativeQueue queue }`
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — Sleep 보유 피격 시 enqueue
- 신규 `Assets/_Project/Scripts/Battle/Effects/CcClearSystem.cs` — 큐 소비 → 해당 kind CcEffect 제거
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 싱글턴 큐 생성/Dispose(기존 큐 lifecycle 옆)

## 구현
- `DamageApplicationSystem`(Units): IncomingDamage 를 실제 적용(데미지>0)한 엔티티가 `CcEffect` 에 Sleep 보유(RO lookup)면
  `CcClearRequest{entity, CcKind.Sleep}` enqueue. **Stun 은 enqueue 안 함**(wake 대상 아님).
- `CcClearSystem`(Effects, BattleSimGroup): 큐 dequeue → 해당 entity 의 `CcEffect` 버퍼에서 그 kind 제거(RemoveAtSwapBack).
  - **순서(MED2)**: `[UpdateAfter(typeof(DamageApplicationSystem))]` 로 고정 → **당 프레임 wake**(다음 프레임 지연 금지). `UnitLifecycleSystem`(DeadTag 파괴)보다는 앞. CcApplySystem/CcDecaySystem 과의 상대 순서도 명시(apply→clear 모호성 제거).
  - **가드(MED3)**: `CcApplySystem.cs:27` 선례처럼 `if (!EntityManager.Exists(evt.target)) continue;` + `HasBuffer<CcEffect>` 확인. lethal 히트로 곧 파괴될 엔티티 enqueue 도 이 가드로 안전.
  - **Burst(LOW4)**: dequeue+임의 엔티티 버퍼 변경 → `CcApplySystem.OnUpdate`(주석 :19)처럼 **non-Burst** 로 둔다(Burst 에러 추적 낭비 방지).
- **피격 predicate(MED3)**: "피격" 판정은 `DamageApplicationSystem` 이 이미 계산하는 `totalDamage > 0f` 게이트를 **재사용**(Units 단일 소스). lethal 포함(생존 여부 무관 — CcClearSystem Exists 가드가 처리).
- 채널 수명 = 기존 NativeQueue 싱글턴 패턴(BattleBridge Ensure/Dispose). **채널 15→16**, CLAUDE.md 목록에 `CcClearRequestsSingleton` 추가.

## 계약
- Units→Effects 단방향 이벤트. Units 는 CcEffect **읽기만**(Sleep 유무 판정), 제거는 Effects(CcClearSystem)만.
- "피격" = 실제 데미지 적용(0 데미지·힐 제외).

## 완료 기준
- [ ] 컴파일 클린. 채널 16개로 CLAUDE.md 갱신.
- [ ] Sleep 유닛 피격 → 즉시 Sleep 제거(행동 재개). Stun 유닛 피격 → 유지.
