# 0 — 저작 컴포넌트 + 기즈모

## 목적

디오라마 스테이지의 저작 어휘를 만든다. 전부 **선언만 하는 MonoBehaviour** — 런타임 로직 0, ECS 참조 0 (Authoring/Runtime 분리, CLAUDE.md 제약 4). 아티스트가 씬에서 프랍을 놓는 순간 "이 프랍이 어느 셀을 차지하는지" 기즈모로 보인다.

## 변경 대상

- 신규 폴더 `Assets/_Project/Scripts/Core/MapStage/`
  - `MapStage.cs` — 스테이지 루트
  - `PropFootprint.cs` · `SpawnMarker.cs` · `GoalMarker.cs` · `RouteMarker.cs` · `PlacementBlockZone.cs`
- 신규 `Assets/_Project/Editor/MapStageGizmos.cs` (또는 각 컴포넌트 `OnDrawGizmos` — 에디터 전용 코드는 `#if UNITY_EDITOR`)

## 구현

**`MapStage`** (스테이지 프리팹 루트에 1개):
- `Vector2Int playAreaCells` — 격자 크기 (= `GeneratedMap.gridSize`)
- `Vector3 gridOriginLocal` — 셀 (0,0) 의 최소 모서리가 놓이는 **스테이지 로컬** 위치. **의미 계약 (critic C-1)**: 이 필드가 sim(0,0)↔뷰 대응의 유일한 저작 지점이고, 런타임 정렬(unit 2)이 `grid.transform` 을 이것에 맞춘다. `grid.transform` 의 다른 writer 는 존재하지 않는다(`CenterBoardAtWorldOrigin` 은 unit 2 에서 제거).
- `float previewTileSize = 1` — **에디터 기즈모 표시 전용** (프리팹 격리 모드엔 `BattleBridge` 가 없어 필요). 런타임 양자화는 항상 `BattleBridge.tileSize` 인자를 받는다. 두 값이 다르면 기즈모가 거짓말하므로 unit 1 의 린트가 경고한다.
- 에디터 버튼: "Ground 렌더러 바운즈에서 playArea 제안" (자동 제안 → 수동 트림)

**`PropFootprint`**: `Vector2Int size`(w×h, 최소 1×1) + `Vector2Int anchorOffset`. 차지 셀 = 프랍 위치의 양자화 앵커 셀 + offset 에서 size 만큼. 에디터 버튼: "렌더러/콜라이더 바운즈에서 footprint 제안" (제안일 뿐, 선언이 정본 — 사용자 결정 D6).

**`SpawnMarker`**: `int laneIndex` (결정론 정본 — README 계약 5). **`GoalMarker`**: 셀은 위치에서. (뷰 훅 필드는 unit 4 에서 추가 — 여기서는 마커 존재만.) **`RouteMarker`**: `int routeIndex` + `int order` — 같은 routeIndex 의 order 순 체인이 웨이포인트 경로 하나. **`PlacementBlockZone`**: `Vector2Int size` rect — 배치 금지 영역.

**기즈모**: footprint 차지 셀(적색 반투명), 스폰(레인 번호 라벨), 골, 루트 체인(선 연결), BlockZone, `MapStage` 의 playArea 외곽선. 셀 표시는 전부 `MapStage` 기준 양자화와 **같은 산식**을 써야 한다 — unit 1 의 순수 함수를 에디터가 재사용(산식 이중화 금지).

**역할 프랍 셀 스냅** (D6): footprint/마커 컴포넌트가 붙은 오브젝트는 기즈모에 스냅 프리뷰를 보여주고, 에디터 버튼 "셀 중심에 스냅"을 제공한다. 강제는 아니다 — 양자화가 최종 정본이므로 스냅은 저작 편의.

## 완료 기준

- [ ] compile (Unity 없으면 `dotnet build` — csproj 파일 명시 나열 주의)
- [ ] 빈 씬에 `MapStage` + 프랍 몇 개를 놓고 기즈모로 차지 셀·playArea·스폰 라벨이 보인다 (에디터 육안)
- [ ] 제안 버튼 2종(playArea/footprint)이 동작한다
- [ ] 런타임 로직 0 확인 — Update/Awake 등 수명 메서드 없음

확인 2026-08-18 · 커밋 `c05d1993` — 컴파일 에러 0 사용자 확인 (최초 CS0118: `Wassup.Editor` 네임스페이스 충돌 → `UnityEditor.Editor` 완전 수식으로 수정). 기즈모/버튼 육안은 파일럿 저작 시(unit 5) 재확인.
