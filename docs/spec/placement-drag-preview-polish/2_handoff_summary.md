# Handoff — placement-drag-preview-polish

## Commit
- `d96cd82` fix(placement): 드래그 프리뷰 빌보드 각도 정합 (unit0)
- `354e418` feat(placement): 드래그 프리뷰 포인터 sway (unit1)
- (sibling: `docs/spec/placement-attack-range-preview/` — 공격범위 격자 프리뷰, 완료)

## Implemented
- 드래그 프리뷰가 배치 유닛과 **동일 45° 빌보드 틸트**로 섬(이전엔 꼿꼿). Play readback euler `(45,0,0)==(45,0,0)`.
- 프리뷰 = **3노드**: `root(빈 wrapper, Billboard·position·Destroy 대상) → pivot(머리 위 +swayHangHeight = 고리) → child(SkeletonAnimation, 아래로 -오프셋)`. root 에 `Billboard`(Tilted, `CharacterBillboardTilt`).
- **포인터 sway = 매달린 키링**(`aa17880` 정정): 포인터=고리, 몸이 아래 매달려 스윙. `swayPivot`=**pivot**(고리) 회전. 매 프레임 `Update()` 가 **가속도 구동 진자** 적분 — 포인터 속도 스무딩·감쇠 후 그 변화(=고리 가속도)로 `_swayVel += -Δv·accelScale` + 중력복원·감쇠. 등속=수직 / 출발=역lag / 정지=overshoot.
- sway 파라미터 7종 SerializeField: `swayHangHeight 1.5`/`swayMaxAngle 24`/`swaySpring 60`/`swayDamping 6`/`swayAccelScale 0.03`/`swayPointerResponse 20`/`swayPointerDecay 12`.
- fallback capsule 프리뷰: `swayPivot=null` → sway/빌보드 스킵(스코프 밖).

## Key Files
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 전부 여기. `TryCreateSpinePreview`(계층), `Update()`(스프링), `UpdateDrag`(impulse), `CleanupSession`(리셋), `DragSession.swayPivot`.
- 소유권 참조: `Assets/_Project/Scripts/Presentation/Billboard.cs`(root 회전 소유), `SpineUnitView.cs`(배치 유닛의 동일 Billboard 셋업).

## Verified
- compile 0 errors (unit0·unit1).
- 빌보드: 프리뷰 euler == 배치 유닛 euler == 45° X (Play readback + `drag_billboard.png`).
- sway: x+ 플릭 시 `_swayAngle`/`_swayVel` 비영(lean), 정지 후 `angle=0 vel=0` 수렴(settle). 방향 = 시계방향/윗부분 오른쪽.
- 스크린샷: `drag_billboard.png`(틸트+범위격자 추종). sway 전용 스크린샷은 에디터 MCP approval 거부로 미생성.

## Notes (되돌리면 안 되는 의도)
- **Billboard 는 root 를 매 `LateUpdate` 로 통째 덮어씀** → sway 는 반드시 **child(swayPivot)** 에 얹어야 함(root 에 주면 지워짐). 이게 2노드 계층의 이유(unit0 이 전제).
- **스프링 적분은 반드시 `Update()`(매 프레임)** — 드래그 입력(`OnDrag→UpdateDrag`)은 포인터 이동 시만 발화하므로 거기서 적분하면 멈추는 순간 각도 고정(F1). `dt=Time.unscaledDeltaTime`.
- **피벗 = 머리 위 고리**(발 아님). 몸이 고리 아래 매달려 스윙하므로 발이 뜨는 건 **의도**(키링 dangle). swayPivot 을 발(localPos 0)로 되돌리면 오뚝이가 됨 — 되돌리지 말 것.
- forcing 은 포인터 **속도**가 아니라 **가속도**(속도의 변화). velocity 로 주면 등속 드래그에서 계속 기운 채 유지(진자 아님). `swayPointerDecay` 로 포인터 속도가 0으로 감쇠해야 "정지" 감속이 역스윙으로 잡힘.
- 배치 결과(실제 유닛)에는 sway 없음. 프리뷰 전용.

## Follow-up
- **사용자 focused Play 육안**: sway 흔들림 느낌 + impulse 부호 최종 확인. 부호 뒤집기 원하면 `UpdateDrag` 의 `_swayVel += -(dx)*swayImpulseScale` 부호 1줄.
- 파라미터(각도/스프링/감쇠/impulse) 실기 튜닝은 SerializeField 로 조정.
- 후속 후보(README): 배치 유닛 idle sway · 세로/전후 흔들림 · 드롭 bounce · fallback capsule 각도/sway.
