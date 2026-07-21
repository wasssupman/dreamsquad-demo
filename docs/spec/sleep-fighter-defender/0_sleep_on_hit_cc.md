# 0 — sleep-on-hit CC 경로 (SO → 베이크 → RESOLVE enqueue)

## 목적

방어유닛 공격이 히트한 주 타겟에게 Sleep(N초)을 부여하는 데이터 주도 경로를 연다.
넉백(`DefenderCcData`)과 동일한 3정거장: SO 필드 → 배치 베이크 → AttackSystem RESOLVE enqueue.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `sleepOnHitSec` 필드 (float, 기본 0 = 비활성). knockback 필드군 옆.
- `Assets/_Project/Scripts/Battle/Combat/DefenderCcData.cs` — 동명 필드 추가.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CreateDefenderEntity` 의 `DefenderCcData` 베이크(4332 인근)에 1줄.
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — RESOLVE 의 `[Defender only] Knockback CC` 블록(970 인근)과 같은 게이트 안에 분기 추가.

## 구현

- AttackSystem: 기존 `ccWriter.HasValue && defenderCcLookup.HasComponent(attackerEntity)` 게이트 내부, 넉백 if 와 형제 분기로:

  ```csharp
  if (ccData.sleepOnHitSec > 0f)
      ccWriter.Value.Enqueue(new EnemyCcEvent
      {
          target = bestTarget,
          effect = new CcEffect { kind = CcKind.Sleep, remainingTime = ccData.sleepOnHitSec },
      });
  ```

- 대상은 넉백과 동일하게 **bestTarget 1체** (README 계약 3). 방향 계산 불요 — 넉백의 flow-field 분기와 무관.
- 병합/해제/게이트는 전부 기존 시스템(CcApply/CcClear/CcDecay·CcActionLock)이 처리. 신규 로직 없음.
- 순수 함수 추출 없음 — 자명한 단일 분기(제약 10 인라인 기준 충족).

## 완료 기준

- [ ] compile 클린 (Unity 콘솔 0 에러).
- [ ] 기존 EditMode 전체 그린 (회귀 0 — 특히 CcEffectMerge/CcActionLock 계열).
- [ ] `sleepOnHitSec=0` 인 기존 전 유닛 동작 불변 (분기 자체가 비활성).
- 동작 Play 검증은 unit 1 에서 에셋과 함께 수행.
