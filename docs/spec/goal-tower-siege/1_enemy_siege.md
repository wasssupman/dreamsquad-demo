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

**1-a. 예외: 공격 수단이 없는 적은 자폭한다** (착수 후 실측으로 추가) — `Enemy_Runner` ·
`Enemy_Swift` 는 `attackMethod: None` + outputs 0 이라 `AttackState` 자체가 안 붙는 **돌격형**
이다. 이들이 골에 눌러앉으면 아무 피해도 못 주면서 `NoQueuedAttackersRemain()` 만 영구히
거짓으로 만든다 → **전멸 진행이 그 판 내내 죽는다.**

그래서 `UnitLifecycleSystem` 이 `AttackState` 보유 여부로 갈라 판정하고, 그 결과를
`GoalReachedEvent.canSiege` 에 실어 보낸다(소비 시점엔 엔티티가 이미 파괴됐을 수 있어 브리지가
컴포넌트를 되읽을 수 없다). `canSiege == false` 면 기존대로 파괴하고, 브리지가 그 적의
`stabilityDamage` 를 **타워 버퍼로** 넣는다 — 풀의 writer 를 하나로 유지하려고 안정도를 직접
깎지 않는다. 읽힘은 "돌격형이 골에 몸을 부딪고 사라진다".

`AttackState` 는 Combat 소유지만 여기서는 읽기만 한다(맥락 간 RO 읽기 허용 — 같은 시스템의
`DcTriggerSlot` 선례).

**2. 도달한 적에게만 타워를 열어준다** — 브리지가 `GoalReachedEvent` 를 드레인할 때 그 적의
`AttackState.targetMask |= (int)Faction.GoalTower` 를 쓴다. **base mask 에 넣지 않는 이유**:
넣으면 사거리 3타일 원거리 적이 골에서 3칸 떨어진 지점에서 `HasFireTarget` → `Engaging` →
(`engageMovement == Halt`면) 정지해 버린다. 그러면 골 셀에 도달하지 않아 `PastGoalTag` 도,
`GoalReachedEvent` 도, 스트레스 카운트도 발생하지 않는다.

> Units 맥락(브리지 경유)이 Combat 컴포넌트를 쓰는 지점이다. 브리지는 ECS 게이트웨이라
> 허용되지만, 시스템 간 직접 쓰기로 옮기지 말 것.

**3. 유출 처리 변경**(`DrainGoalEvents`) — `canSiege` 로 두 갈래가 된다.

- **공성(true)**: 뷰 despawn·표식 회수·`_enemyTypeByEntity` 제거·즉발 피해를 **전부 안 한다**
  (적이 살아 있다). 남기면 안 보이는 적이 타워를 때리고 데미지 폰트만 허공에 뜬다.
  `targetMask` 만 연다.
- **자폭(false)**: 기존 유출 경로 그대로 + `stabilityDamage` 를 타워 버퍼로.

스트레스(`_goalReachedCount++`)와 HUD 갱신은 **두 경로 공통**이다 — "몇 번 뚫렸나" 는 집계
지표라 적이 살아남든 자폭하든 똑같이 오른다.

**3-a. authority 이관** — 브리지 Update 가 `GoalTowerHealth` 싱글턴을 폴링해 `_goalStability`
미러를 갱신하고 패배를 판정한다(`DrainGoalEvents` 안의 패배 블록은 제거). 표시는 올림
(`CeilToInt`)이고 패배 판정은 원본 float — 0.3 남았는데 화면에 0 이 뜨면 "죽었는데 안 죽었다"
가 된다. 공개 API(`GoalStabilityCurrent`/`GoalStabilityMax`)는 불변이라 체력바와 tie-break 는
정본이 옮겨간 것을 모른다.

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
