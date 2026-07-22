# 2. 8탭 병합 payload 빌더

## 목적

전 8탭(유닛 2 + DC 6)을 **in-memory 단일 JSON** 으로 병합해 POST 바디를 만든다. 지금 `DcSheetExporter.ExportCombinedFile` 이 DC 6탭만 파일로 병합하는데, 이를 (a) 유닛 탭 2종 포함 8탭으로, (b) 파일 대신 문자열/JObject 반환으로 일반화한다. **수집 로직은 기존 exporter 재사용** — 중복 없음.

## 변경 대상

- 수정: `Assets/_Project/Editor/UnitStatImport/DcSheetExporter.cs` — `ExportCombinedFile` 내부 병합부를 `BuildCombinedPayload(...) → JObject` 로 추출(파일 쓰기는 그 위 래퍼로 유지, 기존 버튼 무변).
- 신규 or 수정: `Assets/_Project/Editor/UnitStatImport/UnitStatExporter.cs` — 유닛 2탭 행배열을 in-memory 로 반환하는 진입점(현재 `ExportToFolder` 가 파일만 씀 → 행 수집부 추출).
- 신규: 8탭 병합기 — 유닛 2탭 + DC 6탭 행배열을 `SheetTabRegistry`(유닛 1) 순서로 `{ "<탭명>": [rows], ... }` JObject 로 조립. `_note` 등 `_` 접두 top-level 키는 넣지 않음(Apps Script 가 무시하지만 push 바디는 순수 데이터).

## 구현

- 직렬화 설정은 기존 exporter 와 동일: `NullValueHandling.Ignore`(null 필드 생략 = blank=keep 계약) + `StringEnumConverter`(enum=멤버명). `targetClassMask` 는 필드 attribute 컨버터 우선.
- 유닛 탭: `UnitStatExporter.ToDto` 재사용(atk/heal 고유 투영 규칙 포함). DC 탭: `DcSheetExporter` 의 CardRow/MechanicRow/SkillRow + `_skillId`/`_projectileId`/`_effect`/`_target` 정보성 열 그대로.
- 반환: POST 바디용 `string`(직렬화) + 필요 시 JObject. 파일 export(기존 버튼)는 이 빌더 위에서 파일로 쓰는 얇은 래퍼로 남긴다.

## 완료 기준

- [ ] EditMode: 병합 JObject 가 8개 탭 키를 모두 갖고, 각 탭 행이 키 필드(`id` 또는 `cardId`+`slot`)를 포함하며, null 필드가 생략됨을 검증.
- [ ] 기존 "Export SO → JSON Files" / "시트 페이로드(1파일)" 버튼 동작 무변(회귀 없음).
- [ ] compile + 확인 일자·커밋 해시 기록.
