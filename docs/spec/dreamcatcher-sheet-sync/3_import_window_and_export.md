# 3. Import 창 Dreamcatcher 섹션 + Export + 실 왕복 검증

## 목적

에디터에서 버튼 한 번으로 6탭을 fetch→apply 하고, SO→JSON export 로 시트 재시드가 가능하게 한다. 실 구글 시트 왕복으로 검증한다.

## 변경 대상

- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs` — "Dreamcatcher" 섹션 추가 (탭명 6필드는 기본값 고정 + EditorPrefs, Import/Export 버튼)
- `Assets/_Project/Editor/UnitStatImport/DcSheetExporter.cs` (신규) — 카드/스킬/컨피그 SO → 탭별 행 JSON (자식 탭은 배열 → slot 행 전개, `_skillId`/`_projectileId` 역기록)

## 구현

- fetch: `SheetFetcher.Fetch` 6콜 병렬, 탭별 독립 실패 (성공 탭만 적용).
- apply: unit 2 `DcSheetApplier` + AssetDatabase 스캔(`Assets/_Project/Data/Dreamcatcher`, `Data/Skills`) + SetDirty/SaveAssetIfDirty.
- export: `{탭명}.json` 6파일, id/cardId 오름차순 — 시드 JSON 과 같은 행 형태.

## 완료 기준

- [ ] compile 0 error
- [ ] 실 시트 왕복 ①: 시드 입력된 시트 import → no-op (매칭/스킵 카운트 확인, git diff 재직렬화 정규화 제외)
- [ ] 실 시트 왕복 ②: 시트에서 밸런스 1건+텍스트 1건 변경 → 해당 asset 2개에만 반영됨을 diff 로 확인
- [ ] Export 결과가 시드 JSON 과 구조 일치
