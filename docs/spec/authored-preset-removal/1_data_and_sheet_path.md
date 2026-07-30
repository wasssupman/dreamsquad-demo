# 1 — 데이터·시트 경로 제거

## 목적

authored 프리셋의 데이터 타입·에셋·적용 헬퍼와 `Presets` 시트 import/export/push 경로를 제거한다. **이 단위가 이 spec 에서 가장 넓다** — 시트 툴체인에 파라미터로 얽혀 있다.

## 변경 대상

**삭제** (`.cs.meta` / `.asset.meta` 짝 포함):
- `Assets/_Project/Scripts/Data/Preset/SquadPresetCollection.cs` (+ 빈 `Preset/` 폴더)
- `Assets/_Project/Data/Preset/SquadPresetCollection.asset` (+ 빈 폴더)
- `Assets/_Project/Scripts/Core/Profile/PresetApply.cs`
- `Assets/_Project/Tests/EditMode/Profile/PresetApplyTests.cs`
- `Assets/_Project/Scripts/Data/PresetImport/PresetDto.cs`
- `Assets/_Project/Scripts/Data/PresetImport/PresetSheetApplier.cs` (+ 빈 `PresetImport/` 폴더)
- `Assets/_Project/Tests/EditMode/PresetImport/PresetSheetApplierTests.cs` (+ 빈 폴더)
- `Assets/_Project/Scripts/Core/PresetSheetRuntimeRefresher.cs`
- `Assets/_Project/Editor/UnitStatImport/PresetSheetExporter.cs`
- `Assets/_Project/Editor/UnitStatImport/PresetCollectionAsset.cs`

**편집**:
- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs` — preset 관련 전 사이트:
  `DefaultPresetTab` 상수 · `PresetTabPrefsKey`(`:40`) · `_presetTab` 필드(`:54`) · prefs 로드(`:69`) · Push 활성 조건의 `_presetTab` 절(`:174`) · Preset UI 섹션 전체(`:190~219`: 라벨·탭 필드·`Import Preset`·`Export Preset SO → JSON`) · `RunPresetImport`(`:384`) · `PresetCollectionAsset.Load`(`:404`) · `RunPresetImport` 언급 주석(`:426`) · `BuildCombinedJson` 인자(`:471`)
- `Assets/_Project/Editor/UnitStatImport/SheetPushPayload.cs` — `presetTab` 파라미터(`:18`) · `hasPresets` 산출(`:30`) · `if (hasPresets) AddTab(...)`(`:38`) · 헤더 주석의 탭 수
- `Assets/_Project/Tests/EditMode/UnitStatImport/DcSheetAttachRequireExportTests.cs:64` — `BuildCombinedJson` 호출 인자

**씬** (UnityMCP): `AllRuntimeRefresher.refresherSources` 배열에서 `PresetSheetRuntimeRefresher` 엔트리 제거 후 배열 압축. 해당 컴포넌트를 들고 있던 GameObject 도 제거.

**문서**: `docs/spec/preset-sheet-import/README.md` 상단에 은퇴 한 줄.

## 구현

1. 시트 툴체인부터 정리한다(컴파일이 깨진 채로 오래 두지 않도록 Editor → Runtime 순): `SheetPushPayload.BuildCombinedJson` 에서 `presetTab` 파라미터를 제거하고, 두 호출처(`UnitStatImportWindow:469`, `DcSheetAttachRequireExportTests:64`)를 맞춘다.
2. `UnitStatImportWindow` 의 preset 사이트 10곳 제거. **Push 활성 조건(`:174`)을 빠뜨리지 않는다** — `_presetTab` 이 공백이면 Push 가 비활성이던 절이라, 필드만 지우고 조건을 남기면 컴파일 에러가 된다.
3. Editor exporter 2파일 → 런타임 refresher → DTO/applier → SO/에셋 → `PresetApply` + 테스트 순으로 삭제.
4. 씬에서 refresher 엔트리·GameObject 제거.

## 완료 기준

- [ ] 컴파일 그린 (런타임 + Editor 어셈블리 양쪽)
- [ ] EditMode 전체 그린. 삭제된 2개 테스트 파일만큼 케이스 수가 줄고 **실패 0**
- [ ] `SquadPreset|SquadPresetCollection|PresetApply|PresetDto|PresetSheet` 검색 0건
- [ ] UnitStatImport 창 열림 — Preset 섹션 없음, 나머지 섹션(Unit/Enemy/DC/Cost) 정상 표시
- [ ] **Push 왕복 실전 검증**: 시트 Push 1회 실행 → 성공 응답, `Presets` 를 제외한 9탭이 반영됨
- [ ] 로비 Play — 스탯 리프레시 버튼 정상(`AllRuntimeRefresher` 가 남은 refresher 3개를 돌림), 콘솔 에러 0

---

**검증 기록 2026-07-30 · `5592b676`** — 런타임+Editor 컴파일 errors=0 · EditMode 1617→1600, 실패 0(삭제한 두 테스트 파일 6+11=17 과 정확히 일치) · 삭제 타입 전수 검색 0건 · 고아 폴더 `.meta` 4개 제거 · 씬 `AllRuntimeRefresher` 4→3 압축. **미검증**: UnitStatImport 창 육안 · **시트 Push 왕복(공유 Google 시트에 쓰는 외부 동작이라 승인 없이 미실행)** · 로비 스탯 리프레시 Play.
