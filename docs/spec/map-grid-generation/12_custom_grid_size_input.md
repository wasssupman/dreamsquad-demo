# Unit 12 — Custom Grid Size Input

## 목적

`MapSettingsPanelView` 에 자유 W×H 입력 추가 (default 20×10). 기존 preset 버튼 (Auto/30x15/20x20/10x20) 은 quick-fill 보조로 유지. `MapGridPreset?` override 를 `int2? gridSizeOverride` 로 단순화하고 preset 은 UI 편의용에 한정.

## 변경 대상

- 수정: `MapGridBattleAdapter.cs` — `Build` 의 4번째 인자를 `int2? gridSizeOverride` 로 교체. 우선순위: cacheDoc > gridSizeOverride > seed-based preset pick.
- 수정: `BattleBridge.cs` — `_mapGridPresetOverride` 제거, `_mapGridGridSizeOverride` (int2?) 신설. setter / getter 동일하게 교체. `BuildMapForBattle` 의 MapGrid 케이스 호출 갱신.
- 수정: `DraftController.cs` — `SetMapGridPreset(MapGridPreset?)` 제거, `SetMapGridGridSize(int2?)` 신설. `SelectedMapGridPreset` 제거, `SelectedMapGridGridSize` (int2?) 신설.
- 수정: `MapSettingsPanelView.cs` — Preset row 는 quick-fill 로 유지(Auto/30x15/20x20/10x20). 그 아래 Map Size row 추가 (W/H InputField, default 20/10). 입력 변경 시 preset 하이라이트 해제 + grid size 푸시.

## 정책

- **Default grid size**: `(20, 10)` — 패널 초기 상태.
- **Auto 버튼**: `gridSizeOverride = null` → BattleBridge 가 seed % allowedPresets 로 크기 선택. 입력 칸은 grayed (참고용).
- **Preset 버튼 (30x15/20x20/10x20)**: 입력 칸 W/H 를 preset 값으로 채움 + `gridSizeOverride = (preset W, preset H)` 푸시.
- **W/H 직접 입력**: preset 하이라이트 해제(Custom 상태) + `gridSizeOverride = (W, H)`. 최소값 6 미만 입력 시 6 으로 clamp.

## 완료 기준

- [ ] 컴파일 0 ERROR.
- [ ] EditMode 0 회귀 (단, `MapGridBattleAdapterTests` 의 preset 시나리오는 int2 override 로 갱신).
- [ ] PlayMode: 패널에서 W=25 H=12 입력 → 25×12 맵 생성. preset 버튼 클릭 → 해당 크기로 변경 + 입력 칸 갱신.
- [x] 2026-05-23 · 7397198 (코드 b96fd8f)
