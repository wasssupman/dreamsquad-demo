# 2 — 시트 동기화 (DcCards 탭 3열)

## 목적

부착 제한 3필드를 시트 왕복(import/export)에 편입한다. 시트가 제어 지점이라는 요구의 핵심 unit.

## 변경 대상

- `Assets/_Project/Scripts/Data/StatImport/DcSheetImportDto.cs` — `DcCardDto`
- `Assets/_Project/Editor/UnitStatImport/DcSheetExporter.cs` — export blank 규칙
- `Assets/_Project/Tests/EditMode/UnitStatImport/DcSheetImportTests.cs`

## 구현

1. `DcCardDto` append (필드명 = SO 와 1:1 — reflection 매퍼가 양방향 자동 처리):
   ```csharp
   public DcAttachType? attachType;
   public string attachValue;
   ```
   빈 셀 = null = 기존 값 유지(blank=keep, 기존 컨벤션 — `ApplyNonNullFields` 는 `dtoValue == null` 만 skip 한다: `UnitStatFieldMapper.cs:78`).

   **enum 셀은 이름 문자열이다 — 확인됨**: exporter 가 `StringEnumConverter` 를 쓰고(`DcSheetExporter.cs:21`) 실제 시드 JSON 도 `"type": "Active"`, `"axis": "ClassRanger"` 형태다(`docs/spec/dreamcatcher-sheet-sync/1_seed_dreamcatcher.json`). 따라서 시트에 `Class` / `Guardian` 을 그대로 적는다.

   **제한 해제 수단 = `attachType` 열에 명시적으로 `None`** — 빈 셀은 "유지"이므로 해제가 아니다. `None` 이면 `attachValue` 는 읽히지 않으므로 값 칸은 비우지 않아도 된다.
2. **export blank 규칙 — 제한 없는 카드만**: `attachType == None` 인 행만 2열 전부 blank 로 내보낸다(현재 전 카드가 여기 해당 → 시트에 enum-zero 노이즈 없음).

   > **unit 7 rev 로 구 함정이 사라졌다.** 구 3필드 설계에선 비소비 companion 열을 blank 하면 시트에 안 보이는 잔존값이 kind 를 바꾸는 순간 blank=keep 으로 조용히 되살아났다(review M2 — `attachRequireClass=Support` 잔존 → 의도 "가디언 전용"이 "서포트 전용"으로). 그때는 companion 열을 보이게 내보내는 우회로 막았지만, 값 칸이 하나가 된 지금은 **종류를 바꿀 때 같은 칸을 반드시 함께 보게 되어** 우회가 필요 없다.
3. 서버 시트(DcCards 탭)에는 새 키를 오른쪽 새 열 **2개**로 추가한다(card-visibility 전례 — 시트 측 안내는 handoff 에 한 줄).

> **첫 import 는 카드 에셋 YAML 대량 diff 를 만든다.** `UnitStatImportWindow.cs:326-327` 이 매칭된 모든 SO 에 `SetDirty` + `SaveAssetIfDirty` 를 하므로, 새 필드가 추가된 뒤 첫 import 시 전 카드 에셋에 2개 키가 기록된다. 회귀가 아니라 append 필드의 정상 동작 — handoff 에 미리 적어 다음 세션이 놀라지 않게 한다.

## 완료 기준

- compile 통과.
- EditMode 라운드트립: `attachType=Class/Guardian` 페이로드 적용 → SO 반영 / 빈 셀 → 기존 값 유지 / `UnitId` 케이스 동일 — `DcSheetImportTests` 패턴으로 3케이스 이상.
- export 결과 JSON 에서 무제한 카드 행에 attach 계열 키 미출현(blank 규칙) 확인.

> 아래 확인 기록의 테스트 수·문구는 **rev 전(3필드) 시점**이다. rev 후 최종 수치는 `7_field_shape_rev.md` 참조.

확인 2026-07-25 — 컴파일 에러 0 · EditMode 1327건(1325 pass / 0 fail / 2 기존 Ignore), 신규 5건: import 4(이름 문자열 파싱 · Class/UnitId 적용 · 빈 셀 keep · None 명시 해제) + export 1(`DcSheetAttachRequireExportTests` — 실제 exporter 를 임시 폴더로 돌려 제한 없는 행에 attach 3열이 **부재**함을 확인).
