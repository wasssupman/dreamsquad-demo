# 5. 멀티-골 렌더 (마커 + 구조물 + 배경 클리어런스)

## 목적

골 시각을 단일에서 `map.goals` 순회로 확장 → N개 출구가 화면에 보이고, 배경 프랍이 모든 골 구조물을 침범하지 않는다.

## 변경 대상 (리뷰 M2 반영 — TilemapMapView 밖 병렬 단일골 표현 포함)

- `Assets/_Project/Scripts/Core/TilemapMapView.cs`
  - `PaintMarkers(map)` (`:344`) — 골 마커 goals 순회
  - `InstantiateStructureProps(map, theme, plan)` (`:719/721`) — 골 구조물 goals 순회(전 골 동일 프랍)
  - `ResetStructureVisualAnchors(:745)` / `_goalVisualAnchorWorld`(`:93`) — **primary(단일) 유지**(의도: 튜토리얼 앵커는 goals[0]. 리뷰 m2/m3)
- `Assets/_Project/Scripts/Data/BoardVisualPlan.cs` (`:15` `int2 goal`) → `goals[]` 보유
- `Assets/_Project/Scripts/Data/BoardVisualPlanBuilder.cs` (`:82`) — plan.goals 를 map.goals 로 채움
- `Assets/_Project/Scripts/Data/BackgroundPropPlacer.cs` (`:402` `IsNearSpawnOrGoal`) — `plan.goal` 단일 → 전 골 순회(2차 골 구조물 위 배경프랍 오염 방지 — 리뷰 M2)

## 구현

1. **PaintMarkers**: `map.goal` 1칸 → `foreach goal in map.goals`. (스폰 마커는 이미 `map.spawns` 순회 — 동형 복사.)
2. **InstantiateStructureProps**: 골 구조물을 goals 순회로. 각 골 동일 구조물(전 골 동일 취급 계약).
3. **BoardVisualPlan/Builder**: `goal`(단일) 유지하되 `goals[]` 추가(또는 goal→goals). `BackgroundPropPlacer.IsNearSpawnOrGoal` 가 전 골 반경 클리어.
4. **골 비주얼 앵커는 primary 단일 유지** — 튜토리얼(`FirstSessionTutorialController:210`)이 goals[0] 만 지목(멀티골 지목은 후속 후보). handoff 에 의도 명시.
5. 오버레이 정렬·z-fight 규약 유지(바닥 오버레이=음수 정렬 등 기존 gotcha).

## 계약

- 전 골 동일 시각(개별 목표/구분 없음 — 후속 후보).
- 생성 파이프라인 정거장 불변, **인스턴스 수만 1→N**.
- 단일골 맵: goals=[goal] → 마커/구조물/클리어런스 1개(기존과 동일).

## 완료 기준

- [ ] PaintMarkers·InstantiateStructureProps goals 순회, N개 표시
- [ ] BoardVisualPlan.goals + BackgroundPropPlacer 전 골 클리어(2차 골 구조물 위 배경프랍 없음)
- [ ] 골 비주얼 앵커 primary 유지(튜토리얼 무회귀)
- [ ] 단일골 맵 렌더 기존과 동일(회귀)
- [ ] 2골 맵: 골 마커/구조물 2개 육안, 배경프랍 침범 0(오프스크린/Play 스샷)
- [ ] compile 0 error, EditMode green
- [ ] `docs/reference/object-pipeline-map.md` 골 정거장 갱신 필요 여부 확인
