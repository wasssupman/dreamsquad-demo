# 3 — Handoff (units 0~2)

## Commit

- `43f85107` feat(goal-tower-siege): unit 0 — 골 타워 엔티티 + 공유 체력 풀
- `15fc28f1` feat(goal-tower-siege): unit 1 — 적이 골에서 살아남아 타워를 때린다
- unit 2 — 커버리지·케이던스·계약 정리 (이 문서와 같은 커밋)

**푸시 안 함**(승인제). **Unity 실행 검증 없음** — dotnet 컴파일만 통과.

## Implemented

- 골 셀마다 타워 엔티티(`GoalTowerTag` + `Health` + `IncomingDamage` + `FactionTag{GoalTower}`
  + `LocalTransform`), 체력은 공유 1풀(`GoalTowerHealth` 싱글턴 = 정본).
- `GoalTowerDamageSystem` `[UpdateBefore(DamageApplicationSystem)]` — 버퍼 직접 소비 → 풀 →
  전 타워 미러. 초안의 역산 델타(누적 결손 재차감)를 구조적으로 불가능하게 만든 지점.
- 골 도달이 죽음이 아니다: `GoalReachedMarker` 로 이벤트 1회 고정, 엔티티 존속.
- **돌격형 예외**: `AttackState` 없는 적(Runner·Swift)은 기존대로 자폭 + `stabilityDamage` 를
  타워 버퍼로. 안 그러면 그들이 골에 눌러앉아 전멸 진행을 그 판 내내 막는다.
- `Faction.GoalTower` 는 **골 도달 시점에만** 부여(`GrantGoalTowerTarget`).
- `PastGoalTag` 타겟팅 배제 5곳 해제 + 도발 마스크 보정.
- 안정도 authority 를 브리지 → 싱글턴으로 이관(`SyncGoalStabilityFromPool`). 공개 API 불변.
- `ProjectileHitSystem` 의 TileAoe 피해자 풀에 타워 포함 — 보스 AreaBarrage 가 타워를 못
  때리던 침묵 결함 수정.

## Key Files

- `Battle/Units/GoalTowerTag.cs` · `GoalTowerHealth.cs` · `GoalTowerDamageSystem.cs` ·
  `GoalReachedMarker.cs` · `GoalReachedEvent.cs`(canSiege) · `UnitLifecycleSystem.cs`
- `Battle/Combat/AttackSystem.cs` · `TauntAttackGrantSystem.cs` ·
  `Projectile/ProjectileHitSystem.cs` · `Projectile/Emission/ProjectileEmitterSystem.cs` ·
  `Projectile/ProjectileMoveSystem.cs`
- `Bridge/BattleBridge.cs` — `EnsureGoalTowers`/`DestroyGoalTowers`/`GrantGoalTowerTarget`/
  `EnqueueGoalTowerDamage`/`SyncGoalStabilityFromPool`, `DrainGoalEvents` 2갈래
- `Tests/EditMode/GoalTowerPoolTests.cs`(신규) · `UnitLifecycleSystemTests.cs` ·
  `FrontmostAttackLockTests.cs`

## Verified

- `dotnet build` Wassup.Runtime / Tests.EditMode / Tests.PlayMode **오류 0**.
- 맵 실측: 6개 맵 9개 골 **전부** 인접 거리 1 에 배치칸 존재 → 공성 적을 못 잡는 맵은 없다.
- 적 12종 `attackMethod` 실측 → Runner·Swift 만 공격 수단 없음(돌격형 예외의 근거).

## Notes (되돌리면 안 되는 의도)

- `GoalTowerDamageSystem` 을 `UpdateAfter(DamageApplicationSystem)` 로 옮기지 말 것. 옮기는
  순간 타워 `Health` 를 그 시스템이 먼저 깎고, 역산 델타·개별 `DeadTag` 문제가 되살아난다.
- `Faction.GoalTower` 를 base `targetMask` 에 넣지 말 것(원거리 적이 골 앞에서 멈춘다).
- 타워에 `ModifierStats`/`StatModifierSlot`/`ShieldSlot`/`IncomingHeal` 을 붙이지 말 것.
- 보스는 `hunting`(방어유닛 생존 시) 동안 골 셀 판정을 건너뛴다 — **의도된 구멍**이다.
  방어유닛이 전멸해야 보스가 타워로 향한다.
- 안정도 표시는 `CeilToInt`, 패배 판정은 원본 float. 바꾸면 "0 인데 안 죽음" 이 보인다.

## 검증 체크리스트 (Unity 열리면)

- [ ] EditMode 전량 + `GoalTowerPoolTests`(7) + `UnitLifecycleSystemTests` + `FrontmostAttackLockTests`
- [ ] Play: 근접 적이 골에 도달해 **사라지지 않고** 타워를 때리고 안정도가 지속적으로 준다
- [ ] Play: 원거리 적(Sniper·Needler)이 골 앞에서 멈추지 않고 골 셀까지 들어온다
- [ ] Play: Runner·Swift 는 골에서 사라지며 안정도를 1회 깎는다(자폭 경로)
- [ ] Play: 골 인접 배치칸에 유닛을 놓으면 공성 적이 죽고 **전멸 진행이 살아난다**
- [ ] Play: 데미지 폰트가 허공에 뜨지 않는다(공성 적의 뷰가 살아 있다)
- [ ] Play: 보스 AreaBarrage 가 골에 떨어지면 안정도가 준다(TileAoe 수정 확인)
- [ ] Play: 안정도 0 → 패배, 결과 화면의 `남은 안정도` 가 0
- [ ] 스트레스 카운터가 공성/자폭 양쪽에서 오른다

## Follow-up

- **밸런스**: 공성 DPS 가 붙으면서 안정도 20 이 순식간에 녹을 수 있다. `goalStabilityMax` 와
  적 공격력의 관계를 실측해 재조정할 것(three-minute-survival `5_verification_checklist.md` §4).
- **공성 연출**: 타워 피격에 데미지 폰트·히트 플래시가 없다(유닛이 아니라 의도). 타워가
  맞고 있다는 시각 신호가 필요한지는 Play 후 판단.
- **타워 파괴 연출**: `goalStructureProp` 은 정적 prop 이라 엔티티를 추적하지 않는다.
- 보스 공성 규칙(hunting 우회)을 유지할지 재검토.
