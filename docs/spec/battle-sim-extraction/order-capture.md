# BattleSimGroup 유효 시스템 총순서 캡처 (battle-sim-extraction unit 0)

- 캡처: 2026-08-03 23:56 · Unity 6000.4.7f1 · HEAD `54e0d7af`
- 시스템 수: **44** (기대 44 — 다르면 시스템 증감 후 재캡처된 것)
- 이 표가 틱 페이즈 순서의 정본이다. "어트리뷰트" 열은 각 시스템의 **선언** — 비어 있으면 이 자리의 순서는 정렬 tie-break 산물이다.
- 신규 핀 여부는 핀 커밋에서 이 문서에 수기로 표기한다.

| # | 시스템 | 선언 어트리뷰트 (Before/After) |
|---|---|---|
| 1 | `LastRunSystem` | Before(DamageApplicationSystem) |
| 2 | `HazardLifetimeSystem` | Before(CcApplySystem) · Before(MovementSystem) |
| 3 | `AllyBuffFieldSystem` | Before(ModifierApplySystem) |
| 4 | `BossPeriodicTriggerSystem` | Before(ModifierApplySystem) |
| 5 | `ZoneApplySystem` | Before(CcApplySystem) · Before(ModifierApplySystem) · After(HazardLifetimeSystem) |
| 6 | `ObstacleLifetimeSystem` | Before(MovementSystem) |
| 7 | `DefenderFieldSystem` | Before(MovementSystem) |
| 8 | `AggroStateSystem` | Before(MovementSystem) |
| 9 | `ModifierApplySystem` | Before(StatModifierTickSystem) |
| 10 | `CcApplySystem` | Before(MovementSystem) |
| 11 | `HealthDeathSystem` | Before(UnitLifecycleSystem) |
| 12 | `LethalTimerSystem` | Before(DamageApplicationSystem) |
| 13 | `TauntAttackGrantSystem` | Before(AttackSystem) · Before(MovementSystem) · After(AggroStateSystem) |
| 14 | `EnemyAiStateSystem` | Before(MovementSystem) · After(TauntAttackGrantSystem) |
| 15 | `DotApplySystem` | Before(CcDecaySystem) · Before(DamageApplicationSystem) · After(CcApplySystem) |
| 16 | `PatrolFieldSystem` | Before(MovementSystem) |
| 17 | `MovementSystem` |  |
| 18 | `HazardCastSystem` | Before(AttackSystem) · After(MovementSystem) |
| 19 | `ShieldCastSystem` | After(MovementSystem) |
| 20 | `ResignationThresholdSystem` |  |
| 21 | `HeatAccrualSystem` | Before(DamageApplicationSystem) |
| 22 | `PickupSpawnSystem` |  |
| 23 | `PickupConsumeSystem` | After(PickupSpawnSystem) · After(ModifierApplySystem) |
| 24 | `HitFlashSystem` |  |
| 25 | `EffectTickSystem` |  |
| 26 | `ProjectileMoveSystem` | After(MovementSystem) |
| 27 | `ProjectileHitSystem` | Before(DamageApplicationSystem) · After(ProjectileMoveSystem) · After(ModifierApplySystem) |
| 28 | `FatigueAccrualSystem` | After(ModifierApplySystem) |
| 29 | `StatModifierTickSystem` | Before(ModifierStatsAggregateSystem) · After(ModifierApplySystem) |
| 30 | `ModifierStatsAggregateSystem` | After(StatModifierTickSystem) |
| 31 | `MaxHealthScaleSystem` | After(ModifierStatsAggregateSystem) |
| 32 | `StackModifierTickSystem` | After(ModifierStatsAggregateSystem) |
| 33 | `AttackSystem` | After(MovementSystem) · After(ModifierApplySystem) |
| 34 | `DamageApplicationSystem` | After(AttackSystem) · After(ModifierApplySystem) |
| 35 | `ResignationDropSystem` | Before(UnitLifecycleSystem) · After(DamageApplicationSystem) · After(HealthDeathSystem) |
| 36 | `PatrolLifecycleSystem` | Before(UnitLifecycleSystem) · After(DamageApplicationSystem) · After(HealthDeathSystem) |
| 37 | `CcClearSystem` | After(DamageApplicationSystem) |
| 38 | `ProjectileEmitterSystem` | After(BossPeriodicTriggerSystem) · After(AttackSystem) |
| 39 | `DreamCocoonSystem` | Before(CcDecaySystem) · After(CcClearSystem) · After(ModifierApplySystem) |
| 40 | `CcDecaySystem` | After(MovementSystem) |
| 41 | `UnitLifecycleSystem` | After(DamageApplicationSystem) |
| 42 | `HealthThresholdSystem` | After(DamageApplicationSystem) · After(ModifierApplySystem) |
| 43 | `UltimateLeapSystem` | Before(BlinkApplySystem) · After(HealthThresholdSystem) |
| 44 | `BlinkApplySystem` | After(HealthThresholdSystem) |

## 신규 핀 (unit 0 — 2026-08-03)

캡처된 현행 순서를 그대로 고정하는 어트리뷰트 **13건 / 파일 12개**. 핀 전/후 batch 재덤프로
순서 **완전 동일(diff 0)** · 컴파일 에러 0 확인 — 행동 변화 없이 tie-break 산물이던 순서가 선언이 됐다.

- `LastRunSystem` · `DotApplySystem` · `ProjectileHitSystem` → `Before(DamageApplicationSystem)` —
  IncomingDamage 기록의 같은-프레임 소비 고정. **`ProjectileHitSystem` 은 캡처 후 실사에서 발견된
  추가 대상**(감사 목록에 없었음 — 착탄 데미지 기록 5곳에 소비자 대비 선언 0이었다).
- `ProjectileMoveSystem` → `After(MovementSystem)` — 호밍의 이동-후 최신 위치 읽기 고정.
- `BossPeriodicTriggerSystem` · `ZoneApplySystem` → `Before(ModifierApplySystem)` — 모디파이어
  enqueue 의 같은-프레임 적용 고정.
- `PickupConsumeSystem` · `FatigueAccrualSystem` · `AttackSystem` · `DamageApplicationSystem` ·
  `DreamCocoonSystem` · `HealthThresholdSystem` · `ProjectileHitSystem` → `After(ModifierApplySystem)` —
  enqueue 의 다음-프레임 적용(현행 1프레임 지연) 고정.
- 핀 제외(기선언 보존): `AllyBuffFieldSystem`(명시 Before) · `StackModifierTickSystem`(의도된
  1프레임 지연의 전이 핀 — "고치지" 말 것).
