# 9. 보드 조준 가이드 — 레인 점등 + 화살표 탭

## 목적

방향 지정을 **화면 스와이프 → 보드 탭**으로, 가이드를 **화면 UI → 보드 오버레이**로 옮긴다. 초안(unit 6)의 화면 글리프는 정보가 틀린 층에 있었다: 조준 중 보드는 아무것도 알려주지 않아 레인이 어디까지인지·저 적이 사거리에 드는지 볼 수 없었다(사용자 지적 2026-07-17).

## 플레이어 인식 판단 (설계 근거)

1. **방향은 화살표만이 말한다.** 레인은 대칭이라 "위로 쏜다"와 "위에서 쏜다"가 같아 보인다 — 켜진 타일은 범위를 말할 뿐 방향을 말하지 못한다.
2. **범위는 기본 상태에서 이미 보여야 한다.** 선택 전 4레인을 흐리게 켜둔다. 안 켜면 탭 전까지 사거리를 모르고, 빠른 탭이면 영영 못 본다.
3. **테이퍼(그라데이션)는 넣지 않는다.** 직관에 반하지만 끝을 흐리게 하면 "어디까지"가 오히려 모호해진다 — **하드 엣지가 경계를 정확히** 알린다.
4. **탭 = 셀 판정이라 모호함이 0.** 화면 델타가 아니라 셀로 고르면 카메라 pitch/iso 와 무관하다. 축 투영·데드존·동률 규칙이 통째로 사라진다(iso 에서 "화면 위"가 +Y/−X 동률이던 문제 소멸 — unit 5 rev1 이 풀던 문제를 아예 없앤다).
5. **화살표는 어포던스, 판정은 레인 전체.** 1타일 화살표만 노리면 손가락이 가린다.
6. **press-to-preview → release-to-commit.** 모바일엔 hover 가 없다(각성 손패 press-to-lift 선례). 누르면 그 레인이 켜지고 옮기면 따라오고 떼면 확정 — 단순 탭은 그 자리 press+release 라 "터치해서 설정"이 그대로 성립하면서 오조작 교정이 덤.
7. **foreshortening 은 받아들인다.** 카메라 pitch 때문에 먼 레인이 압축돼 보이지만, 모든 범위 표시가 같은 성질이라 일관성이 이긴다.

## 변경 대상

- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — 임의 셀 점등 + 세기 배율 + 화살표(절차적 스프라이트)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SetAimGuide`/`ClearAimGuide` + `PlacementAim` 소유자
- `Assets/_Project/Scripts/UI/DirectionAimLogic.cs` — 스와이프 해석 → **셀 → 레인** 판정
- `Assets/_Project/Scripts/UI/DirectionAimController.cs` — 글리프 캔버스 제거 → 보드 가이드 + 탭 입력
- `Assets/_Project/Scripts/Data/DirectionAimSettings.cs` — 글리프/데드존 파라미터 제거(slowmoScale 만 남음). **이후 rev(`044b639a`)에서 SO 자체 폐기 → `DragSwaySettings` 로 병합.**
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — `AimArrowOrder`

## 구현

**표시 규칙** (드래그·조준이 같은 언어를 쓴다):
- **방향 미정**(드래그 중 + 조준 대기): **4레인 십자를 흐리게**(alpha ×0.45) + 유닛을 둘러싼 **화살표 4개** = "이 중 하나를 고른다".
- **누르는 중**: 그 레인만 **또렷하게**(×1) + 그 화살표 강조(불투명 + 확대).
- **확정/취소**: 소거.

두 상태가 동시에 뜨지 않으므로 타일맵 단일 색으로 충분하다(타일별 색 불필요).

**화살표**: 각 레인의 첫 칸(유닛 인접)에 눕는 절차적 삼각형 스프라이트. 블롭/확정 팝과 같은 방식(`grid` 자식 → 타일과 코플레이너). 색은 새로 만들지 않고 `TileSetData.rangeColor` 를 쓴다 — 범위 타일과 같은 언어. 유닛을 둘러싼 D-pad 라 엄지로 닿고, 레인이 어디서 출발하는지도 같은 자리에서 말한다.

**탭 판정**: 화면 → `TryScreenToCell` → `DirectionAimLogic.Evaluate(center, cell, tileRange)` → 4레인 중 포함되는 것. 화살표 칸은 레인의 첫 칸이므로 "화살표 탭"과 "레인 탭"이 같은 답이 된다.

**뷰 계약**: `TilemapMapView` 는 write-only(자기 헤더 계약) — 어느 칸에 어느 각도로 무엇이 선택됐는지는 전부 `BattleBridge` 가 정해 넘긴다.

**UI == sim 보장**: 칠할 셀을 시뮬의 발사 게이트와 **같은 `LaneMath.IsInLane`** 으로 고른다. 따로 계산하면 언젠가 어긋난다 — 보이는 칸과 실제로 맞는 칸이 구조적으로 일치한다.

**기존 거짓말 수정**: 드래그 중 방향 유닛에게 보여주던 **네모 사거리는 거짓**이었다(레인만 때린다). `SetPlacementRange` 가 `directionalAttack` 이면 십자 레인을 칠한다.

**소유권**: `RangeDisplayOwner.PlacementAim` 신설. `Begin` 이 십자를 칠하면서 소유를 가져가므로, 바로 뒤따르는 드래그 세션의 `CleanupSession`→`ClearPlacementRange`(Placement 소유)가 이 레인을 **지우지 못한다** — 드롭과 조준 사이에 보드가 비는 프레임이 없다. 기존 소유권 메커니즘이 그대로 답이다.

**알파 소유권**: 범위 타일의 알파는 `TilemapMapView.Update` 의 펄스가 소유한다(`rangePulseMin/MaxAlpha`). 호출부가 색을 직접 박으면 다음 프레임에 덮이므로, 세기 차이는 **배율(`_rangeAlphaMul`)로만** 낸다.

**리페인트**: 레인은 방향이 바뀔 때만 다시 칠한다(매 프레임 `SetTile` 로 타일맵을 갈아엎지 않는다).

**화면 글리프 전면 제거**: 보드 게임 위에 뜬 화면 UI 의 이물감이 사용자 지적의 정체였다. 글리프 SO 의 색/크기/반경/데드존도 함께 제거 — 색·펄스는 `TileSetData`, 판정 규칙은 `LaneMath` 가 소유하므로 남는 튜닝값은 `slowmoScale` 뿐이었고, 그마저 이후 rev(`044b639a`)에서 `DragSwaySettings` 로 흡수해 전용 SO 를 없앴다.

## 완료 기준

- [x] compile 클린 · EditMode 912 green (실패 0)
- [x] 레인 셀 실측: 십자 12칸 / +X 레인 = 발사 게이트와 동일한 (6,5)(7,5)(8,5) / 자기 칸 제외
- [x] 화살표 각도 실측: 4방향 전부 스프라이트 +Y 가 해당 cardinal 을 정확히 가리킴(-90/90/0/-180°)
- [x] 탭 판정 테스트: 화살표 칸·레인 중간·레인 끝 = 선택 / 자기 칸·사거리+1·대각·한 칸 옆 = 미선택
- [ ] Play 검증(사용자): 드롭 시 십자+화살표 → 방향 탭에 그 레인만 또렷 → 떼면 확정·소거. 일반 유닛의 네모 사거리 무변화

확인 2026-07-17 — 구현·실측 완료. 커밋은 병행 세션 WIP 와 얽혀 보류(아래).

**커밋 주의**: `TilemapMapView.cs`·`BattleBridge.cs`·`DefenderDragPlacementController.cs` 에 병행 세션의 `defender-tap-to-place`/`placement-cell-snap unit 7`(끈적함 블롭) WIP 가 섞여 있다. 그쪽 커밋 후 내 hunk 만 격리해 커밋할 것.
