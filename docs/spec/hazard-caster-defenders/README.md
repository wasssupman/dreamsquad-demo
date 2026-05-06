# Hazard Caster Defenders Spec

**작성일**: 2026-05-05  
**상태**: 완료 2026-05-06  
**연결 문서**: `docs/spec/path-zone-hazards/`, `docs/spec/destructible-blocking-hazards/`, `docs/spec/modifier-framework-and-healer/`

## 목표

신규 방어 유닛 4종을 추가한다. 각 유닛은 일반 공격 데미지 대신, 정해진 쿨타임마다 범위 안의 공격 유닛 위치에 hazard 를 생성한다.

- Fire caster defender: 공격 유닛 위치에 화염 zone hazard `1 x 1` 생성
- Ice caster defender: 공격 유닛 위치에 얼음 zone hazard `1 x 1` 생성
- Poison caster defender: 공격 유닛 위치에 독 zone hazard `1 x 1` 생성
- Blocking caster defender: 공격 유닛 위치 또는 근처 유효 cell 에 차단형 방해 hazard `1 x 1` 생성

이 spec 의 핵심 검증은 “Healer 처럼 범위 내 target 을 고르고 action 을 수행하되, 대상은 아군이 아니라 `Faction.Enemy` 공격 유닛이고, 결과는 heal 이 아니라 target cell hazard spawn request 인가?”이다.

## 구현 문서 목록

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_authoring_contract.md` | `DefenderUnitData` 의 hazard caster authoring 계약과 4종 방어 유닛 데이터 |
| 1 | `1_runtime_request_pipeline.md` | ECS runtime state, spawn request queue, BattleBridge drain 계약 |
| 2 | `2_hazard_cast_system.md` | enemy target selection, cooldown, request enqueue system |
| 3 | `3_unit_assets_and_spine.md` | 신규 defender SO, hazard asset 연결, Spine skin/animation 매핑 |
| 4 | `4_validation.md` | EditMode/PlayMode 검증과 콘솔 clean 기준 |
| 5 | `5_handoff_summary.md` | 구현/검증/후속 인계 요약 |

## 공통 계약

- 신규 유닛은 `DefenderUnitData` 기반 defender 이며 `DefenderUnitTag`, `Faction.Defender`, `DefenderTile`, `AttackState` 를 기존 방어 유닛과 동일하게 가진다.
- Hazard caster 는 일반 `AttackOutput[]` 공격과 분리한다. 본 spec 의 4종은 `outputs[]` 없이 hazard spawn action 만 수행한다.
- Target 기본값은 `Faction.Enemy` 전용이다. `Faction.BlockingHazard` 는 기본 target 에 포함하지 않는다.
- Target 후보는 `Faction.Enemy + LocalTransform + PathFollowState` 를 가진 공격 유닛이다. 기존 `ZoneApplySystem` 이 `PathFollowState` 유닛에게 효과를 적용하므로, zone hazard 는 기존 소비 시스템을 그대로 재사용한다.
- Authoring 은 `HazardCasterAction` 개념을 따른다: `enabled`, `range`, `cooldown`, `hazardKind`, `hazard asset`, `width`, `height`.
- MVP footprint 는 전부 `1 x 1`이다. 기존 `*_3x3` hazard asset 을 직접 쓰지 않고, 같은 effect/visual 을 복제한 `*_1x1` variant 를 만든다.
- `width x height` 의 기준점은 `centerCell`이다. 짝수 크기 정책은 후속에서 정한다.
- ECS runtime component 는 unmanaged 값과 registry index 만 가진다. `HazardSO`, `BlockingHazardSO`, prefab, material, Spine 참조를 ECS component 에 넣지 않는다.
- Zone hazard 와 blocking hazard 는 같은 request envelope 를 공유할 수 있지만 drain 은 반드시 분기한다.
- 실제 visual/prefab 생성은 `BattleBridge` 만 담당한다.
- Zone hazard 는 `SpawnHazardWithVisual` 의 기존 walk-cell 보정을 허용한다. 공격 유닛은 path 위에 있으므로 보정은 보통 no-op 이다.
- Bridge drain 으로 생성된 hazard 는 다음 Simulation tick 부터 효과 적용되는 것을 허용한다.

## 비목표

- Defender 에게 적용되는 hostile field 신설
- 3x3 또는 직사각형 hazard 실제 적용
- 새 hazard effect 종류 추가
- hazard stacking 정책 변경
- projectile/VFX 분리
- wave weight 시스템 도입
- blocking hazard 의 HP/타겟 정책 변경

## 후속 후보

- `width x height` footprint sampler (`SampleRect(center, width, height)`)
- caster target priority 정책: nearest / first path progress / random
- caster 가 BlockingHazard 를 target 할 수 있는 별도 defender 변종
- cast warning VFX 또는 tile preview
- hazard spawn request 를 ECS 내부 drain 으로 옮겨 same-frame 효과 적용
