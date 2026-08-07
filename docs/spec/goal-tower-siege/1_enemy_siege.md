# 1 — 적 공성

## 목적

적이 골에 도달해도 **사라지지 않고 멈춰서 타워를 때린다.** 유출 즉발 피해를 걷어내고 안정도를
지속 피해로 바꾼다. 선행: unit 0.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/UnitLifecycleSystem.cs` — 골 도달 처리
- 신규 `Assets/_Project/Scripts/Battle/Units/GoalReachedMarker.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `DrainGoalEvents`, 도달 시 mask 부여
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` ·
  `Combat/Projectile/Emission/ProjectileEmitterSystem.cs` · `Combat/Projectile/ProjectileMoveSystem.cs`
  — `PastGoalTag` 배제 해제
- `Assets/_Project/Scripts/Battle/Combat/TauntAttackGrantSystem.cs` — mask 덮어쓰기 보정
- `Assets/_Project/Tests/EditMode/UnitLifecycleSystemTests.cs` · `FrontmostAttackLockTests.cs`

## 구현

**1. 골 도달 = 죽음이 아니다** — `UnitLifecycleSystem` 의 PastGoal 루프에서 `DestroyEntity` 를
제거한다. `GoalReachedEvent` 는 **1회만** 발화해야 하므로 zero-size `GoalReachedMarker` 를 같은
루프에서 ECB 로 붙이고, 쿼리를 `WithAll<PastGoalTag, AttackUnitTag>().WithNone<GoalReachedMarker>()`
로 좁힌다. **in-loop 플래그 검사로 만들지 말 것** — 공성 인구가 영구 잔존하므로 쿼리에서
빼지 않으면 매 프레임 전원을 순회한다.

이동 정지는 기존 `MovementSystem` 의 `WithNone<PastGoalTag>` 가 이미 처리한다.

**2. 도달한 적에게만 타워를 열어준다** — 브리지가 `GoalReachedEvent` 를 드레인할 때 그 적의
`AttackState.targetMask |= (int)Faction.GoalTower` 를 쓴다. **base mask 에 넣지 않는 이유**:
넣으면 사거리 3타일 원거리 적이 골에서 3칸 떨어진 지점에서 `HasFireTarget` → `Engaging` →
(`engageMovement == Halt`면) 정지해 버린다. 그러면 골 셀에 도달하지 않아 `PastGoalTag` 도,
`GoalReachedEvent` 도, 스트레스 카운트도 발생하지 않는다.

> Units 맥락(브리지 경유)이 Combat 컴포넌트를 쓰는 지점이다. 브리지는 ECS 게이트웨이라
> 허용되지만, 시스템 간 직접 쓰기로 옮기지 말 것.

**3. 유출 처리 변경**(`DrainGoalEvents`) — three-minute-survival unit 0 이 넣은 즉발 차감을
제거하고, 뷰 despawn(`enemyViewPool`/`spineUnitPool`)과 표식 회수(`NotifyEnemyGoneIfMarked`)도
걷어낸다. 남기면 **안 보이는 적이 타워를 때리고**, 살아 있는 적의 현상금이 무보상 회수된다.
유지: `_goalReachedCount++`(스트레스), HUD 갱신. `_enemyTypeByEntity` 제거는 **하지 않는다** —
그 적은 아직 살아 있고 킬 경로가 나중에 쓴다.

**4. 타겟팅 배제 해제(5곳)** — `PastGoalTag` 를 "곧 사라질 놈"으로 배제하던 곳:

| 위치 | 배제 대상 | 해제 후 |
|---|---|---|
| `AttackSystem.cs:475` | frontmost 추적(끝을 보는 눈) | 골에 붙은 적이 진짜 frontmost 다 |
| `AttackSystem.cs:583` | frontmost 락 유효성 | 락이 유지된다 |
| `AttackSystem.cs:1595` | 니들 폴백(`PickFallbackTarget`) | 공성 적도 폴백 대상 |
| `ProjectileEmitterSystem.cs:101` | 발사 명세 후보 풀 | 공성 적에게도 발사 |
| `ProjectileMoveSystem.cs:72` | 호밍 재조준 풀 | 공성 적으로 재조준 |

`NearestTargeting.cs` 에는 필터가 없다(순수 랭킹 유틸) — 주석만 갱신한다. 주 최근접 타겟 루프
(`:424-441`)에도 필터가 없어 **일반 방어유닛은 지금도 공성 적을 때린다**.

**5. 도발** — `TauntAttackGrantSystem:48` 이 `targetMask` 를 `Defender` **단독으로 덮어쓴다.**
골에 도달한 적이 도발되면 타워를 못 때리면서 필드를 점유해 전멸 트리거만 막는다. 덮어쓰기
대신 `Defender` 비트를 **더한다**(기존 비트 보존).

## 완료 기준

- [ ] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러/경고 0
- [ ] Play: 적이 골에 도달해 **사라지지 않고** 제자리에서 타워를 때리고 안정도가 줄어든다
- [ ] Play: 원거리 적이 골 앞에서 멈추지 않고 골 셀까지 들어온다(스트레스 카운트가 오른다)
- [ ] Play: 골 근처 방어유닛이 공성 적을 때려 죽인다
- [ ] Play: 데미지 폰트가 허공에 뜨지 않는다(뷰가 살아 있다)
- [ ] EditMode: `GoalReachedEvent` 가 적 1기당 정확히 1회 발화한다
- [ ] EditMode: `UnitLifecycleSystemTests` 의 "PastGoalTag → DestroyEntity" 단언을 새 계약으로 교체
- [ ] EditMode: `FrontmostAttackLockTests` 의 "골 도달 시 락 해제" 단언 교체
