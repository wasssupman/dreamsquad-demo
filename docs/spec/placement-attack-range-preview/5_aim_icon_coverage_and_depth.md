# 5 — 조준 아이콘: 전 범위 칸 표기 + 보드 깊이로 하강

## 목적

unit 4 로 조준 표시가 불투명 채움이 되면서 두 가지가 드러났다(사용자 지적 2026-07-28):

1. **아이콘이 유닛 주위 4칸에만 있다.** 채움만으로는 레인이 대칭이라 "위로 쏜다"와 "위에서 쏜다"가
   여전히 같아 보인다(unit 9 판단 1 은 그대로 유효). 방향을 말하는 건 화살표뿐인데, 그 화살표가
   레인의 첫 칸에만 있으니 레인 몸통은 방향을 말하지 못한다.
2. **아이콘이 유닛 위에 렌더된다.** 조준 대상인 유닛이 자기 조준 UI 에 가려진다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CollectLaneCells`(셀+방향 동시 수집) · `SetAimGuide`
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `SetAimArrows` 시그니처(`selectedIndex` → `emphasized`)
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — `AimArrowOrder` 11500 → **−11**

## 구현

- **아이콘 = 칠해진 칸**: `CollectLaneCells` 가 `_laneCellScratch`(셀)와 `_laneDirScratch`(그 셀이 속한
  방향)를 **나란히** 채우고, 타일 페인트와 화살표가 **같은 목록**을 쓴다 — 둘이 어긋날 수 없다.
  방향별로 나눠 훑되 판정은 여전히 시뮬의 발사 게이트(`LaneMath.IsInLane`)라 "보이는 칸 = 맞는 칸"
  계약(unit 9)은 유지된다.
- **강조는 상태 하나**: 방향이 확정되면 그 레인만 칠해지므로 남은 아이콘은 전부 그 레인의 것이다 →
  `SetAimArrows(cells, angles, emphasized)`. 개별 `selectedIndex` 는 의미를 잃어 제거했다.
- **깊이**: `AimArrowOrder = −11` — 범위 타일(−12) 바로 위, overlay(−10)·그림자(−5)·유닛(양수) 아래.
  스폰 예고 라인(−9..−6)이 "바닥에 그려진 것은 음수 대역"이라고 정한 규칙을 그대로 따른다.
  `ArrowGroundLift`(world +Y)는 유지 — 그건 정렬이 아니라 코플레이너 z-acne 대책이다.
- **폭탄맨은 무변경**: 착지 셀은 방향당 1칸이라 이미 모호하지 않고, unit 8 이 "화살표 없음 = 머신거너와
  다른 모드"로 정한 구분이다. 필요해지면 같은 목록 구조를 그대로 쓸 수 있다.

## 완료 기준

- [x] compile 0 · 아이콘 12개(4방향 × 3칸) 생성 · `sortingOrder = -11` 실측.
- [x] 오프스크린 렌더: 미선택 = 십자 전 칸에 흐린 화살표, 선택 = 그 레인 3칸에 또렷한 화살표.
- [x] (Play) 조준 중 유닛이 화살표 **위**에 보인다(유닛이 아이콘에 안 가림).
- [x] (Play) 레인 몸통 어디를 봐도 방향이 읽힌다.

확인 2026-07-28 (사용자 Play).
