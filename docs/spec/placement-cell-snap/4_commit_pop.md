# 4 — 하이라이트 확정 팝 (commit pop)

**작업 구분**: feature · 의존: unit 1·3 (확정 셀 변경 시점을 소비)

## 목적

고스트는 키링 스윙을 유지하므로(unit 2 스냅 되돌림), "어느 칸에 확정됐는지"의 시각 punctuation 을
**타일 하이라이트**에 얹는다. 포커스 셀이 확정(변경)되는 순간 셀 위에 스케일 오버슈트+알파 페이드
"팝"을 1회 재생 → 타일 스냅 느낌을 고스트를 얼리지 않고 되찾는다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` (`PlacementCommitPopOrder = 12000`)
- Modify: `Assets/_Project/Scripts/Core/TilemapMapView.cs` (팝 렌더/애니 + serialized 파라미터)
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`PulsePlacementHover` 포워딩)
- Modify: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` (`SetHover` 의 `changed` 에서 팝 발화)

## 구현

- **발화 시점**: `SetHover` 의 `changed`(확정 셀이 실제로 바뀔 때)에서만 `bridge.PulsePlacementHover(cell, valid)`.
  디바운스(unit 3)로 확정이 게이팅돼 매 프레임 스팸이 아니다.
- **렌더**: `TilemapMapView` 가 재사용 `SpriteRenderer`(grid 자식 → 타일과 코플레이너) + 절차적 흰 스프라이트
  (`Texture2D.whiteTexture`, pivot 중심). sortingOrder=`PlacementCommitPopOrder`(상승 overlay 10002 위, 드래그 프리뷰 20000 아래),
  sortingLayer=overlay 와 동일.
- **애니**: `commitPopDuration` 동안 스케일 `startScale→endScale`(타일 크기 배수, OutQuad) + 알파 `startAlpha→0`.
  `Time.unscaledDeltaTime`(배치 슬로우모 무관). valid=청록 / invalid=빨강 색.
- **파라미터**(하드코딩 금지 — `TilemapMapView` serialized): duration/startScale/endScale/startAlpha/valid·invalid color.
- **정리**: `Clear()`(맵 리빌드) 에서 코루틴 중단 + 팝 오브젝트 파괴.

## 완료 기준

- 컴파일 클린. (팝 자체는 시각물이라 EditMode 대상 아님 — 발화 게이팅은 unit 3 테스트가 커버.)
- Play: 포커스 타일이 인접/큰 점프로 **확정될 때마다** 그 셀에서 팝이 1회 튀고 사라짐. 고스트 키링 스윙은 유지.
- 확정이 디바운스로 게이팅돼 팝이 연속 스팸되지 않음.
- 무효 셀 확정 시 빨강 팝(유효=청록).
- 사용자 Play 체감 확인 일자 + 커밋 해시 추가 후 커밋.

**완료: 2026-07-17 · `a3812079` — Play 검증.**
