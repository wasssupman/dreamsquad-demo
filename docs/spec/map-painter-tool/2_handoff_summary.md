# 2. Handoff Summary — map-painter-tool

## Commit

- `6b26e47e` spec 초안
- (이 커밋) units 0+1 — MapPainterWindow.cs 구현

## Implemented

- **`Window/Wassup/Map Painter`** EditorWindow 로 MapDocument 를 격자에서 직접 편집.
- 툴바: New(w/h) blank · MapDocument ObjectField + Load · 툴(Road/Buildable/Spawn/Goal).
- 격자 페인팅: 클릭·드래그로 Walk/Place 칠, 스폰(최대 4·토글)·골 찍기. y=0 하단 규약.
- 실시간 검증: 스폰1~4, 골·스폰 Walk, BFS 연결성(각 스폰→골), 2×2 walk 금지. 실패 시 사유 표시 + Bake 비활성.
- Bake: mergeDegree(4방향 인접 Walk)·chokepoint(deg≥3)·propLayerId=0 계산 → `MapDocumentBuilder.WriteToDocument` (기존=GUID 유지, 신규=SaveFilePanel). authoringSeed=-1.

## Key Files

- `Assets/_Project/Editor/MapPainterWindow.cs` — 전부 여기 한 파일.

## Verified

- compile 0 errors. ArkFunnel Load 정확(walk68/place82/spawns3/goal). Bake 왕복 tileDiff=0·파생값 일관. Validate 4 실패케이스 검출.
- 마우스 페인팅·격자 렌더는 인터랙티브라 사용자 육안(로직은 reflection 검증됨).

## Notes

- 에디터 전용(Assembly-CSharp-Editor). 런타임/씬/ECS 무관.
- Deco/Env 는 칠하지 않음(Deco 는 런타임 테마 keepRatio 소관). Walk/Place + spawns + goal 만 authoring.
- 기존 맵 수정: ObjectField 에 MapDocument 넣고 Load → 편집 → Bake(같은 asset, GUID 불변).

## Follow-up

- Deco/Env 수동 지정, 맵 테마 프리뷰, undo/redo, 덱 편집기.
