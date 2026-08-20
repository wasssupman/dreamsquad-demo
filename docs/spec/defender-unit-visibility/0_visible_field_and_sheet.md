# 0. visible 필드 + 시트 컬럼

## 목적

`DefenderUnitData` 에 노출 스위치를 두고, 그 값을 유닛 스탯 시트에서 읽고 쓸 수 있게 한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `visible` 필드
- `Assets/_Project/Scripts/Data/StatImport/UnitStatImportDto.cs` — `DefenderStatDto.visible`

## 구현

SO 에 노출 스위치를 더한다:

```csharp
// defender-unit-visibility unit 0 — 0 = 목록에서 숨김, 그 외 = 노출.
// 카탈로그에서는 빼지 않는다 — 저장 프로필의 id 해석은 계속 되어야 한다.
// 기존 에셋은 YAML 에 키가 없어 초기값 1(노출)을 유지한다(백필 없음).
public int visible = 1;
```

DTO 에는 같은 이름의 nullable 필드를 더한다:

```csharp
public int? visible;
```

이름이 1:1 이면 `UnitStatFieldMapper` 가 양방향을 알아서 처리한다 — `ReadFieldsToDto` 가
export 시 읽고(→ 시트 push 때 새 열 등장), `ApplyNonNullFields` 가 import 시 쓴다. 빈 셀은
null → 기존 값 유지(blank=keep). **exporter/applier/서버 `Code.gs` 는 건드리지 않는다.**

`NonReflectedFields` / `ExportSkippedFields` 에 넣지 않는다 — `visible` 은 투영 필드가 아니라
SO 에 그대로 있는 live 필드다(`cost`·`maxOnBoard` 와 동형).

`AttackUnitData`(적)에는 넣지 않는다. 적은 목록 UI 가 없어 소비처가 0 이다.

## 완료 기준

- [ ] 컴파일 통과
- [ ] 유닛 스탯 export 결과의 defenders 27행 전부에 `"visible": 1` 이 있고 기존 컬럼은 불변
- [ ] `visible: 0` 행을 import 하면 그 필드만 `0` 이 되고 `displayName`·`health` 등은 불변
- [ ] `visible` 키를 뺀 행을 import 하면 `0` 이 유지된다(blank=keep). `1` 로 되돌리는 것도 동작
- [ ] 기존 27개 에셋이 백필 없이 `visible == 1` 로 읽힌다 (백필 생략 근거 실증)
