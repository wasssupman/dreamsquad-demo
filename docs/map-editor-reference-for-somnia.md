# Map Editor Reference for Somnia

이 문서는 `dreamsquad-demo`의 **Unity Editor 기반 맵 저작 과정과 구현 방식만** 조사한 참고 자료다. Dreamsquad의 문서·코드·에셋은 모두 Somnia Editor UX와 구현 선택을 비교하기 위한 **낮은 권위의 관찰 증거**이며 Somnia 계약의 source of truth가 아니다. 관찰 사실을 검증할 때는 현재 코드와 직렬화된 에셋, Git 이력을 spec/reference 문서보다 우선했다.

Somnia 적용성 분류에는 사용자 제공 전제인 **designer-authored source → explicit export → Client/Server consumption** 흐름과 **map-specific generator code를 두지 않는다**는 방향을 사용한다. 이는 Dreamsquad에서 도출한 계약이 아니다. 이 문서는 Somnia의 `MapDataV1`, Client/Server DTO, 좌표·ID 정책, 게임 규칙, Runtime/Server 구조, export schema·versioning·산출물 형식 또는 CI 정책을 정의하지 않는다.

현재 HEAD의 핵심 결론은 다음과 같다.

- 현행 맵 저작은 전용 `EditorWindow`가 아니라 **Prefab Mode + Unity 기본 Selection/Transform + Custom Inspector + Gizmos** 조합이다.
- 과거 `MapPainterWindow`와 `MapDocument` 기반 셀 페인터는 삭제되었다. 과거 UX를 언급하는 곳은 반드시 역사 자료로 표시한다.
- 현행 논리 맵은 저장된 `MapStage` 프리팹을 배틀 진입 때 스캔하여 `GeneratedMap`으로 파생한다. 별도 Bake 결과물을 저장하지 않는다.
- `MapStageDuelGenerator`는 Demo의 재현·교차 확인용 자동화 증거일 뿐이다. 맵별 좌표·프랍·마커를 코드에 고정하는 방식은 Somnia production authoring 후보가 아니다.
- Duel에는 저작된 `RouteMarker`가 없고 두 `SpawnMarker.routeIndex`도 `-1`이므로 lane 기본 authored-route binding이 없다. 다만 Unit 또는 Wave Concept binding이 우선할 수 있으며, 모든 binding 해석 결과가 최종 `-1`일 때만 waypoint를 사용하지 않는다. 어느 경우도 Spawn→Goal 직선 이동을 뜻하지 않는다.
- Duel의 활성 웨이브는 별도 `WavePlanAsset`이 아니라 `Deck_Duel`의 고정 seed와 생성 규칙에서 파생된다. runtime pending spawn과 runtime forecast가 같은 expanded 결과를 쓰는 패턴은 참고하되, 이를 Editor preview parity로 부르거나 생성기 코드를 Client/Server 계약으로 복사하지 않는다.
- 맵 좌표는 2D로 볼 수 있지만 이동에는 Ground/Air 두 논리 layer가 필요하다. Demo의 `Ground`/`Path`/`Air` 명명과 배치·통행 비트 재사용은 Somnia layer 계약이 아니다.

## Baseline

- Branch: `master`
- Commit SHA: `40bb3af3480a119c5c88fddc3c6abe69ae28eca1`
- Unity Version: `6000.4.3f1` (`39d1a88d4dd1`)
- Working Tree at original investigation start: clean (`## master...origin/master`)
- Path/Wave reinforcement preflight: 기존 신규 문서 `docs/map-editor-reference-for-somnia.md` 한 파일만 untracked였고 tracked/cached 변경은 없었다.
- Repository instructions: 루트 `AGENTS.md`가 가리키는 `CLAUDE.md` 적용
- Investigation date: 2026-08-28 (Asia/Seoul)
- Time limit: 3시간 상한 안에서 조사했으며, 범위 확장 대신 확인하지 못한 항목을 아래 `Unknowns and Limits`에 남겼다.

## Editor Entry Points

### Current authoring surface

| Entry point | Current status | Actual file/type and role |
| --- | --- | --- |
| EditorWindow | 없음 | 현재 맵 전용 `EditorWindow`가 없다. 기존 프리팹은 Project 창에서 열어 Prefab Mode로 편집한다. |
| CustomEditor | 있음 | 맵 선언은 `Assets/_Project/Editor/MapStageEditors.cs`의 `MapStageEditor`, `PropFootprintEditor`, 각 Marker/Zone Editor가 담당한다. 웨이브 asset은 `Assets/_Project/Editor/WavePlanAssetEditor.cs`의 `WavePlanAssetEditor`가 별도 timeline Inspector를 제공한다. |
| PropertyDrawer | 없음 | 맵 관련 `CustomPropertyDrawer`/`PropertyDrawer` 구현이 없다. |
| SceneView | 기본 기능만 사용 | 맵 관련 `OnSceneGUI`, `SceneView.duringSceneGui`, `EditorTool`, `ToolManager`, hot control 구현이 없다. Unity 기본 Move/Rotate/Scale 도구로 자식 오브젝트를 배치한다. |
| Overlay | 없음 | 맵 관련 Unity Overlay 구현이 없다. `MapStageCameraFraming.DrawCellOverlay`는 프리뷰 PNG에 임시 Quad를 그리는 함수이며 SceneView Overlay가 아니다. |
| Handles | 라벨만 사용 | `Assets/_Project/Scripts/Core/MapStage/MapStageGizmoUtil.cs`의 `MapStageGizmoUtil.Label`이 `UnityEditor.Handles.Label`을 호출한다. 드래그 핸들 또는 위치 핸들은 없다. |
| Gizmos | 있음 | `MapStage`, `PropFootprint`, `PlacementBlockZone`, 각 Marker의 `OnDrawGizmos`; `MapStage.OnDrawGizmosSelected`는 선택 중일 때 내부 격자선을 표시한다. |
| Unity Menu | 있음 | `Window/Wassup/Map Stage/Generate Duel Stage`, `Window/Wassup/Map Stage/Frame Scene Camera As Battle`, `Window/Wassup/Map Stage/Create Goal Portal (Yellow)` |
| Asset create menu | 있음 | `Assets/Create/Wassup/WavePlan`으로 `WavePlanAsset`, `Assets/Create/Wassup/AttackDeck`으로 `AttackDeck`을 만들 수 있다. `AttackDeck`과 `WaveConceptData`는 별도 map-aware Custom Editor 없이 기본 Inspector로 편집한다. |
| Automation runner | 있음 | `Assets/_Project/Editor/RalphEditorTasks.cs`의 `RalphEditorTasks`는 `[InitializeOnLoad]` 파일 프로토콜 runner와 `Window/Wassup/Ralph/Run Requested Editor Task` 메뉴를 제공한다. Map 생성·marker 저작·preview·camera API를 호출하지만 아티스트용 interactive map editor는 아니다. |

`Assets/_Project/Editor/MapStageEditors.cs`의 Custom Inspector는 모두 `DrawDefaultInspector()`를 기본으로 사용한다. 따라서 선택한 Hierarchy 오브젝트의 직렬화 필드를 Inspector에서 편집하고, 다음 보조 동작만 추가한다.

- `MapStageEditor`: `자식 렌더러 바운즈에서 playArea 제안`, `Dev 엔트리로 등록 (MapStagePool)`
- `PropFootprintEditor`: `렌더러 바운즈에서 footprint 제안`, `셀 중심에 스냅`
- Marker/Zone Editor: 기본 필드 + `셀 중심에 스냅`
- `BonusSpawnMarkerEditor`, `StructureMarkerEditor`: 저작 제약을 설명하는 `HelpBox`

`WavePlanAssetEditor`는 map component Inspector와 달리 wave summary, warning, timeline, group 편집, Play test를 직접 그린다. 그러나 map, lane, route, Ground/Air를 함께 보여 주는 통합 EditorWindow는 아니며 구체 공백은 Authoring Workflow 6절에 기록한다.

SceneView의 색상 의미는 다음과 같다.

- Cyan: `MapStage` play area 외곽과, 선택 시 내부 grid
- Red: `PropFootprint`가 막는 셀
- Orange: `PlacementBlockZone`이 배치만 금지하는 셀
- Green `S{laneIndex}`: `SpawnMarker`
- Yellow `G`: `GoalMarker`
- Purple `R{routeIndex}.{order}`와 연결선: `RouteMarker`
- Pink `B`: `BonusSpawnMarker`
- Blue/Red `I`: Defender/Enemy `StructureMarker`의 3×3 Instinct footprint
- Gray: play area 밖으로 양자화된 셀

### Historical, retired EditorWindow

`MapPainterWindow : EditorWindow`는 현재 작업 트리에 없다. 삭제 직전 구현은 Git blob `ba70aaab^:Assets/_Project/Editor/MapPainterWindow.cs`에서 확인할 수 있고, 커밋 `ba70aaabd5b5b59d147acbfc2d7ce64a7dff5653`가 `MapPainterWindow`, `MapDocument` 및 관련 파이프라인을 삭제했다.

과거 진입 메뉴는 `Window/Wassup/Map Painter`였다. 창 내부에는 New 크기, `MapDocument` target/Load, Road/Buildable/Deco/Spawn/Goal/Mask/Structure/Waypoint toolbar, IMGUI grid, validation 결과와 Bake 버튼이 있었다. 이 구현은 다음 이유로 **현행 진입점이 아니다**.

- 편집 상태가 `_tiles`, `_placeMask`, `_spawns`, `_goals`, `_waypointPaths`, `_tool`, `_activeWaypointPath` 같은 창 내부 메모리에 있었다.
- 셀 stroke와 marker 편집에 Unity Undo 연동이 없었다.
- 저장은 Bake 시점의 `MapDocument` 쓰기에 의존했다.
- 현행 `map-diorama-stage` 계약은 프리팹을 정본으로 삼으며 `MapDocument`를 사용하지 않는다.

## Authoring Workflow

### Representative map

대표 맵은 `Assets/_Project/Art/Theme/duel/MapStage_Duel.prefab`이다. Duel을 선택한 이유는 전체 프리팹을 재현하는 generator가 있어 생성 과정과 저장 결과를 교차 확인할 수 있고, 현행 선언 컴포넌트를 가장 폭넓게 함께 사용하기 때문이다. 이는 live 맵의 공통 제작 방식이라는 뜻이 아니다.

- `Assets/_Project/Data/Maps/MapStagePool.asset`의 live `entries[0]`이다.
- `Assets/_Project/Editor/MapStageDuelGenerator.cs`의 `MapStageDuelGenerator`가 전체 생성을 코드로 재현하므로 실제 프리팹과 생성 과정을 교차 확인할 수 있다.
- 현재 프리팹은 23×10 play area, Spawn 2개, Goal 1개, Bonus Spawn 2개, `PropFootprint` 3개, `PlacementBlockZone` 1개, `StructureMarker` 4개, `RouteMarker` 0개를 가진다.

현재 tree에서 전체 live prefab generator가 확인되는 맵은 Duel뿐이다. Street/Subway/StreetDay는 저장된 prefab이 정본이고, `RalphEditorTasks`와 `MapStageAuthoringTools`는 marker 교체나 root 정규화 같은 대상별 변경만 자동화한다. 네 live prefab의 정적 구조를 비교하면 다음과 같다.

| Live prefab | Play area / grid origin | Cell declarations | Full-map generator evidence |
| --- | --- | --- | --- |
| `MapStage_Duel` | 23×10 / `(0, 0.19, 0)` | `PropFootprint` 3, `PlacementBlockZone` 1, `StructureMarker` 4 | `MapStageDuelGenerator` 있음 |
| `MapStage_Street` | 30×11 / `(0, 0.19, -0.09)` | `PropFootprint` 8, zone/structure 0 | full-map generator 미확인; footprint는 SpriteRenderer art GameObject에 동거 |
| `MapStage_Subway` | 30×11 / `(0, 0.19, 1.39)` | `PropFootprint` 12, zone/structure 0 | full-map generator 미확인; footprint는 SpriteRenderer art GameObject에 동거 |
| `MapStage_StreetDay` | 30×9 / `(0, 0.87, -0.75)` | `PropFootprint` 12, zone/structure 0 | full-map generator 미확인; footprint는 SpriteRenderer art GameObject에 동거 |

네 맵 모두 `previewTileSize=1`, `suppressEffectTiles=false`, Spawn 2개(`laneIndex=0/1`, `routeIndex=-1`), Goal 1개, Bonus Spawn 2개, Route 0개다. 크기·origin·footprint 수는 가변이며, zone과 structure는 공통 필수가 아니라 Duel에서만 사용 중이다.

### 1. Create or open

Dreamsquad Demo의 신규 Duel 자동 생성은 메뉴 `Window/Wassup/Map Stage/Generate Duel Stage`가 `MapStageDuelGenerator.GenerateMenu`를 호출하는 경로다. 기존 프리팹이 있을 때 이 메뉴 경로만 덮어쓰기 확인 대화상자를 띄운다. 이후 `Generate`가 루트·바닥·배경·프랍·마커를 다시 만들고 `PrefabUtility.SaveAsPrefabAsset`으로 저장한다. `MapStagePool.EditorUpsertLiveEntry(..., insertIndex: 0)`는 같은 이름의 live entry가 있으면 그 자리에서 참조를 교체하고, 없을 때만 0번에 삽입한다. 기준선의 Duel은 이미 `entries[0]`이므로 결과적으로 0번이 갱신된다. pool `SetDirty`와 `AssetDatabase.SaveAssets`는 upsert 결과가 변경됐을 때만 실행된다.

기존 맵 전용 Open 메뉴는 없다. Project 창에서 `MapStage_Duel.prefab`을 열어 Prefab Mode에서 편집한다. 생성기는 프리팹 전체를 다시 쓰고 Undo를 기록하지 않는다. 메뉴의 `GenerateMenu`는 기존 프리팹에 한해 확인을 받지만, automation runner가 직접 호출하는 `Generate()`는 확인 없이 덮어쓴다.

이 경로는 현재 Demo의 재현 가능한 생성·저장 동작을 관찰하기 위한 evidence다. Somnia에서는 맵별 generator를 제작 흐름으로 사용하지 않고, 공통 Editor가 designer-authored source를 만들고 공통 export 단계가 이를 Client/Server 소비 데이터로 변환한다.

### 2. Grid and background

`MapStageDuelGenerator`는 루트에 `Wassup.Core.MapStage`를 붙이고 다음 값을 저작한다.

- `playAreaCells = (23, 10)`
- `gridOriginLocal = (0, 0.19, 0)`
- `previewTileSize = 1`
- `suppressEffectTiles = false`

배경은 `Floor`, `Floor_L`, `Floor_R`, `Floor_Front` Plane과 `Backdrop`, `Deco_0..4` SpriteRenderer, global `Volume` 자식으로 구성한다. Terrain, Mesh, Texture 또는 Collider를 읽어 논리 셀을 자동 생성하지 않는다. `MapStage`의 선언과 아래 footprint/marker가 논리 정본이다.

수동 저작에서는 `MapStageEditor`의 `자식 렌더러 바운즈에서 playArea 제안` 버튼이 renderer bounds를 stage-local로 바꾼 뒤 셀 수를 올림 계산한다. 자식 전체를 함께 이동하고 `gridOriginLocal.xz`를 0으로 맞추지만, 결과는 제안일 뿐 저작자가 Inspector에서 다시 수정할 수 있다.

### 3. Cell editing

현행에는 Cell painting brush가 없다. 열린 play area 셀은 기본적으로 통행·배치 가능하며, SceneView에서 다음 선언 컴포넌트를 배치해 논리를 바꾼다.

- `Wassup.Core.PropFootprint`: `size`와 `anchorOffset`의 사각 셀을 `Deco` + `placeMask=0`으로 파생해 기본 Walk/Path 통행과 모든 배치를 차단한다. `PlacementLayers.Derive(Deco)`가 Air를 열기 때문에 공중 통행은 유지된다.
- `Wassup.Core.PlacementBlockZone`: `size` 영역의 배치 mask만 0으로 만들고 통행은 유지한다.

Duel은 다음 cell declaration을 가진다.

- `divider_s`: anchor `(11, 0)`, size 1×1
- `divider_c`: anchor `(11, 3)`, size 1×4 — `(11, 3..6)` 차단
- `divider_n`: anchor `(11, 9)`, size 1×1
- `enemy_zone`: anchor `(17, 0)`, size 6×10 — x=17..22 배치 금지

Duel generator는 `divider_*` 전용 host에 `PropFootprint`를 붙이고 시각 tile을 그 자식으로 만들며, zone과 marker도 전용 child GameObject로 만든다. 그러나 `MapStageScanner`는 이 hierarchy 형태를 강제하지 않는다. Street/Subway/StreetDay의 현재 `PropFootprint`는 모두 SpriteRenderer가 있는 art GameObject에 직접 붙어 있고, 그 Transform을 cell anchor로 양자화한 뒤 명시적 `size`/`anchorOffset`을 적용한다. 따라서 요소 생성·삭제에는 전용 host GameObject의 create/delete와 기존 art GameObject의 component add/remove 두 경로가 있으며, 수정은 Transform과 Inspector 필드 편집이다. 비활성 **GameObject**는 `GetComponentsInChildren<T>(false)` 수집에서 빠지지만 scanner는 `MonoBehaviour.enabled`를 검사하지 않으므로 component만 disable해도 제외되지는 않는다.

`MapStageMath.LocalToCell`이 좌표 변환의 단일식이다.

```text
cell.x = floor((localPosition.x - gridOriginLocal.x) / tileSize)
cell.y = floor((localPosition.z - gridOriginLocal.z) / tileSize)
```

`MapStageScanner`는 world position을 `stage.transform.InverseTransformPoint`로 local position으로 바꾸고 호출자가 준 runtime tile size로 이 함수를 호출한다. Gizmo와 Custom Inspector도 같은 `MapStageMath`를 사용하지만 입력은 `stage.previewTileSize`다. 즉 산식은 공유하지만 입력값과 결과가 자동으로 같아지는 것은 아니며, `DioramaMapBuilder.Validate`가 preview/runtime tile size 불일치를 형식 오류로 잡는다. `셀 중심에 스냅`은 `CellCenterLocal`로 XZ만 옮기고 저작된 local Y 높이는 유지한다.

예외도 있다. `MapStageDuelGenerator.Host`와 `MapStageAuthoringTools.Host`는 `CellCenterLocal`을 호출하지 않고 `gridOriginLocal + (cell.x + 0.5, 0, cell.y + 0.5)`를 직접 계산해 tile size 1을 전제한다. `MapStageCameraFraming.DrawCellOverlay`는 더 나아가 `Scan(stage, 1f)` 뒤 overlay Quad를 `(cell.x + 0.5, gridOriginLocal.y + 0.03, cell.y + 0.5)`에 놓아 tile size와 `gridOriginLocal.xz`를 모두 반영하지 않는다. 기준선 live map의 origin z는 Duel 0, Street -0.09, Subway 1.39, StreetDay -0.75이므로 Duel 외 세 맵에서는 preview 논리 overlay가 아트/마커와 어긋날 수 있다. 따라서 현행도 일반화 가능한 단일 변환 경로는 아니며, Somnia에서는 Editor·preview·validator·공통 export가 같은 좌표 정책과 순수 변환 함수를 사용해야 한다.

### 4. Spawn and Goal placement

Duel의 실제 마커는 다음과 같다.

| Element | Cell | Key fields |
| --- | --- | --- |
| `spawn0 : SpawnMarker` | `(20, 3)` | `laneIndex=0`, `routeIndex=-1` |
| `spawn1 : SpawnMarker` | `(20, 5)` | `laneIndex=1`, `routeIndex=-1` |
| `goal : GoalMarker` | `(2, 4)` | `visualRoot`가 비면 marker transform 사용 |
| `bonus_portal_0 : BonusSpawnMarker` | `(11, 2)` | 보너스 포탈 첫 셀 |
| `bonus_portal_1 : BonusSpawnMarker` | `(11, 7)` | 보너스 포탈 둘째 셀 |
| `instinct_ally_a : StructureMarker` | `(4, 2)` | Defender, `Structure_GuardInstinct` |
| `instinct_ally_b : StructureMarker` | `(4, 7)` | Defender, `Structure_GuardInstinct` |
| `instinct_enemy_a : StructureMarker` | `(18, 2)` | Enemy, `Structure_WatchInstinct` |
| `instinct_enemy_b : StructureMarker` | `(18, 7)` | Enemy, `Structure_WatchInstinct` |

`SpawnMarker.laneIndex`가 hierarchy 순서가 아닌 lane 순서의 정본이다. 빌더는 lane index 순으로 spawn 배열을 정렬한다. Spawn/Goal Custom Inspector는 기본 필드와 셀 스냅을 제공하고, Gizmo는 녹색 `S0`/`S1`과 노란 `G`를 즉시 표시한다.

스폰/골 포탈 비주얼은 각 맵 프리팹에 반드시 저장되는 데이터가 아니다. `Assets/_Project/Data/Maps/MarkerPropStyle.asset`과 `Assets/_Project/Scripts/Presentation/MarkerPropInstaller.cs`의 `Wassup.Presentation.MarkerPropInstaller`가 `MapStage.Enabled` 시점에 `visualRoot == null`인 활성 마커를 찾는다. 공용 프랍을 marker transform의 자식으로 identity 생성한 뒤, 생성된 프랍 transform 자체를 `visualRoot`에 지정한다. 이미 `visualRoot`가 채워진 마커는 건너뛴다. Unity 재시작 후에도 style asset 참조를 통해 런타임에 다시 구성된다.

`MapStageAuthoringTools.AuthorSpawnsAndGoal`은 기존 marker GameObject를 `DestroyImmediate`로 지우고 2 Spawn + 1 Goal을 다시 만든 뒤 프리팹을 저장하는 코드/runner 전용 API다. 사용자 메뉴가 아니며 Undo를 지원하지 않는다.

### 5. Path editing

#### Path/navigation terminology

아래 용어는 Dreamsquad 내부의 서로 다른 `Path` 의미를 구분하기 위한 문서 표준이다. Somnia 데이터 계약을 정의하지 않는다.

| Document term | Dreamsquad implementation | Meaning and evidence |
| --- | --- | --- |
| Authored route | `Assets/_Project/Scripts/Core/MapStage/RouteMarker.cs` — `RouteMarker(routeIndex, order)` | 저작자가 셀들로 명시한 ordered waypoint chain. `DioramaMapBuilder.Assemble`이 route별 `order` 오름차순으로 평탄화한다. |
| Route node / waypoint | 개별 `RouteMarker` | authored route를 이루는 한 목적지 셀. `RouteMarker.OnDrawGizmos`가 보라색 라벨과 다음 node 연결선을 표시한다. |
| Route binding | `SpawnMarker.routeIndex`, `AttackUnitData.waypointPathIndex`, `WaveConceptSlot.pathIndex` | authored route를 고르는 raw integer reference. `WaypointRouting.ResolvePathIndex`의 우선순위는 Unit SO → Wave Concept → lane default → `-1`이며 stable identity가 아니다. |
| Lane | `SpawnMarker.laneIndex`, wave group `laneIndex` | Spawn과 wave 출현 지점을 연결하는 구분값. authored route나 traversal layer가 아니다. |
| Traversal layer | `Assets/_Project/Scripts/Data/PlacementLayer.cs` — `PlacementLayer.Path/Air`; `AttackUnitData.EffectiveTraversalLayers` | 어떤 셀을 통과할 수 있는지 고르는 movement mask. 특히 `PlacementLayer.Path`는 authored route가 아니라 기본 지상 통행 비트다. |
| Derived navigation | `SimFieldInstaller`, `FlowFieldBuilder`, `MovementSystem`의 목적지 × traversal-mask flow | 지형, 현재 목적지와 unit traversal mask에서 런타임에 파생되는 방향장. `RouteMarker` chain과 다른 데이터다. |
| Goal-targeted navigation | `FlowFieldSingleton.GoalSentinel`을 목적지로 한 derived navigation | Goal을 향하는 flow라는 뜻일 뿐 Spawn→Goal 직선 segment를 뜻하지 않는다. hunting이나 structure 목적지가 우선할 수도 있다. |
| Runtime invalid-reference fallback | `BattleBridge.CreateEnemyEntity`의 invalid positive index 처리 | 존재하지 않는 Unit/Concept binding을 warning 후 버리고 `WaypointFollow` 없이 current-destination navigation을 쓰는 예외 처리. 정상적인 최종 `-1`과 구분한다. |

#### Current authored-route surface and reconstructed authoring procedure

다음은 Unity를 직접 조작해 관찰한 절차가 아니라, 현재 Prefab·Component·Inspector 구현에서 복원한 사용 가능 절차다. **Confirmed**는 현재 코드/직렬화 에셋으로 직접 확인, **Inferred**는 그 surface로부터 복원, **Unknown**은 이번 조사에서 확인하지 못했다는 뜻이다.

| Step | Evidence | Current procedure and limit |
| --- | --- | --- |
| 맵 열기 | **Inferred** | Project 창에서 `MapStage_*.prefab`을 Prefab Mode로 연다. 전용 route/open 메뉴는 없다. |
| node 생성 | **Inferred** | stage 자식 GameObject를 만들고 `RouteMarker` Component를 수동 추가한다. 전용 create/insert tool은 찾지 못했다. |
| route/order 입력 | **Confirmed** | `RouteMarkerEditor`가 `DrawDefaultInspector()`로 raw `routeIndex`, `order`를 노출한다. route index는 0부터 빈 번호 없이 이어져야 하고 `(routeIndex, order)`는 유일해야 한다. `order` 자체의 연속성은 검사하지 않고 숫자 오름차순으로만 정렬한다. |
| 위치와 셀 snap | **Confirmed** | Unity 기본 Transform으로 옮긴 뒤 `셀 중심에 스냅`을 누른다. 이 버튼만 `Undo.RecordObject(transform, "Snap To Cell Center")`를 호출하고 XZ를 스냅하며 Y는 보존한다. |
| lane 기본 binding | **Confirmed** | `SpawnMarker` 기본 Inspector에서 `routeIndex`를 입력한다. `-1`은 **그 lane의 기본 authored-route binding 미지정**이며 Unit/Concept binding을 지우지 않는다. |
| SceneView 확인 | **Confirmed** | `RouteMarker.OnDrawGizmos`가 `R{routeIndex}.{order}` 라벨과 다음 order node까지의 선을 그린다. draggable Handle은 없다. |
| 생성·삭제·reorder | **Confirmed** | 전용 UX가 없다. 기본 Hierarchy/Add Component/Delete와 `order` 정수 직접 편집만 가능하다. |
| 저장 | **Inferred** | route Inspector에는 `SetDirty`, prefab 저장 또는 `SaveAssets` 호출이 없다. 일반 Prefab Mode 저장과 Unity 직렬화에 맡긴다. 이번 조사에서 UI 저장·재시작은 실행하지 않았다. |
| populated live 사례 | **Unknown** | 네 live prefab 모두 serialized `RouteMarker`가 0개다. plain builder 테스트는 정렬/조립을 다루지만 live designer workflow와 Play UX를 증명하지 않는다. |

#### Duel's derived navigation result

Duel의 두 `SpawnMarker.routeIndex=-1`은 lane 기본 binding이 없다는 뜻이고, serialized prefab에도 `RouteMarker`가 0개다. 그러나 이것만으로 모든 유닛의 최종 route가 `-1`이라고 단정할 수는 없다. `WaypointRouting`은 Unit SO와 Wave Concept binding을 먼저 해석한다. **모든 binding의 최종 결과가 `-1`일 때** `BattleBridge`가 `WaypointFollow`를 붙이지 않으며, `MovementSystem`은 Goal 또는 hunting/structure 등 현재 선택된 목적지와 unit traversal layer에 맞는 derived flow를 사용한다.

중앙 분리대는 x=11의 y=0, y=3..6, y=9를 막고 y=1..2와 y=7..8을 열어 둔다. 코드와 에셋에 따른 Goal-targeted 기본 flow는 다음처럼 해석된다.

- 일반 지상 적의 기본 traversal mask는 `Path`다. 분리대를 통과하지 못하므로 `(20,3)` lane은 아래 통로가 더 짧다. `(20,5)` lane은 위·아래 통로의 weighted cost가 같고, 현행 `FlowFieldBuilder`의 결정적 tie-break를 포함한 기본 flow는 위 통로를 선택하는 것으로 정적 추론된다.
- Air 적은 `Deco`를 포함한 모든 현행 타일에서 통행 가능하므로 분리대 셀을 가로지르는 더 직접적인 flow를 가질 수 있다.
- 실제 전투에서는 hunting이나 structure destination이 Goal-targeted flow보다 우선할 수 있다. 여기서 참고하는 결과는 같은 2D 맵에서도 목적지와 traversal layer별로 derived navigation이 달라지는 구조다.

이는 Unity를 실행해 궤적을 육안 측정한 결과가 아니라 `MapStage_Duel.prefab`, `PlacementLayers.Derive`, `SimFieldInstaller`, `FlowFieldBuilder`, `MovementSystem`을 교차한 정적 추론이다. preview와 Client/Server path 해석의 권위는 아래 PD-03, PD-04, PD-12, PD-14에 별도 open policy로 기록한다.

#### Skimmer cross-asset evidence chain

정상적인 최종 `-1`과 invalid positive reference는 다른 상태다. Spawn의 invalid positive lane default는 `DioramaMapBuilder.Validate`가 map 형식 오류로 막는다. 반면 공유 Unit SO 또는 Wave Concept의 map-local positive index는 map-only validator가 보지 않으며, `CreateEnemyEntity`의 런타임 branch가 warning 후 해당 waypoint binding을 버린다.

| State | Evidence | Finding |
| --- | --- | --- |
| Asset-level mismatch | **Confirmed** | `Assets/_Project/Data/Enemies/Enemy_Skimmer.asset`은 Air와 `waypointPathIndex=0`을 저장한다. Duel prefab의 serialized `RouteMarker`는 0개이며 두 Spawn binding은 `-1`이다. |
| Assembled path count | **Inferred** | `MapStageScanner`가 `RouteMarker`만 수집하고 `DioramaMapBuilder`가 그 목록으로 ranges를 만들므로 Duel의 `WaypointPathCount=0`이 파생된다. 이번 조사에서 실제 scan/assemble 값은 실행하지 않았다. |
| Encounter candidate | **Confirmed** | `MapStagePool.asset` live entry 0이 Duel과 `Deck_Duel`을 연결하고, `Deck_Duel.attackUnitPool`이 Skimmer GUID를 포함한다. |
| Fixed-seed resolved inclusion | **Confirmed — existing test source** | `WaveConceptAuthoringTests.SiegeDecks_MainPhase_ShowsEveryNonBossEnemy`는 `Deck_Duel`의 저장 seed와 siege lane count 2로 plan을 만들고 첫 14 waves에 pool 전 종이 포함되는지 검사한다. 따라서 repository가 Skimmer 포함을 active invariant로 pin한다. 이번 조사에서는 이 테스트를 실행하지 않았고 Skimmer의 정확한 wave/group도 산출하지 않았다. |
| Invalid-reference handler | **Confirmed** | Skimmer의 Unit SO binding 0이 Concept/lane보다 우선한다. map path 수 밖이면 `BattleBridge.CreateEnemyEntity`는 warning을 기록하고 `WaypointFollow`를 붙이지 않아 non-waypoint current-destination navigation으로 대체한다. |
| Observed Duel fallback | **Unknown** | 실제 Duel Play에서 Skimmer spawn과 해당 warning이 발생하는 장면을 관찰하거나 저장 로그/`LogAssert`로 확인하지 않았다. 코드·에셋·test invariant의 결합으로 branch 활성 조건은 추론할 수 있지만 관찰 결과로 쓰지 않는다. |

Somnia의 ID, route binding, validation/fallback 선택은 PD-02, PD-07, PD-13에 분리해 둔다.

### 6. Wave authoring and Duel result

#### Wave terminology

| Document term | Dreamsquad implementation | Meaning |
| --- | --- | --- |
| Authored wave source | `WavePlanAsset` / `AuthoredWave` / `AuthoredSpawnGroup` | Inspector에서 wave, group, 상대 시간과 수량을 명시적으로 저장하는 source asset. |
| Generated wave source | `AttackDeck` + seed + referenced assets + `WavePatternGenerator` | 입력 recipe로 transient `GeneratedWavePlan`을 만드는 경로. `waveGeneratorVersion` 숫자만으로 알고리즘 구현을 선택하지는 않는다. |
| Resolved/expanded spawn | `WavePatternGenerator.ExpandWave`의 `ExpandedWaveSpawn` 목록 | group을 실제 unit/time/lane/path 단위 spawn으로 펼친 runtime 결과. |
| Runtime forecast | `WavePatternGenerator.BuildSpawnGuideForecasts`, called by `BattleBridge.QueueWave` | actual pending과 같은 expanded 목록으로 만든 런타임 안내. `WavePlanAssetEditor`의 source timeline이나 Editor resolved-result preview가 아니다. |

#### Authored `WavePlanAsset` workflow

| Step | Evidence | Current procedure and limit |
| --- | --- | --- |
| asset 생성 | **Confirmed** | `WavePlanAsset`의 `[CreateAssetMenu(menuName="Wassup/WavePlan")]`에 따라 `Assets/Create/Wassup/WavePlan`을 사용한다. |
| plan/wave 편집 | **Confirmed** | `WavePlanAssetEditor`에서 display name, timer, duration, interval, group unit/trigger/count를 편집하고 wave up/down, duplicate, delete와 group add/delete를 사용한다. group reorder/duplicate 전용 버튼은 없다. |
| timeline·warning 확인 | **Confirmed** | summary와 source `triggerTimeSec` marker, 빈 wave/group·null unit·count·trigger bounds warning을 표시한다. `FromPlanAsset`/`ExpandWave`를 호출하지 않으므로 resolved/runtime preview가 아니다. |
| lane/path/layer 설정 | **Confirmed** | 데이터에는 `AuthoredSpawnGroup.laneIndex`가 있지만 custom row가 그 필드를 그리지 않는다. authored group에는 path 또는 traversal-layer override 필드가 없다. |
| encounter 연결 | **Inferred** | `MapStagePool.Entry`가 `stage + deck + optional plan`을 저장하므로 pool asset의 기본 Inspector에서 plan을 할당하는 것으로 복원된다. 전용 `MapStagePool` Custom Editor는 없다. 실제 할당 UI는 실행하지 않았다. |
| Play test | **Confirmed** | `▶ Test this plan (Play BattleScene)`은 plan GUID만 `SessionState`로 전달한다. `GameManager.StartTestModeMatch`가 pool에서 맵을 먼저 고른 뒤 plan을 override하므로 exact map+plan pair를 고정하지 않는다. |
| Undo/dirty | **Confirmed** | Inspector는 `SerializedObject.Update/ApplyModifiedProperties`를 사용해 일반 SerializedProperty Undo/dirty를 따른다. 수동 `Undo.RecordObject`, `SetDirty`, `SaveAssets`, explicit Save 버튼은 없다. |
| 저장·재시작 | **Unknown** | 일반 Unity asset 저장에 맡기는 구조는 확인했지만 이번 조사에서 UI 저장과 재시작 persistence를 실행하지 않았다. |

#### Generated `AttackDeck` workflow

| Step | Evidence | Current procedure and limit |
| --- | --- | --- |
| deck 생성·선택 | **Confirmed** | `Assets/Create/Wassup/AttackDeck`으로 만들 수 있고 `AttackDeck` 전용 Custom Editor가 없어 기본 Inspector를 사용한다. |
| recipe 편집 | **Confirmed** | `useGeneratedWaves`, seed/version, unit pool, wave/unit count, jitter/spacing/interval/growth/lead-in, boss, timer, concept/hold, ramp 등을 직렬화한다. |
| 현재 Duel pairing | **Confirmed** | serialized `MapStagePool.asset`의 live entry 0은 `MapStage_Duel + Deck_Duel + plan=null`이다. |
| stage/deck source 연결 | **Inferred** | 전용 `MapStagePool` Custom Editor가 없으므로 기본 Inspector에서 stage와 deck을 연결하고 authored plan을 비우는 수동 절차로 복원된다. |
| generated mode 선택 | **Confirmed** | `plan=null`만으로 충분하지 않다. test override와 유효한 encounter plan이 없고 `ActiveDeck.useGeneratedWaves=true`일 때 `TryInitializeGeneratedWaves`가 generated path를 선택한다. 아니면 authored 또는 legacy/fallback 경로가 우선한다. |
| transient 생성 | **Confirmed** | `WavePatternGenerator.Generate(AttackDeck, seed, laneCount)`가 plain `GeneratedWavePlan`을 만들고 `BattleBridge`가 런타임에 보유한다. 이를 asset으로 저장/freeze하는 Editor 경로는 찾지 못했다. |
| runtime pending/forecast | **Confirmed** | `QueueWave`가 `ExpandWave`를 한 번 호출해 같은 `entries`를 `_pending`과 `BuildSpawnGuideForecasts`에 전달한다. 이는 runtime 내부 parity다. |
| Editor resolved preview | **Not applicable** | 전용 기능을 찾지 못했다. `BuildBriefingWavePlan`은 runtime briefing이며 Editor preview가 아니다. |
| source 저장·재시작 | **Unknown** | AttackDeck/MapStagePool 기본 Inspector 저장과 재시작을 이번 조사에서 실행하지 않았다. generated 결과 자체는 저장되지 않는다. |

#### Duel active result and determinism evidence

`MapStagePool.asset` live entry 0은 `MapStage_Duel + Deck_Duel + plan=null`이고 `Deck_Duel.useGeneratedWaves=true`라 현재 generated mode 조건을 만족한다. 활성 source인 `Assets/_Project/Scripts/Data/Decks/Deck_Duel.asset`에는 다음 값이 저장되어 있다.

- 고정 `waveSeed=20261972`, `waveGeneratorVersion=7`
- attack unit 15종, 명목 wave 100개, wave당 5→24기, intra-wave spacing 0.5초
- wave 진행 상한 20초, growth 1.12, spawn lead-in 2초
- 9 wave마다 boss, escort 3→4기
- timer 180초, concept 5종, concept hold 3 wave, ramp break wave 15 / units 12

런타임은 명목 20초 grid를 그대로 재생하지 않는다. 첫 wave는 즉시 queue하고 이후에는 전멸, 20초 상한 또는 명시적 pull에 따라 다음 wave를 queue한다. 100-wave unit/group 결과는 asset에 저장되지 않는다.

결정론 주장은 다음 증거 수준으로 제한한다.

| Evidence layer | Status | Exact evidence and conclusion |
| --- | --- | --- |
| Algorithm | **Confirmed** | `WavePatternGenerator.Generate`가 explicit seed를 정규화해 local `Unity.Mathematics.Random`을 만들며 전역/시간 RNG를 사용하지 않는다. 반복 입력에는 seed와 lane count뿐 아니라 deck 및 참조 asset의 값·배열 순서와 generator implementation revision도 포함된다. |
| Version marker | **Confirmed** | `waveGeneratorVersion`은 plan metadata로 전달되지만 그 숫자로 알고리즘 branch/implementation을 선택하지 않는다. 따라서 version 숫자만으로 다른 코드 revision의 결과 재현을 보장하지 않는다. |
| Generic test source | **Confirmed — existing test source** | `WavePatternGeneratorTests.SameSeedProducesSameWaveSummary`, `SameSeedProducesByteIdenticalExpandedSequence`, `WaveConceptGenerationTests.Generation_IsDeterministicForSameLaneCount`, `WaveConceptVariantTests.변주가_있어도_같은_시드는_같은_편성을_낸다`가 일반 generator의 동일-input repeat invariant를 검사한다. |
| Duel-specific repeat/full signature | **Unknown** | `WaveConceptAuthoringTests.EveryMapDeck_IsDeterministic`의 `MapDecks`는 Serpent/Coil/Twin/Spiral/Zig/Hook뿐이고 Duel은 별도 `SiegeDecks`라 제외된다. Duel을 동일 입력으로 여러 번 생성해 full signature를 비교하는 기존 테스트는 찾지 못했다. |
| This investigation's execution/output | **Not executed** | 기존 테스트를 실행하지 않았고 `Deck_Duel`의 100-wave signature를 새로 생성·비교하지 않았다. |

따라서 현재 구현은 **동일 seed, lane count, 전체 입력 데이터와 generator implementation revision에서 반복 가능하도록 구성되어 있고 일반 테스트 source가 그 invariant를 검사한다**고 기술할 수 있다. `Deck_Duel` 전체 출력의 전용 반복 테스트나 이번 조사에서의 실행 통과를 주장할 수는 없다. Authored/generated 중 canonical form, 시간 의미, generated result 고정 여부와 export 경계는 Demo만으로 결정하지 않고 PD-09..12에 남긴다.

### 7. Ground/Air logical layers

Demo의 `PlacementLayer`는 이름과 달리 배치, 통행, 전투 target filter가 공유하는 비트 공간이다. 현재 의미는 다음과 같다.

| Demo term | Current meaning | Somnia interpretation caution |
| --- | --- | --- |
| `Ground` | `MapTileType.Place`가 여는 배치 지면 | 일반 지상 적 이동 layer가 아님 |
| `Path` | `MapTileType.Walk`가 여는 기본 지상 통행 비트 | waypoint path 또는 제3의 높이 layer가 아님 |
| `Air` | 모든 현행 tile 종류가 여는 비행 통행 비트 | no-fly cell이 없다는 Demo 규칙일 뿐 |

`AttackUnitData.traversalLayers=None`은 `Path`로 fallback하고 Skimmer/Dragon 같은 비행 적은 `Air`를 명시한다. `SimFieldInstaller`는 배치용 `placeMask`를 통행 정본으로 사용하지 않고 `tiles`에서 `cellLayers`를 다시 파생한 뒤, 목적지 × 실제 traversal mask마다 flow field를 만든다. 이 분리 덕분에 Spawn/Goal 셀의 배치를 닫아도 이동 경로는 닫히지 않는다. `MovementSystem`은 entity mask에 맞는 목적지 flow slot과 layer-aware `NavGrid`를 선택해 이동과 충돌에 사용한다.

반면 현행 Stage 저작 surface는 모든 layer 허용 또는 전부 배치 금지에 가깝고, Air는 모든 정적 tile과 지상 dynamic obstacle을 통과한다. `MapConnectivity.AllSpawnsReachGoal`도 `Walk`만 보는 4-neighbor 검사라 Air, waypoint 각 구간, 실제 wave roster의 traversal mask를 검증하지 않는다. Somnia의 2D Ground/Air 모델과 검증 경계는 PD-03, PD-04, PD-06, PD-13에 분리해 둔다.

### 8. Selection and selected properties

별도 map selection model 또는 tool manager가 없다.

- 선택 객체: Unity Hierarchy/SceneView에서 선택한 GameObject와 Component
- 선택 도구: Unity 기본 Move/Rotate/Scale
- 속성 수정: `DrawDefaultInspector()`가 표시하는 public/serialized field
- 보조 동작: bounds 제안, 셀 스냅, dev pool 등록

즉 SceneView, Inspector, EditorWindow의 역할은 다음처럼 나뉜다.

| Surface | Current role |
| --- | --- |
| SceneView | 기본 Transform 조작, grid/footprint/marker Gizmo 확인 |
| Inspector | 선택한 선언 컴포넌트의 필드 수정, 제안·스냅·등록 버튼 |
| EditorWindow | 현행 맵 저작 역할 없음 |

과거 `MapPainterWindow`는 `_tool`로 brush mode, `_activeWaypointPath`로 경로, `_target`으로 대상 `MapDocument`를 관리했지만 selected cell 또는 Unity `Selection` 통합은 없었다. 셀 클릭은 선택보다 즉시 mutation에 가까웠다.

### 9. Validation

검증은 한 화면에 통합되어 있지 않으며 다음 계층으로 나뉜다.

1. `MapStageScanner.Scan(MapStage, tileSize)`가 활성 authoring component를 plain `StageScan`으로 수집하고 좌표를 셀로 양자화한다.
2. `DioramaMapBuilder.Validate(StageScan)`가 play area/tile size, Spawn 개수와 lane 연속성, Goal 존재, marker bounds와 blocked overlap, route index/order, bonus spawn, structure 제약의 오류를 전수 목록으로 반환한다.
3. `DioramaMapBuilder.Assemble`은 `Validate` 실패 시 모든 오류를 묶어 `MapGenerationFailedException`을 던진다.
4. `BattleBridge`가 별도로 `MapConnectivity.AllSpawnsReachGoal`을 호출하고, 형식 또는 연결성 실패를 Console error로 남긴 뒤 map teardown으로 hard-fail한다. 이 검사는 `Walk` 셀의 Spawn→Goal 연결성만 보며 Air와 waypoint 구간은 보지 않는다.
5. `StagePoolBuildabilityTests.AllPoolStages_ScanAssembleAndConnect`가 live/dev stage 참조 non-null, live deck non-null, live play area 상한(x≤30, y≤12)을 먼저 검사하고, 모든 stage를 scan → assemble → 같은 connectivity 순서로 검사한다.

따라서 현행 asset gate는 map 자체의 기본 지상 연결성은 확인하지만 map×deck×wave의 최종 참조를 교차 검증하지 않는다. Duel의 Skimmer Unit SO binding 0 → authored route 0건 불일치, wave group의 lane/path 유효성, Ground/Air별 Spawn→route nodes→Goal 도달성은 통과할 수 있다. Somnia의 validation/export 선택은 PD-03, PD-07, PD-13에 분리해 둔다.

편집 중 표시 UX는 제한적이다.

- `MapStage.OnValidate`, `PropFootprint.OnValidate`, `PlacementBlockZone.OnValidate`는 최소 크기 clamp만 한다.
- `PropFootprintEditor`는 parent `MapStage`가 없으면 Inspector Warning을 표시한다. 다른 Marker/Zone Inspector는 부모 부재 경고가 없고 snap 클릭이 조용히 no-op한다. Bonus/Structure에는 별도 규칙 Info `HelpBox`가 있다.
- play area 밖 셀은 Gizmo에서 회색으로 표시한다.
- `MapStageCameraFraming.RenderPrefabPreview`는 `overlay=true`일 때만 `DrawCellOverlay`를 거치는 **부분 저작 진단**이다. 이 경로는 `DioramaMapBuilder.Validate`를 호출하므로 `BonusSpawnAuthoringRules`의 보너스 포탈→골 4-이웃 BFS까지 실행하지만, `Assemble`과 일반 Spawn→Goal `MapConnectivity.AllSpawnsReachGoal`은 실행하지 않는다. `Validate`가 실패하면 논리 overlay를 생략하고 결과 문자열에 `형식오류:...`를 포함하지만, top-level 결과는 `OK|`이고 PNG 생성도 계속하므로 save/build gate가 아니다. `overlay=false`인 preview 호출은 이 validation도 실행하지 않는다.
- 현재 `MapStageEditor`에는 전체 validation 결과를 보여주는 버튼이나 패널이 없다.

### 10. Undo, dirty state, save, and restart persistence

| Operation | Undo | Dirty/save behavior |
| --- | --- | --- |
| Default Inspector/Transform 편집, 수동 Hierarchy 생성·삭제 | Unity 기본 직렬화/Hierarchy Undo가 소유하며 map-specific transaction은 없음 | Prefab Mode 또는 Scene 저장 흐름에 따름; 이번 조사에서 UI 실행은 검증하지 않음 |
| 셀 중심 스냅 | `Undo.RecordObject(transform)` | Transform 변경으로 dirty 처리 |
| play area 제안 | `MapStage`와 모든 직계 child Transform에 Undo 기록 | `EditorUtility.SetDirty(stage)` |
| footprint 제안 | `PropFootprint` 하나에 Undo 기록 | `EditorUtility.SetDirty(footprint)` |
| Camera framing 메뉴(`FrameActiveScene`) | stage/camera와 grid-origin 이동 대상에 Undo 기록 | `EditorSceneManager.MarkSceneDirty`; 메뉴 호출은 자동 저장하지 않음 |
| Camera framing runner(`FrameScene`) | 내부 `FrameActiveScene`과 같은 Undo 기록 | scene을 열고 성공 결과가 `OK`이면 `EditorSceneManager.SaveScene`으로 자동 저장 |
| `NormalizePrefabRoot` runner API | root position/rotation/scale에는 Undo 기록이 없고, 내부 `NormalizeGridOrigin`만 stage/child에 Undo를 기록한다. prefab contents를 즉시 저장·unload하므로 완전한 사용자 Undo transaction은 아님 | `PrefabUtility.SaveAsPrefabAsset`으로 대상 prefab을 직접 저장 |
| Dev pool 등록 | 없음 | pool `SetDirty` 후 `AssetDatabase.SaveAssets` |
| `AuthorSpawnsAndGoal` | 없음 | `SaveAsPrefabAsset`; 신규 dev 등록이 일어난 경우만 pool `SetDirty` + `SaveAssetIfDirty` |
| `AuthorBonusPortals` | 없음 | 대상 prefab만 `SaveAsPrefabAsset`; pool은 건드리지 않음 |
| `EnsureMarkerPropStyle` | 없음 | asset이 없으면 `CreateAsset`; 기존 값을 보존하며 빈 spawn/goal 슬롯만 채우고, 변경 시 `SetDirty` + `SaveAssetIfDirty` |
| `Create Goal Portal (Yellow)` | 없음 | instantiate/unpack 후 `GoalPortal_Yellow.prefab`을 `SaveAsPrefabAsset`으로 직접 저장 |
| Duel generator | 없음 | `GenerateMenu`만 기존 prefab 덮어쓰기를 확인하고 직접 `Generate()`/runner는 확인하지 않음; prefab은 항상 `SaveAsPrefabAsset`, pool `SetDirty` + global `SaveAssets`는 upsert가 변경된 경우만 |

Unity 재시작 후 유지되는 정본은 저장된 `MapStage` 프리팹의 hierarchy/Transform/component field와 `MapStagePool.asset`, `MarkerPropStyle.asset` 참조다. `GeneratedMap`은 저장하지 않고 배틀 진입 때 다시 파생한다. 저장하지 않은 Prefab/Scene 편집은 유지된다고 보장할 수 없다.

과거 `MapPainterWindow`에서는 target이 없을 때만 `SaveFilePanelInProject` → `CreateInstance<MapDocument>` → `CreateAsset`으로 새 asset을 먼저 만들었다. 이후 신규/기존 target 모두 `MapDocumentBuilder.WriteToDocument` → `SetDirty` → dev 등록 → `SaveAssets`의 공통 Bake 경로를 거쳤다. Bake된 asset은 재시작 후 유지되지만, 창 내부의 미직렬화 편집 buffer는 Bake 전 유지되지 않으며 Undo도 없었다.

## Implementation Evidence

| Concern | File/type | Confirmed behavior |
| --- | --- | --- |
| Current root authoring type | `Assets/_Project/Scripts/Core/MapStage/MapStage.cs` — `MapStage` | play area, origin, preview tile size를 선언하고 외곽/grid Gizmo 표시 |
| Current inspectors | `Assets/_Project/Editor/MapStageEditors.cs` | Default Inspector에 bounds 제안, snap, dev 등록을 추가하며 일부 동작에 Undo/dirty 적용 |
| Coordinate conversion | `Assets/_Project/Scripts/Data/MapStage/MapStageMath.cs` — `MapStageMath`; `Assets/_Project/Editor/MapStageCameraFraming.cs` — `DrawCellOverlay` | local XZ → cell floor/center 산식은 공유하지만 scanner의 runtime tile size와 Gizmo/snap의 preview tile size는 입력 권위가 다름; preview overlay는 origin xz/tile size를 우회 |
| Scene scan | `Assets/_Project/Scripts/Core/MapStage/MapStageScanner.cs` — `MapStageScanner` | 활성 footprint/zone/marker를 수집해 plain `StageScan` 생성 |
| Cell blocking | `Assets/_Project/Scripts/Core/MapStage/PropFootprint.cs` — `PropFootprint`; `Assets/_Project/Scripts/Core/MapStage/PlacementBlockZone.cs` — `PlacementBlockZone` | 전자는 기본 Walk/Path 통행+모든 배치 차단(Air 통행 유지), 후자는 배치만 차단 |
| Live prefab attachment | `Assets/_Project/Art/Theme/duel/MapStage_Duel.prefab`; `Assets/_Project/Art/Theme/street/MapStage_Street.prefab`; `Assets/_Project/Art/Theme/subway/MapStage_Subway.prefab`; `Assets/_Project/Art/Theme/street_day/MapStage_StreetDay.prefab`; `Assets/_Project/Scripts/Core/MapStage/MapStageScanner.cs` — `Scan` | 네 live 맵은 같은 component scan surface를 쓰지만 Duel footprint는 전용 host, 다른 세 맵의 footprint는 SpriteRenderer art GameObject에 동거하며 scanner는 두 형태 모두 component Transform에서 cell anchor를 얻음 |
| Spawn/Goal | `Assets/_Project/Scripts/Core/MapStage/SpawnMarker.cs` — `SpawnMarker`; `Assets/_Project/Scripts/Core/MapStage/GoalMarker.cs` — `GoalMarker` | explicit marker, snap, colored/labeled Gizmo, lane index 정렬 |
| Path nodes | `Assets/_Project/Scripts/Core/MapStage/RouteMarker.cs` — `RouteMarker` | route/order 필드와 chain Gizmo는 있으나 interactive node tool은 없음 |
| Duel derived path | `Assets/_Project/Art/Theme/duel/MapStage_Duel.prefab`; `Assets/_Project/Scripts/Data/PlacementLayer.cs` — `PlacementLayers`; `Assets/_Project/Scripts/Bridge/SimFieldInstaller.cs`; `Assets/_Project/Scripts/Battle/Effects/FlowFieldBuilder.cs`; `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` | authored route 0건과 별개로 Goal/목적지 × traversal mask별 flow가 파생됨; 중앙 분리대는 기본 Path를 우회시키고 Air에는 열림 |
| Route resolution | `Assets/_Project/Scripts/Battle/Movement/WaypointProgress.cs` — `WaypointRouting`; `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CreateEnemyEntity` | Unit SO → Wave Concept → lane default 순으로 raw index를 해석함; 최종 `-1`은 정상적인 no-waypoint 상태이고 invalid positive Unit/Concept index는 warning 후 binding을 버리고 current-destination navigation 사용 |
| Layer semantics | `Assets/_Project/Scripts/Data/PlacementLayer.cs` — `PlacementLayer`; `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `EffectiveTraversalLayers`; `Assets/_Project/Scripts/Bridge/SimFieldInstaller.cs` | Demo의 Ground는 배치 지면, Path는 기본 지상 통행, Air는 모든 tile 통행이며 placement mask와 traversal 파생은 분리됨 |
| Encounter pairing | `Assets/_Project/Scripts/Data/MapStage/MapStagePool.cs` — `MapStagePool.Entry`; `Assets/_Project/Data/Maps/MapStagePool.asset`; `BattleBridge.TryInitializeGeneratedWaves` | stage + deck + optional plan을 짝지음; Duel은 `Deck_Duel`, `plan=null`, `useGeneratedWaves=true`이고 test/authored override가 없어 generated mode 조건을 만족 |
| Duel wave source/result | `Assets/_Project/Scripts/Data/Decks/Deck_Duel.asset`; `Assets/_Project/Scripts/Data/WavePatternGenerator.cs`; `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryInitializeGeneratedWaves/QueueDueWaves/QueueWave` | seed·lane count·전체 deck/reference 입력과 구현 revision에서 transient 100-wave plan을 만들고 전멸/20초 상한/pull로 진행; 같은 expanded spawn 목록을 runtime pending과 runtime forecast가 공유 |
| Authored wave surface | `Assets/_Project/Scripts/Data/WavePlanAsset.cs`; `Assets/_Project/Editor/WavePlanAssetEditor.cs`; `Assets/_Project/Editor/WavePlanTestLauncher.cs` | source timeline Inspector와 Play 버튼은 있으나 lane 필드를 숨기고 path/layer 축이 없으며 exact map+plan을 고정하거나 resolved result를 preview하지 않음 |
| Wave determinism algorithm | `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `Generate` | explicit seed로 local `Unity.Mathematics.Random`을 생성함; `waveGeneratorVersion`은 metadata이며 algorithm selector가 아님 |
| Generic determinism test source | `Assets/_Project/Tests/EditMode/WavePatternGeneratorTests.cs`; `WaveConceptGenerationTests.cs`; `WaveConceptVariantTests.cs` | 동일 입력 반복 결과를 비교하는 일반 generator 테스트가 존재함; 이번 조사에서는 실행하지 않음 |
| Duel repeat-test coverage | `Assets/_Project/Tests/EditModeAssets/WaveConceptAuthoringTests.cs` — `MapDecks`, `SiegeDecks`, `EveryMapDeck_IsDeterministic` | repeat signature 테스트의 `MapDecks`에는 Duel이 없고 Duel은 별도 `SiegeDecks`; Duel full-output 전용 반복 비교는 찾지 못함 |
| Skimmer asset route binding | `Assets/_Project/Data/Enemies/Enemy_Skimmer.asset` | `traversalLayers=Air`, `waypointPathIndex=0` |
| Duel authored-route count | `Assets/_Project/Art/Theme/duel/MapStage_Duel.prefab`; `Assets/_Project/Scripts/Core/MapStage/RouteMarker.cs.meta` | serialized `RouteMarker` script GUID 0건, 두 Spawn의 lane default binding은 `-1` |
| Skimmer encounter candidate | `Assets/_Project/Data/Maps/MapStagePool.asset`; `Assets/_Project/Scripts/Data/Decks/Deck_Duel.asset`; `Enemy_Skimmer.asset.meta` | live entry 0이 Duel과 Deck_Duel을 짝짓고 deck pool이 Skimmer GUID를 포함 |
| Fixed-seed Skimmer inclusion invariant | `Assets/_Project/Tests/EditModeAssets/WaveConceptAuthoringTests.cs` — `SiegeDecks_MainPhase_ShowsEveryNonBossEnemy` | 현재 test source가 Deck_Duel 저장 seed/2 lanes 결과의 첫 14 waves에 pool 전 종 포함을 직접 검사함; 이번 조사에서 실행 결과나 정확한 wave/group은 산출하지 않음 |
| Runtime invalid-reference handler | `Assets/_Project/Scripts/Battle/Movement/WaypointProgress.cs` — `WaypointRouting`; `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CreateEnemyEntity` | Skimmer Unit SO binding 0이 우선하며 map 범위 밖이면 warning 후 `WaypointFollow`를 생략함; 실제 Duel Play warning 관찰은 미확인 |
| Cross-asset validation boundary | `Assets/_Project/Tests/EditModeAssets/StagePoolBuildabilityTests.cs`; `Assets/_Project/Scripts/Data/MapConnectivity.cs`; `DioramaMapBuilder.Validate` | invalid positive Spawn lane binding은 map validation이 막지만 Unit/Concept binding은 교차 검사하지 않음; connectivity는 Walk Spawn→Goal만 검사 |
| Bonus/Structure markers | `Assets/_Project/Scripts/Core/MapStage/BonusSpawnMarker.cs` — `BonusSpawnMarker`; `Assets/_Project/Scripts/Core/MapStage/StructureMarker.cs` — `StructureMarker` | 보너스 셀과 Instinct 구조물을 typed marker 및 semantic Gizmo로 선언 |
| Validation core | `Assets/_Project/Scripts/Data/MapStage/DioramaMapBuilder.cs` — `DioramaMapBuilder.Validate/Assemble` | 오류 전수 수집, invalid authoring hard-fail, 결정적 정렬 |
| Connectivity | `Assets/_Project/Scripts/Data/MapConnectivity.cs` — `MapConnectivity`; `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BattleBridge` | assemble 이후 별도 검사, 실패 시 Console error와 teardown |
| Asset validation | `Assets/_Project/Tests/EditModeAssets/StagePoolBuildabilityTests.cs` | live/dev pool 전수 scan/assemble/connect 검사 |
| Representative generation | `Assets/_Project/Editor/MapStageDuelGenerator.cs` — `MapStageDuelGenerator` | Duel 23×10 전체 prefab 생성·저장; 이름이 같은 live entry는 제자리 교체하고 없을 때만 0번 삽입, pool 변경 시에만 저장. 현재 확인되는 유일한 full-live-prefab generator이며 Somnia 적용 대상이 아님 |
| Programmatic marker edits | `Assets/_Project/Editor/MapStageAuthoringTools.cs` — `MapStageAuthoringTools` | marker 교체/추가와 prefab 저장, 공용 marker style asset 보장; 다른 live prefab에는 targeted mutation일 뿐 전체 맵 generator가 아니며 완전한 사용자 Undo transaction도 없음 |
| Shared marker visuals | `Assets/_Project/Scripts/Data/MapStage/MarkerPropStyle.cs` — `MarkerPropStyle`; `Assets/_Project/Scripts/Presentation/MarkerPropInstaller.cs` — `MarkerPropInstaller` | prefab 참조는 style asset에 저장하고, 활성 marker 자식으로 생성한 prefab transform을 `visualRoot`로 등록 |
| Preview/scene camera | `Assets/_Project/Editor/MapStageCameraFraming.cs` — `MapStageCameraFraming` | runtime framing 식 재사용; 메뉴는 scene dirty만 표시하고 runner는 성공 시 scene 저장; preview는 overlay를 켠 경우에만 부분 validation 뒤 cell overlay 생성 |
| Persistent pool | `Assets/_Project/Scripts/Data/MapStage/MapStagePool.cs` — `MapStagePool` | live/dev stage와 deck/plan pairing을 ScriptableObject에 저장; live upsert는 이름 기반 제자리 갱신 또는 신규 위치 삽입 |
| Historical window | `ba70aaab^:Assets/_Project/Editor/MapPainterWindow.cs` — `MapPainterWindow` | IMGUI grid brush, in-memory tool state, validation/Bake; current HEAD에는 없음 |
| Historical retirement | commit `ba70aaab` | `MapPainterWindow`, `MapDocument`와 관련 assets/tests 삭제 |

## Patterns for Somnia

| Pattern | Classification | Reason | Somnia adaptation |
| --- | --- | --- | --- |
| EditorWindow 레이아웃 | **Adapt carefully** | 역사 Painter의 toolbar → grid → diagnostics/Bake 흐름은 이해하기 쉽지만, 단일 IMGUI 창의 in-memory state와 현행 삭제 상태를 그대로 가져갈 수 없다. | Somnia editor model을 정본으로 하는 모듈형 창으로 재구현하고 tool palette, viewport, selection properties, diagnostics를 명시적으로 분리한다. |
| SceneView 도구 전환 | **Do not copy** | 현행에는 map-specific `EditorTool`, `OnSceneGUI`, Overlay 또는 tool-state 구현이 없다. 기본 Transform만으로 cell paint UX를 설명할 수 없다. | Somnia가 SceneView painting을 택하면 `EditorTool`/명시적 toolbar와 입력 캡처·취소 규칙을 새로 설계한다. |
| Cell painting UX | **Adopt concept** | 역사 Painter의 명시 brush, click/drag stroke, 즉시 cell feedback은 유용하다. `MapDocument` 저작 세대와 그 UI는 은퇴했고 Undo가 없었다. 단 `MapTileType` 자체는 현행 `GeneratedMap.tiles`가 계속 사용한다. | brush semantics와 즉시 피드백만 채택하고 Somnia 계약 위에서 stroke 1회 단위 Undo, bounds 처리, selection 충돌 규칙을 재구현한다. |
| Spawn/Goal 배치 | **Adopt concept** | typed marker, cell snap, 색/라벨 Gizmo, 명시 lane index가 저작 의도를 즉시 보여준다. | Somnia의 좌표·ID 정책으로 marker model을 정의하고 create/move/delete를 동일 validation·Undo 경로에 연결한다. |
| Path 편집 | **Adapt carefully** | `(routeIndex, order)`는 단순하고 결정적이지만 live 사용 0건이며 drag/reorder/stable-ID UI가 없다. raw index는 unit/concept/lane asset에 분산될 수 있다. | 안정 route/node ID, 명시 edge/order, node/segment selection, insert/delete/reorder UX와 실제 encounter Play test를 함께 설계한다. |
| Derived path preview | **Adopt concept** | 같은 2D topology에서도 목적지와 traversal layer에 따라 실제 flow가 달라지고, runtime spawn forecast와 actual spawn이 같은 route resolution을 사용한다. 현행 Editor에는 resolved path preview가 없다. | 선택 wave/group의 Spawn→route nodes→Goal을 Ground/Air별로 표시하고 preview·validator·export가 같은 resolver를 사용하게 한다. |
| Wave timeline과 resolved result | **Adopt concept** | authored Inspector source timeline과 generated runtime forecast는 기획자가 결과를 읽는 데 유용하지만 현재 서로 분리되어 있고 Editor resolved-result preview는 없다. | unit/count/timing/lane/route/resolved layer를 하나의 encounter timeline에서 편집·확인하고 map viewport와 양방향 highlight한다. |
| Deterministic wave generation | **Adapt carefully** | explicit seed와 local RNG는 반복 가능한 보조 저작에 유용하지만 결과가 transient이고, generator version은 algorithm selector가 아니며 런타임 진행 규칙도 결합되어 있다. | 공통 생성 보조 도구로만 사용하고 기획자가 펼친 결과를 검토·고정할지 여부를 정책으로 결정한다. Client/Server가 Unity 생성기를 각각 재실행하게 하지 않는다. |
| Ground/Air logical traversal | **Adopt concept** | 2D cell 위에서 unit별 traversal mask와 목적지별 flow slot을 사용해 지상·공중 결과를 분리한다. | Somnia의 Ground/Air를 명시적 movement layer로 정의하고 layer별 cell 통행, route compatibility, connectivity preview를 제공한다. |
| Demo layer encoding | **Do not copy** | `Ground`는 배치 지면이고 기본 지상 이동은 `Path`이며, Air는 모든 tile을 열고 같은 enum을 placement/traversal/target에 재사용한다. | movement, terrain/surface, traversal, placement, combat targeting을 별도 개념으로 두고 Somnia 용어와 mask 규칙을 새로 정한다. |
| Map + wave encounter pairing | **Adopt concept** | stage와 deck/plan의 명시적 짝은 유용하지만 source mode가 `plan`, override와 `useGeneratedWaves` 우선순위에 암묵적으로 좌우되고 Play test는 exact pair를 고정하지 않는다. | 하나의 authoring context에서 map과 wave source/mode를 명시하고 validate, preview, Play, export가 같은 pair와 revision을 사용하게 한다. |
| Path reference와 fallback | **Do not copy** | map-local raw index가 공유 unit/concept asset에 들어가며 invalid positive Unit/Concept binding은 runtime warning 뒤 waypoint가 제거되어 저작 오류가 non-waypoint navigation으로 계속 진행된다. | encounter-local stable reference로 완전히 resolve하고 dangling/layer-incompatible route는 export hard error로 처리한다. |
| Selection | **Adapt carefully** | Unity 기본 selection은 단순하지만 selected cell/element와 active map tool을 표현하지 않는다. 역사 Painter도 클릭 즉시 mutation이고 custom selection이 없다. | Unity `Selection`과 연동하되 editor-owned `activeTool`, `selectedElement`, hover/preview 상태를 구분하고 Inspector/SceneView가 같은 상태를 보게 한다. |
| Undo/Redo | **Adapt carefully** | 현행 제안/snap/camera는 Undo를 쓰지만 generator, marker replacement, pool mutation은 닫히지 않는다. 역사 Painter stroke도 Undo가 없다. | paint stroke, create/move/delete, path reorder, property edit를 각각 원자적 Undo transaction으로 묶고 저장은 Undo와 분리한다. |
| Validation | **Adopt concept** | scan → plain validator → assemble → connectivity → asset test의 규칙 재사용과 오류 전수 수집은 강한 패턴이다. 다만 편집 중 통합 UI가 없고 map×wave×layer 교차 검증은 빠져 있다. | Somnia validator를 단일 source로 두고 EditorWindow, SceneView, export gate, tests가 structured diagnostic을 공유하며 resolved encounter 전체를 검사하게 한다. |
| 저장 방식 | **Do not copy** | prefab=비주얼+논리 정본, `MapStagePool`, `GeneratedMap` 파생 구조는 Somnia MapData/DTO/export 정책의 근거가 아니다. global `SaveAssets` 부작용도 있다. | designer-authored source의 저장·dirty·Undo와 explicit export를 분리하고 Client/Server는 export 산출물을 소비한다. source identity, export schema·versioning·DTO는 Somnia 계약이 별도로 정한다. |
| 맵별 generator 코드 | **Do not copy** | `MapStageDuelGenerator`는 Duel의 좌표·아트·마커·pool mutation을 코드에 고정하고 재실행 시 designer edit를 덮어쓴다. 사용자 제공 Somnia production 전제와 충돌한다. | 맵 차이는 designer-authored source에 두고 모든 맵이 공통 Editor와 공통 export 경로를 사용한다. 맵 이름별 generator, 하드코딩 좌표, 맵 전용 생성 타입을 두지 않는다. |
| 좌표 변환 | **Adopt concept** | scanner, Gizmo, snap이 `MapStageMath` 산식을 공유해 drift를 줄이고 validator가 preview/runtime tile size 불일치를 잡는다. 그러나 두 generator `Host`와 preview overlay는 tile size 1 직접식을 복제하며, overlay는 origin xz도 누락한다. | 축·원점·반올림 정책은 Somnia 것으로 교체하고 Editor·preview·validator·공통 export가 같은 순수 변환 함수를 사용한다. |
| Semantic Gizmo | **Adopt concept** | 의미별 색·라벨·footprint·route chain과 선택 시에만 상세 grid를 보여 주어 SceneView 노이즈를 제한한다. | Somnia 요소별 색/아이콘 legend와 error overlay를 정의하고 선택/hover 우선순위를 추가한다. |
| Custom Inspector | **Adapt carefully** | 기본 Inspector에 작은 snap/help/action을 더하는 방식은 유지비가 낮지만 전체 workflow, selection, validation을 소유하지 못한다. | Inspector는 선택 요소 세부 속성만 담당하고, 생성 도구·목록·전체 diagnostics는 EditorWindow/SceneView 도구가 담당하게 한다. |

## Policy Decisions Required for Somnia-client

아래 항목은 Dreamsquad에서 채택할 결론이 아니라, Demo 구현이 모호하거나 Somnia 요구와 권위가 달라 **Somnia가 별도로 결정해야 하는 open policy**다. 이미 확정된 전제는 designer-authored source가 정본이고, 명시적 export를 거쳐 Client/Server가 소비하며, 맵별 C# generator에 맵 데이터를 하드코딩하지 않는다는 것뿐이다. `M0 recommended default`는 100시간 MVP의 구현 범위를 줄이기 위한 제안이며 승인된 Somnia 계약이 아니다. 결정이 확정되면 별도 Somnia spec/ADR이 권위를 가져야 한다.

| ID | Open policy decision | Why the Demo does not decide it | M0 recommended default |
| --- | --- | --- | --- |
| PD-01 | Authoring aggregate와 논리 source의 소유권 | Demo는 prefab hierarchy, deck/plan SO, pool을 나눠 저장하고 runtime에서 결합한다. Somnia의 source 형식이나 visual scene과의 관계를 정하지 않는다. | map, wave source, pairing을 하나의 encounter authoring context에서 열되 논리 source는 art/scene과 분리해 저장한다. invalid draft 저장은 허용한다. |
| PD-02 | Stable identity와 reorder 규칙 | Demo의 이름, lane/route/order 정수와 배열 위치는 참조 안정성을 보장하지 않는다. | map, spawn, goal, lane, route, node, wave/group에 opaque stable ID를 쓰고 표시 순서/order는 별도 필드로 둔다. duplicate/delete 시 dangling reference를 즉시 진단한다. |
| PD-03 | 2D 좌표와 navigation topology | Demo는 XZ→cell을 쓰지만 preview/runtime 입력이 갈리고 flow는 8-neighbor, asset connectivity는 4-neighbor라 규칙이 일치하지 않는다. | 정수 2D cell, 축/원점/rounding을 한 순수 함수로 고정한다. MVP topology는 한 종류만 선택하고 preview·validator·Client/Server가 neighbor/cost/corner rule을 공유한다. |
| PD-04 | Ground/Air, terrain, traversal, placement 모델 | Demo의 `Ground`/`Path`/`Air`는 배치와 통행 의미가 혼재하고 Air는 항상 열린다. | Ground/Air를 movement layer로 두고 cell traversal mask와 placement mask, terrain/surface를 독립 축으로 정의한다. Air-all-cells와 no-fly 여부는 명시 저작값으로 결정한다. |
| PD-05 | Spawn/Goal/lane cardinality | Demo Stage validator는 Spawn 2개를 요구하지만 다른 builder/connectivity 경로는 1개를 허용한 이력이 있어 Somnia 게임 규칙이 아니다. | 최소 1 Spawn과 1 Goal만 editor 구조 기본값으로 두고, 실제 개수·lane 제약은 Somnia 게임 규칙 validator가 소유한다. lane은 stable ID로 참조한다. |
| PD-06 | Route 표현과 movement layer 결합 | live route 사용례는 0건이고 route 자체에는 layer가 없으며 같은 node를 unit layer별 flow로 해석한다. | optional ordered route를 stable route/node ID로 저작하고 지원 movement layer를 명시한다. waypoint 없음은 암묵값 `-1` 대신 `NoAuthoredRoute` 또는 `DerivedNavigation` 같은 직선 의미가 없는 명시 mode로 표현한다. |
| PD-07 | Lane·wave·unit의 route binding과 precedence | Demo는 Unit SO → Wave Concept → lane default의 숨은 우선순위를 사용한다. 최종 `-1`은 정상이고 invalid positive Unit/Concept binding만 runtime에서 warning 후 버려진다. 공유 Unit SO가 map-local path index를 가질 수 있다. | catalog 값은 제안/default로만 사용하고 encounter에서 각 exported group의 final route를 명시적으로 resolve한다. UI에 값과 출처를 보이며 invalid reference는 fallback하지 않는다. |
| PD-08 | Movement layer의 저작 권위 | Demo generated wave는 unit SO의 traversal layer를 concept altitude로 filter하지만 authored wave에는 layer 축이 없다. | MVP에서는 unit catalogue가 하나의 Ground/Air movement layer를 소유하고 wave는 이를 읽기 전용으로 표시·검증한다. wave별 layer override나 layer 전환은 별도 게임 요구가 생길 때 추가한다. |
| PD-09 | Wave의 canonical authoring form | Demo에는 explicit `WavePlanAsset`, generated `AttackDeck`, legacy spawn list가 동시에 있고 선택은 null/fallback에 좌우된다. | explicit wave/group timeline을 canonical source로 삼고 unit, count, time/spacing, lane, route를 직접 저작한다. source mode는 UI에서 명시하고 legacy fallback은 두지 않는다. |
| PD-10 | Wave 진행 시간의 의미 | authored plan은 absolute timeline이고 Duel generated mode는 전멸/20초 cap/pull로 진행한다. 명목 trigger time이 실제 queue time과 다르다. | MVP는 absolute timeline 한 종류로 시작한다. clear/cap/pull이 필요하면 별도 명시적 trigger rule로 모델링하고 Editor preview와 Server 규칙에 함께 노출한다. |
| PD-11 | Procedural wave 생성과 결과 고정 | 동일 전체 입력과 구현 revision에서 반복 가능한 구조지만 Duel의 100-wave 결과는 asset에 저장되지 않는다. `waveGeneratorVersion`도 algorithm code를 선택하지 않아 숫자만으로 재현을 보장하지 않는다. | generator는 authoring assistant로만 두고 기획자가 preview한 resolved groups를 source 또는 export에 freeze한다. seed/recipe/version은 재현 metadata로 보존할 수 있다. |
| PD-12 | Export artifact와 gameplay-derived data의 생성 주체 | Demo는 `GeneratedMap`, `GeneratedWavePlan`, flow field를 Unity runtime에서 다시 파생한다. 어떤 결과를 Server와 공유할지 정하지 않는다. | versioned logical payload에 cell layer masks, stable references, ordered routes, resolved wave groups를 포함한다. Unity object/ECS native flow data는 export하지 않으며 gameplay-critical 파생은 단일 exporter/compiler 또는 동일 버전의 공통 계약 한 곳이 소유한다. |
| PD-13 | Validation severity와 fallback | map 검증은 분산돼 있다. invalid positive Spawn lane binding은 map validation이 막지만 공유 Unit/Concept binding은 runtime에서야 warning 후 제거되며, 정상 최종 `-1`과도 표현이 섞여 있다. | invalid draft 저장은 허용하되 export는 non-bypassable hard gate로 막는다. missing ID, lane/path/layer 불일치, layer별 unreachable segment는 error이며 runtime silent fallback은 금지한다. |
| PD-14 | Preview, Play, export의 exact-pair parity | 현행 plan test는 plan만 고정하고 map은 pool 선택 규칙에 맡긴다. | 선택한 map + wave source + revision을 하나의 snapshot으로 고정하고 preview, validate, Play, export가 같은 resolver와 validator 결과를 소비한다. |
| PD-15 | Versioning, reproducibility, Client/Server import failure | Demo의 generatorVersion은 Somnia schema/export 계약이 아니며 양측 소비 실패 정책도 없다. | schema/exporter version, source ID/revision/hash, canonical ordering을 기록한다. unsupported version, hash/reference mismatch는 Client와 Server 모두 명시적 load failure로 처리한다. |
| PD-16 | Save, Undo, validation, export lifecycle | Demo에는 auto-save runner, global `SaveAssets`, no-Undo generator와 일반 Inspector 저장이 혼재한다. | edit transaction은 원자적 Undo를 제공하고 Save는 draft persistence, Validate는 진단, Export는 마지막 저장 source의 명시 동작으로 분리한다. export 성공이 editor buffer나 source를 몰래 수정하지 않게 한다. |

정책 결정을 실제로 닫을 때는 각 ID에 owner, 결정 시점, 상태, Somnia spec/ADR 링크를 추가한다. 특히 PD-03/04/06/07/08은 map source model 전에, PD-09/10/11은 wave editor 전에, PD-12/13/15는 Client/Server export 계약 전에 합의되어야 한다.

## Unknowns and Limits

- Unity Editor를 실행해 메뉴 클릭, SceneView 조작, Prefab Mode 저장을 육안 확인하지 않았다. 위 내용은 코드, 직렬화된 prefab/pool asset, spec/reference 문서의 정적 교차 조사 결과다.
- 새 맵을 저장한 뒤 Unity를 재시작하는 persistence 실험은 하지 않았다. 저장된 prefab/SO가 정본이라는 것은 코드와 Unity 직렬화 구조로 확인했지만, 저장하지 않은 상태는 보장하지 않는다.
- 현행 live map 네 개에 `RouteMarker`가 없어 populated route의 authoring과 Play UX를 확인하지 못했다. non-empty route의 양성 근거는 plain builder 단위 테스트이며, populated route의 live asset/Play 검증은 0건이다. Duel의 derived Goal-targeted flow는 코드/에셋에서 추론했지만 Unity에서 실제 궤적을 육안 계측하지 않았다.
- Skimmer의 Unit SO route 0, Duel authored route 0건, Deck_Duel 후보 포함과 fixed-seed 포함 invariant는 확인했다. 그러나 이번 조사에서 해당 테스트를 실행하지 않아 현재 pass 결과와 정확한 Skimmer wave/group을 산출하지 않았고, 실제 Duel Play에서 invalid-route warning이 발생하는 장면도 관찰하지 않았다.
- 일반 generator의 동일-input 반복 테스트 source는 확인했지만 Duel은 기존 full-signature repeat 테스트 대상이 아니다. 이번 조사에서 generator를 실행하거나 정확한 100-wave unit/group signature를 새로 생성·비교하지 않았다.
- 통합 validation Inspector/Window는 현재 코드에 없다. 프리뷰 runner의 실제 일상 사용 빈도와 저작자가 오류를 발견하는 체감 흐름은 확인하지 않았다.
- 과거 `MapPainterWindow`의 사용자 체감과 도메인 reload 동작은 실행하지 않았다. 코드상 편집 buffer와 tool state가 직렬화되지 않았고 Bake 전 persistence/Undo 경로가 없음을 확인했다.
- 과거 문서의 2×2 Walk 금지는 `docs/spec/map-painter-tool/4_walk_width_limit_lift.md`에서 철회되었다. 현행 validation 규칙으로 취급하지 않는다.
- 대표 workflow는 Duel 하나만 end-to-end로 추적했다. Street/Subway/StreetDay는 현행 prefab의 정적 구조와 full-map generator 부재만 비교했으며, 전체 수동·자동화 제작 이력은 조사하지 않았다.
- 사용자 전제로 확정된 범위는 designer-authored source → explicit export → Client/Server consumption과 map-specific generator 금지까지다. 위 PD-01..16은 구현 전에 닫아야 할 질문과 비권위 권고안일 뿐이다. 구체 `MapDataV1`, Client/Server DTO, 게임 규칙, Runtime/Server, export schema·산출물, CI/배포는 이 문서에서 확정하지 않았다.
- `docs/production-transition/**`는 repository firewall과 작업 비범위에 따라 읽거나 사용하지 않았다.
- 이번 조사에서 Unity 테스트와 빌드는 실행하지 않았다. `StagePoolBuildabilityTests` 등 기존 검증 코드의 존재와 동작을 읽어 확인한 것이며, 새 테스트 결과를 주장하지 않는다.
- 최종 검증 시 HEAD는 기준 SHA에서 변하지 않았고, tracked/cached diff는 비어 있으며 `git status`에는 이 신규 문서만 표시됐다. untracked 파일 대상 `git diff --no-index --check`에서도 whitespace 오류가 없었고 EOF newline을 확인했다. 코드와 Asset 변경은 없다.
