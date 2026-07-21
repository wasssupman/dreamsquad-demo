# 0 — config 계약 + 물통 산식

## 목적

새 레이아웃의 모든 치수를 `BattleHudTrayConfig` 로 옮기고, 폴링 판정에 필요한 상수를 계약으로 고정한다. 물통 채움 비율은 경계 분기가 있어 순수 함수로 빼고 EditMode 테스트로 고정한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/BattleHudTrayConfig.cs`
- `Assets/_Project/Data/Config/BattleHudTrayConfig.asset`
- `Assets/_Project/Scripts/UI/CostWellMath.cs` (신규)
- `Assets/_Project/Tests/EditMode/CostWellMathTests.cs` (신규)

## 구현

### config 추가

```
[Header("Tray Sizing")]
public float slotWidth = 154f;          // 목표 슬롯 폭 (트레이 폭 산출 입력)
public float slotHeight = 134f;
public float cornerReservedWidth = 640f; // 하단 코너 위젯 예약폭 (unit 4 D3 실측)

[Header("Cost Cell")]
public float costCellWidth = 154f;      // 계약: <= slotWidth
public float cellNumberHeight = 48f;
public float cellRowGap = 4f;
public Vector2 wellPadding = new Vector2(8f, 6f);
public Color wellBackColor;
public Color wellLiquidColor;
public Color wellLiquidFullColor;
public Color wellSurfaceColor;
public float cellNumberFontSize = 52f;  // 슬롯 가격(40)보다 크게 — 위계 계약

[Header("Slot Name Band")]
public float nameBandHeight = 36f;
public float nameTextHeight = 38f;
```

`slotWidth`/`slotHeight` 를 두는 이유: 트레이 폭은 상수가 아니라 슬롯 수에서 유도한 뒤 클램프되는 값이다(README 치수 절). `placementSize` 는 폴백으로만 남는다.

`cellNumberFontSize`(52) > `costFontSize`(40) 는 위계 계약이다. 같은 크기면 "내 잔량"과 "유닛 가격" 숫자 8개가 한 줄에 동급으로 늘어서 전주의적 구분이 안 된다.

### config 갱신

- `costPlateSize` → `52 × 44`, `costFontSize` → `40`
- `nameBandColor` 알파 `0.72` → `0.88`

### config 제거

- `railSize` · `railOverlap` — unit 1 에서 레일이 사라진다
- `slotCostBolt` — unit 3 에서 **슬롯의** 볼트가 사라진다. 단 코스트 셀에는 볼트를 1개 남기므로(unit 2 H6) 스프라이트 참조 자체는 `cellEnergyIcon` 으로 **이름을 바꿔 유지**한다. 필드를 지우면 authored 스프라이트 링크가 끊긴다.

### config 유지 (제거 취소)

`roles` · `RolePresentation` · `TryGetRole()` 은 **남긴다**. 배지 렌더링은 사라지지만 `entry.color` 가 이름 밴드 틴트의 입력이 된다(unit 3). `roleBadgeSize` / `roleFontSize` 만 제거한다.

### 순수 함수

```csharp
namespace Wassup.UI
{
    // 물통 = CostRuntime.Current 의 소수부. 단 max 에서는 리젠이 멈춰 소수부가
    // 0 이므로, 그대로 그리면 "만땅"이 "빈 통"으로 보인다. 그 분기가 이 함수의
    // 존재 이유다.
    public static class CostWellMath
    {
        // 소수부 되감김 판정 임계. float32 누적 오차로 AddCost 후 소수부가
        // 1 ULP 하향 드리프트하는 경우가 있어(측정: AddCost 케이스의 약 19%),
        // epsilon 없이 비교하면 외부 획득이 자연 충전으로 오분류된다.
        public const float FillEpsilon = 1e-4f;

        public static float WellFill(float current, float max);
        public static int DisplayInt(float current);
    }
}
```

- `WellFill`: `max <= 0` → 0. `current >= max` → 1. 그 외 `current - floor(current)` 를 `Clamp01`.
- `DisplayInt`: `Mathf.FloorToInt(current)`. **클램프하지 않는다** — `CostRuntime.CurrentInt`(`CostRuntime.cs:29`)와 정확히 같은 값을 내야 한다. `_current` 는 음수가 될 수 없으므로(`TrySpend` 가 부족을 막고 `ResetToStart` 가 `Clamp(0, max)`) 방어 클램프는 불필요하고, 넣으면 `CurrentInt` 와의 등가가 깨진다.

### epsilon 이 두 비교에서 정반대인 이유

- **`current >= max` (WellFill 의 max 분기) — epsilon 불필요.** `CostRuntime` 이 상한에서 `_current = _max`(`:99`) / `Mathf.Min(_max, ...)`(`:67`)로 **정확히 대입**하므로 오차가 끼지 않는다.
- **`fill < _prevFill` (unit 2 의 되감김 판정) — epsilon 필수.** 이쪽은 대입이 아니라 **누적 연산의 결과끼리 비교**한다. 정수를 더해도 결과가 다른 binade 로 넘어가면 소수부가 1 ULP 내려간다. `AddCost(1)` 의 약 10% 가 `delta == 1 && fill < _prevFill` 을 동시에 만족해 "자연 충전"으로 오매치된다.

## 완료 기준

- [ ] EditMode 테스트 통과. 최소 케이스:
  - `WellFill(0, 10) == 0`
  - `WellFill(3.5, 10) == 0.5`
  - `WellFill(9.9, 10)` ≈ `0.9`
  - **`WellFill(10, 10) == 1`** (max 분기 — 이 unit 의 핵심)
  - `WellFill(x, 0) == 0` (0 나눗셈 방어)
  - `DisplayInt(3.9) == 3`, `DisplayInt(10) == 10`
  - `DisplayInt(v)` 가 임의 `v ∈ [0, max]` 에서 `CostRuntime.CurrentInt` 와 일치 (등가 계약)
- [ ] 컴파일 통과. rail/roleBadge 참조가 남아 CS 에러가 나면 unit 1·3 에서 함께 걷어낸다(같은 커밋에 묶어도 된다).
- [ ] 에셋 YAML 에 `railSize` / `roleBadgeSize` / `roleFontSize` 키가 남아 있지 않고, `roles` 5엔트리는 **살아 있다**.
- [ ] `costCellWidth <= slotWidth` 가 성립한다.
