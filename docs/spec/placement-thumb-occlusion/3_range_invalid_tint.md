# 3 — 사거리 invalid 틴트 + 전이 플래시

## 목적

배치 불가 상태를 **가려지지 않는 넓은 채널**로 중복 송출한다. 사거리 격자는 중심 셀을 제외한
1..tileRange 링이라 손가락 바깥에 있고 면적이 커서 주변시로 읽힌다 — 가림 문제에 구조적으로 면역인
유일한 채널이다.

강도는 **frame 틴트 + 전이 순간 1회 플래시**(사용자 결정). 형태(격자 outline)는 무변경 — solid 승급은
후속 후보로 남긴다(맵 가림이 사거리 solid 폐기 결정의 원 이유).

unit 0 과 독립이라 병행 가능.

## 변경 대상

- `Assets/_Project/Scripts/Data/TileSetData.cs` — invalid 색·플래시 필드 3개
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — 유효성 상태 + 틴트 분기 + 플래시 엔벨로프
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 포워딩 1개
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 호출 2 + 리셋 2
- `Assets/_Project/Data/TileSets/TileSet_Desert.asset` — 색 값 반영 (현재 유일한 라이브 `TileSetData`.
  `Assets/_Project/Generated/Tiles/AutoTileTest/TileSet_AutoTileTest.asset` 은 생성 테스트용이라 선택)

## 구현

### `TileSetData` (Attack range highlight 그룹 하단)

```
rangeInvalidColor        Color   기본 (1, 0.16, 0.13, 1)
rangeInvalidFlashSeconds float   [Range(0, 1)]  기본 0.18
rangeInvalidFlashBoost   float   [Range(0, 1)]  기본 0.7   // 흰색으로 끌어올리는 정도
```

Tooltip: 주황 → 적색은 색상 거리가 짧아 정적 틴트만으로는 소형 화면·낮은 알파에서 안 읽힐 수 있다.
플래시(명도 변화)가 그걸 보완하고, 색약자에게도 작동하는 유일한 성분이다.

### `TilemapMapView`

상태 2개. **소유자는 `SetPlacementRangeValidity` 단독**이다:

```
private bool  _rangeInvalid;
private float _rangeInvalidSince;   // Time.unscaledTime, false→true 전이에만 스탬프
```

```csharp
public void SetPlacementRangeValidity(bool valid)
{
    bool invalid = !valid;
    if (invalid == _rangeInvalid) return;      // 전이에만 반응 — 매 프레임 호출돼도 스팸 아님
    _rangeInvalid = invalid;
    if (invalid) _rangeInvalidSince = Time.unscaledTime;
}
```

`ApplyRangeTint()` 에 분기를 넣는다:

- `_rangeAimStyle == true` 면 **유효성 무시**(계약 — 조준 채널 오염 금지).
- `_rangeInvalid` 면 tint 를 `rangeInvalidColor` 로 바꾸고, `t = clamp01((unscaledTime -
  _rangeInvalidSince) / flashSeconds)` 로 엔벨로프: RGB 를 흰색 쪽으로 `flashBoost * (1 - t)` 만큼
  lerp, 알파는 `1` 쪽으로 같은 비율 lerp. `t == 1` 이면 순수 `rangeInvalidColor`.
- 알파의 최종 소유는 그대로 `Update()` 다(기존 계약 — `ApplyRangeTint` 는 `Update():190-192` 가 매
  프레임 호출하므로 별도 리페인트 경로를 **신설하지 않는다**. 이게 이 단위가 싼 이유다).

`SetPlacementRange` / `SetPlacementCells` / `ClearPlacementRange` 는 **`_rangeInvalid` 를 건드리지
않는다**. `SetPlacementRange` 는 내부에서 `ClearPlacementRange` 를 먼저 부르므로, 여기서 리셋하면
셀이 바뀔 때마다 false→true 전이가 재발생해 무효 영역을 훑는 동안 플래시가 연발한다.

### `BattleBridge`

```csharp
public void SetPlacementRangeValidity(bool valid)
{
    if (tilemapMapView != null) tilemapMapView.SetPlacementRangeValidity(valid);
}
```

`SetPlacementHover`/`SetPlacementStretch` 포워딩과 같은 자리·같은 형태.

### `DefenderDragPlacementController`

이미 `valid` 를 계산하는 두 지점에서 매 프레임 호출한다. 호출 순서가 중요하다 — **`SetHover` 뒤**여야
한다(`SetHover` 가 셀 변경 시 `SetPlacementRange` 를 부르고, 그 페인트는 유효성을 모른다).

- `ResolveFocusAndTarget` — `SetHover(cell, valid)` 직후. 시뮬(탭) 경로는 사거리를 억제하므로
  `if (!_simulatedDrag)` 로 게이트(범위 억제와 일관).
- `UpdateBoardScout` — `valid` 계산 직후 매 프레임(스카우트는 이미 매 프레임 유효성을 안다).

세션 경계 리셋은 **컨트롤러가 명시적으로** 한다(뷰의 페인트 API 에 리셋을 얹지 않기 위해):

- `ClearHover()` — `bridge?.ClearPlacementRange()` 옆에 `bridge?.SetPlacementRangeValidity(true)`.
- `ClearBoardScout()` — 동일.

### 라이브 에셋

`TileSet_Desert.asset` 에 `rangeInvalidColor` 를 반영한다. 시안 배치 하이라이트(쿨) · 주황 사거리(웜)
대비에 적색이 들어오므로, **주황과 한눈에 구분되는지**를 렌더로 확인한 뒤 값을 고정한다 — 안 구분되면
색을 더 어둡고 채도 높게 밀거나 `rangeInvalidFlashBoost` 를 올린다.

주의: 클래스 기본값을 바꿔도 **이미 직렬화된 에셋은 따라오지 않는다**. 필드 신설 시 에셋에는 `0`
(검정·알파 0)으로 들어오므로 반드시 에셋 값을 명시적으로 채운다 — 안 하면 무효 상태에서 사거리가
사라진 것처럼 보인다.

## 완료 기준

- 컴파일 에러 0.
- 에디터 Play 실드래그: 배치 가능 칸 → 주황 격자, 배치 불가 칸으로 이동 → **격자가 적색으로 바뀌며
  전이 순간 1회 밝게 번쩍**한다. 다시 가능 칸으로 → 주황 복귀.
- **코스트 부족 상태**(코스트를 소진한 뒤 드래그)에서 판 전체가 공간상 배치가능이어도 사거리가
  적색이다 — 이게 이 단위의 핵심 실효 구간이다.
- 무효 영역을 **훑는 동안 플래시가 연발하지 않는다**(셀은 계속 바뀌지만 유효성은 안 바뀌므로).
- armed 보드 스카우트(슬롯 탭 후 보드 누른 채 이동)에서도 같이 작동.
- 스킬 조준/텔레그래프/방향 레인의 주황 표시가 **무변경**(aimStyle 경로 오염 0).
- 새 드래그 세션이 직전 세션의 적색을 상속하지 않는다.
- `rangeInvalidColor` 를 `rangeColor` 와 같게 두면 현행과 동일(회귀 0 확인 경로).
