# 2 — 맵·이동 (조각 A)

## 목적

전투 코어가 **맵 스냅샷을 받아 적을 골까지 걷게 한다.** 흐름장·통행층별 벽·장애물 재빌드·이동 결정 순서·평활화·충돌·분리·감지·어그로·도발 이동·거점 목적지·순찰·포탈·당김·순간이동·골 도달까지. 공격·피해는 unit 3 이지만 「사거리 안인가」 술어(`AttackReach.InReach`)는 이동의 정지 조건이므로 여기서 salvage 한다. 규칙 정본 = `ledgers/rules.md` 맵·이동 절(필수 17 · 보류 9 · 제거 4) + `census-map-movement.md`.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 맵 런타임 | `BattleCore/Map/`: `MapSnapshot`(plain — `tiles`·`placeMask`·`spawns`·`goals`·`waypoints`·`spawnRoutes`·`structures`·`bonusSpawns`·`size`·`tileSize`) · `MapRuntime`(`FlowFieldSet` · `NavGridSet` · `ObstacleSet` · `DefenderHuntField` · `PlacementOccupancy`) · `FlowSlot`(직접 인덱싱 금지 뷰) |
| 순수 함수 salvage(복사 후 `NativeArray`→배열, `Unity.Collections` 제거, 파일 헤더에 원본 경로 기록) | `Battle/Effects/FlowFieldBuilder.cs` · `TraversalSlots.cs` · `Battle/Movement/{NavGrid, MovementCellTrim, PathSmoothing, AgentCollision, Separation, SpawnSpread, FlowRecovery, WaypointProgress(WaypointRouting), GridMath}.cs` · `Battle/Effects/{PatrolAreaMath, PatrolStep}.cs` · `Battle/Movement/StructureChoice.cs` · `Battle/Combat/{DetectionChaseField, AggroChaseMath, AttackReach}.cs` · `Skills/SkillMath.cs` 는 이미 코어가 참조 가능 |
| 개체 부분 | `Unit.Move`(`MoveState`: radius · lastMoveDir · holdingGround · waypointProgress · chase 캐시) · `Unit.Detection` · `Unit.Aggro` · `Unit.Patrol` · `Unit.Footprint`(방어유닛) — nullable 부분 객체 |
| phase | `FieldPrepPhase`(장애물 시그니처 → 흐름장 부분 재빌드 · 사냥판 · 어그로 상태) · `AiMovePhase`(적 FSM 결정 → 도발 부여 → 거점 목적지 → 이동 → 분리 · 감지) — `TickPipeline` 의 빈 자리 2개를 채운다 |
| 이벤트 | `GoalReached{a, canSiege}` · `Detected{a, targetSimId(트레이스 전용), pos}` · `AggroAcquired` · `Blinked` · 트레이스 채널 |
| Unity 층 | `MatchDefinitionBuilder` 가 `MapStageScanner` 산출을 `MapSnapshot` 으로 접는다(스캐너는 무변) |
| 테스트 | `Tests/EditModeCore/Map*/Move*` — 옛 EditMode 순수 테스트(`AgentCollisionTests`·`SeparationTests`·`PathSmoothing*`·`FlowField*`·`WaypointRouting*`·`SpawnSpread*`·`PatrolArea*`·`StructureChoice*`·`DetectionChase*`)를 **복사·적응**(World 조립 없는 것만). 골든 `march_to_goal` · `detour_obstacle` · `detect_and_chase` |

## 구현

1. **층 두 축(M1).** 배치 `placeMask`(Ground·Path·Air) 와 통행 `cellLayers`(= `tiles` 에서 파생, Path·Air) 는 다른 축. **Ground 통행 슬롯은 만들지 않는다**(M23 — 현행 그대로 2층). `NavGridSet` 은 통행층 종류마다 벽+장애물 뷰를 재조립(M1·한-칸 메모).
2. **흐름장(M3).** `FlowFieldSet.Slot(dest, mask)` = (목적지 × 통행 마스크). 다중 소스 다익스트라, 옥타일 10/14, 코너컷 방지, 라벨 정정. 기하(`tileSize`·`size`·`origin`)는 `MapRuntime` 이 한 벌. **슬롯 없음 = loud**(예외/Report) — M2 fail-open 은 안 옮긴다(이식 제외).
3. **장애물.** `ObstacleSet` XOR 시그니처 + 개수 혼합 → 변경 틱에 흐름장 부분 재빌드. 배치 유닛(footprint 전 칸, M29 다칸)·길막 장판이 소스.
4. **이동 결정 순서(cM 「이동 결정 순서」 행 그대로).** 외력 합성 단일 지점(넉백·당김은 상태 무관) → 상태 갈림(`EnemyAi.Evaluate`: Marching/Engaging/Chasing/Standoff — 여기서 끝나는 상태는 포털·골·당김을 안 지난다) → 포털 → 셀·골 판정 → 당김 → 교전 정책 → 스텝 소스(**어그로 > 감지 > 웨이포인트 > 거점 > 골**) → 평활화(lookahead 24, 첫 후보 채택 유지 — M26 보류) → 충돌 trim(변위 상한 0.9칸 · 축분리 스윕 · 슬라이드 · 접선 속도 보존 · Skin 1e-3 = 코너 오프셋, M12) → (별도 패스) 분리.
5. **정지(holdingGround).** 케이스 열거가 아니라 결과 관찰: 진입 시 1, 실제로 움직인 지점에서만 0. CC 잠금도 접는다. 정지한 유닛은 밀어냄의 전진 성분을 거부.
6. **붙으러 가기(M4·M5).** 어그로·사냥 레인 둘 다 「도착했는데 못 쏜다」 보정(`TryCloseIn`): 거리장 없으면 스텝 0(fail-closed), 소스 영역(dist 0) 이탈 스텝 금지, `!locked` 게이트는 **두 호출부 각각**.
7. **분리(M13·M27).** 이동 뒤 별도 패스, 누적 먼저 적용 나중, **`SimEntityId` 오름차순 누적**(1 ULP 의존 닫음). 강도 0.5/틱·상한 반지름·소프트. 「프레임당」 상수는 고정 틱 기준으로 **의미만** 옮긴다(값 재검토는 이식 제외 표에).
8. **스폰 흩뿌림(M14).** `SpawnSpread.LaneFraction` — |오프셋| < 0.5칸 불변식. `spawnOrdinal` 파생은 후속 후보(현행 카운터 의미 유지).
9. **감지(M8·M9·M20).** `detectionRange` 0 = 없음 / >0 = 반경 / <0 = 무제한. 유한 = 대상 지향 추격판(`DetectionChaseField`, 키 = 대상 + 장애물 시그니처 — 「대상은 움직이지 않는다」 전제 명시) · 무제한 = 공용 사냥판(`DefenderHuntField`, R = 동시 헌터 사거리 **min fold**(M7), 도착지 추정). 네 박자 상수 1/2/5/6s 코드 상수 유지(표식 쿨 > 억제). CC·도약은 「막힘」이 아니다. 유출 면제(leak-proof)는 **무제한 감지 전용**. `Detected` 이벤트는 `hunting` 0→1 전이 1회.
10. **어그로·도발(M10).** 히트 획득은 unit 3 이 `AggroAcquire` 를 보낸다 — 여기서는 상태·추격판·해제만. 수용량 + 선점, 도발은 수용량·선점 우회. 리무버 둘(만료/해제 · 장애물 변경 무효화), 도발된 적은 필드만 떼고 어그로 유지. 어그로 sticky 배타성(가디언만 봄).
11. **거점 목적지(M18).** 「팰 수 있는 거점 중 최근접」, 웨이포인트 뒤. 동률 = 칸 사전순(`StructureChoice.IsBefore`) — 뷰 예고선이 같은 함수를 읽는다.
12. **순찰(M11).** 거점 박스 안 순찰, 마스크는 쓰는 쪽이 스스로 0 초기화. 골 판정 함께 갈아탐(박스 안 골 칸에 `PastGoal` 안 붙음). 소환사 사망 → 소환물 소멸(`SummonedBy`).
13. **포탈·당김·순간이동.** 포탈 입구 반경 → 출구 순간이동(다음 틱 흐름장이 방향). 당김은 이동을 대체하지 않는 가산 변위, 벽에 막힘, 판정은 원 + 피해자 몸(제약 13). 순간이동은 `Blink` 요청 → 이 phase 에서 위치 쓰기(위치는 이동이 소유).
14. **골 도달.** 1회 고정(`PastGoal` bool) → `GoalReached{canSiege = targetMask & DefenderCore}`. 돌격형은 unit 4 `HeartMeter` 가 소비해 스트레스 피해, 공성형은 살아서 거점을 팬다(unit 3).
15. **좌표.** 코어 좌표 = 격자 원점 0 의 평면(`float3`, y = 0). `BoardSpace` 는 뷰 소관(unit 5).

## 이식 제외

> 구현 뒤 실제로 안 옮긴 것으로 채웠다. **플레이 중 이상하면 이 표부터 본다.**

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 흐름장 슬롯 fail-open(`SlotFor` 폴백) | 미저작 목적지가 엉뚱한 길로 가는 것을 조용히 덮는다 → `FlowFieldSet.Slot` 이 **던진다** | 보류(M2) |
| `FlowFieldSingleton` 단일 구조·flat stride | Entities 산물. **배열의 배열 + `FlowSlot` 뷰**로 바꿔 stride 개념 자체를 없앴다 — 슬롯마다 길이 = 칸 수 배열을 통째로 든다 | 보류(M3) |
| `hunterLookup` 좀비·Burst lookup 존치 | 존재 이유(Burst 가 조용히 깨짐)가 통째로 소멸 | 제거(M19) |
| `LateralRecenter` | 실측 묘비, 되살리지 않음 | 제거(M15) |
| `MapDocument_MovementStress` 고아 에셋 | 이 unit 에서 손대지 않았다 — 삭제는 도구 처분표(unit 0)의 몫 | 제거(M21) |
| 분리 강도 「프레임당 0.5」의 **값** | 고정 틱 1/60 이라 「프레임당 = 틱당」이 같은 뜻 — **의미만** 옮겼다. 값 재검토는 플레이 뒤 | 보류(M13) |
| Ground 통행 슬롯 | 현행에 없음 — 만들지 않았다(`TraversalSlots.DefaultMask = Path`) | 보류(M23, 현행 유지) |
| 효과 타일 시드 -1 | 선정 **규칙**(소금 XOR · `\|1u` 가드 · row-major)만 옮겼다. 시드를 어디서 받을지는 맵 파이프라인 결정이라 미정 | 보류(M25) |
| 이동 가디언 추격판 재굽기 | 이동 가디언 저작 0. 전제(**「대상은 움직이지 않는다」**)를 `ChaseFieldCache` 헤더에 명시로 남겼다 | 보류(M6) |
| 감지 유지의 히스테리시스 폭 | **옛 값 0.1 그대로.** 옛 `DetectionSystem` 이 `TargetPersistence.KeepsLock`(0.1)을 **재사용**했으므로 감지 유지 = 공격 락 유지 = 같은 자다. unit 2 초판이 0.5 를 따로 들었던 것은 드리프트였고 unit 3 에서 `Combat.TargetPersistence.HysteresisTiles` 하나로 합쳤다 — `AiMovePhase.HysteresisTiles` 는 그 상수의 별칭이다 | 이식 |
| 감지 후보 탐침의 `EnemyTargetFilter.classMask` | 방어유닛 **클래스** 축이 정의표에 아직 없다(unit 4 의 저작). 진영·통행층 필터는 그대로 옮겼다 | 보류 |
| 보스 어그로 면역 | 티어·보스 태그가 unit 3 의 저작 축이다. 어그로 게이트 자리는 `AiMovePhase.GrantAggro` 에 이미 있다 | 보류 |
| 순찰 소환물의 **소환** 경로 | 소환 스킬이 unit 7 이다. `Patrol.SummonedBy` 와 「소환사 사망 → 소멸」 규칙은 옮겼다(`FieldPrepPhase.StepPatrol`) | 보류 |
| `AttackShapeBaked` 의 **저작·bake** | 도형 진입점(`InReachShaped`)은 옮겼지만 각도 → `(sin, cos)` bake 는 unit 3 | 보류 |
| 옛 EditMode 테스트의 `[Ignore]` 분리 순서 사례 | 새 코어가 그 축을 `SimEntityId` 오름차순으로 **닫았다**(M27). 닫힌 것을 증언하는 테스트로 대체 | 제거 |

### 복사·적응한 순수 테스트

| 옛 파일 | 새 파일 | 비고 |
|---|---|---|
| `GridMathTests` | `MapGridMathTests` | 반올림·`FlowStep`·`RangeToTiles` |
| `FlowFieldBuilderTests` | `FlowFieldBuilderTests` | `NativeArray`→배열, `CellQueue` 인자 추가 |
| `NavGridTests` · `MovementCellTrimTests` · `FillWalkMaskTests` | `NavGridAndTrimTests` | 셋이 같은 조립을 묻는다 |
| `AgentCollisionTests` | `AgentCollisionTests` | 코너 오프셋 단언을 「칸 반폭 + 반지름 + skin」으로 다시 적었다(M12) |
| `PathSmoothingTests` | `PathSmoothingTests` | 「첫 후보 무조건 채택」·반지름 가시선 |
| `SeparationTests` | `SeparationTests` | `[Ignore]` 순서 사례는 옮기지 않았다(위) |
| `SpawnSpreadTests` · `WaypointProgressTests` · `FlowRecoveryTests` · `AggroChaseMathTests` · `StructureDestinationTests`(선택 부분) | `MovePureMathTests` | 순수 함수 다섯을 한 파일로 |
| `PatrolAreaMathTests` | `PatrolAreaMathTests` | 스크래치 객체로 서명 변경 |
| `AttackReachTests` | `AttackReachTests` | 도형 케이스는 unit 3 에서 합류 |

## 필수 17 ↔ 검증 지점

| # | 규칙 한 줄 | 어디서 증언하나 |
|---|---|---|
| M1 | 놓을 수 있는 칸과 지나갈 수 있는 칸은 다른 축 | `NavGridAndTrimTests.통행_마스크는_칸_층과_슬롯_마스크의_교집합이다` · `MapSnapshot.Normalize`(배치 폴백은 `OpenPlacement`, 통행은 `Derive`) · `MovementRulesTests.배치_유닛이_장애물이_되고_퇴근하면_풀린다` |
| M4 | 「도착했는데 못 쏜다」를 두 레인이 각자 보정 | `AiMovePhase.TryCloseIn`(공유) + 호출부 둘(`StepChasing` · `TryHuntCloseIn`) · `MovePureMathTests.접근_보정은_지배축_cardinal_이다` |
| M5 | 거리장 없으면 안 뗀다 · 소스 이탈 스텝 금지 | `AiMovePhase.TryCloseIn`(fail-closed + `firingDist != 0` 거부) · `DetectionRulesTests.유한_감지는_방어유닛_앞에서_멈춘다` |
| M7 | 사냥 반경 = 헌터 사거리의 min fold | `DetectionRulesTests.사냥판_반경은_가장_짧은_사거리로_내려간다` |
| M8 | 발견한 대상 ≠ 걸어가는 목적지 | `DefenderHuntField`(진영 필터) ↔ `ReachProbe.IsLegalDetectionTarget`(타겟 마스크·통행층) · `DetectionRulesTests.무제한_감지는_유출_면제를_받는다` |
| M9 | 네 박자 상수 · 표식 쿨 > 억제 | `DetectionRulesTests.표식_쿨이_억제보다_길다` · `.발견은_전이_1회다` |
| M10 | 리무버 둘 · 도발은 필드만 뗀다 | `DetectionRulesTests.장애물이_바뀌면_어그로가_풀리고_도발은_표시만_남는다` · `.도발_시한이_지나면_풀린다` |
| M11 | 순찰 구역 마스크는 스스로 비운다 | `PatrolAreaMathTests.구역_마스크는_스스로_0_으로_시작한다` |
| M12 | 충돌 여유와 코너 오프셋은 같은 값 | `AgentCollisionTests.코너_조준_오프셋은_충돌_여유와_같은_값을_쓴다` |
| M13 | 밀어내기는 틱당 0.5 · 상한 반지름 · 소프트 | `SeparationTests.누적_적용은_상한을_넘지_않는다` · `.깊게_겹칠수록_세게_민다` · 골든 `march_to_goal` |
| M14 | 스폰 흩뿌림은 반 칸을 못 넘는다 | `MovePureMathTests.오프셋은_반_칸을_절대_못_넘는다` |
| M16 | 효과 타일 자리 뽑기의 결정론 3요소 | `EffectTileSelectTests.같은_시드면_같은_칸이다` · `.시드_0_도_판을_만든다` · `.배치_가능_칸만_뽑는다` |
| M18 | 거점 동률 = 칸 사전순, 코어와 예고선이 공유 | `StructureChoiceTests.칸_사전순이_동률을_가른다` · `AiMovePhase.SortStructures` |
| M20 | 무제한 사냥만 도착지를 추정한다 | `AiMovePhase.TryHuntCloseIn`(유한은 자기 대상, 무제한은 최근접 추정) · 골든 `detect_and_chase` |
| M22 | 맵↔덱 짝을 이름으로 추론하면 틀린다 | `MatchDefinitionBuilder.BuildMap` 이 **이름을 안 본다** — 스캐너 산출을 그대로 접는다(추론 지점 0) |
| M27 | 분리 누적은 `SimEntityId` 오름차순 | `MovementRulesTests.분리_누적은_id_오름차순이다` · `DeterminismTests.적_20기_군집도_두_실행이_같다` |
| M29 | 다칸 저작이 라이브다 | `PlacementOccupancyTests.다칸_점유와_주인은_항상_짝이다` · `ObstacleSetTests.다칸_점유를_통째로_막는다` · 골든 `detour_obstacle`(2×2) |

## 완료 기준

- [x] 헤드리스 `dotnet build/test` 초록. 복사한 순수 테스트 전부 통과(적응 목록은 위 표).
- [x] 골든 3종: `march_to_goal`(레인 2·적 6·골 1 — 레인 안에서 `GoalReached` 순서가 스폰 순서와 일치) · `detour_obstacle`(길목에 2×2 방어유닛 배치 → 흐름장 재빌드 → 우회, 교착 0) · `detect_and_chase`(유한 감지 적이 방어유닛 앞에서 정지, 표식 1회).
- [x] 결정론: 같은 정의표·스케줄 2회 트레이스 동일(분리 누적 정렬 포함 — 적 20기 군집).
- [x] 군집 통과 검산: 1칸 복도 적 20기 100초 교착 0(20기 전원 통과).
- [x] 규칙 분류표 맵·이동 「필수」 17 이 각각 테스트 또는 골든 시나리오에 매핑돼 있다(위 표).
- [ ] Unity EditMode lane 초록 — **에디터 열릴 때**(헤드리스 lane 은 초록).
- [ ] `core-reviewer` APPROVE.

### 구현에서 갈린 점 (기록)

- **`detour_obstacle` 의 적은 거점 전담이다.** 유닛을 노리는 적으로 두면 사거리에 든 순간
  교전으로 멈춰 서는데, 그 정지를 푸는 수단(전투)이 unit 3 에 있다 — 우회를 묻는 시나리오가
  교전을 묻게 된다. 라이브의 마음사냥꾼과 같은 저작이라 규칙을 왜곡하지 않는다.
- **레인 «사이» 도달 순서는 규칙이 아니다.** 입구마다 골까지 거리가 달라 섞이는 것이 정상이고,
  단언은 「같은 문에서 나온 순서는 뒤집히지 않는다」로 좁혔다.
- **`AttackShapeBaked` 를 이 unit 에서 가져왔다.** 도형 진입점(`InReachShaped`)을 「나중에
  쓸 거니까」로 빼 두면 그때 **원 항만 쓰는 복사본**이 생긴다 — 제약 13 이 막는 그 형태다.
