# 0. 공유 import 로직 런타임 이동

## 목적

시트→SO 적용 로직을 빌드에서 쓸 수 있도록 Editor 전용 asmdef 에서 런타임 어셈블리로 옮긴다. **동작 무변경 리팩토링** — 규칙이 에디터/런타임에서 갈라지지 않도록 코드를 하나로 공유하는 것이 목적.

## 변경 대상

- 이동 (Editor → `Assets/_Project/Scripts/Data/StatImport/`, 네임스페이스 `Wassup.Data.StatImport`):
  - `UnitStatImportDto.cs` (payload + defender/enemy DTO)
  - `UnitStatFieldMapper.cs` (양방향 리플렉션 매퍼)
  - `DefenderClassFlagsJsonConverter.cs`
  - envelope 파싱 `ParseSheetRows<T>` — `UnitStatImportWindow` 에서 분리해 `SheetEnvelopeParser` (신규 파일)로
- 잔류 (Editor): `UnitStatImportWindow.cs` (UI + AssetDatabase 스캔 + SaveAssetIfDirty), `UnitStatExporter.cs` (파일 저장 UI 포함 — ToDto/ToRowsJson 은 매퍼·컨버터가 런타임으로 가므로 함께 이동해도 무방하나, 이번 unit 은 import 경로만 필수)
- `Assets/_Project/Editor/UnitStatImport/Wassup.Editor.UnitStatImport.asmdef` — 런타임 어셈블리 참조 확인
- `Assets/_Project/Tests/EditMode/Wassup.Tests.EditMode.asmdef` — 참조 갱신 (이미 런타임 참조 보유)
- `Assets/_Project/Tests/EditMode/UnitStatImport/UnitStatImportTests.cs` — using 갱신
- 런타임 asmdef 에 `Newtonsoft.Json.dll` 참조 추가 (`com.unity.nuget.newtonsoft-json` 은 런타임 패키지)

## 구현

- 파일 이동 + 네임스페이스 변경 + using 정리만. 로직 diff 0.
- `ParseSheetRows` 분리 시 `UnitStatImportWindow` 는 새 파서를 호출하는 형태로 축소.
- internal 접근: 이동한 타입들은 public 유지 (이미 public), 테스트의 `InternalsVisibleTo` 의존 멤버는 Window 잔류분(`ApplyPayload`/`BuildSheetUrl`)뿐 — 기존 유지.
- meta 파일은 이동으로 GUID 유지 (`git mv` + Unity 밖 이동 금지 — Unity가 meta 를 따라가게 폴더 이동은 에디터/스크립트로).

## 완료 기준

- [x] compile 오류 없음 (2026-07-06, 신규 어셈블리 위치에서 Newtonsoft 자동 참조 확인 — asmdef 수정 불필요)
- [x] 기존 EditMode 테스트 무수정 통과 (using 1줄 추가만) — 518개 스위트, 무관 상시 실패 1건 외 전부 green
- [x] Import 스모크 1회: `Matched 25, unmatched 0, fields applied 336, projected 19` + asset diff 0 (no-op). Export 는 스위트 내 실 에셋 통합 테스트로 확인
