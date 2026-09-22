# 맵·이동 — 키워드 census

> READ-ONLY 조사. 모든 코드 포인터는 grep 으로 존재를 확인했고, 결정 이력은 spec README·handoff·
> `docs/spec/README.md` Follow-up Backlog 에서 직접 인용했다. 못 찾은 것은 「이력 미발견」으로 적었다.
> 조사 기준일 2026-09-22 · 브랜치 main.

---

## 1. 키워드 표 (71행)

| 키워드 | 한 줄 정의 (게임 언어로) | 현행 구현 포인터 | 판정 | 애매하면: 결정 이력 + 미결 요지 |
|---|---|---|---|---|
| 논리 맵 (GeneratedMap) | 한 판 동안만 사는 «칸 격자 + 자리들» 스냅샷 | `Data/GeneratedMap.cs` (`tiles`·`placeMask`·`spawns`·`goals`·`waypointCells/Ranges`·`spawnRoutes`·`structures`·`bonusSpawns`·`seed`·`generatorVersion`) | **확정** | — |
| 칸 종류 (Walk / Deco) | 걸을 수 있는 땅인가 장식인가. 디오라마에선 «차단 프랍이 덮였나» 하나로 갈린다 | `Data/MapTileType.cs` · `Data/MapStage/DioramaMapBuilder.Assemble` | **확정** | — |
| 배치 층 비트 (placeMask) | 그 칸이 **어떤 유닛에게 자리를 내주는가**. 칸 종류와 직교 | `GeneratedMap.placeMask` · `GeneratedMap.LayersAt` · `Data/PlacementLayer.cs` | **확정** | `placement-mask` units 0~4 완료 2026-08-07 |
| 배치 판정 단일 술어 | 「놓을 수 있나 = 칸이 연 층 ∩ 유닛이 선 층 ≠ 0」. 코드는 유닛 클래스를 **안 본다** | `GeneratedMap.PlaceableAt` · `BattleBridge.SpatialPlacementCheck:7357` | **확정** | 설계도 불변식 6 (`battle-core-architecture.md` §8) |
| 층 3종 (Ground/Path/Air) | 배치지면·경로·공중. 이름은 **공간 기준**이지 직업 기준이 아니다 | `Data/PlacementLayer.cs` · `PlacementLayers.Derive`/`Sanitize`/`CellBits` | **확정** | `placement-mask` unit 4 — 2026-08-07 사용자 결정 B-1 |
| 열린 칸의 기본 층 | 디오라마에서 안 막힌 칸은 Ground·Path·Air **셋 다** 연다 | `DioramaMapBuilder.OpenCellLayers` (= `Ground\|Path\|Air`) | **애매** | 배치용은 이 값인데 통행용 `cellLayers` 는 `Derive(Walk)` = `Path\|Air` 라 **Ground 가 빠진다.** 라이브에 Ground 통행 슬롯이 존재하지 않는데 `traversal-layers` unit 5 주석은 그 존재를 전제한다(MapDocument 시절 유산). 설계인지 디오라마 전환 부작용인지 **이력 미발견** |
| 스폰 (레인) | 적이 나오는 자리. `laneIndex` 는 **웨이브 결정론 키** | `Core/MapStage/SpawnMarker.cs` · `DioramaMapBuilder.StageSpawnPoint` | **확정** | 0부터 연속·중복 금지, 맵당 ≥2 (`map-stage-authoring.md` 형식 제약 2) |
| 골 / 마음 | 적이 도달하면 마음이 깎이는 자리. 기계는 1~4, 콘텐츠는 1 | `Core/MapStage/GoalMarker.cs` · `GeneratedMap.goals` · `FlowFieldSingleton.IsGoalCell` | **애매** | `multi-goal-map` 완료 2026-07-23 이 N개를 열었고, `map-rework` README 계약 3 이 **「마음 1개 통일 (사용자 결정)… 멀티골 기계는 건드리지 않는다 — 콘텐츠만 은퇴」**. 라이브 4스테이지 전부 `GoalMarker` 1개 실측 확인. 그런데 `wide-board-content` 선행 계약이 **「마음 N개 공유 체력」**(`heart-stress-axis/12_shared_heart_pool.md`, 사용자 결정 2026-08-25)을 다시 요구 — 재구축이 어느 쪽을 정본으로 삼을지 미결 |
| 웨이포인트 경로 | 맵이 「이 길로 와라」고 정한 경유점 열 | `Core/MapStage/RouteMarker.cs` · `GeneratedMap.waypointCells/waypointRanges` · `FlowFieldSingleton.WaypointAt` | **확정** | — |
| 레인 기본 경로 (spawnRoutes) | 그 입구로 나온 적의 기본 경로 번호 | `GeneratedMap.RouteForSpawn` · `SpawnMarker.routeIndex` | **애매** | 코드·검증 완료인데 **어느 맵도 저작하지 않았다**(전 맵 -1 = 골 직행). `enemy-movement-algorithm.md` §3: 「레인 기본 축은 코드·검증까지 끝났지만 어느 맵도 아직 저작하지 않았다」. 살릴 축인지 은퇴할 축인지 미결 |
| 경로 선택 우선순위 | 좁은 쪽이 이긴다: 적 SO > 웨이브 컨셉 > 레인 기본 > 골 직행 | `Movement/WaypointProgress.cs:62` `WaypointRouting.ResolvePathIndex` | **확정** | 삼항으로 안 푸는 이유 = 계약의 source of truth 를 EditMode 로 고정할 지점이 필요 |
| 웨이포인트 도달 판정 | 자기 칸 + 8이웃이면 지났다 | `WaypointProgress.ArrivalChebyshevRadius` (=1) | **확정** | 「튜닝 손잡이가 아니라 **격자 위상**」 — 저작 필드 노출 금지. 원래 정확 셀 일치였는데 스웜 20기에서 분리가 밀어내 어긋나 바뀜 |
| 거점 (본능) | 판 위의 공격 가능한 구조물. 3×3 **점유**(배치 배제)이되 통행은 안 막는다 | `Core/MapStage/StructureMarker.cs` · `Data/StructurePlacement.cs` (`StructurePlacements.FootprintOf`) | **확정** | 마음(Core)은 빌더가 거부 (`map-diorama-stage` 계약 11) |
| 보너스 포탈 칸 | 보너스 웨이브가 들어오는 문. 맵당 0개 또는 **정확히 2개** | `Core/MapStage/BonusSpawnMarker.cs` · `Data/MapGrid/BonusSpawnAuthoringRules.cs` · `GeneratedMap.bonusSpawns` | **확정** | `spawns` 와 절대 안 섞는다 — `spawns.Length` 는 레인 수이자 ExpandWave 라운드로빈 분모 |
| 차단 프랍 (PropFootprint) | 통행 + 배치를 함께 막는 사각형. **명시 선언이 정본**(시각 ≠ 논리 저작 허용) | `Core/MapStage/PropFootprint.cs` · `Data/MapStage/MapStageMath.FootprintCells` | **확정** | `map-diorama-stage` D6 |
| 배치 금지 구역 (PlacementBlockZone) | 통행은 그대로 두고 **배치만** 막는다. 「전선」 저작 수단 | `Core/MapStage/PlacementBlockZone.cs` | **확정** | 옛 placeMask 브러시의 후계. 둘로 나뉜 이유 = 배치/통행 직교 원칙 |
| 스폰·골 포탈 프랍 | 스폰/골 비주얼은 스테이지가 아니라 **공용 스타일 에셋 1장**에서 온다 | `Assets/_Project/Data/Maps/MarkerPropStyle.asset` · `MarkerPropInstaller.Apply` | **확정** | 라이브 풀 스테이지에 공용 프랍 내장 금지(Assets lane 이 막음). 맵 전용은 `visualRoot` 채우면 그쪽이 이김 |
| 스테이지 프리팹 = 맵 정본 | bake 없음. 배틀 진입 시 프랍을 셀로 양자화해 논리 맵을 그 자리에서 판다 | `Core/MapStage/MapStage.cs` · `MapStageScanner.Scan` · `DioramaMapBuilder.Assemble` | **확정** | `map-diorama-stage` 완료 2026-08-27 (구 파이프라인 68파일 은퇴) |
| 형식 제약 하드 실패 | 연결성·레인 번호·골 개수 등 8종 위반 = 배틀 진입 즉시 실패, **폴백 맵 없음** | `DioramaMapBuilder.Validate` · `Data/MapConnectivity.AllSpawnsReachGoal` · `BattleBridge:1305` | **확정** | `BuildFallbackLinear` 은퇴 (테스트 픽스처 빌더로만 잔존) |
| 맵 풀 삼중 짝 | 엔트리 = (스테이지, 덱, 플랜). 맵이 뽑히면 그 맵의 적 패턴이 함께 뽑힌다 | `Data/MapStage/MapStagePool.cs` (`entries` / `devEntries`) | **확정** | dev 슬롯은 `Count` 밖 → **시드 결정론에 불가시**. 라이브 4장(Duel·Street·StreetDay·Subway) |
| 맵 선택 순서 | dev 강제 > 디버그 시드 > 토너먼트 서버 시드 > 0번 | `BattleBridge.BuildMapForBattle:1271~1305` · `Data/MapGrid/MapPoolSelect.cs` | **확정** | 1순위가 PlayerPrefs(`DevMapOverride`)라 **머신 상태**가 샌다 — 골든 코퍼스 오염 전례(backlog 2026-09-04) |
| 토너먼트 맵 배정 | 같은 토너먼트 참가자 = 같은 (맵, 덱). `seed % count` | `MapPoolSelect.SelectIndexFromTournamentSeed` | **확정** | `tournament-seed-map-select` 완료 2026-07-23. 시드 6계열의 **예외 1번** |
| 효과 타일 | 배치 가능 칸 중 일부가 버프/디버프를 건다. **놓는 순간 1회, 회수 없음** | `Data/EffectTilePlacer.SelectCells` · `Data/EffectTileData.cs` · `BattleBridge.ApplyEffectTileOnce:9126` · `MapStage.suppressEffectTiles` | **애매** | 계약(Ground 층 고정·엔티티당 1회·회수 없음)은 확정. 다만 시드가 `GeneratedMap.seed` 인데 디오라마 맵은 **-1 고정** → 같은 맵이면 **매판 같은 칸**(설계도 §7 시드 예외 3번에 명시). 의도인지 전환 부작용인지 **이력 미발견** |
| 배치 판정 순서 | 페이즈 → 공간(footprint 전체) → 유닛 유효 → 풀 → 보드 상한 → 코스트 | `BattleBridge.CanPlaceDefenderAt:7459` | **확정** | 「구조 > 자원」 순서라야 트레이 표현(소진 > 쿨타임 > 코스트)과 일치 |
| Footprint | 앵커(min 코너) + W×H 둘만 저장. **«대표 셀» 은퇴** | `Data/FootprintMath.cs` (`Cells`/`GeometricCenterOffset`/`FootOffset`) | **확정** | 설계도 불변식 8. 짝수 변엔 중심 칸이 없다 — 대표 셀은 정수 나눗셈 동전 던지기였고 사거리를 반 칸 옮겼다 (`distance-based-range` 사용자 확정 결정 1, 2026-09-01) |
| 발밑 = sim 위치 | 유닛이 서는 점 = 하단 행 가로 중앙. 사거리 원점·몸 원·자기중심 폭심이 전부 이 점 | `FootprintMath.FootOffset` | **확정** | 2026-09-03 베이스 통일. Y 소팅도 이 값(중심으로 소팅하면 앞줄이 뒤로 들어간다) |
| 손가락 셀 | 드래그 손끝 = footprint 하단 행 가로 중앙. 유닛은 손가락 위로 자란다 | `FootprintMath.AnchorFromBottomCenter` | **확정** | `defender-footprint` unit 2 (요구 문서 5절) |
| 점유 쌍 | `_occupiedTiles`(칸 집합)와 `_defenderCellOwner`(칸→주인)는 **항상 함께** 갱신 | `BattleBridge:279`·`:285` · `OccupyDefenderFootprint:4201` / `ReleaseDefenderFootprint:4219,4241` | **확정** | — |
| multi-cell 저작 | 시스템은 살아 있고 **값만 전 유닛 1×1** | `DefenderUnitData.footprintWidth/Height` · `DefenderUnitData.Footprint` | **애매** | `defender-footprint` README: 「콘텐츠 저작은 전 유닛 1×1 로 되돌렸다(2026-08-30 사용자 결정)… 다시 켜려면 값만 올리면 된다(코드 변경 0)」. 캐논 2×2·말파이트 3×3·버스터즈 2×3·배스티온 2×1 저작 철회. backlog 「multi-cell 저작 재개 [S]」. 재구축이 다칸을 계속 지원할지 미결 |
| 자석 스냅 | 무효한 앵커면 반경 안 최근접 유효 칸으로. **row-major first-win** | `BattleBridge.TryFindNearestPlaceableAnchor` | **확정** | RNG 아닌 구조 결정론 (설계도 §7) |
| 배치 폐쇄 (CloseCellLayers) | 스폰·골·거점 칸은 판 시작에 `placeMask` 를 0 으로 닫는다 | `BattleBridge.CloseCellLayers:7590` | **확정** | 저작본 중 판 중에 바뀌는 유일한 맵 데이터 |
| 통행 층 (cellLayers) | 「이 칸을 어느 층이 지날 수 있나」. **`tiles` 에서만 파생**하고 `placeMask` 를 안 본다 | `Bridge/SimFieldInstaller.cs:103~122` · `PlacementLayers.Derive` · `FlowFieldSingleton.cellLayers` | **확정** | 실측 사고 기록: placeMask 를 통행 정본으로 삼았다가 `MapDocument_Test` 에서 **통로 23칸이 라우팅에서 사라지고 데코 7칸이 새 통로**가 됐다 (`traversal-layers` unit 1b 회귀 수리) |
| 통행 슬롯 정의식 | 「걸을 수 있다 ⇔ (칸 층 & 슬롯 마스크) ≠ 0」. 장애물은 포함 안 한다 | `Battle/Effects/TraversalSlots.cs` (`FillWalkMask`, `DefaultMask` = `Path`) | **확정** | 정의식이 여기 한 곳에만 있다 |
| 흐름장 슬롯 | 슬롯 하나 = (목적지 × 통행 마스크). flat stride 로 한 배열에 | `Battle/Effects/FlowFieldSingleton.cs` (`FlowSlot`/`DistSlot`/`SlotFor`/`MaskAt`/`DestinationAt`) · `SimFieldInstaller:150~255` | **확정** | 기하(tileSize/gridSize/origin)는 1벌, 라우팅만 N벌 |
| 경로 알고리즘 | 다중 소스 다익스트라 → 방향장. 옥타일 비용 10/14, 코너컷 방지, 라벨 정정법 | `Battle/Effects/FlowFieldBuilder.cs` (`CostOrtho`=10 / `CostDiag`=14 / `DiagonalAllowed` / `BuildFromSources` / `CollectDefenderSources`) | **확정** | 적별 A\*·NavMesh·Funnel·RVO 기각 사유가 `enemy-movement-algorithm.md` §6 에 전수 기록 |
| 벽 질의 (NavGrid) | 정적 벽 + 동적 장애물을 합친 **프레임 뷰**. 유닛 통행층마다 재조립 | `Movement/NavGrid.cs` · `MovementCellTrim.BuildNavGrid` | **확정** | 층 종류 수만큼만 재조립(한-칸 메모). 프레임당 하나였을 때 순찰병이 dir 을 받고도 안 움직였다 (`traversal-layers` unit 5) |
| 동적 장애물 | 배치 유닛·차단 해저드가 칸을 막으면 흐름장을 다시 굽는다 = 「막으면 돌아간다」 | `Effects/ObstacleSingleton.cs` · `ObstacleSignature.Compute` · `ObstacleLifetimeSystem` · `FlowFieldRebuildSystem` · `FlowFieldSingleton.blockedSignature` | **확정** | XOR 시그니처(순회 순서 무관) + 개수 혼합 |
| 이동 결정 순서 | 외력 합성 → 상태 갈림 → 포털 → 셀·골 판정 → 당김 → 교전 정책 → 스텝 소스 → 평활화 → 충돌 trim → (별도 패스)분리 | `Movement/MovementSystem.cs` (791줄) · `enemy-movement-algorithm.md` §3 | **확정** | 상태 갈림(Standoff/Chasing)은 **여기서 끝난다** — 포털·골·당김을 안 지난다 |
| 스텝 소스 우선순위 | 어그로 > 감지 > 웨이포인트 > 거점 > 골. 위가 아래를 **잠시 덮는다** | `MovementSystem:481~560` | **확정** | 뒤집으면 맵 저작이 조용히 무시된다. 순찰 소환물은 이 줄 밖 |
| 외력 합성 단일 지점 | 「멈춤」 = 자기 변위 0. 넉백·당김은 **상태와 무관하게** 적용 | `MovementSystem.ComposeMove` (파일 말미) | **확정** | 복사본 7벌 시절 교전·도발·순찰·고립이 통째로 넉백 면역이었다 (`defender-knockback-on-impact` unit 2) |
| 정지 표식 (holdingGround) | 「시뮬이 이 유닛을 멈췄다」. **케이스 열거가 아니라 결과 관찰**(진입 시 1, 실제로 움직인 지점에서만 0) | `Movement/PathFollowState.holdingGround` · `MovementSystem:~150` | **확정** | CC 잠금도 함께 접는다. 소비: 정지한 유닛은 겹침 밀어냄의 **전진 성분을 거부** |
| 평활화 (string pulling) | 흐름장이 8방향이라 꺾이는 걸 전방 가시점 직행으로 편다 | `Movement/PathSmoothing.cs` (`TryStepTarget`, `DefaultLookahead`=24, `TryCornerAim`) | **애매** | 문서가 스스로 **「첫 후보 무조건 채택은 평활화의 불변식이 아니라 구멍을 막은 자국」**이라 적는다. 조준 진동도 미해소(13.4타일 주행 총회전 1161° 실측, 감사가 TangentBug 계열로 분류) — `enemy-movement-algorithm.md` §7 |
| 충돌 해결 | 축분리 스윕 + 슬라이드 + **접선 속도 보존**(프레임 변위 크기 복원) | `Movement/AgentCollision.cs` (`Resolve`, `Skin`=1e-3, `PreserveTangentialSpeed`) | **확정** | 판정 형상은 원이 아니라 **변 2r AABB** (명명 정정 기록 있음) |
| 프레임 변위 상한 | 0.9타일 — 터널링 차단 | `MovementCellTrim.ClampDisplacement:123` | **확정** | — |
| 겹침 해소 (분리) | 이동 **뒤** 별도 패스. 누적 먼저, 적용 나중(야코비 1회) | `Movement/AgentSeparationSystem.cs` · `Separation.cs` (`DefaultStrength`=0.5 **프레임당**, 상한 = 반지름, `PairPush`, `RejectForwardPush`) | **애매** | float 결합법칙 부재로 **누적 순서에 1 ULP 의존** — 전체 리플레이는 안전, 스냅샷 부분 재시뮬은 위험. 해소(stable id 정렬)는 `battle-sim-extraction` unit 1 소관으로 **미착수**. `SeparationTests` 에 `[Ignore]` 실패 사례 보존 |
| 몸 반지름 0.25 | 통과 여유 < 밀어냄 폭이면 교착. **군집 통과로 검산한다**(단독 통과 아님) | `BattleBridge.agentRadiusTiles:56` (0.25) → 스폰 시 `PathFollowState.radius` 주입 | **확정** | 0.35→0.25 로 6맵 100초 교착 소멸 (`continuous-agent-movement` unit 12) |
| 스폰 측면 분산 | 같은 문에서 나와도 겹치지 않게 옆으로 벌린다. RNG 없는 N-레인 round-robin | `Movement/SpawnSpread.cs` (`LaneFraction`, `MaxHalfFraction`=0.49, `FractionRange`) | **확정** | \|오프셋\| < 0.5타일 = **셀 침범 방지 불변식**. 평활화 도입 후 `LateralRecenter` 가 은퇴해 분산이 유지된다 |
| 밀려난 뒤 복구 | 넉백으로 길 없는 칸에 떨어지면 4이웃 최소 거리로 내려간다 | `Movement/FlowRecovery.RecoveryDir` | **확정** | — |
| 감지 반경 | 「반경 안에 때릴 수 있는 방어유닛이 있고 **내 층으로 갈 수 있으면**」 그쪽으로 | `Combat/DetectionRange.cs` (`tiles`, `Unlimited => tiles < 0`) · `Combat/DetectionSystem.cs` | **확정** | `enemy-detection-range` 완료 2026-09-08 (units 0~9). **부착 자체가 게이트** — `detectionRange == 0` 이면 컴포넌트를 안 붙인다. 규칙에 **비행 분기가 없다**(계약 13) |
| 감지 유지 3규칙 | 유지(경계 흔들림 방지) · 관성(대상 잃어도 잠깐 더) · 막힘 해제(못 가면 놓고 한동안 안 봄) | `Combat/DetectedTarget.cs` (`hunting`/`graceRemaining`/`stuckSeconds`/`suppressRemaining`/`markCooldown`) · `DetectionSystem` 상수 4종 | **확정** | CC·도약은 「막힘」이 아니다(남이 묶은 걸 자기 실패로 세면 재운 사이 감지가 풀린다). 막힘 해제는 **무제한 감지에 적용 안 함** |
| 대상 지향 추격판 | 유한 반경 감지는 **그 대상까지 내 층으로** 구운 적별 필드를 따른다 | `Combat/DetectionChaseField.cs` (`DetectionChaseDist`/`DetectionChaseFlow`) · `DetectedTarget.chaseBuiltFor`/`chaseSignature` | **확정** | unit 8. 이전엔 공용 필드라 도착지가 **실측 5.0%** 갈렸고 **비행이 벽 위에서 조용히 죽었다**(그게 「비행은 감지 대상 밖」으로 오독됐다) |
| 공용 사냥판 | 무제한 감지(보스·보너스)는 「아무 방어유닛이나」라서 공용 필드가 맞는 답 | `Effects/DefenderFieldSystem.cs` · `DefenderFieldSingleton.cs` | **애매** | **층별 슬롯이 없다** — 아직 지상 마스크(`goalField.walkMask`)로만 굽는다. backlog 「사냥판 층별 슬롯」: 「오늘 무제한 저작 4종이 전부 지상이라 무해하지만, **비행 무제한 사냥꾼을 저작하는 날** 필요해진다」 |
| 유출 면제 (leak-proof) | 골 칸을 밟아도 유출 안 함 = **무제한 감지 전용** | `MovementSystem:~330` (`_detectionRangeLookup[...].Unlimited && huntField.dist[idx] != MaxValue`) | **확정** | `enemy-detection-range` 계약 9. 유한 감지에 상속시키면 감지가 **유일한 패배 통로**(골→마음 HP→스트레스 100→남은 시간 몰수)의 조절기가 된다. `hunting` 에 묶지 않는 이유도 명시(리뷰 H2) |
| 어그로 | 가디언이 **히트로** 획득. 수용량 + 선점(먼저 온 쪽이 이김) | `Effects/Aggroed.cs` · `AggroCapacity.cs`(max/held, 매 틱 full recompute) · `AggroStateSystem.cs` Pass 1~4 · `AggroAcquireEvents.cs` | **확정** | `Aggroed.remainingTime` 0 = **무기한 sentinel**(픽스처 8곳 보호, 뒤집지 말 것) |
| 도발 | 같은 채널인데 **수용량과 선점 둘을 우회**한다(나중에 부른 쪽이 이긴다) | `AggroStateSystem.cs:140~210` · `Effects/TauntAttackGranted.cs` | **확정** | `on-place-skill-rework` unit 3. 「집단 도발」이 수용량 저작과 무관하게 성립하는 근거. 선점 게이트를 **kind 별로** 가른다 |
| 어그로 추격판 | 가디언 인접 칸까지의 적별 거리장. 획득 시 **1회** 굽는다 | `Effects/AggroChaseCell.cs` · `Combat/AggroChaseMath.BuildChaseField:48` | **애매** | 동적 해저드가 나중에 길을 막아도 **재판정 안 한다** — backlog 「동적 해저드의 chase 경로 무효화 [S]」(aggro-tile-chase 종료 이관 2026-07-20) 미착수. 같은 그룹에 「cell-trim wall-slide [S]」·「대각 코너 슬립 차단 [S]」·「`aggroAttackRange` 전 적 1 고정 [S]」 |
| 발견 표식 | 적이 방어유닛을 **처음** 찾은 순간에만 1건 (`hunting` 0→1 전이) | `Combat/DetectionEvents.cs` · `DetectedTarget.markCooldown`(6초) | **확정** | 페이로드의 `targetSimId` 는 **트레이스 전용** — 화면이 그 대상을 가리키면 무제한 감지에서 거짓말이 된다 |
| 골 도달 | 1회 고정. 돌격형 = 마음 깎고 소멸(유출) / 공성형 = 살아서 거점을 팬다 | `Movement/PastGoalTag.cs` → `Units/UnitLifecycleSystem.cs:77~98` → `Units/GoalReachedEvent.canSiege` | **확정** | `canSiege` = `targetMask & DefenderCore` |
| 골 안정도 | 마음이 체력을 갖고 0 이면 붕괴 → 그 골이 유출 지점으로 전환 | `AttackDeck.goalStabilityMax`(기본 1500 · 라이브 `Deck_Duel` = 1500) · `BattleBridge.SyncGoalStability:6926` · `GoalCollapsedEventsSingleton` · `Core/StressMath.cs` | **애매** | `goal-stability` 완료 2026-08-04 이나 backlog 에 **「실맵 `goalMaxStability` 콘텐츠 값 결정」**(검증용 임시값 미커밋)과 **「붕괴 골을 적 라우팅에서 제외 [M]」**(2026-08-12 발견, **사용자 결정 B — 착수 보류**: 맵 개편으로 멀티골 은퇴 예정이라 시인성 보강만)이 남아 있다. 골 목적지가 빌드 시 고정이라 멀티골에서 부서진 골로 계속 간다 |
| 거점 목적지 | 「내가 팰 수 있는 거점 중 가장 가까운 것」. **웨이포인트보다 뒤**에 온다 | `Movement/StructureDestination.cs` · `StructureChoice.cs` (`IsBefore` 사전순 타이브레이크) · `StructureDestinationSystem.cs` | **확정** | 웨이포인트는 맵의 계약, 거점 선택은 그 안의 전술 — 뒤집으면 저작이 조용히 무시된다 (`instinct-content` unit 3) |
| 순찰 소환물 이동 | 자기 거점 박스 안에서만 돈다. **골 판정도 함께 갈아탄다** | `Effects/PatrolStep.cs` · `PatrolAreaMath.cs` · `PatrolFieldSystem.cs` · `Movement/PatrolAnchor.cs` | **확정** | 골 칸이 박스 안에 들어와도 `PastGoalTag` 가 안 붙는다(3중 동결 방지 — 영구 동결·파괴 불가·소환사 재소환 불가) |
| 포탈 텔레포트 | 입구 반경에 들어오면 출구로 순간이동. 다음 프레임 흐름장이 방향을 준다 | `Effects/PortalLink.cs` · `MovementSystem:~355` | **확정** | `exitWaypointIndex` 는 Phase 9 에 제거 |
| 회오리 당김 | 이동을 **대체하지 않는** 후처리 가산 변위. 벽/장애물에 막힌다 | `Effects/TornadoField.cs` · `MovementSystem:~415` (`SkillMath.ReachFromCell`) | **확정** | 판정은 원 + 피해자 몸(`HitRadius`) — 셀 양자화 소멸 (`distance-based-range` unit 18) |
| 도약 (보스·궁극기) | 심은 **즉시 텔레포트**하고 **뷰만** 아치로 난다 | `Movement/BlinkRequestEvents.cs` · `BlinkApplySystem.cs` · `Combat/LeapFlight`(잠금) | **확정** | 착지 슬램의 형은 「자리에 떨어지는 것」(2026-09-07 사용자 결정 — 운석과 같다) |
| 좌표 변환 | 심은 격자 원점 0 의 평면 좌표. **sim-Y 를 화면 세로에 더하지 않는다** | `Core/BoardSpace.cs` (`Configure`/`ToView`/`ToSim`/`ToViewVector`/`RaycastPlane`/`IsConfigured`) | **확정** | 안 지나면 스테이지마다 최대 1.95칸 어긋남. identity 폴백 모드는 `legacy-render-removal` unit 3 에서 제거 |
| 격자 수학 | 월드→셀은 `floor(x/t + 0.5)` + clamp. clamp 없는 판이 따로 있다 | `Movement/GridMath.cs` (`WorldToCell`/`WorldToCellUnclamped`/`CellToWorldCenter`/`CellIndex`/`ChebyshevDistance`) | **확정** | `ChebyshevDistance` 는 **격자 계층 전용** — 사거리 판정에 쓰지 않는다(`distance-based-range` unit 9) |
| 매치 시드 6계열 | 시드 1 → 맵·웨이브·뷰·픽업·기믹·메테오로 salt 분리. 0 을 반환하지 않음 | `Core/MatchSeed.cs` (`DeriveMapSeed`/`DeriveWaveSeed`/`DeriveVisualSeed`/`DerivePickupSeed`/`DeriveGimmickSeed`/`DeriveMeteorSeed`) | **확정** | 예외 5건은 설계도 §7 에 명시(토너먼트 서버 시드·DC 큐 원값·효과 타일 -1·dev 슬롯 불가시·구조 결정론) |
| 스테이지 좌표 관례 | 프리팹 루트 = 원점·무회전·스케일 1, `gridOriginLocal.xz = 0`. **아트를 격자에 맞춘다** | `BattleBridge:1315~1322` · `map-stage-authoring.md` 「좌표 관례 (2026-08-25)」 | **확정** | 결과: 전투 카메라 포즈가 (`playAreaCells`, 화면비)만의 함수 |
| 판 크기 정책 | 현재는 판 전체를 한 화면에 우겨넣는다(라이브 Duel 23×10) | `docs/spec/wide-board-camera/README.md` · `wide-board-content/README.md` · `squad-slots-ten/` | **애매** | 둘 다 **착수 전 초안 2026-08-25**. 팬·홀드-투-피크 오버뷰·36×14 저작이 대기 중. 탐색 튜닝(pitch/fov/후처리)은 검증 없이 `wip/wide-board-camera` 브랜치에 격리 |
| 폭1 협곡 은퇴 | 「직선 복도는 폭1 금지」 → **unit 7 에서 뒤집힘**(폭2 로 다 바꿨더니 근접 유닛이 판에서 사라졌다) | `docs/spec/map-rework/README.md` 계약 1 · `0_width_concept_guards.md` · `7_melee_lanes.md` · `Data/MapGrid/MapConceptRules.cs` | **애매** | 사용자 실측 2026-08-12: 「은퇴시킨 것은 「폭1」이 아니라 **「단방향」**」. **더 큰 문제**: `map-rework` 가 재저작하려던 5맵(Serpent·Coil·Twin·Spiral·Zig)은 MapDocument 맵이고 **그 파이프라인이 통째로 은퇴했다**(`map-diorama-stage` unit 12, 2026-08-26 — `Prefabs/Maps` 11종 삭제, PlayMode 기본판 Serpent→Street). units 8~13 은 **대상이 없는 사문**이다 |
| waveSeed 불변 | 맵 지형을 고쳐도 웨이브 시드는 안 건드린다. 난이도는 knob 으로 | `map-rework/README.md` 계약 8 · `docs/reference/map-wave-balancing.md` | **확정** | 맵 타일과 웨이브는 시드가 분리돼 decorrelated |

---

## 2. ECS 고유라 새 설계에서 개념 자체가 사라지는 것

각 항목은 **사라지는 껍데기**와 **다른 형태로 살아남아야 할 규칙**을 함께 적는다.

1. **싱글턴 컴포넌트 3종** — `FlowFieldSingleton` · `DefenderFieldSingleton` · `ObstacleSingleton`.
   살아남을 규칙: 경로 필드는 **판당 1벌을 모두가 공유**한다(적별 A\* 아님) · 수명이 맵 빌드~판 종료로 묶인다 ·
   `walkMask` 같은 공유 배열의 **소유자는 정확히 하나**다(두 곳이 들면 이중 해제로 죽는다 — 실제 주석 경고).

2. **flat stride 슬롯 배열** — `NativeArray<NativeArray<T>>` 가 불법이라 `[slot * CellCount + cell]` 로 접었다.
   순수 C# 에선 배열의 배열이면 끝난다. 다만 **「직접 인덱싱 금지, `FlowSlot`/`DistSlot` 뷰로만」**이라는
   규율이 지금은 **주석**으로만 막혀 있다 — 타입으로 대체하지 않으면 「슬롯이 늘어나는 순간 조용히 다른
   슬롯을 읽는다」가 재발한다.

3. **`DynamicBuffer` 적별 필드** — `AggroChaseCell` · `DetectionChaseDist/Flow`.
   살아남을 규칙: **캐시 키**다. 「어느 대상까지 / 어느 장애물 상태로 구웠나」(`chaseBuiltFor` + `chaseSignature`).
   이게 없으면 그리드 전체 BFS 를 사냥 중인 적 전원이 매 프레임 돌린다(Android 실질 비용).
   전제도 함께 옮겨야 한다 — **「대상(방어유닛)은 움직이지 않는다」**. 이동하는 방어유닛 저작이 생기면
   두 추격판을 같이 고쳐야 한다.

4. **`ComponentLookup` / Burst 제약** — 설계도 불변식 14(「`OnUpdate` 의 `GetComponentLookup` 을 지우면
   Burst 가 조용히 깨진다」, 프로젝트 재발 5회). **전부 소멸**. `MovementSystem` 이 소비처 0 인
   `hunterLookup` 을 일부러 남겨 둔 것도 이 이유이고, 재구축에선 **삭제해야 할 유물**이다.

5. **`NativeQueue` 맥락 채널** — `BlinkRequestEvents` · `GoalReachedEvents` · `GoalCollapsedEvents` ·
   `DetectionEvents` · `AggroAcquireEvents`.
   살아남을 규칙: **「위치는 Movement 가 소유하니 남은 요청으로 보낸다」**는 소유권 계약.
   채널이 사라지면 아무 데서나 좌표를 대입하게 된다.

6. **`ECB` 구조 변경 = 태그 부착** — `PastGoalTag`.
   순수 C# 에선 bool 한 줄. 살아남을 규칙: **「골 도달 판정은 1회 고정」**이고 그 뒤로 이동 루프에서
   빠진다. 그리고 그 태그가 **다른 시스템의 파괴 조건**(`UnitLifecycleSystem`)과 짝이라는 점.

7. **아키타입 게이트** — `detectionRange == 0` 이면 `DetectionRange` 컴포넌트를 **안 붙인다**(「분기 하나
   대신 아키타입으로 가른다」). 순수 C# 에선 그냥 분기가 되므로, 규칙 문장(**「감지 0 = 오늘과 같은 경로」**)을
   명시로 남기지 않으면 의미가 소멸한다.

8. **청크 순회 순서** — 분리 누적의 1 ULP 의존 · `StructureChoice` 타이브레이크 · 대상 선정 순위가
   전부 여기 기대고 있다. 순수 C# 에선 **`SimEntityId` 정렬로 닫을 수 있고, 닫아야 한다**
   (`battle-sim-extraction` unit 1 이 이미 그 축을 소유).

9. **`[UpdateBefore]`/`[UpdateAfter]` 순서 속성** — 프레임 순서가 **계약**인 지점이 최소 6개다:
   `ObstacleLifetime` → `FlowFieldRebuild` → `EnemyAiState` → `Detection` → `PatrolField`/`DefenderField` →
   `Movement` → `AgentSeparation`. 속성이 사라지면 이 순서를 **호출 순서로 명시**해야 하고,
   특히 **「분리가 이동 뒤에 도는 것이 계약」**과 **「`DetectionSystem` 이 같은 프레임에 버퍼를 재생해
   `MovementSystem` 이 본다」**는 잃으면 즉시 버그가 된다.

10. **`IsCreated` 픽스처 보호 불변식** — `goals`·`walkMask`·`cellLayers`·`maskValues`·`bonusSpawns` 를
    일부러 `IsCreated` 불변식에서 뺐다(직접 초기화 픽스처 수십 개가 뒤집히는 걸 막으려고).
    순수 C# 에선 null/옵션이 같은 일을 하지만, **「미생성 = 폴백 동작」**이라는 의미가 조용히 사라지기 쉽다.

---

## 3. 코드에만 박혀 있고 문서에 없는 규칙 (rebuild 가 놓치기 쉬운 것)

- **열린 칸의 배치 마스크 ≠ 통행 마스크.** `DioramaMapBuilder.OpenCellLayers` 는 `Ground|Path|Air` 인데
  `SimFieldInstaller` 의 `cellLayers` 는 `PlacementLayers.Derive(Walk)` = `Path|Air` 다.
  **Ground 는 배치 전용 비트이고 라이브에 Ground 통행 슬롯이 존재한 적이 없다.** 문서는 「직교」라고만 쓴다.
- **`SlotFor` 완전일치 실패 = primary 슬롯 폴백.** 목적지·마스크가 안 맞으면 예외가 아니라
  **조용히 다른 슬롯**을 준다(`FlowFieldSingleton.SlotFor` — "현행 안전망").
- **흐름장의 기하는 1벌, 라우팅만 N벌.** 소비처 15곳 중 11곳이 라우팅을 안 읽고 `tileSize`/`gridSize`/`origin`
  만 읽는다 — 그래서 싱글턴을 안 쪼갰다(쪼개면 `GetSingleton` 이 2개 매치에서 throw).
- **「도착했는데 못 쏜다」 보정이 2곳**(어그로 레인 · 사냥 레인). 추격 필드 소스는 **셀 디스크(체비셰프,
  `CollectDefenderSources`)** 인데 발사 판정은 **월드 원**이라 모서리에서 dist 0 + 사거리 밖 = **영구 동결**이
  난다(사거리 1 기준 실측 2.05칸 vs 도달 1.5칸). `TryCloseIn` 은 기계장치만 공유하고 **`!locked` 게이트는
  호출부가 각자** 건다 — 한쪽만 걸어 잠긴 헌터가 걷는 결함이 실제로 났다.
- **`TryCloseIn` 은 fail-closed** — 거리장이 없으면 스텝을 아예 안 취한다. 그리고 **소스 영역(dist 0)
  이탈 스텝은 취하지 않는다**(왕복 방지). 단 외력은 이 불변식 밖.
- **가디언이 움직이면 추격 필드가 stale 해져 적이 「옛 디스크 가장자리에서 정지」한다.** 근본 수정(셀 변화 시
  재굽기)은 **없다** — 오늘 이동 가디언 저작이 0종이라 도달 불가.
- **`DefenderFieldSystem` 의 R = 동시 헌터 사거리의 min fold.** 이질 사거리 헌터가 동시에 살아 있으면
  짧은 쪽으로 내려간다(보너스 당기기 근접 잡몹 10기 + 원거리 보스가 그 조건).
- **`DefenderFieldSystem` 의 소스 수집은 faction 필터 하나뿐**인데 감지는 legal 필터(`targetMask`·통행층·
  `classMask`)를 지난다. **같지 않은 것이 정상**이고 그 차이가 「감지 대상 ≠ 이동 도착지」다.
  부작용: 최근접이 **못 때리는** 방어유닛이면 그쪽으로 다가가 눌러붙을 수 있다(선재 결함 상속, 후속 후보).
- **감지 상수 4종이 코드 상수다**: grace 1s / 막힘 해제 2s / 억제 5s / 표식 쿨 6s.
  `sceneKnobs` 에 **일부러 안 올렸다** — 올리면 `configHash` 가 움직여 골든 red 가 「조건 드리프트」로 읽힌다.
  그리고 **표식 쿨(6) > 억제(5)** 관계 자체가 계약이다(리뷰 L5).
- **`Aggroed` remover 가 둘이다** — `AggroStateSystem`(해제·만료)과 `FlowFieldRebuildSystem`(장애물 변경
  무효화). 컴포넌트 주석의 「AggroStateSystem 만」은 이미 stale 이었다.
  단 **도발된 적은 필드만 떼고 `Aggroed` 를 남긴다**(1회성이라 재획득 경로가 없어 통째로 풀면 도발이 사라짐).
- **`PatrolAreaMath.FillAreaMask` 가 버퍼를 스스로 0 초기화한다.** 호출자 계약으로 뒀더니 순찰병 **2기
  이상에서만** 재현되는 「거점을 벗어나 걸어나감」 버그가 났다.
- **`AgentCollision.Skin`(1e-3)을 `PathSmoothing` 코너 꼭짓점 오프셋과 공유**한다.
  갈리면 「조준한 자리에 실제로 설 수 없다」가 된다.
- **`Separation.DefaultStrength` 0.5 는 프레임당**이고 상한은 반지름이다(dt 무관). 소프트 분리 —
  **관통을 하드 블록하지 않는다**(1타일 복도에서 하드 블록은 교착).
- **`SpawnSpread` 의 \|오프셋\| < 0.5타일 불변식** — 넘으면 스폰 칸을 침범해 `WorldToCell`·골 판정·
  cell-trim 이 다른 칸으로 본다.
- **`LateralRecenter` 가 은퇴한 자리에 주석 묘비가 있다**(`MovementSystem` 말미). 실측: 대각 주행 중
  좌우 꺾임 19회·총회전 624°·단일 프레임 최대 43.9°. **되살리지 말 것**.
- **`EffectTilePlacer` 의 `SeedSalt` XOR + `|1u` 0-seed 가드 + partial Fisher-Yates** — 프랍 배치
  (`BackgroundPropPlacer`)와 decorrelate 하려는 것. 수집은 row-major 라 결정론.
- **`MapStagePool.devEntries` 는 인덱스 공간 `[Count .. Count+DevCount-1]` 로 이어붙는다** —
  별도 축이 아니라 같은 정수 축의 뒤쪽이다(`BattleBridge:1272~1276`).
- **`StructureChoice.IsBefore`(셀 사전순)를 sim 과 예고선이 공유**한다. 한쪽만 정렬하면 동률에서만
  간헐 재현되는 「가이드 ≠ 실제 이동선」이 된다.
- **`MovementSystem` 이 `hunterLookup` 을 소비처 0 인데도 유지**한다 — 지우면 Burst 가 깨진다.
  주석이 「다른 시스템이 그 태그를 쓰니까는 근거가 아니다」라고 명시(리뷰 L4).
- **`DetectionSystem` 의 legal 필터와 `MovementSystem` 의 최근접 추정이 다르다** — 무제한 사냥은
  도착지가 특정되지 않아 「소스 칸을 만든 것이 그중 하나」로 **추정**한다. 유한 감지만 추정하지 않는다.
- **고아 에셋**: `Assets/_Project/Data/Maps/MapDocument_MovementStress.asset` 이 이미 삭제된
  `Wassup.Data.MapGrid.MapDocument` 스크립트 GUID(`ee23ad58b8ffa4808bd0fd9ad5c00ad0`)를 가리킨다 —
  `class MapDocument` 는 코드베이스에 **없다**. 최근 커밋 `dd2b9edd` 로 들어왔다. 참고용 저작 기록인지
  정리 대상인지 판단 필요.
- **라이브 맵↔덱 배정이 이름과 안 맞는다**: Street = `Deck_Serpent` · Subway = `Deck_Zig` ·
  StreetDay = `Deck_Coil` · Duel = `Deck_Duel` (2026-08-26 사용자 결정 ⓑ, 현행 세대 맵 덱 재배정).
  덱 이름이 은퇴한 옛 맵 이름이라 **덱 이름으로 맵을 추론하면 틀린다**.

---

## 4. 이 영역에서 rebuild 가 결정해야 할 열린 질문 (5)

1. **맵 정본을 엔진에서 떼어낼 것인가.**
   지금 맵의 정본은 **Unity 프리팹**이고, 논리 격자는 배틀 진입 시 `MapStageScanner` 가 씬 계층을 훑어
   그 자리에서 파생한다(bake 없음 = 이 파이프라인의 강점). 엔진-프리 sim 은 `GeneratedMap` 같은
   plain 스냅샷만 있으면 도는데, 그 스냅샷을 **누가 언제** 만드는지가 정해져야 한다 —
   에디터에서 export 해 굽나, 런타임 스캔을 경계 바깥에 그대로 두나.
   그냥 뒤집으면 「프리팹이 곧 맵이자 비주얼」이라는 저작 흐름이 무너진다.

2. **Ground 통행층이 실재하지 않는 현행을 그대로 옮길 것인가.**
   배치 마스크는 `Ground|Path|Air`, 통행 마스크는 `Path|Air` 라 층이 3개 선언돼 있는데 통행에서는
   2개만 쓰인다. 이게 설계인지 디오라마 전환의 부작용인지 이력이 없다.
   두 마스크를 하나로 접을지 3층을 유지할지에 따라 **흐름장 슬롯 수**가 정해지므로 초기에 답해야 한다.

3. **마음은 1개인가 N개인가.**
   기계는 1~4 를 지원하고 라이브 콘텐츠는 1(`map-rework` 사용자 결정)인데, `wide-board-content` 는
   **「마음 N개 공유 체력」**(사용자 결정 2026-08-25)을 선행 계약으로 요구한다.
   멀티골 슬롯을 유지할지 접을지에 따라 흐름장 소스 확산·골 도달 판정·붕괴 라우팅이 전부 달라지고,
   **「붕괴 골을 라우팅에서 제외」**(착수 보류 중)가 딸려 온다.

4. **결정론을 어느 등급까지 보증할 것인가.**
   현행은 「**같은 틱 열이면 같은 결과**」까지고, 분리 누적이 순회 순서에 1 ULP 의존한다
   (전체 리플레이 안전 / 스냅샷 부분 재시뮬 위험). 순수 C# 이면 `SimEntityId` 정렬로 닫을 수 있다 —
   **닫아서 고정 스텝 완전 재현을 보증할지**, 아니면 현행 등급을 유지하고 골든 A/B 만 믿을지.
   (같은 축에 `StructureChoice` 타이브레이크·대상 선정 순위가 매여 있다.)

5. **효과 타일의 시드를 살릴 것인가.**
   디오라마 맵의 `GeneratedMap.seed` 가 **-1 고정**이라 같은 맵이면 **매판 같은 칸**에 효과 타일이 뜬다
   (설계도 §7 이 시드 예외 3번으로 명시했지만 의도인지 이력 미발견).
   「배치 선택에 리스크/리워드 축을 더한다」는 원래 목적이 고정 배치에서도 성립하는지,
   매치 시드로 갈아탈지 결정이 필요하다.

---

## 5. 부록 — 조사 중 확인한 사실 (근거)

| 사실 | 확인 방법 |
|---|---|
| `class MapDocument` 는 코드베이스에 없다 | `grep -rln "class MapDocument" Assets/_Project/Scripts/` → 0건. 남은 참조는 전부 주석 |
| 라이브 맵 풀 = 4장 | `Assets/_Project/Data/Maps/MapStagePool.asset` 엔트리 4 (Duel·Street·StreetDay·Subway) |
| 라이브 4스테이지 전부 골 1개 | 각 프리팹에서 `GoalMarker` 스크립트 guid `1924173c6216958448be8cde6b5d4962` 카운트 = 1 |
| `Deck_Duel.goalStabilityMax` = 1500 | 에셋 직접 grep. `AttackDeck` 기본값도 1500 |
| 공성이 라이브다 | `goalStabilityMax > 0` → 골이 전투 대상 엔티티 (`goal-stability` M>0 경로). 「임시 300」 기억은 stale |
| 에이전트 반지름 = 0.25 | `BattleBridge.cs:56` `[SerializeField, Range(0f, 0.49f)] private float agentRadiusTiles = 0.25f` |
| 평활화 lookahead = 24 | `PathSmoothing.cs:45` `public const int DefaultLookahead = 24` |
| 옥타일 비용 = 10/14 | `FlowFieldBuilder.cs:37~38` |
| `MapDocument_MovementStress` 는 고아 | 에셋 헤더의 `m_Script` guid 로 역grep → 0건 |
