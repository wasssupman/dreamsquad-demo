# 0. MapPainterWindow — 창 + 격자 페인팅

## 목적

에디터 창을 띄우고, 편집할 `MapDocument`(또는 신규 크기)를 잡고, 격자를 클릭·드래그로 칠하는 뼈대를 만든다. 이 단위는 페인팅까지만(검증·Bake 는 unit 1).

## 변경 대상

- `Assets/_Project/Editor/MapPainterWindow.cs` (신규)

## 구현

- `class MapPainterWindow : EditorWindow`, `[MenuItem("Window/Wassup/Map Painter")]` 로 오픈.
- 상태(in-memory 편집 버퍼):
  - `int _w, _h;` 격자 크기(기본 15×10)
  - `MapTileType[] _tiles;` (Walk/Place 만 사용) — 길이 w*h, 기본 Place
  - `List<Vector2Int> _spawns;` (0~4), `Vector2Int _goal;`
  - `MapDocument _target;` (ObjectField — 지정 시 그 doc 의 tiles/spawns/goal 로드)
  - `enum Tool { Road, Buildable, Spawn, Goal } _tool;`
- 툴바(`EditorGUILayout`): `[New]`(w/h 입력→빈 격자), target ObjectField + `[Load]`, Tool 선택(툴바 버튼), (Bake 버튼은 unit 1).
- 격자 렌더: `w*h` 셀을 `Rect` 로 그린다(`EditorGUI.DrawRect`). 색: Walk=진회색 도로, Place=연갈/녹 배치칸, Spawn 셀=파란 테두리+`S`, Goal 셀=금색+`G`. y=0 을 하단으로(런타임 좌표 규약: 스폰 상단 y=H-1, 골 하단).
- 페인팅: 격자 영역에서 `Event.current` mouse down/drag → 해당 셀에 활성 툴 적용:
  - Road → `_tiles[idx]=Walk`; Buildable → `Place`; Spawn → 스폰 토글(최대 4, 셀은 Walk 로 승격); Goal → `_goal` 이동(셀 Walk 로 승격).
  - 드래그로 연속 칠 지원(Road/Buildable 만). `Repaint()`.
- Load: `MapDocumentBuilder` 는 GeneratedMap 왕복이지만 여기선 `MapDocument` public getter(Width/Tiles/Spawns/Goal)로 직접 버퍼에 로드.

## 완료 기준

- [ ] compile 0 errors (신규 .cs, scope=all refresh)
- [ ] `Window/Wassup/Map Painter` 로 창 열림, 에러 없음
- [ ] 기존 `MapDocument_ArkFunnel` Load 시 격자에 도로/스폰3/골 정확히 표시
- [ ] Road/Buildable 드래그 페인팅, Spawn/Goal 찍기 동작(버퍼 반영)
