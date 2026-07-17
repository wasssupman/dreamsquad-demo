# 6 — Handoff Summary

## Commit
- `922db9b9` — unit 0: `PlacementCellSnap.Resolve` 순수 함수 + EditMode
- `a3812079` — units 1·3·4·5: 히스테리시스 배선 + throttle 스냅 + 확정 팝 + 수평 스큐 보정

## Implemented
- **unit 1** 경계 히스테리시스: 포커스 셀이 sticky 밴드(`placementStickMargin=0.3`) 안에서 유지 → 경계 지터 흡수. 순수 함수 `PlacementCellSnap.Resolve`(frac→int cell) + bridge read 헬퍼(`DebugWorldToCellFractional`, `DebugGridSize`).
- **unit 3** throttle: `placementCommitInterval=0.5`(2Hz)마다 현재 칸으로 스텝 커밋. 이동 중에도 주기적 갱신(실시간 휙휙도 freeze도 아님). 순수 함수 `PlacementSnapDebounce.Step`.
- **unit 4** 확정 팝: 포커스 셀 변경 시 하이라이트에 스케일-페이드 팝(`TilemapMapView`, 재사용 SpriteRenderer). `BoardSortOrder.PlacementCommitPopOrder=12000`.
- **unit 5** 수평 스큐 보정: `TryComputeRingUnit` 에서 `feet` 보드-수평을 손가락 직접 히트와 정렬 → 손가락 가리키는 열에 판정(카메라 위치 무관).
- **unit 2** 되돌림: 고스트 셀 중심 스냅은 키링 줄/스윙을 죽여 폐기. 고스트는 손가락 연속 추종(키링) 유지, 판정 안정화는 논리 레이어(1·3), "스냅 느낌"은 하이라이트 팝(4)이 담당.

## Key Files
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `ResolveFocusAndTarget`(히스테리시스+throttle), `TryComputeRingUnit`(스큐 보정), `SetHover`(팝 발화)
- `Assets/_Project/Scripts/UI/PlacementCellSnap.cs` / `PlacementSnapDebounce.cs` — 순수 함수
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `PulsePlacementHover` + 팝 렌더
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — `placementStickMargin`/`placementCommitInterval` 튜닝값

## Verified
- EditMode 13/13 (`PlacementCellSnapTests` 8 + `PlacementSnapDebounceTests` 5). 컴파일 클린.
- Play 검증(사용자): throttle 스텝·확정 팝·키링 스윙 복원·스큐 해소. 실측: 수평 스큐 0.4→0셀, 수직 스큐 0.25셀(현행 유지).

## 사후 수정 (2026-07-17 critic 리뷰)

- **릴리즈 우회**: `EndDrag` 가 `ResolveFocusAndTarget(0f, forceCommit:true)` 로 throttle 을 우회해 손가락 최종
  칸(히스테리시스 통과)에 배치 — 빠른 드롭이 stale 칸에 배치되던 회귀 수정. throttle 은 드래그 중 표시 전용.
- 커밋 꼬리 `CommitPlacementAt` 로 통일(EndDrag/탭 시뮬 공용), unit 5 의 시뮬 제외 분기 제거(dead code).
  상세: `docs/spec/defender-tap-to-place/README.md` 리뷰 반영 기록.

## Notes (되돌리면 안 되는 의도)
- **unit 2 재도입 금지**: 고스트를 셀에 스냅하면 키링 스윙이 사라진다. 스프링 타깃은 raw feet(손가락) 유지.
- **판정 기준 = 손가락 위치**(`_unitTargetWorld`, 마우스 바로 아래), 흔들리는 유닛 위치 아님.
- **수직 보드공간 오프셋 전환 금지 결정**: 스큐 미미(0.25셀) + 가림 여유 손해(화면 상단 80→69%). 현행 camUp 오프셋 유지.
- **모델 이력**: 셀-거리 게이트 → 속도 게이트 → settle(정지후) → **throttle(주기)** 로 수렴. `3_settle_to_commit.md` "해석 이력" 참조.

## Unit 7 rev (2026-07-18, 커밋 `dedde0f6`)

- **끈적함 액체 하이라이트** 추가 — 히스테리시스 시각화. 오버레이 블롭(1차)은 신호 겹침으로 폐기,
  최종 = 하이라이트 자체가 액체(SDF 셰이더: 고정 테두리 + smin 번짐 + 점액 관성 스프링). 상세/함정
  (Mathf.SmoothStep ≠ HLSL smoothstep · z-fight 리프트 · 쿼드 캔버스 · Shader.Find 스트리핑)은
  `7_stickiness_blob.md` 가 source of truth. 사용자 Play 체감 통과("느낌 좋네").
- EditMode 20 (snap 15 + debounce 5). 튜닝: 모양=`PlacementLiquidTile.mat`, 팔레트/관성=TilemapMapView,
  토글=`stickyBlobEnabled`, 끈적함=`placementStickMargin`(에셋 0.81, interval 0 — 사용자 라이브 튜닝값).

## Follow-up
- **`defender-tap-to-place`**: 커밋 `dedde0f6` 에 units 0~2 포함(같은 커밋). `3_handoff_summary.md` 참조.
- 확정 팝(솔리드 사각)이 액체 톤과 어울리는지 — 필요 시 팝을 액체 이완 모양으로 교체 후보.
- 절대 오프셋(유닛이 손가락보다 ~3셀 아래)이 거슬리면 별도 논의(줄 길이 or 조준 습관). 스큐 아님.
- 후속 후보: 드래그 유닛 반투명, 릴리즈 실패 복귀 애니+햅틱(README 참조).
