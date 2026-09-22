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

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 흐름장 슬롯 fail-open(`SlotFor` 폴백) | 미저작 목적지가 엉뚱한 길로 가는 것을 조용히 덮는다 → loud | 보류(M2) |
| `FlowFieldSingleton` 단일 구조·flat stride | Entities 산물. 배열의 배열 + `FlowSlot` 뷰 타입으로 직접 인덱싱 금지를 **타입**으로 | 보류(M3) |
| `hunterLookup` 좀비·Burst lookup 존치 | 존재 이유 소멸 | 제거(M19) |
| `LateralRecenter` | 실측 묘비, 되살리지 않음 | 제거(M15) |
| `MapDocument_MovementStress` 고아 에셋 | 삭제 후보(도구표) | 제거(M21) |
| 분리 강도 「프레임당 0.5」의 값 | 의미(소프트·상한 반지름)만 옮기고 값은 플레이 후 재검토 | 보류(M13) |
| Ground 통행 슬롯 | 현행에 없음 — 만들지 않음 | 보류(M23, 현행 유지) |
| 효과 타일 시드 -1 | 현행 그대로(같은 맵 = 같은 칸). 재결정은 플레이 뒤 | 보류(M25) |
| 이동 가디언 추격판 재굽기 | 이동 가디언 저작 0 | 보류(M6) |

## 완료 기준

- [ ] 헤드리스 `dotnet build/test` 초록. 복사한 순수 테스트 전부 통과(적응 목록을 이 파일에 기록).
- [ ] 골든 3종: `march_to_goal`(레인 2·적 6·골 1 — `GoalReached` 순서가 스폰 순서와 일치) · `detour_obstacle`(길목에 2×2 방어유닛 배치 → 흐름장 재빌드 → 우회, 교착 0) · `detect_and_chase`(유한 감지 적이 방어유닛 앞에서 `holdingGround`=1, 표식 1회).
- [ ] 결정론: 같은 정의표·스케줄 2회 트레이스 동일(분리 누적 정렬 포함 — 적 20기 군집).
- [ ] 군집 통과 검산(memory: 단독 통과 ≠ 군집 통과): 1칸 복도 적 20기 100초 교착 0.
- [ ] 규칙 분류표 맵·이동 「필수」 17 이 각각 테스트 또는 골든 시나리오에 매핑돼 있다(표를 이 파일 하단에).
- [ ] `core-reviewer` APPROVE.
