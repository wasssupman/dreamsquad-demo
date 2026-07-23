# 7. Handoff Summary — multi-goal-map

## Commit

- unit 0 `dafdbd4c` goals 데이터 계약 · unit 1 `6cdfdbae` 멀티-소스 flow field · unit 2 `5d6b7b4d` IsGoalCell · unit 3 `e0b5af19` connectivity · unit 4 `d048a9bb` 페인터 · unit 5 `ca6736c5` 렌더 · unit 6 (이 커밋) 맵 5장 + handoff

## Implemented

- 맵당 골 **1~4개** 지원. 각 스폰이 자기 전용 복도로 **자기 골까지** 완전 독립(명일방주식 다출구).
- **경로탐색**: `FlowFieldBuilder.BuildFromSources`(기존, N-소스 BFS 최근접-골). `BuildFlowField` 가 goals 전체를 Persistent 소스로 굽고 `FlowFieldSingleton.goals` 에 저장.
- **골 판정**: `FlowFieldSingleton.IsGoalCell(cell)`(goals 멤버십, 미설정 시 goalCell 폴백). 도달(MovementSystem)·wall예외(MovementCellTrim)·해저드검증(EffectSpawner)·스모크 4곳 전환.
- **데이터**: `MapDocument.goals[]` + `GeneratedMap.goals`(primary goal=goals[0] 병존, 폴백 [goal]).
- **검증**: `MapConnectivity` 멀티-소스 BFS(각 스폰 아무 골이든 도달). 페인터 골 N개 authoring.
- **렌더**: 골 마커·구조물 N개(map.goals 순회), `BoardVisualPlan.goals` 로 배경프랍 전 골 회피. 골 비주얼 앵커는 primary 유지.
- **맵**: 풀 5장 분리복도 멀티골로 GUID 유지 덮어쓰기(Serpent/Coil/Twin/Spiral/Zig — 6_ 표 참조).

## Key Files

- `Scripts/Data/GeneratedMap.cs`·`MapGrid/MapDocument.cs`·`MapGrid/MapDocumentBuilder.cs` (goals 계약)
- `Scripts/Battle/Effects/FlowFieldSingleton.cs` (goals + IsGoalCell) · `Bridge/BattleBridge.cs` BuildFlowField
- `Scripts/Battle/Movement/MovementSystem.cs`·`MovementCellTrim.cs`·`Battle/Effects/EffectSpawner.cs` (IsGoalCell)
- `Scripts/Data/MapConnectivity.cs` · `Editor/MapPainterWindow.cs` · `Scripts/Core/TilemapMapView.cs` · `Data/BoardVisualPlan(Builder).cs`·`BackgroundPropPlacer.cs`
- 맵 검증기/bake: `scratchpad/akmaps_mg.py`, `bake_data.txt`

## Verified

- compile 0, EditMode **1278 중 1276 green**(2 skip=기존 Ignored). ecs-reviewer 유닛 1·2 SOUND(지적 0).
- 실증: 단일 소스==기존 Build 바이트 동일(회귀 0), 2골 nearest-goal dist, Coil 3골 flow field 각 골 dist=0·각 스폰 자기 복도 도달.
- **병행 세션 dreamcatcher WIP 로 메인 컴파일 일시 차단 → wassup-testrig 격리 배치로 unit 2 검증(1274 green), 이후 메인 회복.**

## Notes (되돌리면 안 됨)

- **접근 변경**: 골 판정은 `dist==0` 이 아니라 **`IsGoalCell`(goals 멤버십/goalCell 폴백)** — dist all-zero EditMode 픽스처가 dist==0 이면 전부 골로 오판하기 때문. 폴백이 픽스처/단일골 회귀를 0으로 유지하는 핵심.
- **무형 롤아웃**: goals 미설정 생산자/픽스처는 전부 goalCell/[goal] 폴백 → 단일골 동작 불변. `GeneratedMap.IsCreated`·`FlowFieldSingleton.IsCreated` 에 goals 넣지 말 것.
- **소유권**: `FlowFieldSingleton.goals` 는 BuildFlowField 가 Persistent 로 만들어 싱글턴에 이관, TeardownFlowField 가 dispose. `_generatedMap.goals` 는 GeneratedMap 소유(BuildFlowField 는 CopyFrom).
- **예산 불변**: 누수=이벤트 카운트, 골 개수 무관. budget-equality·same-map-same-wave 승계.
- 골 비주얼 앵커·튜토리얼은 primary(goals[0]) 만 지목(의도 — 골별 구분은 후속).

## Follow-up

- **사용자 Play**: 각 스폰 자기 골 독립 진행·모든 골 누수·N개 골 렌더 육안. PlayMode `MovementIntegritySmokeTest` 는 IsGoalCell proxy 로 전환됨(멀티골 풀 green).
- 맵 asset 파일명↔모양 불일치(GUID 유지) — 원하면 RenameAsset(GUID 안전).
- `object-pipeline-map.md` 골 정거장 N개 반영 여부.
- 골별 시각 구분/개별 목표, 스폰↔골 명시 배정(현재 최근접 emergent).
