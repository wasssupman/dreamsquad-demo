# 2 — 배치 드래그 Ghost UX (4색 고스트 · 자석 · 하단 중앙 기준점 · 뷰 중심)

## 목적

드래그 중 「확정될 footprint 영역 + 왜 안 되는지」를 4색으로 즉독하게 만들고(결정 3: 배치가능 전체 하이라이트 은퇴, 배치 불가 위주 표시), 손끝 규약(하단 중앙)·자석 스냅·짝수 footprint 뷰 중심을 세운다. 배치 경로 3종(트레이 D&D·탭·armed 보드 드래그)이 같은 앵커 산식을 공유한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/FootprintMath.cs` — `AnchorFromBottomCenter`(손끝 규약) · `CenterOffsetFromPrimary`(짝수 변 +0.5)
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — ⑬ 그룹: 고스트 4색 · 컨텍스트 반경 · 자석 반경
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — per-cell 색 고스트 레이어(`SetGhostCells`, telegraphTile 재사용)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 고스트 포워딩 · `TryFindNearestPlaceableAnchor`(자석, 결정론 탐색) · `GetPlacementCellReasons(anchor, size, …)` · 뷰 중심 오프셋(sync/RestViewPos/비행 앵커) · `GridAnchorToViewCenter`
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 앵커 산식 편입 · 고스트 페인트 · 전체 하이라이트 스위치 오프 · 커밋/조준/활성화에 앵커·대표 셀 전달
- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` — 전체 하이라이트 스위치 공유 · 목적 셀 앵커 산식
- `Assets/_Project/Tests/EditMode/FootprintMathTests.cs` — 신규 산식 케이스 추가

## 구현

- **손끝 규약**: 손가락 셀 = footprint 하단(min y) 행의 가로 중앙 → `anchor = (finger.x − (W−1)/2, finger.y)`. 1×1 은 항등. 기존 히스테리시스·throttle·릴리즈 forceCommit 재해석은 손가락 셀 축에서 그대로 돌고, 앵커는 그 결과의 순수 파생이라 안정성이 승계된다.
- **자석**: 후보 앵커가 **공간 사유**(Occupied/NotBuildable/OutOfBounds)로 무효일 때만 반경(`placementMagnetRadiusCells`) 안 최근접 유효 앵커로 흡착. 탐색 순서·동률 처리 결정론(row-major). 없으면 배치 불가 유지(원거리 강제 보정 금지 — README 계약 5·6).
- **4색 고스트**: 한 번의 `GetPlacementCellReasons`(anchor−r, size+2r) 스캔에서 footprint 칸 = 하늘/빨강(칸 사유 또는 비공간 사유 전체 빨강 — «성공으로 보였는데 실패» 금지), 컨텍스트 칸 = Occupied 노랑 / NotBuildable 무채색, None·맵밖 = 무표시. 페인트는 변경시에만(리스트 diff).
- **전체 하이라이트 은퇴**: `PlaceableAreaHighlightEnabled = false` 스위치(selection-entry-narrowing 관용). 코드·리페인트 경로는 유지(스위치로 복원 가능).
- **뷰 중심**: sim 위치는 대표 셀 중심 불변(계약 2). 짝수 변만 뷰 피드(sync·RestViewPos·비행 앵커·타일 게이지)에 +0.5칸 오프셋 — 투사체·데미지 넘버 반 칸 치우침은 수용(계약 2 명시).
- **재배치 정합**: 제자리 재정비 호출(to=대표 셀)을 fromAnchor 로 정규화, 목적 셀도 하단 중앙 산식. 재배치 스카우트의 고스트 표시는 후속 후보.

## 완료 기준

- [x] 컴파일 에러 0 · EditMode 코어 무회귀 — 2492 전건 실패 0
- [x] FootprintMathTests 신규 케이스 그린(하단 중앙 앵커·중심 오프셋) — +10 케이스
- [x] 1×1 라이브 동작 동등(앵커=손가락 셀·오프셋 0·자석은 무효시만 발동이라 기존 «사후 판정만» 대비 스냅이 추가되는 것이 유일한 체감 변화)
- [ ] 육안 Play: 2×2 테스트 유닛 드래그 시 고스트 4색·자석·착지 중심 확인 (**사용자 확인 대기 축**)
