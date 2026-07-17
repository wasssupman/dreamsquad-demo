# defender-directional-volley — 방향 지정 배치 + 다연발 투사체

상태: 구현 중 2026-07-17 — unit 0·5 완료(78f5c38a·80b26662). origin merge(0cc77e44) + placement-cell-snap units 0~5 커밋(a3812079)으로 **BattleBridge 클린 → unit 1 착수 가능**. 접점 앵커 생존 확인: ActivateDeployedDefender·PlayDeploymentPresentation·SpawnProjectile·ResolveProjectileAxes·TryBeginDefenderDeployment. AttackSystem·DefenderUnitData·ProjectileData 는 merge 무변경. 단 드래그 컨트롤러 계열(DefenderDragPlacementController·DefenderDragSlot·DragSwaySettings)은 병행 수정 계속 중 — unit 6 착수 전 재독 필수

## 상위 목표

배치 스와이프를 2페이즈로 확장한 신규 메커니즘 유닛을 만든다.

- 기존 유닛 = **D&D 페이즈**(드래그→드롭=확정, 현행 유지).
- 신규 유닛 = D&D 페이즈 뒤에 **공격방향 페이즈** 추가: 드롭 시 슬로우모션 유지 + 줌인 + 상하좌우 가이드 UI → 스와이프로 방향 하이라이트 → 스와이프 종료 시 방향 확정(명일방주식 영구 고정).
- 확정된 방향으로 **레인 기반 공격**: 방향 레인(폭 1타일 × 사거리)에 적이 있을 때만 쿨다운 발사. 타겟 엔티티 없이 방향으로 발사.
- 투사체 엔진을 **1트리거=1발 → 다연발**로 일반화: 버스트(0.1초 간격 N연발)와 스프레드(부채꼴 각도 N발)를 SO 파라미터로. 실증 유닛은 머신건 1종.

검증 질문: *"드롭 후 방향 지정이 자연스러운 두 번째 제스처로 이어지고, 지정 방향 버스트 사격이 기존 시뮬 계약(맥락 경계·캐리어 패턴·시간 도메인)을 깨지 않고 도는가?"*

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | sim 순수 로직 | `0_volley_sim_math.md` | VolleyMath·LaneMath·SweepHitMath 순수 함수 + EditMode 테스트 |
| 1 | 데이터 계약 | `1_data_contract.md` | SO 신규 필드 · DeployedFacing · SpawnRequest 방향 필드 · Bridge API (컴파일 안전 뼈대) |
| 2 | ECS 투사체 | `2_directional_projectile.md` | MovementKind.DirectionalLinear + PayloadKind.PathHit arm |
| 3 | ECS 타겟팅 | `3_lane_targeting.md` | facing 유닛 분기: 레인 게이트 + 타겟 없는 방향 단발 발사 |
| 4 | ECS 다연발 | `4_volley_fire.md` | 스프레드 동프레임 N발 + 버스트 시간차 연발 |
| 5 | UX 순수 로직 | `5_aim_phase_logic.md` | DirectionAimLogic(데드존/방향 스냅/전이) + EditMode 테스트 |
| 6 | Mono 컨트롤러 | `6_aim_controller.md` | DirectionAimController: 핸드오프·줌·가이드 UI·확정→활성화 |
| 7 | 실증 유닛 | `7_machinegun_unit.md` | 머신건 유닛 에셋 + 카탈로그 등록 + e2e 검증 |

## 공통 계약 (feature-wide)

1. **로직/아키텍처 분리** (CLAUDE.md 제약 10): 페이즈 전이·버스트 틱·스프레드 각·레인 판정·스윕 히트는 전부 **plain 값 입출력 순수 static 함수**로 두고, ECS(시뮬)와 Mono(프레젠테이션)는 결과값을 소비만 한다. `ModifierMath` 동형. 전부 EditMode 테스트 대상.
2. **방향은 영구 고정**: 배치 확정 시 `DeployedFacing`(Units 소유)에 1회 기록, 이후 불변. 기록 주체는 BattleBridge(유일 창구), Combat 은 읽기 전용.
3. **발사 게이트 대체**: facing 유닛은 최근접 타겟 선택 대신 레인 내 적 존재 검사만 통과하면 발사. 레인 폭 1타일 고정(파라미터화는 후속).
4. **다연발 = 기존 캐리어 엔티티 패턴의 확장**: 트리거당 캐리어 N개(스프레드=동프레임, 버스트=AttackState 틱 시간차). **새 System·NativeQueue·drain 채널 신설 금지.**
5. **투사체 확장은 enum arm 추가로만**: `MovementKind.DirectionalLinear` + `PayloadKind.PathHit`. projectile-trajectory-payload 스펙 계약 준수.
6. **하드코딩 금지**: 발수/간격/확산각/관통수/데드존 등 전 수치는 SO 에서.
7. **슬로우모션은 TimeManager lease 이관**(Battle 도메인), **카메라는 CameraDirector 포커스 피드만**(직접 조작 금지). 버스트 발사 간격은 sim 시간이므로 슬로우모션이 자동 적용된다.
8. **버스트는 시작되면 완주**(도중 레인에서 적이 사라져도). 쿨다운은 버스트 종료 시점부터 기산.
9. **데드존 릴리즈 = 가이드 유지·재스와이프 대기**. 방향 확정 없이는 활성화되지 않는다. 배치 취소/환불은 스코프 밖(후속 후보).

## 파이프라인 커버리지

투사체 아키타입 (`docs/reference/object-pipeline-map.md`) 대조 — 이번 spec 은 기존 정거장을 전부 재사용하고 arm/필드만 확장한다:

| 정거장 | 앵커 | 이번 spec |
|---|---|---|
| 데이터 SO | `Data/ProjectileData.cs` | flightMode `Directional` + `pierceCount` 필드 추가 (unit 1) |
| 스폰 진입점 | `AttackSystem` request stage → `BattleBridge.DrainProjectileSpawnRequests`→`SpawnProjectile` | 동일 2단계 유지. request 에 방향 필드 추가, 방향 request 는 타겟/착탄셀 없음 (unit 1·2) |
| ECS 컴포넌트 (Combat) | `Battle/Combat/Projectile/` ProjectileState 등 | 히트 중복 방지 hit-set 버퍼 추가 (unit 2) |
| 시뮬 시스템 | `ProjectileMoveSystem`(궤적)·`ProjectileHitSystem`(페이로드) | 각 switch 에 arm 1개씩 추가, 기존 arm 무변경 (unit 2) |
| 이벤트 큐 | `ProjectileHitEventsSingleton` | 재사용 — 관통 히트도 히트당 1 이벤트. 신규 큐 없음 |
| View/Pool | `Presentation/ProjectileViewPool.cs` | 무변경 예상 — 평면 직선 비행이라 기존 SyncTransforms 로 동작. arc 없음 |
| 씬 wiring | BattleBridge `_projectileViewPool` | N/A — 기존 배선 그대로 |

방어 유닛 아키타입 대조 — 머신건 유닛(unit 7):

| 정거장 | 앵커 | 이번 spec |
|---|---|---|
| 데이터 SO | `Data/DefenderUnitData.cs` + `DefenderCatalog.cs` | 신규 필드(directionalAttack·shotCount·shotIntervalSec·spreadAngleDeg) + 머신건 에셋 + 카탈로그 등록 |
| 스폰 진입점 | `BattleBridge` `TryBeginDefenderDeployment`→`CreateDefenderEntity` | 동일. `ActivateDeployedDefender` 에 facing 전달 확장 (unit 1) |
| ECS 컴포넌트 (Units) | DefenderUnitTag·Health·… | `DeployedFacing` 추가 (unit 1) |
| 시뮬 시스템 | `Battle/Combat/AttackSystem.cs` | facing 분기(레인 게이트·방향 발사·버스트 틱) (unit 3·4) |
| 이벤트 큐 | DefenderDeath + 공유 UnitAttackVisual/DamageNumber | 재사용, 신규 없음 |
| View/Pool | SpineUnitPool/SpineUnitView | 무변경 — 공격 순간 FaceToward 는 방향 유닛이면 facing 지점을 향함 (unit 3) |
| 체력 표시 | TileHealthGaugeLayer 폴링 | N/A — 무관 |
| 씬 wiring | BattleBridge SerializeField | N/A — 신규 슬롯 없음. DirectionAimController 는 런타임 AddComponent(드래그 컨트롤러 선례) |

## 후속 후보

- 배치 취소/코스트 환불 (공격방향 페이즈 중 취소 제스처)
- 배치 후 방향 재지정 (유닛 탭 → 가이드 재오픈)
- 레인 폭 SO 파라미터화 (현 1타일 고정)
- 스프레드 실증 유닛(샷건형) — 엔진은 이번에 완성, 유닛은 미제작
- 버스트/스프레드 × Homing·Ballistic 궤적 조합 검증 (이번엔 Directional 에서만 e2e)
- 방향 가이드 UI 정식 아트 (이번엔 절차적/임시)
- 곡사 방향 발사 (DirectionalLinear + arc 시각)
- 머신건 연사음 (버스트 캐리어 발은 발사 SFX 게이트 밖 — 볼리당 1회. battle-audio 스코프)
- tap-to-place 배치 경로 연동 — `defender-tap-to-place` spec(승인 대기)이 도입되면 공격방향 페이즈 진입점을 D&D EndDrag 외에 tap 확정 시점에도 연결
