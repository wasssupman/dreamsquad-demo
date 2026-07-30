# 0 — 로비 진입점 + 페이지·뷰 제거

## 목적

로비에서 프리셋 페이지로 가는 길과 그 페이지를 구성하는 UI 를 제거한다. 범용 확인 팝업 하나만 개명해 남긴다.

## 변경 대상

**삭제** (각 `.cs.meta` 짝 포함):
- `Assets/_Project/Scripts/UI/Outgame/PresetPage.cs`
- `Assets/_Project/Scripts/UI/Outgame/PresetPageController.cs`
- `Assets/_Project/Scripts/UI/Outgame/PresetListItemView.cs`
- `Assets/_Project/Scripts/UI/Outgame/PresetUnitCell.cs`

**개명**:
- `Assets/_Project/Scripts/UI/Outgame/PresetConfirmPopup.cs` → `ConfirmPopup.cs` (클래스명 동반 변경)

**편집**:
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs`
  - `presetPanel` SerializeField (`:30`)
  - `OnOpenPreset()` (`:226`)
  - `ClosePanels()` 의 presetPanel 줄 (`:251`)

**씬** (`Assets/_Project/Scenes/OutgameScene.unity`, UnityMCP):
- 로비 `프리셋` 버튼 GameObject
- `PresetPanel` GameObject (하위 `PresetPage`·`CloseButton` 포함)

## 구현

1. `PresetConfirmPopup` → `ConfirmPopup` 개명. 동시에 `Show` 시그니처를 확인 라벨을 받도록 확장한다:
   ```csharp
   public void Show(string message, Action onConfirm, string confirmLabel = "확인")
   ```
   `MakeButton(panel.transform, "적용", ...)` 의 하드코딩 라벨(`:83`)을 이 값으로 교체한다. `EnsureBuilt` 가 1회 빌드라 라벨은 빌드 시점에 고정되므로, 이미 빌드된 뒤 다른 라벨로 `Show` 되면 갱신되도록 확인 버튼의 `TMP_Text` 참조를 필드로 들고 `Show` 에서 대입한다.
2. 뷰·페이지·컨트롤러 4파일 삭제. `ConfirmPopup` 은 이 단위에서 어떤 호출처도 없는 상태가 된다 — 후속 spec 이 쓴다. 미사용 경고가 아닌 미사용 **타입**이므로 컴파일에 영향 없다.
3. `OutgameMenuController` 에서 필드·메서드·ClosePanels 줄 제거. 씬의 버튼 `onClick` 이 `OnOpenPreset` 을 가리키고 있으므로 **씬 버튼을 먼저 지우고** 코드를 지운다(순서가 뒤바뀌면 씬에 missing method 참조가 남는다).
4. 씬에서 버튼·패널 GameObject 제거 후 저장.

## 완료 기준

- [ ] 컴파일 그린 (`read_console` 에러 0)
- [ ] `PresetPage|PresetPageController|PresetListItemView|PresetUnitCell|PresetConfirmPopup` 전체 검색 결과 0건 (`ConfirmPopup` 제외)
- [ ] OutgameScene Play — 로비에 프리셋 버튼 없음, 나머지 버튼(스쿼드·드림캐쳐·히스토리·테스트모드·START) 정상 개폐, 콘솔 에러 0
- [ ] `git status` 에 삭제된 `.cs` 와 `.cs.meta` 가 **짝으로** 올라와 있음
