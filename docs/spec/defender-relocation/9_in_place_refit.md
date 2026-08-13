# 9 — 제자리 재정비 (같은 칸 확정)

## 목적

이동모드에서 **자기 칸에 다시 내려놓는 것**을 취소가 아니라 확정으로 만든다. 상한 1 에서는
"자리는 이미 최적인데 체력만 회복하고 싶다"가 흔한 상황이고, 지금은 그걸 표현할 방법이 없다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.Relocation.cs` — `RelocationCheck` 순수함수
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ShowPlacementHighlight` 에 소스 칸 포함 인자
- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` — `ResolveRelease` 분기 제거, 진입 하이라이트
- `Assets/_Project/Tests/EditMode/RelocationCheckTests.cs` — `SameCell_ReturnsSameCell_NotOccupied` 개정

## 구현

**판정** — `RelocationCheck` 의 같은 칸 early return 을 사유에서 통과로 바꾼다.

```csharp
if (from.Equals(to)) return PlacementRejectReason.None;  // 제자리 재정비 (was: SameCell)
```

⚠ **이 early return 은 반드시 `SpatialPlacementCheck` 앞에 남아야 한다.** from 이 아직 점유 집합에
있어서, 순서가 바뀌면 자기 자리가 `Occupied` 로 오판된다(원 주석이 경고하던 그 지점).
`PlacementRejectReason.SameCell`(값 11) 은 **enum 에서 지우지 않는다** — 직렬화 값 보존.
생산자만 사라진다.

**확정 경로** — `ResolveRelease` 의 취소 분기를 지운다.

```csharp
if (cell == _sourceCell) { CancelMoveMode(); return; }   // ← 삭제. 아래 커밋으로 흐른다.
```

`TryBeginDefenderRelocation` 의 스왑은 from==to 에서 그대로 성립한다(Remove→Add, Remove→대입).
비행은 거리 0 이라 `flightBaseSeconds` 만큼 **제자리에서 폴짝 뛰었다 내려앉는다** — 재정비 연출이
기존 아치에서 공짜로 나온다. 신규 연출 코드 없음.

**어포던스** — 취소 버튼을 만들지 않는다(README 계약 13). 대신 진입 시 이미 켜지는 배치 가능
하이라이트에 **소스 칸을 포함**시킨다. 지금은 소스가 점유 상태라 자동 제외되어 어둡고, 그래서
"거긴 못 놓는 칸"으로 읽힌다 — 확정 칸이 되는 지금은 정반대 신호다.

⚠ **인자로 넘기면 안 된다.** `ShowPlacementHighlight` 는 `_placeableHlShown`/`_placeableHlUnit`
**상태**를 세우고, 실제 칸 목록은 `RepaintPlacementHighlight` 가 **매번 처음부터 다시 계산**한다.
게다가 `TryBeginDefenderRelocation` 자신이 `RefreshPlacementHighlightIfShown()` 을 부른다 —
일회성 인자는 **첫 리페인트에서 조용히 사라진다**. 소스 칸도 `_placeableHlUnit` 과 같은 격의
**상태**로 저장하고 `HidePlacementHighlight` 에서 함께 비운다.

```csharp
public void ShowPlacementHighlight(DefenderUnitData unit, Vector2Int? extraCell = null);
// RepaintPlacementHighlight 가 스캔 뒤 extraCell 을 더한다(점유라 스캔에서 빠지므로).
```

**취소 경로 — `TryScreenToCellStrict` 로 바꿔야 실제로 성립한다.** 구현하며 드러난 사실:
관대한 `TryScreenToCell` 은 보드 밖을 **가장자리 셀로 clamp 해서 true 를 준다**. 그래서 예전의
"보드 밖 릴리즈 = 취소" 는 사실상 동작하지 않았다 — 대부분 무효 셀 reject 로 빠져 이동모드에
갇혔고, 자기 칸 탭이 실질적인 취소를 도맡고 있었다. 그 탭을 확정으로 가져오는 지금 그대로 두면
**8초 타임아웃 말곤 빠져나갈 길이 없다**. `TryScreenToCellStrict` 가 정확히 이 계약
("보드 밖 = 취소, 무차감")을 위해 이미 존재하므로 그것으로 바꾼다.

⚠ **릴리즈(`ResolveRelease`)와 hover(`UpdateScout`) 가 같은 판정을 써야 한다.** 한쪽만 Strict 면
보드 밖에서 가장자리 셀을 보여주다가 릴리즈에서 취소가 나 손과 화면이 어긋난다.

**남는 취소 경로**: 보드 밖 릴리즈(Strict 실패) · 타임아웃 8초 · 대상 사망 · 트레이 세션 충돌.
밝은 영역 밖이 전부 취소라는 것이 하이라이트로 읽힌다.

⚠ **"옮겨 갈 칸" 을 스캔하는 코드는 소스 칸을 명시로 빼야 한다.** 제자리가 유효해지는 순간
`CanRelocateDefender(from, from)` 이 true 라, 그냥 스캔하면 소스 칸을 집어 와 아무 이동도 일어나지
않는다. 실제로 기존 PlayMode 3건이 이걸로 깨졌다(테스트 헬퍼 2곳 + 에디터 디버그 진입점 1곳).

## 완료 기준

- 컴파일 통과.
- **EditMode**: `RelocationCheckTests` 의 같은 칸 케이스가 `None` 을 기대하도록 개정되고, **점유
  오판이 아님**을 같이 단정한다(from 을 점유 집합에 넣은 상태로 `Occupied` 가 아닌 `None`).
- **PlayMode**: 이동모드에서 자기 칸 탭 → 코스트가 줄고 HP 가 차고 배치 스킬이 다시 터진다.
  유닛은 같은 칸에 남는다(`_defenderByTile` 키 불변).
- **취소 회귀**: 보드 밖 탭 → 코스트 변화 0 · 유닛 상태 변화 0 · 슬로모 해제.
- 육안: 이동모드 진입 시 **소스 칸이 밝게** 들어온다. 제자리 확정 시 유닛이 폴짝 뛰었다 내려앉는다.
