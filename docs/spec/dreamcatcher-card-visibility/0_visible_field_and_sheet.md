# 0. visible 필드 + 시트 컬럼

## 목적

`DreamcatcherCard` 에 노출 스위치를 두고, 그 값을 `DcCards` 시트 탭에서 읽고 쓸 수 있게 한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — `visible` 필드
- `Assets/_Project/Scripts/Data/StatImport/DcSheetImportDto.cs` — `DcCardDto.visible`

## 구현

SO 에 노출 스위치를 더한다:

```csharp
// 0 = 인벤토리에서 숨김. 그 외 = 노출. 기존 에셋은 YAML 에 키가 없어 초기값 1 유지.
public int visible = 1;
```

DTO 에는 같은 이름의 nullable 필드를 더한다:

```csharp
public int? visible;
```

이름이 1:1 이면 `UnitStatFieldMapper` 가 양방향을 알아서 처리한다 — `ReadFieldsToDto` 가 export 시 읽고(→ `DcCards.json` 에 컬럼 등장 → push 시 시트에 새 열 추가), `ApplyNonNullFields` 가 import 시 쓴다. **exporter/applier/서버 설정은 건드리지 않는다.** `Code.gs` 의 `upsertTab` 이 "JSON 에만 있는 새 키는 오른쪽에 새 열" 규칙으로 컬럼을 만들고, 빈 셀은 null → 기존 값 유지(blank=keep).

필드 위치는 끝이 아니어도 된다(이름 기반 역직렬화). 단 enum 이 아니므로 순서 상관없는 값이다.

## 완료 기준

- [x] 컴파일 통과 (2026-07-23)
- [x] Export 한 `DcCards.json` 37행 전부에 `"visible": 1` 이 있고 기존 컬럼(`_skillId`/`id`/`displayName`/`type`/`axis`/`description`)은 그대로
- [x] `visible: 0` 행을 import 하면 그 필드만 `0` 이 되고 `displayName` 등은 불변
- [x] `visible` 키를 뺀 행을 import 하면 `0` 이 유지되고 다른 필드만 바뀐다(blank=keep). `1` 로 되돌리는 것도 동작
- [x] 기존 37장 에셋이 백필 없이 `visible == 1` 로 읽힌다 — 백필 생략 근거 실증
