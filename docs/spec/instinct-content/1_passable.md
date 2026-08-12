# unit 1 — 본능은 아무것도 막지 않는다

## 목적

본능은 **건물**이지 **벽**이 아니다. 지금은 자기 3×3 으로 통행까지 봉인해서, 광장 한복판에
세우면 복도가 막히고 적이 우회한다. 사용자 지시(2026-08-12): **«배치 불가를 얘기했지 통행
불가를 지시하지 않았다. 통행 불가이면 안 된다.»** 그리고 배치 배제 여유도 폐지 — **본능은
자기가 선 3×3 만 차지한다.**

이 결함의 뿌리는 «다중 셀 점유» 와 «통행 차단» 이 **버퍼 하나에 겸직**한 것이다.
본능은 3×3 거리 계산(가장 가까운 칸까지)을 위해 `BlockingHazardCellsBuffer` 를 빌려 썼는데,
`ObstacleLifetimeSystem` 이 **버퍼 보유만으로** `blockedCells` 를 만들어 차단까지 딸려왔다.

## 변경 대상

| 축 | 파일 | 변경 |
|---|---|---|
| **통행** | `Battle/Effects/ObstacleLifetimeSystem.cs` | 셀 버퍼 루프에 `.WithAll<BlockingHazard>()` **복원**. 방벽(컴포넌트 동반)만 막고 본능(버퍼만)은 안 막는다 |
| **이름** | `Battle/Effects/BlockingHazardCellsBuffer.cs` (+소비자 4) | `OccupiedCellsBuffer` 로 개명 — 비차단 건물이 「Blocking」 버퍼를 드는 거짓말이 이 결함의 원인이었다 |
| **배치** | `Bridge/BattleBridge.cs` · `Data/StructurePlacement.cs` | `IsHostileInstinct` 배제 분기 + `HostileInstinctPlacementPadding` **삭제**. footprint 만 `CloseCellLayers` |
| **연결성** | `Data/MapConnectivity.cs` | 본능 footprint 벽 취급(계약 12) 삭제 |
| **페인터** | `Editor/MapPainterWindow.cs:645` | 위와 같은 BFS 패리티 삭제 |

`AttackSystem.DistanceSqToTarget` 은 **그대로** 이 버퍼를 읽는다 — 다중 셀 거리는 유지다.
차단만 떼고 점유는 남긴다.

## 구현

축 2개를 컴포넌트/버퍼로 가른다:

- **`OccupiedCellsBuffer` 보유** = 나는 여러 칸을 차지한다 → 타게팅 거리는 가장 가까운 칸으로
- **`BlockingHazard` 컴포넌트 보유** = 나는 통행을 막는다 → `blockedCells` 유입

방벽은 둘 다 갖고, 본능은 앞것만 갖는다. `.WithAll<BlockingHazard>()` 는 battle-structures
unit 4 에서 «본능이 통행을 안 막는다» 를 결함으로 보고 제거했던 절이다 — 그 판단은 폐기되는
계약 12 에 종속됐다. 계약이 뒤집혔으니 절도 되돌아온다.

## 계약 변경

- **계약 12 폐기**: ~~본능 footprint 는 통행 차단~~ → **본능은 비차단(마음과 같다)**
- **계약 13(있다면) 동반 폐기**: 연결성 BFS 의 본능 벽 취급
- 배제는 **footprint 3×3 뿐** — 「건물이 선 자리엔 못 놓는다」 그 이상 아무 규칙 없음

## 완료 기준

- [x] 반전한 기존 테스트 3건이 **새 계약으로 초록**
  - `StructureSpawnAndBreachTests` → `InstinctCells_DoNotEnterBlockedCells_ButWallsStillDo` (9칸 → **0칸**)
  - `StructureAuthoringTests` → `AllSpawnsReachGoal_StructuresNeverOccludeCorridor` (봉인해도 **통과**)
  - `AuthoredTargetMaskTests` → `PlacementExclusion_IsFootprintOnly_ForEveryStructureKind` (삭제된 술어 대체)
- [x] 회귀 방지: 방벽(버퍼 + `BlockingHazard`)은 **여전히** 막는다 — 같은 테스트 안에서 2칸 유입 확인
- [x] 점유 유지: 본능이 `OccupiedCellsBuffer` 9칸을 그대로 든다(다중 셀 거리 살아 있음)
- [x] **EditMode 2,178 / 실패 0** (스킵 3 = 기존 알려진 항목)
- [x] **PlayMode 라이브 통과** — `Instinct_BlocksNeitherMovementNorNeighborPlacement`
  - Coil (10,6) 본능 footprint 9칸 중 **한 칸도** 차단 집합에 없다
  - 배치 거부는 **9칸뿐**이고 바로 바깥 링은 놓을 수 있다
  - 공허 방지 가드: 본능 엔티티 실재 + 셀 (10,6) + 점유 9칸을 먼저 단정

### 라이브 축을 Coil 로 옮긴 이유

원래 이 검증은 dev 슬롯(`MapDocument_Test`, 30×30)에 걸려 있었는데, 그 문서가 **세션 시작
전부터 13×7 무거점으로 덮여** 있었다(git status 스냅샷에 이미 `M`). dev 슬롯은 병행 작업이
수시로 갈아끼우는 스크래치라 고정물로 쓰면 남의 저작에 테스트가 흔들린다. 그래서 새 단정은
**라이브 풀의 Coil**(주 풀 index 1)을 쓴다.

`Structures_BootOnDevMap_SpawnBlockAndSurviveConnectivity` 는 그 덮어쓰기 때문에 **여전히
빨갛다**(프랍 0). 이 unit 의 변경과 무관하며, 워킹트리 자산을 되돌리면 병행 세션의 미커밋
작업이 날아가므로 손대지 않았다 — 복구는 그 자산의 주인이 결정할 일이다.

## 주의

- 통행이 열리면 **적이 건물 위를 걷는다** — 시각적 겹침이 생긴다. 프랍 `viewScale` 0.4 라
  덜하지만, 거슬리면 후속(프랍 높이/렌더 순서)이지 이 unit 의 스코프가 아니다.
- Coil 의 «본능이 로터리를 좁힌다» 지형 압박은 **사라진다.** 위협은 이제 사거리 5 뿐이다.
  밸런스 재확인은 unit 2 실측에서.
