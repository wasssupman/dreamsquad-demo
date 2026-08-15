# unit 5 — 이탈 쿨타임 분리 (퇴근 < 사망)

## 목적

지금 인센티브가 **거꾸로** 걸려 있다. 퇴근은 `placementCooldown`(라이브 에셋 26종 = 4초)을 걸고
사망은 아무것도 안 건다 → **죽게 두는 쪽이 항상 빠르다.** unit 2 가 "사망엔 쿨타임 없음"을
의도로 못박고 README 열린 항목에 남겨 둔 그 자리를, 사용자 결정으로 닫는다.

판에서 유닛이 빠지는 두 출구에 **각자의 재배치 대기**를 준다. 퇴근이 사망보다 짧다 —
그래야 "적극적으로 회수한다"가 "방치한다"보다 이득이다.

## 축이 둘이라는 것

트리거는 셋인데 값은 **둘**이다. 섞지 말 것.

| 트리거 | 무슨 사건인가 | 어느 값 |
|---|---|---|
| 배치 성공 | "방금 놓았다" — 연사 게이트 | `placementCooldown` (그대로) |
| 퇴근 | "자리가 비었다" — 이탈 대기 | `EffectiveRetireCooldown` |
| 사망 | "자리가 비었다" — 이탈 대기 | `EffectiveDeathCooldown` |

`placementCooldown` 은 **건드리지 않는다.** 저건 `maxOnBoard > 1` 일 때의 연사 제어이고
(maxOnBoard 1 이면 배치 즉시 소진이라 죽은 값 — `DefenderUnitData` 주석), 이 unit 이 다루는
것은 이탈 축이다. 퇴근이 `placementCooldown` 을 쓰던 것은 unit 2 의 **재활용**이었고,
사망에 값이 붙는 순간 그 재활용은 두 축을 뒤섞는다.

## 계약 — 퇴근 ≤ 사망은 저작이 아니라 구조가 보증한다

두 초를 따로 저작하게 두지 않는다. **`deathCooldown` 하나 + 비율.**

```csharp
public float deathCooldown = 10f;
[Range(0f, 1f)] public float retireCooldownRatio = 0.4f;

public float EffectiveDeathCooldown  => Mathf.Max(0f, deathCooldown);
public float EffectiveRetireCooldown => EffectiveDeathCooldown * Mathf.Clamp01(retireCooldownRatio);
```

- **왜 비율인가**: 두 초를 독립 저작하면 언젠가 뒤집힌다. 그리고 그 인버전은 **화면에 안 보인다**
  — 지금 이 spec 이 고치는 버그가 정확히 그 종류다(4초 대 0초가 반년 동안 아무 증상 없이 서 있었다).
  0~1 범위면 뒤집을 방법이 없다.
- **`Mathf.Clamp01` 이 진짜 방어선이다.** `[Range]` 는 인스펙터만 막는다. 시트 임포터는
  리플렉션으로 필드에 직접 써서 `OnValidate` 도 `Range` 도 안 탄다(`UnitStatFieldMapper`).
  읽는 자리에서 조이는 것이 유일하게 새지 않는 지점이다.
- **"0 = inert" 유지**: `deathCooldown = 0` 이면 둘 다 0 → `StartCooldown` 이 no-op.
- **에셋 편집 0**: 기존 `.asset` YAML 에 두 키가 없으므로 C# 이니셜라이저가 채운다
  (`maxOnBoard = 1` 이 쓴 것과 같은 수법). 10 × 0.4 = **퇴근 4초** — 오늘의 라이브 값과 같다.
  즉 이 unit 이 바꾸는 체감은 **사망이 10초가 된 것 하나**다.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — 위 2 필드 + 2 프로퍼티
- `Assets/_Project/Scripts/UI/DefenderSelector.cs`
  - `OnDefenderRetired` → `EffectiveRetireCooldown`
  - `OnDefenderDiedRefresh` → `OnDefenderDied` 로 개명, `EffectiveDeathCooldown` 시작 + 리페인트
  - 슬롯 빌드의 오버레이 생성 게이트: `placementCooldown > 0` **또는** `EffectiveDeathCooldown > 0`
    (안 고치면 `placementCooldown 0` + 사망 쿨타임 있는 유닛의 오버레이가 아예 안 만들어져
    쿨타임이 안 보인다)
- `Assets/_Project/Scripts/Data/StatImport/UnitStatImportDto.cs` — 두 필드 nullable 추가

## 시트 저작 (사용자 요청)

두 값은 **시트가 소유한다.** import(`UnitStatFieldMapper.ApplyNonNullFields`)와
export(`ReadFieldsToDto` → `UnitStatExporter`) 둘 다 **DTO 필드 이름 일치 리플렉션**이라
DTO 에 필드를 얹는 것으로 왕복이 끝난다 — 매퍼도 익스포터도 손대지 않는다.

- 계약 문서 등재: `docs/spec/unit-stat-spreadsheet-schema/0_json_schema_contract.md`(열 표) ·
  `3_seed_json_and_sheet_guide.md`(헤더 행)
- **Google 시트 Defenders 탭에 열 2개를 추가해야 실제로 저작된다**: `deathCooldown`,
  `retireCooldownRatio`. 열이 없으면 셀 = null = **미변경**이라 이니셜라이저(10 / 0.4)로
  계속 도는 것이 옳은 폴백이다 — 즉 열 추가 전에도 게임은 정상 동작한다.
- 열을 만드는 가장 안전한 경로는 **Unity 발 export** 다(SO → JSON 이 두 열을 이미 포함).
  시트를 손으로 만들 때 값을 비워 두면 그 유닛만 이니셜라이저로 남는다.
- ⚠ 임포터는 dry-run 이 없다 — 돌리면 즉시 디스크 SO 에 쓴다. 검증은 읽기 전용으로만.
- `retireCooldownRatio` 는 시트가 `2.5` 같은 값을 밀어 넣어도 `Clamp01` 이 읽는 자리에서
  조인다. 시트에 유효성 검사를 못 거는 상황을 코드가 감당하는 지점이다.

## 두 핸들러를 여전히 합치지 않는다

unit 2 의 "⚠ `DefenderDied` 핸들러와 합치지 말 것"은 **살아남는다.** 이유만 강해진다:
합치지 않는 근거가 "한쪽에만 쿨타임이 있다"에서 "**두 쪽이 서로 다른 값을 건다**"로 바뀐다.

## 알려진 한계 (범위 밖)

`PlacementCooldownRuntime` 은 유닛 타입 키로 **덮어쓰기**다. `maxOnBoard > 1` 유닛 2기가
있을 때 하나가 죽어 10초가 걸린 뒤 다른 하나를 퇴근시키면 4초로 **덮여** 사망 대기가 세탁된다.
현재 라이브 에셋은 전부 `maxOnBoard = 1`(YAML 키 없음 → 이니셜라이저)이라 관측 불가.
고치려면 이탈 트리거에 `max(remaining, new)` 의미의 두 번째 진입점이 필요한데, 배치 경로의
full-reset 의미(`StartCooldown_Restarts_To_Full_On_Replace`)와 갈라야 해서 지금은 안 만든다.
**`maxOnBoard` 를 2 이상으로 저작하는 순간 같이 처리할 것** — README 후속 후보.

## 완료 기준

- [ ] compile 클린
- [ ] EditMode `DefenderUnitDataCooldownTests` — 퇴근 ≤ 사망이 **어떤 저작값에도** 성립
      (ratio > 1 을 시트가 밀어 넣은 경우 포함), 0 = inert
- [ ] PlayMode `DefenderRetireTest`
  - `Retire_StartsRetireCooldown_ForThatUnitType` (기존 테스트 개명·값 갱신)
  - `Death_StartsLongerCooldown_ThanRetire` — 기존 `Death_DoesNotStartPlacementCooldown` 을
    **뒤집는다.** 그 테스트는 "사망에는 대가가 없다"를 지키던 경비였고, 이 unit 이 그 계약을 폐기한다
- [ ] 회귀: `PlacementCooldownRuntimeTests`(EditMode) · `BoardLimitTrayStateTest`(PlayMode) 그대로 통과
