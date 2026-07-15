# 5. 픽업 소비 → 라스트런

## 목적

야근 룰 2 완성. 유닛(적 통과 / defender 배치)이 레드불과 같은 셀에 있으면 소비되고 **라스트런** 발동 — **5초간 공격속도 50% 증가하고, 종료되면 최대 체력의 50% 피해를 입는다** (2026-07-15 사용자 재정의: 기존 "최대체력 ×0.1 배율" → "최대체력의 50% 데미지"로 변경, 직관성). 적도 동일.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/LastRun.cs` — 지연 crash 타이머 컴포넌트
- `Assets/_Project/Scripts/Battle/Effects/PickupConsumeSystem.cs` — co-location 소비 판정 + 라스트런 부여
- `Assets/_Project/Scripts/Battle/Effects/LastRunSystem.cs` — remaining 만료 → 영구 최대체력 컷 인큐

## 구현

1. **LastRun** (Effects): `{ float remaining }`. 소비 시 부착/refresh. AS 버프는 소비 즉시 StatModifier 로 별도 인큐(자체 만료) — 이 컴포넌트는 **지연 crash** 만 담당.
2. **PickupConsumeSystem** (Effects, `UpdateAfter(PickupSpawnSystem)`, **non-Burst** — 소비 telemetry 로그용, StackModifierTickSystem 전례). self-gate: `OverworkGimmickConfig`+`FlowFieldSingleton`+`StatModifierApplyEventsSingleton`.
   - Pickup 들을 `NativeHashMap<int2,Entity>`(cell→pickup) 로 수집. 비면 early-return.
   - **Defender**: `DefenderTile.cell` (배치 셀, 권위값). **Enemy**: `GridMath.WorldToCell(LocalTransform, flow.*)`. 둘 다 `WithNone<PendingDeployment,DeadTag>`.
   - 셀이 맵에 있으면 소비: 맵에서 제거(첫 소비자 승) → pickup ecb.DestroyEntity → AS 버프 인큐(`AttackSpeedMul ×lastRunAttackSpeedMul`, dur=`lastRunDuration`, source=unit) → `LastRun{remaining=lastRunDuration}` add/refresh.
   - Defender·Enemy 통합 로직은 local function `Consume(cell, unit)`.
3. **LastRunSystem** (Effects): `LastRun.remaining -= dt`; ≤0 → **최대체력의 `lastRunDamageFraction`(0.5) 만큼을 데미지로** — 정식 데미지 인박스 `IncomingDamage`(Units 소유, TRD 2.5.2 cross-context 채널)에 `{ amount = Health.max × 0.5, source = Null(자해·킬 미귀속) }` append + `RemoveComponent<LastRun>`. DamageApplicationSystem(Units)이 소비해 Health 감산·사망 처리 (Health 쓰기는 Units 소유 — 맥락 경계 유지).
   - **주의**: DamageApplicationSystem 이 `dmgTakenMul` 을 곱한다. 야근 기믹엔 dmgTakenMul 모디파이어가 없어 실질 정확히 50%지만, 무관한 방어버프가 있으면 경감된다(정상 데미지 파이프라인 일관). "true damage" 필요 시 후속.
4. **origin**: 라스트런 StatModifier 는 `ModifierOrigin.Unspecified` (그 enum 은 unit-buff-debuff-aura 세션 소유 — 전용 `Gimmick` origin 은 조율 후 follow-up). 현 소비자(HasActiveDreamcatcherModifier)는 Dreamcatcher 출처만 봄 → 오분류 없음.

## 계약 노트

- AS 버프와 crash 타이머는 같은 `lastRunDuration` — 버프 종료 순간 최대체력 컷(에너지드링크 crash 테마).
- 재소비: source=unit·stackId=0 → 슬롯 refresh (중첩 아님). 재소비 남용 밸런스는 후속.
- Health 쓰기는 Units(MaxHealthScaleSystem)만 — 본 unit 은 modifier 이벤트만 인큐 (맥락 경계 유지).

## 완료 기준

- compile 통과 + 콘솔 클린.
- Play(기믹 연결): 적이 Walk 셀 레드불 통과 시 소비 telemetry 로그 → 5초 후 해당 유닛 최대체력 ×0.1. defender 를 레드불 셀에 배치 시 동일. (`Log Fatigue Stacks` 로 defender 공속 ×1.5→최대체력 ×0.1 확인 가능.)
- gimmick=null → 소비/라스트런 미발생.

확인 2026-07-15 · 커밋 `30d70b4e` — Editor.log 실측: consumed→(5s)→crash 페어 다수(예: Entity 105:1 소비 후 5초 뒤 crash), 최대체력 ×0.10 인큐 확인, 에러 0. MaxHealthMul 적용 경로는 unit 3 번아웃에서 직접 실증(진영 무관). 소비 유닛이 전부 적(transient)이라 최종 Health.max 값은 unit 6/7 defender 배치로 재확인.
