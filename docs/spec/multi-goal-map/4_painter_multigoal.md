# 4. 페인터 멀티-골 authoring

## 목적

`MapPainterWindow` 로 골을 **여러 개** 찍고 검증·Bake. 지금은 단일 골 1개만.

## 변경 대상

- `Assets/_Project/Editor/MapPainterWindow.cs`

## 구현

1. **골 상태**: 단일 `_goal` → `List<Vector2Int> _goals`(스폰 토글과 동형). `Tool.Goal` 로 셀 찍으면 add/remove 토글, 상한 4.
2. **Load**: `MapDocument.Goals` 전체 로드(폴백: goals 비면 `[Goal]`).
3. **검증(실시간)**: `_goals` 1~4, 각 Walk, 멀티-골 연결성(유닛 3 `AllSpawnsReachAnyGoal` 재사용) — 각 스폰이 아무 골이든 도달. 실패 시 사유 + Bake 비활성.
4. **Bake**: `WriteToDocument` 에 `goals` 배열 기록(유닛 0 계약). primary=goals[0].
5. 렌더: 골 셀 전부 'G' 글리프/색(스폰처럼 다중 표시).

## 계약

- 골·스폰은 Walk 셀만(기존 규약). Deco 는 안 칠함(런타임 소관, map-painter-tool 계약 유지).
- authoring 이 멀티-골 연결성으로 런타임 실패를 사전 차단(런타임 `AllSpawnsReachAnyGoal` 가 잡기 전에).

## 완료 기준

- [x] Goal 툴 다중 찍기(≤4)·토글, Load 가 goals 전체 로드(폴백 [Goal])
- [x] 검증이 멀티-골 연결성 반영(goals 전체 시드 BFS), 골 1~4·각 Walk, 실패 시 Bake 차단
- [x] Bake 가 goals NativeArray 로 WriteToDocument(왕복은 unit 0 `MultiGoal_...RoundTrip` 이 실증)
- [x] compile 0 error(에디터), 기존 EditMode green
- [ ] (사용자) 페인터로 멀티골 찍고 Bake → 문서 goals 확인 (인터랙티브 육안)

확인 2026-07-23 — `_goal`→`List _goals`, Tool.Goal 토글(≤4), Load/렌더/검증/Bake 전부 멀티골. compile 0, EditMode 1276 green.
