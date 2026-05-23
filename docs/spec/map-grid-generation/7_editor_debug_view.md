# Unit 7 — Editor 디버그 뷰

## 목적

seed 와 GenerationSettings 를 Inspector 에서 조작하고 결과를 즉시 시각화하는 Editor 전용 도구를 만든다. 알고리즘 디버깅과 단조로움 검증에 직접 활용. 런타임 빌드에는 포함되지 않는다 (`#if UNITY_EDITOR` 가드).

## 변경 대상

- 신설: `Assets/_Project/Editor/MapGrid/MapGridDebugWindow.cs`
- 신설: `Assets/_Project/Editor/MapGrid/MapGridGizmoRenderer.cs`
- 신설: `Assets/_Project/Editor/Wassup.Editor.asmdef` (이미 있다면 reference 추가만)

## 구현

### `MapGridDebugWindow` (EditorWindow)

메뉴: `Window > Wassup > Map Grid Debug`.

필드:
- `MapGridGenerationSettings settings`
- `int seed` (필드 + `Re-roll` 버튼: `seed = Random.Range(int.MinValue, int.MaxValue)`)
- `MapGridPreset presetOverride` (Optional, 선택 시 settings 의 풀 무시)
- `int generatedAttempts` (read-only, 마지막 Generate 의 attempt 수)
- `int chokepointCount` (read-only)
- `int branchTurnCounts[]` (read-only, spawn 별)

버튼:
- `Generate` — `MapGridGenerator.Generate` 호출, 결과 캐시 후 `SceneView.RepaintAll()`.
- `Bake to MapDocument...` — 결과를 새 `MapDocument` asset 으로 저장 (FileDialog).
- `Sweep 100 seeds` — seed 0~99 batch 실행, 실패율/avg attempt/avg chokepoint 콘솔 로그.

### `MapGridGizmoRenderer`

`SceneView.duringSceneGui` 콜백으로 그리드 / path / spawn / goal / chokepoint 를 Handles 로 렌더.

- 셀 1단위 = 1 world unit (디버그 전용).
- Walk 셀 = 진한 회색 사각형, Place 셀 = 옅은 사각형.
- spawn 셀 = 녹색 ⬤, goal 셀 = 빨강 ★, chokepoint 셀 = 노랑 ◆.
- `mergeDegree` 숫자를 Handles.Label 로 표시 (체크박스로 토글).

### 메모리/생명주기

- Window 가 보유하는 `GeneratedMap` 은 OnDisable 에서 Dispose.
- Generate 재실행 시 이전 결과 Dispose.
- Bake to MapDocument 후 결과 NativeArray 를 즉시 Dispose (SO 에 카피).

## 수동 검증 시나리오

1. Window 열기 → settings 와 seed 0 설정 → Generate.
2. SceneView 에 path/spawn/goal 가시화 확인.
3. 같은 seed 다시 Generate → 결과 동일 (gizmo overlay 변화 없음).
4. seed 1, 2, 3 ... 으로 Generate 10회 → 시각적 다양성 (chokepoint 위치/지류 길이/모양) 확인.
5. Sweep 100 seeds 실행 → 콘솔에 실패율 ≤ 5%, 평균 attempt ≤ 50 로그.
6. Bake to MapDocument → 새 `.asset` 생성됨 → Inspector 에서 필드 채워져 있음.

## 완료 기준

- [ ] `MapGridDebugWindow` 컴파일, Editor 메뉴에서 열림.
- [ ] Generate 결과가 SceneView 에 즉시 표시.
- [ ] OnDisable 에서 NativeArray 누수 없음 (`Allocator.Persistent` 사용 후 Dispose 확인).
- [ ] Sweep 100 seeds 콘솔 통계가 단위 4의 회귀 기준 (실패율 ≤ 5%, 평균 attempt ≤ 50) 만족.
- [ ] Bake to MapDocument 가 정상적인 .asset 을 만들고, 이후 `mapSource=MapGrid + mapDocument` 로 PlayMode 진입 가능.
- [x] 2026-05-23 · 42d0fce
