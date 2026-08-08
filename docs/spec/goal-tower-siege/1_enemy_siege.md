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
`stabilityDamage` 를 **그 적이 부딪힌 골의 타워** `IncomingDamage` 로 넣는다(표준 경로와 같은
통로). 어느 골인지는 이벤트에 실린 도달 위치로 가른다 — 소비 시점엔 엔티티가 파괴돼 위치를
되읽을 수 없다. 읽힘은 "돌격형이 골에 몸을 부딪고 사라진다".

`AttackState` 는 Combat 소유지만 여기서는 읽기만 한다(맥락 간 RO 읽기 허용 — 같은 시스템의
`DcTriggerSlot` 선례).

**2. 마스크를 열어줄 일이 없다 (rev 2)** — 타워가 `Faction.Defender` 라 적의 base
`targetMask` 가 이미 그것을 포함한다. rev 1 의 `GrantGoalTowerTarget`(브리지가 Combat 소유
`AttackState` 를 쓰던 지점)은 **삭제**됐다 — 리뷰가 맥락 경계 위반으로 지적한 곳이고,
없애는 것이 규칙 위반도 1프레임 지연도 동시에 없앤다.

대가: 사거리가 긴 적은 골 셀에 들어가기 전에 멈춰 타워를 쏜다 → 그 적은 `PastGoalTag` 를
못 받아 **스트레스 카운터에 안 잡힌다.** 스트레스는 이미 패배·점수와 무관한 지표라 손실이
작고, "원거리가 멀리서 골을 두들긴다" 는 연출은 오히려 자연스럽다(2026-08-08 판단).

**3. 유출 처리 변경**(`DrainGoalEvents`) — `canSiege` 로 두 갈래가 된다.

- **공성(true)**: 뷰 despawn·표식 회수·`_enemyTypeByEntity` 제거·즉발 피해를 **전부 안 한다**
  (적이 살아 있다). 남기면 안 보이는 적이 타워를 때리고 데미지 폰트만 허공에 뜬다.
  `targetMask` 만 연다.
- **자폭(false)**: 기존 유출 경로 그대로 + `stabilityDamage` 를 타워 버퍼로.

스트레스(`_goalReachedCount++`)와 HUD 갱신은 **두 경로 공통**이다 — "몇 번 뚫렸나" 는 집계
지표라 적이 살아남든 자폭하든 똑같이 오른다.

**3-a. 안정도 = 타워의 Health (rev 2)** — 별도 정본이 없다. 브리지 Update 가 타워 `Health` 를
읽어 미러(`_goalStability`)를 갱신하고 패배만 판정한다. 표시는 올림(`CeilToInt`), 판정은 원본
float — 0.3 남았는데 화면에 0 이 뜨면 "죽었는데 안 죽었다" 가 된다. 골이 여럿이면 **가장
위험한 골**을 보여주고, **하나라도 부서지면 패배**다. 공개 API 는 불변.

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

**5. 도발 — 패치 불필요 (rev 2)** — 도발이 부여하는 마스크가 `Defender` 단독인데 타워가 바로
그 진영이다. rev 1 의 패치는 되돌렸다.

**6. 싱크 부재 시 fail-open 유지** — 마커는 **이벤트를 실제로 보낸 경우에만** 붙인다. 마커는
쿼리에서 빼는 필터라, 이벤트 없이 붙이면 그 적은 두 번 다시 평가되지 않는다(스트레스도 안
오르고 `AttackUnitTag` 는 유지돼 웨이브 전멸 판정을 그 판 내내 막는 유령이 된다).
원저자의 "fail-open otherwise" 를 fail-closed 로 뒤집지 않기 위한 가드다.

## 완료 기준

- [ ] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러/경고 0
- [ ] Play: 적이 골에 도달해 **사라지지 않고** 제자리에서 타워를 때리고 안정도가 줄어든다
- [ ] Play: 원거리 적이 골 앞에서 멈추지 않고 골 셀까지 들어온다(스트레스 카운트가 오른다)
- [ ] Play: 골 근처 방어유닛이 공성 적을 때려 죽인다
- [ ] Play: 데미지 폰트가 허공에 뜨지 않는다(뷰가 살아 있다)
- [ ] EditMode: `GoalReachedEvent` 가 적 1기당 정확히 1회 발화한다
- [ ] EditMode: `UnitLifecycleSystemTests` 의 "PastGoalTag → DestroyEntity" 단언을 새 계약으로 교체
- [ ] EditMode: `FrontmostAttackLockTests` 의 "골 도달 시 락 해제" 단언 교체
