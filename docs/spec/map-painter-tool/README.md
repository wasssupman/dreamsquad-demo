# Map Painter Tool — 맵 데이터 시각 편집기

**상태: 완료 2026-07-23** (units 0+1 구현·reflection 검증. 마우스 페인팅 육안만 남음)

## 목표

`MapDocument`(수동 맵)을 **격자에서 직접 그려** 만들고 편집하는 EditorWindow 를 만든다. 지금까지 execute_code 로 `road bool[,]` 를 짜 굽던 것을, 클릭·드래그로 도로/배치칸을 칠하고 스폰·골을 찍고 실시간 검증 후 Bake 하는 시각 도구로 대체한다. (random-map-pool 후속 후보 "전용 맵 authoring 에디터 툴".)

## 작업 단위 목록

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | Editor | `0_painter_window_and_paint.md` | `MapPainterWindow` — 창·타깃 로드/신규·격자 렌더·셀 페인팅(Road/Buildable/Spawn/Goal) |
| 1 | Editor | `1_validate_and_bake.md` | 실시간 검증(BFS 연결성·2×2·스폰 수) + Bake(파생값 계산 → asset 쓰기) |
| 2 | Handoff | `2_handoff_summary.md` | 인계 (종료 시) |

## Feature-wide 계약

- **위치**: `Assets/_Project/Editor/MapPainterWindow.cs` (Assembly-CSharp-Editor → Wassup.Runtime 자동 참조). 메뉴 `Window/Wassup/Map Painter`.
- **손으로 칠하는 1차 데이터는 Walk/Place + spawns + goal 뿐**. `Deco`/`Env` 는 칠하지 않는다(Deco 는 런타임 `DesignateDeco` 소관, 테마 keepRatio). `mergeDegree`/`chokepoint` 는 Bake 시 tiles 에서 계산(CellClassifier 정의와 동일: mergeDegree=4방향 인접 Walk 수, chokepoint=deg≥3).
- **수동맵 관례 고정**: Bake 시 `authoringSeed = -1`, `generatorVersion = 0`, `propLayerId = 0`.
- **검증은 런타임 계약과 일치**: 스폰→골 BFS 연결성(각 스폰 도달) + 2×2 walk 블록 금지 + 스폰 1~4개 + 골·스폰은 Walk 셀. 하나라도 실패면 Bake 비활성(런타임 `MapConnectivity.AllSpawnsReachGoal` 가 잡기 전에 authoring 에서 차단).
- **덮어쓰기 = GUID 유지**: 기존 `MapDocument` 타깃에 Bake 하면 `MapDocumentBuilder.WriteToDocument` 로 그 asset 에 다시 굽는다 → 씬/풀 배선 불변. 신규는 저장 경로를 받아 새 asset 생성.
- **런타임 오염 없음**: 순수 에디터 도구. 씬·플레이·ECS 무관.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설 없음. 기존 `MapDocument` 데이터를 만드는 **authoring 경로만** 추가하며, 소비(ToGeneratedMap → 맵 파이프라인)는 불변.

## 후속 후보

- Deco/Env 셀도 수동 지정(현재 Walk/Place 만).
- 맵 미리보기(테마 타일/프랍 프리뷰).
- 덱 편집기(AttackDeck authoring).
- 대칭/미러 페인트, undo/redo.
