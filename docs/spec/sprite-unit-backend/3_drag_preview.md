# 3 · 드래그 그림 — 손끝 고스트 + 보드 실루엣

## 목적

세트 저작 방어유닛을 드래그할 때 **회색 캡슐 대신 idle 시트가 돈다** (2026-09-15 사용자 결정: idle 시트 재생).
`DefenderDragPlacementController` 는 `SpineUnitPool` 밖에서 Spine 을 직접 만드는 4번째 창구라 unit 2 로 자동 해결되지 않는다.

## 변경 대상

- 수정 `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
  - `TryBuildDragSilhouette` (보드 실루엣 · 라이브 D&D) ~1238
  - `TryBuildKeyringPreview` (손끝 키링 고스트 · 탭 시뮬 비행) ~1740
  - `DragSession.skeleton` 옆에 `SpriteFlipbookPlayer flipbook` 핸들 · `SetPreviewAlpha` 의 스프라이트 분기

## 구현

두 빌더 모두 첫 줄 가드가 `skeletonDataAsset == null → false` 다. 그 **앞에** 스프라이트 분기를 넣는다:
`if (unitData.SpriteMotions != null && unitData.SpriteMotions.HasIdle) return TryBuild…Sprite(unitData, …);`
Spine 경로는 한 줄도 안 바뀐다.

스프라이트 빌더 — 같은 트리 구조(`root` → `Billboard` → 자식)에 `SpriteRenderer` + `SpriteFlipbookPlayer`
(`timeDomain = Interaction` — 드래그는 슬로우모 중에도 실시간, `Cfg.silhouetteFollowSpeed` 가 unscaled 인 것과 같은 이유).
재생: 실루엣 = `ResolveLocomotion(false)`(idle) · 키링 고스트 = `ResolveDrag()`(drag→idle).
`flipX = set.SheetFacesRight` · `sortingOrder = DragPreviewOrder` · 알파 = `Cfg.silhouetteAlpha` / 1.

정렬 오프셋 — Spine 은 `localBounds` 로 발/머리를 원점에 맞춘다. 스프라이트는 **피벗이 발**(unit 0)이라
실루엣은 오프셋 0, 키링 고스트는 머리 정렬 = `-(sprite.bounds.max.y × scale)` (`unitHeight = bounds.size.y × scale`).
피벗이 Center 로 잘린 옛 시트는 `sprite.bounds` 가 그걸 이미 반영하므로 산식이 같다.

취소 고스트 알파(`:929`) — `_session.flipbook != null` 이면 그 `SpriteRenderer.color.a` 를 쓴다.

## 완료 기준

- 세트 저작 유닛을 트레이에서 드래그하면 보드 실루엣이 idle 을 재생하고, 탭 배치 비행 키링에 drag(없으면 idle) 시트가 매달린다. 캡슐 0.
- Spine 유닛의 드래그 그림은 종전과 동일(회귀 0).
- 취소 예고 시 스프라이트 고스트도 알파가 떨어진다.

---

2026-09-15 구현 · `ca769aba` — 컴파일 0 에러. Play 확인은 unit 4 로.
