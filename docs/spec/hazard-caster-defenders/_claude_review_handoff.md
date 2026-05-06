# Claude Review Handoff — Hazard Caster Defenders

**작성일**: 2026-05-05  
**상태**: 요구 오해 수정 후 재정리된 spec 초안  
**리뷰 목적**: 신규 방어 유닛 4종이 공격 유닛 위치에 hazard 를 cast 하는 계획이 기존 Hybrid ECS 경계와 hazard pipeline 에 맞는지 검토.

## 핵심 정정

이 feature 는 신규 공격/적 유닛이 아니라 **신규 방어 유닛**이다.

- Caster: `DefenderUnitData` 기반 defender
- Target: `Faction.Enemy` 공격 유닛 (`PathFollowState` 보유)
- Zone hazard 소비: 기존 `ZoneApplySystem` 재사용
- Spawn 위치: 공격 유닛의 current cell snapshot

이 정정으로 이전 리뷰의 “defender 에게 zone hazard 가 적용되지 않는다” 문제는 해소된다. 기존 zone hazard 는 공격 유닛에게 적용되는 시스템이므로, 방어 유닛이 공격 유닛 위치에 hazard 를 깔면 기존 구조와 맞는다.

## 읽을 순서

1. `CLAUDE.md`
2. `docs/spec/hazard-caster-defenders/README.md`
3. `docs/spec/hazard-caster-defenders/0_authoring_contract.md`
4. `docs/spec/hazard-caster-defenders/1_runtime_request_pipeline.md`
5. `docs/spec/hazard-caster-defenders/2_hazard_cast_system.md`
6. `docs/spec/hazard-caster-defenders/3_unit_assets_and_spine.md`
7. `docs/spec/hazard-caster-defenders/4_validation.md`
8. 관련 기존 spec:
   - `docs/spec/path-zone-hazards/README.md`
   - `docs/spec/destructible-blocking-hazards/README.md`
   - `docs/spec/modifier-framework-and-healer/README.md`

## 현재 결정된 설계

- Feature slug 는 `hazard-caster-defenders` 로 정정했다.
- 일반 공격 `AttackOutput[]` 과 hazard cast action 은 분리한다.
- 신규 4종은 `DefenderUnitData` 기반이며 `outputs[]` 없이 hazard caster 로 동작할 수 있어야 한다.
- ECS component 에 `HazardSO`, `BlockingHazardSO`, prefab, Spine, GameObject 참조를 넣지 않는다.
- ECS 는 `HazardSpawnRequest` 만 enqueue 하고, `BattleBridge` 가 기존 visual 포함 spawn API 를 호출한다.
- Zone hazard 와 blocking hazard 는 같은 request envelope 를 써도 drain 은 반드시 분기한다.
- MVP footprint 는 전부 `1 x 1`.
- 기존 `Hazard_Fire_3x3`, `Hazard_Ice_3x3`, `Hazard_Poison_3x3`, `Hazard_Rock_3x3` 을 직접 쓰지 않고, effect/visual 값을 복제한 `*_1x1` variant 를 만든다.
- `width x height` 기준점은 `centerCell`. 짝수 크기 정책은 후속.
- Bridge drain 으로 생성된 hazard 는 다음 Simulation tick 부터 효과 적용되는 것을 허용한다.

## 선행 안전 패치

이미 적용된 안전 패치:

- `CcApplySystem` / `EffectSpawner.ApplyCc`: dead target skip
- `ModifierApplySystem`: dead target stat/stack apply skip
- 관련 EditMode tests 추가

검증 결과:

- `Wassup.Tests.EditMode.CcApplySystemTests`
- `Wassup.Tests.EditMode.ModifierFrameworkTests`
- 총 12개, passed 10, failed 0, skipped 2 (기존 `[Ignore]`)

## 현재 콘솔 주의점

Unity console 에 `GridMath.ChebyshevDistance(int2, int2)` Burst 오류가 남아 있다. `HazardCastSystem` 은 해당 helper 를 호출하지 않고 inline Chebyshev 계산을 사용해야 한다.

## 리뷰 포인트

1. `HazardCasterAction` 필드를 `DefenderUnitData` 에 직접 두는 것이 현 spec 범위에서 적절한가?
2. `HazardCastSystem` 을 Effects context 에 두는 것이 맞는가?
3. `BattleBridge.CreateDefenderEntity` 에서 `HazardCastState` 를 부착하는 계약이 기존 defender spawn 경로와 맞는가?
4. `SpawnHazardWithVisual` 의 nearest walk-cell 보정을 “공격 유닛 target 이므로 보통 no-op”으로 허용하는 계약이 충분한가?
5. Blocking hazard 실패 시 cooldown 소모 정책이 충분히 명확한가?

## Dirty Worktree 주의

작업 트리에 unrelated 변경이 많다. 이 handoff 와 직접 관련 있는 변경은 아래만 본다.

- `Assets/_Project/Scripts/Battle/Effects/CcApplySystem.cs`
- `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs`
- `Assets/_Project/Tests/EditMode/CcApplySystemTests.cs`
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierApplySystem.cs`
- `Assets/_Project/Tests/EditMode/ModifierFrameworkTests.cs`
- `docs/spec/hazard-caster-defenders/**`

## 다음 액션 후보

리뷰 통과 시 `0_authoring_contract.md` 부터 구현한다.

1. `DefenderUnitData` hazard caster authoring 필드 추가.
2. 기존 defender asset 기본값이 비활성인지 확인.
3. `*_1x1` hazard variant asset 생성.
4. 4종 caster defender asset 초안 작성.
