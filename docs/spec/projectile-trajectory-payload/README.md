# Projectile Trajectory × Payload — 투사체 궤적/페이로드 분해 리팩터

> 상태: **완료 2026-07-06** — 엔진 라운드(units 0~5, handoff `6_handoff_summary.md`) + Meteor 라운드(units 7~9: SkyFall×TileAoe 수렴 → 레거시 삭제(채널 15→14) → GA 낙하/임팩트 비주얼). Meteor 라운드 handoff → `10_handoff_summary.md`.
>
> 라운드 검증 질문(답: YES): **Meteor 가 전용 시스템/큐/캐리어 없이 단일 투사체 라이프사이클로 동작하고(무회귀), GA 프리팹 낙하가 "하늘에서 떨어지는" 느낌을 주는가?** — 사용자 육안 확정(Rock02 낙하 + Hit_Rock03 파편, 화면 밖 등장·후반 압축 낙하).

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
| 2 | `2_tileaoe_primitive.md` | 신설+테스트 | `TileAoe.IsInTileRange`/`TileDistance` static 순수함수 (Meteor L70-77 과 동일 의미) + EditMode |
| 3 | `3_ballistic_arc_trajectory.md` | arm+테스트 | MoveSystem `BallisticArc` arm: `ArcPosition` + `flightTime=dist/speed` static + EditMode |
| 4 | `4_tileaoe_payload.md` | arm | ImpactSystem `TileAoe` payload arm: 착탄 셀 반경 flat AOE + impact VFX |
| 5 | `5_spawn_wiring.md` | 배선 | AttackSystem RESOLVE `flightMode` 분기 + 셀 고정 + **단일** SpawnRequest 경로 + BattleBridge convert/drain |
| ~~6~~ | ~~integration_validation~~ | → 이관 | Play e2e(곡사 실발사→arc→AOE→셀낙하→틸트보드 Y)는 authored 유닛이 필요해 `artillery-defender` 로 이관. 엔진 로직은 EditMode 로 pin됨 |
| 6 | `6_handoff_summary.md` | 인계 | 커밋/구현/주의/후속 요약 (엔진 라운드) |
| 7 | `7_meteor_skyfall_convergence.md` | 이관 | Meteor → `SkyFall`×`TileAoe` 동작 보존 수렴 (시각 무변경, 레거시 미사용화) |
| 8 | `8_meteor_legacy_removal.md` | 삭제 | `MeteorPending`/`MeteorResolutionSystem`/`MeteorBurst` 큐 삭제 + 채널 목록 문서(CLAUDE.md·TRD) 갱신 |
| 9 | `9_meteor_ga_skyfall_visual.md` | 비주얼 | GA 프리팹 낙하 연출 (후보 스크린샷 비교→사용자 픽, `MeteorFall` 은퇴, view-공간 Y) |

## Feature-wide 계약 (load-bearing)

1. **직교 2축.** 투사체 = 궤적 × 페이로드. `ProjectileState` 가 `MovementKind` + `PayloadKind` 를 discriminator 로 보유한다. 두 축은 자유 조합(홈잉+AOE, 곡사+단일 도 성립).
2. **단일 라이프사이클.** `ProjectileSpawnRequest → BattleBridge drain → 엔티티+뷰 → MoveSystem → ImpactSystem → 파괴`. **궤적/페이로드별 별도 시스템·드레인·태그 신설 금지** (band-aid 금지). 캐리어 태그는 `ProjectileTag` 하나 → teardown/뷰 회수 자동 커버.
3. **MoveSystem = switch(MovementKind).** 각 arm 이 위치 갱신 + 자기 도착 클램프 소유. `Homing`(타겟 추적, 도착=거리, 타겟 소실 시 파괴) / `BallisticArc`(origin→impact XZ lerp, Y=sin(t·π)·arcHeight, 도착=t≥1).
4. **ImpactSystem = switch(PayloadKind).** 도착 판정 + 해결 소유(현 `ProjectileHitSystem` 위치 유지). `SingleSplash`(기존 outputs + splash + HitFlash) / `TileAoe`(착탄 셀 반경 flat, HitFlash 미적용 = Meteor 선례). **`TileAoe` 는 AOE 중심을 `ProjectileState.impact`(고정 착탄점)에서 읽는다 → payload=TileAoe 스폰은 궤적과 무관하게 `impact` 를 락해야 한다.** v1 은 `BallisticArc` 와만 페어링(곡사포). `Homing+TileAoe` 는 impact=타겟 도착위치 락이 필요 → 후속.
5. **순수 계산은 static Burst 함수 + EditMode 테스트.** `ArcPosition`, `flightTime`(speed>0 가드 + min clamp), `TileAoe.IsInTileRange`/`TileDistance`(셀 Chebyshev 멤버십). 테스트는 `Assets/_Project/Tests/EditMode/`.
6. **데미지 출처** — defender 발사 = `DefenderUnitData.outputs` 의 Damage 합산(홈잉 경로와 동일). **skill 발사(unit 7+) = `SkillData` magnitude 를 `request.damage` 에 스냅샷.** 어느 쪽이든 새 magic damage 필드 금지. 궤적/페이로드 파라미터(arcHeight/impactTileRange/flightMode/flightTime)만 request/`ProjectileData` 신규 필드.
7. **홈잉 무회귀가 완료 기준.** 기존 PlayMode smoke + splash 동작이 unit 1 이후 그대로여야 한다.
8. **신규 궤적 비용 = enum 케이스 + 위치 순수함수 + MoveSystem arm 1개** (시스템/드레인/태그 0). 베지어가 이 계약의 리트머스 — 후속 후보.
9. **새 ECS 맥락/NativeQueue/Manager 0.** 전부 Combat. `IncomingDamage` 는 Combat→Units 채널(선례 유지). `ProjectileHitEventsSingleton` VFX 채널 재사용.

## 비목표 / 후속 후보

- **곡사포 authored 유닛** (ProjectileData/DefenderUnitData SO · 프리팹 · 아이콘 · draft 편입 · 실매치 play) → `docs/spec/artillery-defender/` (이 리팩터의 첫 소비자).
- **Bezier 궤적** [S] · `MovementKind.BezierToPoint` + `BezierPos` 순수함수 + MoveSystem arm 1개. 이 spec 의 seam 이 옳은지의 증명 대상이지만 실제 arm 은 소비자 생길 때.
- ~~**Meteor 를 `TileAoe` 로 수렴** [S]~~ → **units 7~9 로 승격**(2026-07-06, 함수 dedup 을 풀 파이프라인 수렴+GA 비주얼로 확장).
- **non-Damage payload** [M] · 착탄 시 ApplyStat/ApplyStack 도 AOE(slow-곡사포 등). 현재 TileAoe payload 는 Damage-only.
- **임팩트 CC/knockback** [S] · `DefenderCcData` 를 AOE 대상에 적용.

## 주의

- **BattleBridge.cs 병행 세션 충돌** — (엔진 라운드 당시 dreamstone WIP 는 커밋됨) units 7~8 이 BattleBridge drain/teardown 을 건드리므로, 각 unit 착수 직전 `git status` 로 BattleBridge dirty 여부 확인 후 명시 경로 스테이징(lessons/02 병행 세션 커밋 위생).
- **틸트보드 Y (해결 2026-07-06)** — `BoardSpace.ToView` 는 sim-Y 를 **drop**(평면 보드 = 셀 XZ 만). 따라서 arc 를 sim-Y 에 실으면 화면에 안 보인다. **arc 높이는 view 공간에서** `ProjectileViewPool` 이 `BallisticArc.ArcHeight(saturate(elapsed/flightTime))` 로 view.y 에 더한다(기존 heightOffset 패턴). velocity 에 접혀 포탄이 arc 따라 피칭. sim(ArcPosition)/AOE/타이밍 무변경. Cannon 임시 곡사화로 Play 검증 OK.
- **신규 .cs refresh scope=all** — enum/컴포넌트/시스템 신규 파일 추가 시 부분 refresh 면 cascading CS0246. scope=all 로 refresh.
