# 3 — handoff summary

## Commit

- `5592b676` feat(presets): 페이지별 플레이어 프리셋 + authored 프리셋 철거 — spec A 3 units 전부 + spec B 0~6 이 한 커밋에 들어갔다(두 spec 을 분리 커밋하려 했으나 A 의 타입 삭제가 B 의 개명과 같은 컴파일 단위라 분리 시 중간 상태가 컴파일 불가였다).
- 후속 수정은 `docs/spec/page-local-presets/7_handoff_summary.md` 참조.

## Implemented

- 로비 **프리셋 버튼 + PresetPanel 제거**. `OutgameMenuController` 의 `presetPanel` 필드·`OnOpenPreset`·`ClosePanels` 줄 삭제.
- `PresetPage` · `PresetPageController` · `PresetListItemView` · `PresetUnitCell` 삭제.
- `PresetConfirmPopup` → **`ConfirmPopup` 개명 + 확인 라벨 파라미터화**. 원래도 preset 전용 로직이 없는 범용 위젯이었고, page-local-presets 의 미저장 경고·삭제 확인이 이걸 쓴다.
- authored 데이터·시트 경로 전부 삭제: `SquadPresetCollection`(+ `.asset`) · `PresetApply`(+테스트) · `PresetDto` · `PresetSheetApplier`(+테스트) · `PresetSheetRuntimeRefresher` · `PresetSheetExporter` · `PresetCollectionAsset`.
- `UnitStatImportWindow` 의 Preset 섹션 **10개 사이트** 제거(상수 2·필드·prefs 로드·Push 활성 조건·UI 섹션·`RunPresetImport`·`ApplyPresetFetched`·주석·`BuildCombinedJson` 인자·`using`).
- `SheetPushPayload.BuildCombinedJson` 에서 `presetTab` 파라미터 제거(+ 테스트 호출처 1곳).
- 씬: `AllRuntimeRefresher.refresherSources` 4→3 압축, `PresetSheetRuntimeRefresher` 컴포넌트 제거.
- 사문화 레거시 `SquadBuilderView` · `DreamcatcherDeckBuilderView` + 구 빌더 자식 GameObject **10개** 제거.

## Key Files

- `Assets/_Project/Scripts/UI/Outgame/ConfirmPopup.cs` (개명 신규)
- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs` · `SheetPushPayload.cs`
- `Assets/_Project/Scenes/OutgameScene.unity`
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs`

## Verified

- 컴파일 **errors=0** (런타임 + Editor 어셈블리). `dotnet build` 로도 독립 확인.
- EditMode: 베이스라인 1617 → 삭제 후 **1600 / 0 fail**. 정확히 −17 로, 삭제한 두 테스트 파일(`PresetApplyTests` 6 + `PresetSheetApplierTests` 11)과 일치.
- 삭제 타입 전수 검색 0건. 폴더 `.meta` 4개(고아)까지 제거.
- 씬 `missing script` 경고 0.

## Notes — 되돌리면 안 되는 의도

- **`ConfirmPopup` 을 지우지 말 것.** 이름만 preset 이었고 page-local-presets 가 미저장 경고·삭제 확인에 쓴다.
- **`DeckPrune` 은 손대지 않았다.** `profile.dreamcatcherDecks` **전체**를 순회하므로 프리셋 30개 세계에서도 이미 옳다. 확정분 하나로 좁히면 시트에서 숨긴 카드가 나머지 프리셋에 영구 잔존한다.
- **`DcAttachRequirementWiringTests` 의 구 빌더 단정을 되살리지 말 것.** 사문화 컴포넌트를 핀하던 것이고 그 컴포넌트는 이제 없다. 남은 덱 표면은 `DreamcatcherDeckPage` 하나다.
- **서버 시트의 `Presets` 탭과 Apps Script 라우팅은 살아 있다.** 클라이언트가 push 바디에 넣지 않을 뿐이다(무해한 미사용 탭). 서버를 건드리지 않았다.
- 이름만 겹치는 무관 자산은 건드리지 않았다 — `Assets/Layer Lab/**`, `Assets/Spine/Editor/**/ImporterPresets`, `Data/Camera/CameraPreset_*.asset`, `BoardCameraPreset.cs`, `LayerLabPresetImporter.cs`.
- 은퇴 표기: `docs/spec/loadout-preset-page/README.md` · `docs/spec/preset-sheet-import/README.md`.

## Follow-up — 미검증으로 남은 것

- **UnitStatImport 창 육안 확인**: Preset 섹션이 사라지고 나머지(Unit/Enemy/DC/Cost)가 정상인지. 컴파일·코드 제거는 확인됐으나 창을 열어보지 않았다.
- **시트 Push 왕복 실전 검증**: `Presets` 를 제외한 9탭이 정상 반영되는지. **공유 Google 시트에 쓰는 외부 동작이라 사용자 승인 없이 실행하지 않았다.**
- 로비 Play 에서 스탯 리프레시 버튼(`AllRuntimeRefresher` 가 남은 3개를 돌림) 동작 확인.
- `Assets/_Recovery/0 (3).unity` (미추적 복구 잔재)가 삭제된 `PresetPage` 를 참조한다. 빌드·컴파일 무관하고 이번 스코프 밖이라 방치.
