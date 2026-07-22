# 3. 배치/데코 명시 지정 — authored Deco

## 목적

지금은 배치(Place)/데코(Deco) 구분이 런타임 시드 커빙(`DesignateDeco`, theme.keepRatio<1)으로 자동 결정된다. 이걸 **페인터에서 직접 칠해 지정**하고, 런타임은 authored Deco 를 그대로 존중(시드 커빙 스킵)하게 한다. → 맵마다 이동(Walk)/배치(Place)/장식(Deco)을 정확히 authoring.

## 변경 대상

- `Assets/_Project/Editor/MapPainterWindow.cs` — Deco 페인트 툴
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — authored Deco 있으면 `DesignateDeco` 스킵

## 구현

**페인터 (MapPainterWindow):**
- `Tool` enum 에 `Deco` 추가. 툴바 `Road/Buildable/Deco/Spawn/Goal`.
- `ApplyTool` Deco: `_tiles[idx]=MapTileType.Deco`(+ 해당 셀 spawn/goal 이면 해제). Road/Buildable 처럼 드래그 연속 칠.
- Deco 색은 기존 `ColorFor`(녹) 유지. Load 는 이미 전 타일종류 로드. 검증 무영향(Deco 는 non-Walk = BFS 벽, 배치 불가).

**런타임 (BattleBridge, DesignateDeco 블록):**
- 커빙 전 `_generatedMap.tiles` 에 **Deco 셀이 하나라도 있으면 = authored** → 커빙 스킵.
- 조건: `... && theme.keepRatio<1 && IsCreated && !HasAuthoredDeco()`.
- authored Deco → 지정한 Place/Deco 그대로. all-Place 문서/절차맵 → 기존 시드 커빙(unit 8, per-map stable) 유지.

## 계약

- **authored Deco = 시드 커빙 완전 대체** (부분 top-up 아님). 한 칸이라도 Deco 를 칠하면 그 맵은 "수동 데코 맵"으로 간주, 런타임이 배치판을 안 건드린다.
- Deco 셀은 배경 프랍 호스트(기존과 동일 소비) + 배치 불가 + 이동 불가.

## 완료 기준

- [x] compile 0 errors, 기존 EditMode green (1267/1269, 0 fail)
- [x] 페인터 Deco 툴로 칠→Bake→문서에 Deco 저장 (12칸 칠 → 구운 문서 deco=12, place 82→70)
- [x] 런타임 스캔: authored Deco 맵 `hasAuthoredDeco=True`(커빙 스킵), ArkFunnel `False`(커빙 유지). 스킵 시 DesignateDeco 가 유일한 Place→Deco 변환기라 배치판 authored 그대로
- [ ] (사용자) authored Deco 맵 Play — 배치칸 지정대로·Deco 에 프랍 육안

확인 2026-07-23 (unit 3 — Deco 툴 + 런타임 authored-deco 존중, 왕복·스캔 실증).
