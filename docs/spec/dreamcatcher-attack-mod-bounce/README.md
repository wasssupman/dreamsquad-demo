# Dreamcatcher Attack-Mod: Projectile Bounce — 공격 개조형 카드 부류 + 투사체 튕김

> 상태: **작성 2026-07-09, 구현 대기**
>
> 설계 배경: `docs/spec/dreamcatcher-unit-trigger/` 의 "확장 비용 지도" 中 **한 번 지불** 사례를 실측하는 spec. 드림캐쳐 카드 부류 (c) = 공격 개조형(트리거 없음, 기본 공격 산출물 상시 개조)을 열고, 첫 개조로 "투사체 N회 튕김"을 구현한다. 2계층 원칙(정의=아키텍처 비의존 / 해석=bridge 베이크 + Combat)과 부착 인프라(instanceId, 가드, 회수 seam)는 unit-trigger 의 것을 그대로 재사용한다.

## 목표

- **투사체 튕김 프리미티브**: 임팩트 시 파괴 대신, 남은 횟수가 있으면 근처 다른 적으로 재-홈잉 (드림캐쳐와 무관한 Combat 능력 — 이후 유닛/스킬도 사용 가능).
- **공격 개조형 카드 부류 (c)**: 개별 유닛 바인딩 카드가 그 유닛의 기본 공격 투사체에 튕김을 부여. 첫 카드(가칭 "통통 구슬"): 2회 튕김.

## 검증 질문

> 카드를 부착한 원거리 유닛의 기본 화살이 첫 히트 후 **다른 적에게 최대 2회 튕기며** 각 히트에 데미지를 주는가? 미부착 유닛/근접 유닛/기존 투사체(곡사·스킬·dc 트리거 투사체)는 **무회귀**인가?

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_definition_and_fields.md` | 계약 | 정의 계층 (c) 부류 (`DcAttackModKind/Spec`, 카드 `attackMods[]`) + `ProjectileState`/`Request` bounce 필드 (additive, 미사용, 컴파일만) |
| 1 | `1_bounce_retarget_primitive.md` | 신설+테스트 | `BounceRetarget.FindNext` static 순수함수 (Chebyshev 타일 반경 내 최근접, 직전 대상 제외) + EditMode |
| 2 | `2_impact_bounce_arm.md` | arm | ImpactSystem SingleSplash 해결 뒤 조건부 생존 분기 (재타겟·감쇠·재비행) + bridge `SpawnProjectile` 필드 피핑 |
| 3 | `3_attackmod_slot_and_inject.md` | 계약+배선 | `DcAttackModSlot` buffer(Combat) + bridge 부착 확장(원거리 가드) + AttackSystem 스폰 지점 주입 |
| 4 | `4_card_asset_play_validation.md` | 에셋+검증 | 통통 구슬 카드 SO + Play e2e (2회 튕김·감쇠·무회귀) |
| 5 | `5_handoff_summary.md` | 인계 | 종료 시 작성 |

## Feature-wide 계약 (load-bearing)

1. **튕김은 payload 해결의 후처리다.** 임팩트 해결(outputs/damage 적용·VFX·HitFlash)은 기존 SingleSplash arm 을 그대로 통과하고, 그 **뒤에** `bounceRemaining > 0 && 재타겟 성공` 이면 `DestroyEntity` 대신 `ProjectileState` 갱신(target 교체, `impactReached=false`, `bounceRemaining--`)으로 재비행한다. 신규 시스템/드레인/태그 0 — 엔티티가 살아남으므로 뷰/풀/TrailRenderer 도 자동 연속.
2. **재타겟 = 순수함수.** `BounceRetarget.FindNext(히트 위치·직전 대상 제외, ImpactSystem 의 기존 aoe 스냅샷, bounceTileRange)` — Chebyshev 타일 반경(프로젝트 관례) 내 최근접(sq 거리, 동률은 스냅샷 순서). 결정론 유지. 후보 없으면 -1 → 기존대로 파괴. **제외는 직전 대상 1개뿐 — A→B→A 재히트는 v1 의 의도된 동작이다** (적 2기 상황에서 튕김이 죽지 않도록; 전체 히스토리 제외는 후속 결정).
3. **감쇠 = bounceDamageMul.** 튕길 때마다 `state.damage ×= mul` 그리고 outputs 버퍼의 Damage-kind magnitude ×= mul (둘 다 — outputs 보유 투사체는 outputs 경로가 데미지 소스이므로). 수치는 전부 카드 SO 에서 (하드코딩 금지).
4. **호환 계약: ProjectileBounce 는 HomingToEntity × SingleSplash 산출물에만.** ballistic/스킬/TileAoe 는 대상 개념이 달라 v1 비적용 — AttackSystem 주입 지점이 Homing request 에만 얹는다. 부착 가드: `ProjectileRef` 없는(근접) 유닛은 warn + 거절 (unit-trigger 의 비-defender 가드와 같은 결).
5. **다중 개조 슬롯 스택 규칙**: count 는 **합산**, damageMul 은 **곱**, tileRange 는 **max**. (독립 슬롯 유지 — 회수 시 개별 제거 가능해야 하므로 부착 시 병합하지 않고 스폰 주입 시점에 집계.)
6. **정의 계층 불변**: `DcAttackModSpec` 은 순수 데이터, ECS 무참조. 카드 필드는 append-only (`attackMods[]`). `mechanics[]`(트리거형)와 공존 가능.
7. **소유권**: `DcAttackModSlot` 쓰기 = bridge 부착(스폰타임 선례), 읽기 = AttackSystem 스폰 주입. `ProjectileState.bounce*` 쓰기 = ImpactSystem(임팩트 해결 소유 — projectile-trajectory-payload 계약 4 유지).
8. **무회귀 기준**: bounce 필드 기본값 0 = 기존 모든 투사체가 기존 경로 그대로(파괴 분기 무변경). 홈잉/곡사/SkyFall/dc 트리거 투사체 PlayMode 무회귀.
9. **튕김 히트는 v1 에서 AttackOutputLog(세션 로그)에 기록되지 않는다.** 그 채널은 발사 시점(AttackSystem) 채널이고 투사체는 shooter 를 모른다. 검증은 로그가 아니라 **적 Health 감소 / ProjectileHitEvents(히트 VFX) / 육안**으로 한다. 튕김 히트 로깅은 shooter 참조 추가가 필요 → 후속 후보.

## 파이프라인 커버리지 (투사체 아키타입 대조)

기존 정거장 전부 재사용, 신규 정거장 없음:

| 정거장 | 이번 spec | 비고 |
|---|---|---|
| 데이터 SO | `DreamcatcherCard.attackMods[]` (신규 필드) | 신규 SO 타입 없음 |
| 스폰 진입점 | 기존 AttackSystem Homing request 에 bounce 파라미터 주입만 | |
| ECS 컴포넌트 (Combat) | 기존 + `DcAttackModSlot`(buffer) + `ProjectileState/Request` 필드 3개 | 신규 태그 없음 |
| 시뮬 시스템 | 기존 `ProjectileMoveSystem`(무변경) · `ProjectileHitSystem`(SingleSplash 후처리 분기) | |
| 이벤트 큐 | 기존 `ProjectileHitEventsSingleton` — 튕김 히트마다 자연 재발화 | 신규 채널 0 |
| View/Pool | 기존 `ProjectileViewPool` — 엔티티 생존이라 자동 연속 (Trail 이 튕김 궤적을 그림) | N/A 추가 작업 없음 |

## 후속 후보

- **감쇠/횟수 밸런싱 + 전용 투사체 뷰** [S] · 통통 구슬 전용 프리팹/SFX.
- **ballistic/TileAoe 튕김 해석** [M] · 대상 없는 산출물의 "튕김" 재정의 필요(파편? 연쇄 낙하?) — 호환표 확장 결정.
- **bounce 를 유닛 고유 능력으로** [S] · `ProjectileData` 또는 `DefenderUnitData` 에 기본 bounce 값 — 프리미티브는 이미 카드와 무관하게 동작하므로 authoring 노출만.
- **개조형 kind 확장** [?] · pierce(관통)/crit 등 — kind append + 주입/해결 지점 arm. pierce 는 bounce 와 유사한 생존 분기 계열.
- **non-Damage output 감쇠** [S] · 현재 튕김 감쇠는 Damage magnitude 만(계약 3). Slow 등 ApplyStat/ApplyStack output 은 매 튕김 full 적용 — 개조형 카드가 non-Damage output 유닛에 붙으면 밸런스 함정 가능(ecs-review M1). 콘텐츠가 그 조합을 쓸 때 재검토.
- **bounceDamageMul 하한 가드** [S] · unit 3 부착 가드에서 `damageMul > 0` 강제(0 이면 첫 튕김 후 영구 0 데미지). v1 카드는 1.0 이라 무해하나 authoring 방어.
- **튕김 히트 로깅** [S] · `ProjectileState` 에 shooter 참조를 실어 ImpactSystem 이 AttackOutputLog 를 enqueue — 밸런스 로그 완전성.
- **튕김 히스토리 전체 제외** [S] · A→B→A 재히트를 막으려면 히트 히스토리 저장 필요(고정 배열) — 기획이 원할 때.
