# unit 2 — 이동 분기 + Bridge 스폰 경로 + 디버그 메뉴

## 목적

unit 1 이 구운 `PatrolStep.dir` 을 실제 이동으로 바꾸고, 순찰병 엔티티·뷰를 만드는 단일 지점을 세운다. **여기서 처음으로 Play 검증이 성립한다** — 소환 없이 계층 A 전체가 판에서 동작한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CreatePatrolEntity` · `DebugSpawnPatrolAt` · `DestroyBattleEntities`
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `moveSpeed` 필드 추가(맨 뒤 append)
- 신규 `Assets/_Project/Scripts/Battle/Units/SummonedBy.cs` — 구조체만. 소비 시스템은 unit 4
- 신규 `Assets/_Project/Scripts/Battle/Movement/PatrolDebugMenu.cs`

## 구현

### ① dir 소스 합류

보스 `hunting` 이 이미 만든 "**`Marching` = 전진, 목적지는 dir 소스가 결정**" 계약에 세 번째 소스로 합류한다. `AiState` 에 값을 추가하지 않으므로 `EnemyAiStateSystem.Evaluate` 순수 함수와 그 테스트를 건드리지 않는다.

```csharp
float2 dir = hunting ? huntField.flow[idx] : field.flow[idx];   // 현재
```

`patrolling`(= `PatrolStep` 보유)를 최우선 소스로 앞에 세운다. 그 뒤의 속도 배율·CC action-lock·`MovementCellTrim`·`LateralRecenter` 는 **전부 공유한다**.

`PatrolStep.dir == zero` 는 정지다. zero-flow recovery 분기로 떨어뜨리지 않는다 — 그쪽은 goal field 의 `dist` 를 보므로 순찰병에게 의미가 없다.

### ② goal 판정 게이트 — 이 unit 의 최대 회귀 위험

goal 판정은 dir 분기보다 **앞에** 있어서 patrol 분기가 우회하지 못한다:

```csharp
if (!hunting && field.IsGoalCell(cell)) { ecb.AddComponent<PastGoalTag>(entity); continue; }
```

거점 박스 안에 goal 셀이 들어오면 순찰병에게 `PastGoalTag` 가 붙는다. 그러면 ⑴ `MovementSystem` 이 `WithNone<PastGoalTag>` 라 **영구 동결**, ⑵ `UnitLifecycleSystem` 의 PastGoal 파괴 루프는 `WithAll<AttackUnitTag>` 인데 순찰병엔 그 태그가 없어 **파괴도 안 됨**, ⑶ 살아 있으니 `SummonerState.current` 가 계속 유효해 **소환사가 남은 판 내내 재소환하지 못한다**.

보스 leak-proof 와 같은 형태로 `!patrolling &&` 를 조건에 추가한다. 맵은 매판 랜덤이고 배치는 플레이어가 하므로 "goal 근처에 안 놓으면 된다"는 저작 해법은 쓰지 않는다.

### ③ 외력은 그대로 둔다

포털·토네이도·임펄스는 faction 을 안 보고 순찰병을 박스 밖으로 민다. 계약 6 이 수용한 것이고, unit 1 의 "박스 밖 시작" 처리가 다음 틱에 복귀시킨다. **여기서 외력에 예외를 심지 않는다.**

### ④ `CreatePatrolEntity(DefenderUnitData, int2 anchorCell, int tileRadius, Entity owner)`

`CreateDefenderEntity` 를 **재사용하지 않는다** — 그쪽은 `_defenderByTile` 등록과 `DefenderTile` 부착을 한다(배치 점유·재배치·사망 타일 이벤트를 끌고 들어온다).

| 분류 | 컴포넌트 |
|---|---|
| 진영·생명 | `FactionTag{Defender}` · `Health` · `IncomingDamage` · `CcEffect` · `DotEffect` · `ModifierStats` · `ModifierStatsDirty`(disabled) · `IncomingHeal` · `ShieldSlot` · `IncomingShield` |
| 식별 | `DefenderUnitTag` · `DefenderClassTag` (계약 1) |
| 전투 | `AttackState{targetMask=Enemy}` · `AttackOutputElement` 버퍼 |
| AI·이동 | `EnemyAiState` · `EnemyBehavior{engageMovement=Halt}` · `PathFollowState{speed}` |
| 거점 | `PatrolAnchor{cell, tileRadius}` · `PatrolStep` |
| 소유 링크 | `SummonedBy{owner}` — `Entity.Null` 이면 미부착(디버그 스폰) |

**안 붙이는 것**: `DefenderTile`(계약 1) · `AttackUnitTag` · `AggroCapacity` · `PendingDeployment`.
**안 호출하는 것**: `ApplyActiveDcEffectsTo`(계약 11).

anchor 는 `TryGetNearestWalkCell` 로 스냅한 값을 받는다. 스폰 위치 = anchor 셀 중심. 스냅 실패 시 `Entity.Null` 반환(호출자가 취소). 뷰는 `spineUnitPool.TrySpawn(unitData, unitData, entity, world, "SpineGar", out _)` — 기존 defender 경로와 동형이고 실패 시 quad 폴백도 동형.

### ⑤ 매치 경계 정리

`DestroyBattleEntities` 는 **타입 기반 파괴**다. 계약 1 의 `DefenderUnitTag` 부착으로 이미 포함되지만 회귀 방지를 위해 완료 기준에 명시 항목으로 둔다 — 같은 함수 주석에 사직서·AllyBuffField 캐리어가 누락돼 앱 수명 default world 에 잔존했던 사고가 두 번 기록돼 있다.

### ⑥ `PatrolDebugMenu`

선례 5종(`HazardDebugMenu`·`ObstacleDebugMenu`·`BlockingHazardDebugMenu`·`FatigueDebugMenu`·`RelocationDebugMenu`)과 동일 레시피: `#if UNITY_EDITOR` 로 파일 전체를 감싸고 → `[MenuItem(...)]` + 동일 경로 validate `=> Application.isPlaying` → `AssetDatabase.LoadAssetAtPath<DefenderUnitData>` → `FindAnyObjectByType<BattleBridge>()` → `bridge.DebugSpawnPatrolAt(so, cell, tileRadius)`(공개 API, `DebugSpawnHazardAt` 동형).

**거점 기준 셀을 마우스 커서로 잡지 않는다.** 메뉴 항목을 클릭하는 순간 커서는 메뉴 위에 있어서 게임 뷰 좌표가 아니다 — 기존 디버그 메뉴들이 쓰는 커서 레이는 이 이유로 사실상 폴백 상수 셀만 쓰고 있다(선례를 그대로 베끼면 같은 결함을 물려받는다).

대신 **배치된 방어유닛**을 기준으로 삼는다(`BattleBridge.DebugTryGetPatrolAnchorCell`). 실제 소환에서 거점이 소환사 셀이므로, 테스트가 진짜 경로를 그대로 흉내 낸다 — 원하는 자리에 방어유닛을 배치한 뒤 메뉴를 실행하면 거기가 거점이 된다. 여러 기면 `(y, x)` 오름차순 최솟값 하나(Dictionary 열거 순서는 보장이 없어 명시 정렬로 결정론 확보). 배치된 유닛이 없으면 보드 중심.

## 완료 기준

- [ ] 컴파일 통과 · 기존 EditMode 스위트 전량 통과
- [ ] Play 진입 → 메뉴로 순찰병 1기 스폰 → 뷰가 보이고 **위치가 따라 움직인다**
- [ ] 박스 안 적을 향해 cardinal 로 전진하고, 사거리에 들면 `Engaging`+`Halt` 로 정지해 교전한다
- [ ] 적이 구역을 벗어나면 anchor 로 복귀한다 — **단 그 적이 순찰병 사거리 밖일 때만**.
      `EnemyAiStateSystem` 은 구역을 모른다: 사거리(`AttackState.range`) 안에 적이 있으면
      `Engaging` 이 되고 `EngageMovement.Halt` 가 `PatrolStep.dir` 을 읽기 **전에** 멈춘다.
      따라서 실효 교전 반경 = `leashTileRadius + attackRange` 이고, 구역 경계 바로 밖에 선
      적과는 제자리에서 계속 싸운다. 이건 **의도로 채택한 거동**이다 — 막으려면 `AttackSystem`
      타겟 선정에 구역 필터가 필요해 스코프가 넓어진다(README 후속 후보).
- [ ] **벽으로 U자 막힌 박스에서 고착 없음**
- [ ] **goal 셀을 포함한 박스에서 `PastGoalTag` 가 붙지 않는다** (동결 없음)
- [ ] **아군 화염 장판 위에서 피해 안 받음** (unit 0 게이트 실검증)
- [ ] 적이 순찰병을 타겟으로 공격하고, 순찰병이 죽으면 뷰가 회수된다
- [ ] 오버헤드 체력바가 뜨고 이동을 따라간다
- [ ] **전투 종료 후 재진입 시 순찰병이 잔존하지 않는다**
- [ ] 적 이동(마칭·추격·standoff·보스 hunting) 육안 무회귀
- [ ] 콘솔 에러/경고 0
