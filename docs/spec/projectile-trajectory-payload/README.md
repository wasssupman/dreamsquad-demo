# Projectile Trajectory × Payload — 투사체 궤적/페이로드 분해 리팩터

> 상태: 초안 (설계 확정, 미구현) · 2026-07-06

## 목표

투사체를 **궤적(trajectory)** 과 **페이로드(payload)** 라는 직교 두 축으로 분해한다. 기존 홈잉 투사체는 동작 보존으로 새 파이프라인에 이관하고, 두 번째 arm(BallisticArc 궤적 + TileAoe 페이로드)까지 넣어 seam 을 실증한다. **곡사포 유닛 authoring 은 이 spec 밖** — 후속 `docs/spec/artillery-defender/`.

이 리팩터가 없으면 새 이동 방식(곡사·베지어 등)마다 `XxxFireRequest / XxxSystem / XxxTag` 파이프라인을 통째로 복제하게 된다(band-aid). 궤적을 payload 와 분리하면 새 이동 방식은 **enum 케이스 + 위치 순수함수 + MoveSystem arm 1개** 로 붙는다.

## 검증 질문

> 투사체가 **궤적(홈잉 / 곡사arc)** 과 **페이로드(단일+splash / tile-AOE)** 를 독립적으로 조합할 수 있고, 홈잉 기존 동작은 **무회귀**인가? 3번째 궤적(베지어)이 **시스템/드레인/태그 추가 없이** arm 하나로 붙는 구조인가?

## 연결 문서

| 상황 | 문서 |
|---|---|
| 기술 제약 (ECS 맥락/경계) | `docs/TRD.md` §2.5 |
| 곡사포 유닛(이 리팩터의 첫 소비자) | `docs/spec/artillery-defender/` (후속, 대기) |
| 기존 tile-AOE 선례 | `Assets/_Project/Scripts/Battle/Combat/MeteorResolutionSystem.cs` |

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_axis_contract.md` | 계약 | `MovementKind`/`PayloadKind` enum + `ProjectileState`/`ProjectileSpawnRequest` 필드 재구성 (**additive**, 홈잉 무변경, 컴파일만) |
| 1 | `1_homing_migration.md` | 리팩터 | `ProjectileMoveSystem`/`ProjectileHitSystem` → `Move`(switch, Homing arm) / `Impact`(switch, SingleSplash arm) **동작 보존** 이관 |
| 2 | `2_tileaoe_primitive.md` | 신설+테스트 | `TileAoe.CollectInRange` static 순수함수 (Meteor L70-77 과 동일 의미) + EditMode |
| 3 | `3_ballistic_arc_trajectory.md` | arm+테스트 | MoveSystem `BallisticArc` arm: `ArcPosition` + `flightTime=dist/speed` static + EditMode |
| 4 | `4_tileaoe_payload.md` | arm | ImpactSystem `TileAoe` payload arm: 착탄 셀 반경 flat AOE + impact VFX |
| 5 | `5_spawn_wiring.md` | 배선 | AttackSystem RESOLVE `flightMode` 분기 + 셀 고정 + **단일** SpawnRequest 경로 + BattleBridge convert/drain |
| 6 | `6_integration_validation.md` | 검증 게이트 | 홈잉 무회귀 재확인 + ballistic+AOE synthetic 통합 + 틸트보드 Y (코드 커밋 없음) |
| 7 | `7_handoff_summary.md` | 인계 | 구현 종료 요약 |

## Feature-wide 계약 (load-bearing)

1. **직교 2축.** 투사체 = 궤적 × 페이로드. `ProjectileState` 가 `MovementKind` + `PayloadKind` 를 discriminator 로 보유한다. 두 축은 자유 조합(홈잉+AOE, 곡사+단일 도 성립).
2. **단일 라이프사이클.** `ProjectileSpawnRequest → BattleBridge drain → 엔티티+뷰 → MoveSystem → ImpactSystem → 파괴`. **궤적/페이로드별 별도 시스템·드레인·태그 신설 금지** (band-aid 금지). 캐리어 태그는 `ProjectileTag` 하나 → teardown/뷰 회수 자동 커버.
3. **MoveSystem = switch(MovementKind).** 각 arm 이 위치 갱신 + 자기 도착 클램프 소유. `Homing`(타겟 추적, 도착=거리, 타겟 소실 시 파괴) / `BallisticArc`(origin→impact XZ lerp, Y=sin(t·π)·arcHeight, 도착=t≥1).
4. **ImpactSystem = switch(PayloadKind).** 도착 판정 + 해결 소유(현 `ProjectileHitSystem` 위치 유지). `SingleSplash`(기존 outputs + splash + HitFlash) / `TileAoe`(착탄 셀 반경 flat, HitFlash 미적용 = Meteor 선례).
5. **순수 계산은 static Burst 함수 + EditMode 테스트.** `ArcPosition`, `flightTime`(speed>0 가드 + min clamp), `TileAoe.CollectInRange`(반경 경계). 테스트는 `Assets/_Project/Tests/EditMode/`.
6. **데미지 출처 = `DefenderUnitData.outputs` 의 Damage 합산.** 홈잉 경로와 동일. 새 magic damage 필드 금지. 궤적/페이로드 파라미터(arcHeight/impactTileRange/flightMode)만 `ProjectileData` 신규 필드.
7. **홈잉 무회귀가 완료 기준.** 기존 PlayMode smoke + splash 동작이 unit 1 이후 그대로여야 한다.
8. **신규 궤적 비용 = enum 케이스 + 위치 순수함수 + MoveSystem arm 1개** (시스템/드레인/태그 0). 베지어가 이 계약의 리트머스 — 후속 후보.
9. **새 ECS 맥락/NativeQueue/Manager 0.** 전부 Combat. `IncomingDamage` 는 Combat→Units 채널(선례 유지). `ProjectileHitEventsSingleton` VFX 채널 재사용.

## 비목표 / 후속 후보

- **곡사포 authored 유닛** (ProjectileData/DefenderUnitData SO · 프리팹 · 아이콘 · draft 편입 · 실매치 play) → `docs/spec/artillery-defender/` (이 리팩터의 첫 소비자).
- **Bezier 궤적** [S] · `MovementKind.BezierToPoint` + `BezierPos` 순수함수 + MoveSystem arm 1개. 이 spec 의 seam 이 옳은지의 증명 대상이지만 실제 arm 은 소비자 생길 때.
- **Meteor 를 `TileAoe` 로 수렴** [S] · `MeteorResolutionSystem` 이 신설 `TileAoe.CollectInRange` 채택(dedup 완성). cross-context 라 이 spec 밖 — Meteor(Effects/Combat) 안 건드림.
- **non-Damage payload** [M] · 착탄 시 ApplyStat/ApplyStack 도 AOE(slow-곡사포 등). 현재 TileAoe payload 는 Damage-only.
- **임팩트 CC/knockback** [S] · `DefenderCcData` 를 AOE 대상에 적용.

## 주의

- **BattleBridge.cs dirty 상태** — 현재 dreamstone WIP 로 dirty. 이 리팩터는 `SpawnProjectile`(~2076)·convert(~2990-3091) 를 건드리므로, 코드 착수 전 dreamstone WIP 를 커밋/격리해 hunk 충돌을 막는다(병행 세션 커밋 위생).
- **틸트보드 Y** — arc 의 sim-Y 가 `ProjectileViewPool.SyncTransforms`→`BoardSpace.ToView` 를 거쳐 화면에서 "수직 높이" 로 읽히는지 페이즈별 pitch 변동 하에 실측(재작업 위험 1순위, unit 6).
- **신규 .cs refresh scope=all** — enum/컴포넌트/시스템 신규 파일 추가 시 부분 refresh 면 cascading CS0246. scope=all 로 refresh.
