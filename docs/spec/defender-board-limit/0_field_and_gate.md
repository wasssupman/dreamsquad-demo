# 0 — 상한 필드와 배치 게이트

## 목적

유닛 SO 에 **판 위 동시 존재 상한**을 두고, 배치 최종 판정에 그 게이트를 건다. 카운트는 새로 저장하지
않고 판 상태에서 센다. 이 작업 단위만으로 규칙은 완성된다 — UI 표현(unit 1)과 소진 셀 조작(unit 2)이
없어도 상한 초과 배치는 거부되고 로그가 남는다.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs`
- `Assets/_Project/Scripts/Data/StatImport/UnitStatImportDto.cs`
- `Assets/_Project/Scripts/Bridge/PlacementRejectReason.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`CanPlaceDefenderAt` + read seam)
- `Assets/_Project/Tests/EditMode/DefenderBoardLimitTests.cs` (신규)

## 구현

**필드** — `cost` 근처, 배치 관련 필드 무리에 둔다.

```csharp
// defender-board-limit 0 — 판 위 동시 존재 상한. 기본 1 = 이 유닛은 판에 한 기.
// 매치당 총 횟수가 아니다 (죽어서 자리가 비면 다시 배치 가능).
// 무제한은 큰 수(100)로 적는다 — "0 = 무제한" 은 아래 폴백과 충돌해 쓰지 않는다.
public int maxOnBoard = 1;
public int EffectiveMaxOnBoard => maxOnBoard <= 0 ? 1 : maxOnBoard;
```

기존 에셋은 YAML 에 키가 없어 **이니셜라이저(1)로 채워진다**. 폴백은 인스펙터/시트에서 0 이나 음수가
들어온 경우의 두 번째 방어선이다(`EffectivePlacementLayers` 와 같은 2중 구조 — 둘을 함께 유지할 것).

**시트 DTO** — `UnitStatImportDto.DefenderStatDto` 에 한 줄. `UnitStatFieldMapper` 가 이름 1:1 반사
매핑이라 매퍼는 손대지 않는다.

```csharp
// defender-board-limit — 판 위 동시 존재 상한. 기본 1.
public int? maxOnBoard;
```

컬럼이 시트에 없거나 비어 있으면 null → 미적용이라 도입 순서 사고가 없다. export(`ReadFieldsToDto`)는
SO 에 같은 이름 필드가 있으므로 다음 push 때 컬럼을 자동으로 만든다.

**거부 사유** — `PlacementRejectReason` **끝에** append (기존 직렬화 값 보존).

```csharp
// defender-board-limit 0 — 이 유닛이 이미 상한만큼 판에 나가 있다.
LimitReached
```

**브리지 read seam + 게이트** — 카운트는 `_defenderByTile` 순회로 센다(보드 최대 수십 칸).

```csharp
// defender-board-limit 0 — 이 유닛 타입이 지금 판에 몇 기 있나. 상한 판정과 트레이 표현의 단일 출처.
// 파생값이므로 매치 리셋 훅이 필요 없다 (_defenderByTile 이 비면 자동 0).
public int DeployedCountOf(DefenderUnitData unit)
```

`CanPlaceDefenderAt` 안에서 **풀 검사 뒤 · 코스트 검사 앞**에 둔다 — 사유 우선순위(구조 > 자원)와
로그가 일치한다.

```csharp
if (unitData != null && DeployedCountOf(unitData) >= unitData.EffectiveMaxOnBoard)
{
    reason = PlacementRejectReason.LimitReached;
    return false;
}
```

`PlaceDefenderAs` / `TryBeginDefenderDeployment` 둘 다 이 함수를 지나므로 게이트는 한 곳이면 된다.
대기 배치(`PendingDeployment`)는 `TryBeginDefenderDeployment` 이 즉시 `_defenderByTile` 에 넣으므로
착지 전에도 카운트에 잡힌다 — 연속 2기 시도가 착지를 기다리지 않고 막힌다(의도).

## 완료 기준

- 컴파일 통과. ECS 파일 변경 0 · 신규 시스템/큐 0.
- EditMode `DefenderBoardLimitTests` 통과:
  - 미저작(`CreateInstance`) → `EffectiveMaxOnBoard == 1`
  - `maxOnBoard = 0` / `-3` → `1`
  - `maxOnBoard = 3` → `3`, `100` → `100`
- 기존 방어 유닛 에셋을 인스펙터에서 열면 `maxOnBoard` 가 **1** 로 보인다(0 아님).
- Play 수동 확인: 기본 유닛 2기째 배치 시도 → 거부되고 콘솔에 `LimitReached` 로그.
  같은 유닛 `maxOnBoard` 를 100 으로 올리면 3기 이상 연속 배치가 된다(= 지금과 동일 동작).
- 유닛이 죽은 뒤 같은 유닛을 다시 배치할 수 있다(카운트가 파생임을 확인하는 축).
