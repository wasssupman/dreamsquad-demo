# Handoff — placement-drag-preview-polish (완료 2026-07-04)

## Commit
- `d96cd82` unit0 — 드래그 프리뷰 빌보드 각도 정합(root+child 계층)
- `354e418`→`aa17880`→`4e51f1c` unit1 sway — 초기 → 매달린 키링(계층/가속도) → **velocity-lean 최종**
- `fb7bf79`→`1667841` sway 튜닝 — 역동 바운스 → 빠릿+저진폭(현재값)
- `7300cbf` sway 파라미터 `DragSwaySettings` SO 추출(에디터 튜닝 가능화)
- `e849480` 프리뷰 정렬 — 배경 프랍 위로(`DragPreviewOrder 20000`)
- sibling(완료): `docs/spec/placement-attack-range-preview/` 공격범위 격자 프리뷰

## Implemented
- 드래그 프리뷰가 배치 유닛과 **동일 45° 빌보드 틸트**로 섬(이전엔 꼿꼿).
- 프리뷰 = **3노드 매달림**: `root(빈 wrapper·Billboard·position·Destroy) → pivot(머리 위 +hangHeight = 고리) → child(SkeletonAnimation, -오프셋)`. 고리를 회전 → 몸이 아래에서 스윙.
- **포인터 sway = 매달린 키링(velocity-lean)**: 목표각 = `-포인터속도 × leanPerVel`(진행 반대 lean, clamp), 스프링이 lag/overshoot 로 추종. 끄는 내내 뒤로 눕고, 멈추면 목표→0 스윙백+감쇠. (가속도-only 는 정상 드래그에서 ~2°=불가시라 폐기 — 가시성 우선.)
- **정렬**: 프리뷰 SkeletonAnimation 렌더러 sortingOrder = `BoardSortOrder.DragPreviewOrder`(20000) → 배치 중 프랍/유닛/투사체 위.
- **튜닝값 = `DragSwaySettings` SO**: 컨트롤러가 런타임 AddComponent 라 인스펙터가 안 떠서 수치를 SO 로 분리. `Assets/_Project/Data/Config/DragSwaySettings.asset` 편집 → 런타임 반영(Play 중 실시간). 미할당 시 클래스 기본값 폴백.
- fallback capsule 프리뷰: `swayPivot=null` → sway/빌보드 스킵(스코프 밖).

## Key Files
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 핵심. `TryCreateSpinePreview`(3노드+정렬), `Update()`(velocity-lean 스프링), `UpdateDrag`(포인터속도 측정), `Sway` 프로퍼티(SO 폴백), `CleanupSession`(리셋).
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` + `Assets/_Project/Data/Config/DragSwaySettings.asset` — sway 튜닝 편집점.
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — 컨트롤러 런타임 부착 + `swaySettings` SO 주입(`Configure`).
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — `DragPreviewOrder` 상수.
- 참조: `Presentation/Billboard.cs`(root 회전 소유), `SpineUnitView.cs`(배치 유닛 동일 Billboard).

## Verified (Play, MCP)
- compile 0 errors (전 커밋).
- 빌보드: 프리뷰 euler `(45,0,0)` == 배치 유닛 `(45,0,0)`.
- sway 물리: velocity-lean 700px/s → `_swayAngle` -24°(clamp), pivot 실제 회전, 캐릭터 tilt `-24° vs 0°` 스크린샷 비교. 튜닝 시뮬: 드래그 -15° → 정지 스윙백 +6.7° 오버슈트 → 수렴(zeroCross 4, ~0.6s).
- SO 배선: `DefenderSelector.swaySettings` = 에셋(BattleScene 1줄 델타 격리 저장), `Configure` 주입.
- 정렬: 프리뷰 sortingOrder 20000 vs 겹친 프랍 68 → 위로 렌더 확인.

## Notes (되돌리면 안 되는 의도)
- **Billboard 는 root 를 매 `LateUpdate` 로 덮어씀** → sway 는 반드시 **child(swayPivot)** 에. root 에 주면 지워짐.
- **스프링 적분은 매 프레임 `Update()`** — 드래그 입력(`OnDrag`)은 이동 시만 발화하므로 거기서 적분하면 정지 시 각도 고정. `dt=Time.unscaledDeltaTime`(placement 페이즈 timeScale 무관).
- **피벗 = 머리 위 고리**(발 아님). 발이 뜨는 건 의도(키링 dangle). swayPivot 을 발로 되돌리면 오뚝이 — 금지.
- forcing = **velocity-lean**(속도 비례). 순수 가속도 진자는 물리적으론 맞으나 등속 드래그에서 거의 안 움직여 폐기.
- 배치 결과(실제 유닛)에는 sway 없음. 프리뷰 전용.

## Follow-up
- 상위 백로그로 이관: `docs/spec/README.md` Follow-up Backlog 참조(배치 유닛 idle sway · 세로/전후 흔들림 · 드롭 bounce · fallback capsule).
- 사용자 취향 튜닝은 `DragSwaySettings.asset` 에서 (spring/damping/leanPerVel/maxAngle/hangHeight).
