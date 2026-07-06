# 7 — Handoff Summary

## Commit

- `9941f27` — feat(placement): 드래그 배치 중 적 반투명 see-through (units 0~6)

## Implemented

- 디펜더 드래그 배치 중 **적 유닛을 반투명화** → 가려진 뒤 보드 타일이 비쳐 배치 위치 판단 가능.
- 적 = **Spine·Quad 혼합** 둘 다 처리: Quad 는 cutout↔transparent 런타임 블렌드 전환, Spine 은 PMA 라 `skeleton.A` 페이드(블렌드 전환 없음).
- health tint(RGB)와 dim 알파가 **안 싸우게 합성**(Quad `_BaseColor.a`, Spine `skel.A` 독립). `_dying` 존중.
- 발밑 **그림자도 페이드**(blob 알파↓ / 실그림자 casting Off, Spine 은 상태-게이트로 매프레임 alloc 방지).
- 상태·페이드는 BattleBridge 소유(`_enemyDimAlpha` unscaled lerp), 트리거는 DragController(BeginDrag on / CleanupSession off — 모든 종료 funnel). 매 프레임 재적용이라 드래그 중 스폰된 적도 자동 dim.
- **적만** dim: sync 의 적 루프(`_aliveAttackersQuery`)만 `SetDimmed` 호출, 디펜더 루프 미적용 → 디펜더 불투명 유지.
- unit 5: **드래그 유닛(프리뷰) 불투명**(0.62→1.0) — 반투명 적 위 선명한 초점. 소팅은 이미 `DragPreviewOrder`(20000) 최상단.
- unit 6: 드래그 중 **배치 하이라이트(range/hover)를 적 위로** 임시 상승(10000/10002), 종료 시 -12/-10 복원. sticky 로 첫 드래그 lazy 생성도 커버.

## Key Files

- `Assets/_Project/Scripts/Presentation/BlobShadow.cs` — `SetDimAlpha`
- `Assets/_Project/Scripts/Presentation/QuadUnitView.cs` — `SetDimmed`(블렌드 플립), `SetHealthTint`(알파 합성)
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — `SetDimmed`(`skeleton.A` + 그림자 게이트)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SetEnemiesDimmed` + `_enemyDimAlpha` lerp(Update) + 적 sync 루프 합성(~1806) + `SetPlacementHighlightAboveUnits` 포워딩
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `SetPlacementHighlightAboveUnits`(+ `EnsureRangeTilemap` sticky)
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — BeginDrag/CleanupSession 배선 + 프리뷰 불투명

## Verified

- 컴파일 에러 0 (read_console 반복 확인). two-track 리뷰(code-reviewer + ecs-reviewer) **APPROVE**(units 0~4), "ECS 변경 0·채널 14개 불변" 검증됨. M1(Spine 그림자 매프레임 alloc) 반영. 사용자 Play 시각 sign-off 통과.
- units 5·6(프리뷰 불투명 2줄 + 소팅 토글)은 wiring 이라 별도 재리뷰 없이 sign-off 로 확정.

## Notes (되돌리면 안 되는 의도)

- **Quad 는 블렌드 전환 필수**, Spine 은 `skeleton.A` 로 충분(PMA). 되돌려 알파만 낮추면 cutout 이라 안 비침.
- `SetDimmed`→`SetHealthTint` 순서 유지(Quad 알파를 SetHealthTint 가 반영). `Configure` 의 `_transparentApplied/_dimAlpha` 리셋은 재스폰 정확성용.
- Spine 그림자 토글은 **상태 변화 시에만**(매프레임 GetComponentsInChildren alloc 방지). Editor 전용 경로.
- unit 6 소팅값은 TilemapMapView 관례대로 **리터럴**(Core 는 Presentation.BoardSortOrder 미참조). 상승은 드래그 중 한정, "보드<유닛" 기본 규칙은 밖에서 불변.
- 프리뷰 불투명은 keyring 반투명 실루엣(0.62)의 **의도적 반전**.
- 튜닝값은 serialized: `enemyDragDimAlpha`(0.3) / `enemyDragDimFadeSpeed`(8) — 인스펙터 실시간 조정.

## Follow-up

- **스크린스페이스 리빌(스텐실/후처리)** — occluder 타입 무관 통합 대안. README 후속 후보 참조(별도 design + Android perf 스파이크).
- **블로킹 하자드 반투명** — 같은 `SetDimmed` 패턴으로 확장 가능(현 스코프 밖).
- 프리뷰가 불투명 프랍 뒤 depth 로 가려지는 케이스 발견 시 "always-on-top(ZTest 무시)" 별도 처리(공유 spine 머티리얼 주의).
- Android 실기기에서 transparent 큐 전환·페이드 perf 육안 확인(Editor 만 검증됨).
