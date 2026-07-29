# 3 — 사거리 invalid 틴트 + 전이 플래시

## 목적

배치 불가 상태를 **가려지지 않는 넓은 채널**로 중복 송출한다. 사거리 격자는 중심 셀을 제외한
1..tileRange 링이라 손가락 바깥에 있고 면적이 커서 주변시로 읽힌다 — 가림에 구조적으로 면역인
유일한 채널이다.

강도는 **frame 틴트 + 전이 순간 1회 플래시**(사용자 결정). 형태(격자 outline)는 무변경 — solid 승급은
후속 후보(맵 가림이 사거리 solid 폐기 결정의 원 이유).

unit 0 과 독립이라 병행 가능.

## 변경 대상

- `Assets/_Project/Scripts/Data/TileSetData.cs` — invalid 색·플래시 필드 3개
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — 유효성 상태 + 틴트 분기 + 플래시 엔벨로프 + `Clear()` 리셋
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 포워딩 1개 + `ClearRange` 소유권 리셋
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 호출 2 + 리셋 2
- `Assets/_Project/Generated/Tiles/AutoTileTest/TileSet_AutoTileTest.asset` — **런타임 라이브 에셋**
- `Assets/_Project/Data/TileSets/TileSet_Desert.asset` — 색 반영 + **`rangeTile` 공백 배선(선행 조건)**
- `docs/spec/placement-attack-range-preview/README.md` — 계약 개정 + stale 색 문구 교정

## 선행 조건 — 어느 에셋이 라이브인가 (초판이 반대로 적었다)

critic H1, 코드로 확인:

- `BattleBridge.cs:1048-1049` — `theme != null && theme.tileSet != null ? theme.tileSet : tileSet`
- `Assets/_Project/Map/Theme/forest/forest.asset:15` — `tileSet: {fileID: 0}` (null) → **씬 폴백**
- `BattleScene.unity:4249` — `tileSet` guid `d780c834…` = **`TileSet_AutoTileTest`**
- `TileSet_Desert.asset` 은 `season_S2_desert` → desert theme 경유로만 살아난다

즉 기본 시즌·PlayMode 테스트에서 사는 `_tileSet` 은 **AutoTileTest** 다. Desert 만 고치면 런타임 색은
클래스 기본값이 된다.

**그리고 더 나쁜 것**: `TileSet_Desert.asset:24` — `rangeTile: {fileID: 0}`.
`SetPlacementRange`(`TilemapMapView.cs:902`)와 `SetPlacementCells`(`:926`)가 **둘 다** `rangeTile == null`
에서 early-return 한다(`aimRangeTile` 이 있어도 무관 — 그 슬롯은 배선돼 있다). → **사막 시즌에서는
공격 사거리 격자가 아예 렌더되지 않는다**(선행 버그). 그 시즌에서 방안 2 는 송출 채널이 0 이다.

사용자 결정(2026-07-29): **이 단위의 선행 조건으로 처리한다.** `TileSet_Desert.rangeTile` 에
`tile_grid_outline`(`Assets/_Project/Data/TileSets/tile_grid_outline.asset`, AutoTileTest 와 동일 소스)을
배선한다. 사막에서 사거리 격자가 처음 보이게 되므로 그 시즌 룩이 바뀐다 — 의도적 공백이었을 가능성은
낮다(`aimRangeTile`·`placeableTile` 은 모두 배선돼 있다).

## 구현

### `TileSetData` (Attack range highlight 그룹 하단)

```
rangeInvalidColor        Color   기본 (1, 0.16, 0.13, 1)
rangeInvalidFlashSeconds float   [Range(0, 1)]  기본 0.18
rangeInvalidFlashBoost   float   [Range(0, 1)]  기본 0.7   // 흰색으로 끌어올리는 정도
```

Tooltip: 주황 → 적색은 색상 거리가 짧아 정적 틴트만으로는 소형 화면·낮은 알파에서 안 읽힐 수 있다.
플래시(명도 변화)가 그걸 보완하고, **색약자에게도 작동하는 유일한 성분**이다.

### `TilemapMapView`

상태 2개. **소유자는 `SetPlacementRangeValidity` 단독**:

```csharp
private bool  _rangeInvalid;
private float _rangeInvalidSince;   // Time.unscaledTime, false→true 전이에만 스탬프

public void SetPlacementRangeValidity(bool valid)
{
    bool invalid = !valid;
    if (invalid == _rangeInvalid) return;      // 전이에만 반응 — 매 프레임 호출돼도 스팸 아님
    _rangeInvalid = invalid;
    if (invalid) _rangeInvalidSince = Time.unscaledTime;
}
```

`ApplyRangeTint()` 분기:

- `_rangeAimStyle == true` 면 유효성 무시(드롭 후 방향 지정 채널).
- `_rangeInvalid` 면 tint 를 `rangeInvalidColor` 로. `t = clamp01((unscaledTime - _rangeInvalidSince) /
  flashSeconds)` 로 RGB 를 흰색 쪽으로 `flashBoost * (1 - t)` lerp, 알파는 `1` 쪽으로 같은 비율 lerp.
  `t == 1` 이면 순수 `rangeInvalidColor`.
- 알파 최종 소유는 그대로 `Update()`(기존 계약). **재적용 경로를 신설하지 않는다** — `Update():190-192`
  가 `_rangeCells.Count > 0` 동안 매 프레임 `ApplyRangeTint()` 를 호출한다. 이게 이 단위가 싼 이유다.

`SetPlacementRange` / `SetPlacementCells` / `ClearPlacementRange` 는 **`_rangeInvalid` 를 건드리지
않는다**. `SetPlacementRange` 는 내부에서 `ClearPlacementRange()` 를 먼저 부르므로(`:903`/`:927`),
거기서 리셋하면 셀이 바뀔 때마다 false→true 전이가 재발생해 무효 영역을 훑는 동안 플래시가 연발한다.

`Clear()`(`:164-185`, 맵 리빌드 teardown)에는 `_rangeInvalid = false` 를 넣는다 — 정상 경로는 컨트롤러
리셋이 덮지만 맵 경계는 벨트-앤-브레이스(critic L3).

### `BattleBridge` — 포워딩 + 소유권 리셋

```csharp
public void SetPlacementRangeValidity(bool valid)
{
    if (tilemapMapView != null) tilemapMapView.SetPlacementRangeValidity(valid);
}
```

**`_rangeAimStyle` 는 조준 채널을 보호하지 않는다**(critic H2, 확인). `SetSkillAimRange`(`:5068`)와
`PinSkillTelegraph`(`:5077`)는 `SetPlacementRange` 를 타므로 `_rangeAimStyle = false`(`:906`)가 된다.
`aimStyle: true` 는 드롭 **후** 방향 지정(`SetAimGuide` `:4984`, `:4993`)뿐이다.

그래서 소유권이 Placement 를 떠나는 seam 에서 명시적으로 리셋한다 — `ClearRange(caller)`(`:5083`)에
`tilemapMapView.SetPlacementRangeValidity(true)` 를 넣는다(owner 시분할 장치가 이미 그 지점이다).
`TilemapMapView.ClearPlacementRange` 에 리셋을 얹지 않는 판단은 유지 — 그건 플래시 연발 방지 때문이다.

배치 단계의 facing/폭탄 레인(`:4964-4965`)은 `aimStyle: false` 이고 **여기는 적색이 들어와야 맞다**
(배치 채널). 계약을 "aimStyle 이 조준을 보호한다"에서 "**owner 전환 seam 이 리셋을 보장한다**"로 바꾼다.

### `DefenderDragPlacementController`

이미 `valid` 를 계산하는 두 지점에서 매 프레임 호출. **`SetHover` 뒤**여야 한다(`SetHover` 가 셀 변경
시 `SetPlacementRange` 를 부르고 그 페인트는 유효성을 모른다):

- `ResolveFocusAndTarget` — `SetHover(cell, valid)` 직후. 시뮬(탭) 경로는 사거리를 억제하므로
  `if (!_simulatedDrag)` 게이트.
- `UpdateBoardScout` — `valid` 계산 직후 매 프레임.

세션 경계 리셋은 **컨트롤러가 명시적으로**(뷰의 페인트 API 에 리셋을 얹지 않기 위해):
`ClearHover()` 와 `ClearBoardScout()` 의 `ClearPlacementRange()` 옆에 `SetPlacementRangeValidity(true)`.

### 범위 밖 — 재배치 스카우트 (제약 9 준수 기록)

`DefenderRelocationController.UpdateScout:275-276` 은 `if (valid) SetPlacementRange(...) else
ClearPlacementRange()` 다 — 재배치에서 배치 불가는 이미 **사거리 소거**로 인코딩돼 있다. 이 단위는
호출처 2곳(드래그 세션·armed 스카우트)만 배선하므로 재배치는 새 채널을 받지 않는다. **동작 문제는
없지만 feature-wide 언어가 세 번째 경로에서만 다르다** — 통일은 이 spec 범위 밖(critic M7).

### 문서 교정

`docs/spec/placement-attack-range-preview/README.md`:
- "표시 조건: 배치 가능/불가와 **무관하게 항상** 노란 범위 표시" → **형태는 무관, 색은 유효성 반영**.
- `:44` 의 "**노란** 범위" 는 stale 이다(라이브는 주황 `1,0.55,0.12`). 같은 커밋에서 교정(critic L7).

## 완료 기준

- 컴파일 에러 0.
- 에디터 Play 실드래그: 배치 가능 → 주황, 불가로 이동 → **적색 전환 + 전이 1회 밝게 번쩍**, 복귀 시 주황.
- **코스트 부족 상태**(코스트 소진 후 드래그)에서 판 전체가 공간상 배치가능이어도 사거리가 적색이다 —
  이게 이 단위의 핵심 실효 구간이다(공간 무효는 시안 슬랩 부재로 이미 읽힌다).
- 무효 영역을 **훑는 동안 플래시가 연발하지 않는다**.
- **머신거너/폭탄병**(facing·landing 경로, `alphaMul = 0.7`)으로도 무효 시 레인·착지셀이 적색으로
  읽힌다. `ApplyRangeTint` 가 `c.a *= _rangeAlphaMul` 하므로 흐려질 수 있다 — 안 읽히면 invalid
  분기에서 `_rangeAlphaMul` 바이패스를 결정한다(critic M5).
- armed 보드 스카우트에서도 작동.
- **스킬 조준 진입 시 직전 드래그의 적색이 새지 않는다**(`ClearRange` 리셋 확인). 텔레그래프도 동일.
- **사막 시즌에서 사거리 격자가 보인다**(선행 조건 배선 확인). 안 보이면 이 단위는 미완료다.
- 새 드래그 세션이 직전 세션의 적색을 상속하지 않는다.
- `rangeInvalidColor` 를 `rangeColor` 와 같게 두면 현행과 동일(회귀 0 확인 경로).
