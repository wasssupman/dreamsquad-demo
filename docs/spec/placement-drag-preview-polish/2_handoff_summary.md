# Handoff — placement-drag-preview-polish

## Commit
- `d96cd82` fix(placement): 드래그 프리뷰 빌보드 각도 정합 (unit0)
- `354e418` feat(placement): 드래그 프리뷰 포인터 sway (unit1)
- (sibling: `docs/spec/placement-attack-range-preview/` — 공격범위 격자 프리뷰, 완료)

## Implemented
- 드래그 프리뷰가 배치 유닛과 **동일 45° 빌보드 틸트**로 섬(이전엔 꼿꼿). Play readback euler `(45,0,0)==(45,0,0)`.
- 프리뷰 = **3노드**: `root(빈 wrapper, Billboard·position·Destroy 대상) → pivot(머리 위 +swayHangHeight = 고리) → child(SkeletonAnimation, 아래로 -오프셋)`. root 에 `Billboard`(Tilted, `CharacterBillboardTilt`).
- **포인터 sway = 매달린 키링**(`aa17880` 계층·정정 → `4e51f1c` velocity-lean 최종): 포인터=고리, 몸이 아래 매달려 스윙. `swayPivot`=**pivot**(고리) 회전. 매 프레임 `Update()`: 목표각 = `-포인터속도 × swayLeanPerVel`(진행 반대 lean, clamp), 스프링이 lag/overshoot 로 추종. 끄는 내내 뒤로 눕고(가시), 멈추면 목표→0 스윙백+감쇠. (가속도-only 모델은 정상 드래그에서 ~2°=불가시라 폐기.)
- sway 파라미터 7종 SerializeField: `swayHangHeight 1.5`/`swayMaxAngle 24`/`swayLeanPerVel 0.05`/`swaySpring 50`/`swayDamping 5`/`swayPointerResponse 20`/`swayPointerDecay 12`.
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
- 최종 forcing = **velocity-lean**(목표각 ∝ 포인터 속도, 진행 반대). 순수 가속도 진자는 물리적으론 맞지만 정상 드래그(대부분 등속)에서 거의 안 움직여 폐기 — **가시성 우선**. `swayPointerDecay` 로 포인터 속도가 0으로 감쇠 → 정지 시 목표→0 스윙백. 시각 검증은 `docs/spec` 밖 Play(MCP)에서 `-24° vs 0°` 비교로 완료.
- 배치 결과(실제 유닛)에는 sway 없음. 프리뷰 전용.

## Follow-up
- **사용자 focused Play 육안**: sway 흔들림 느낌 + impulse 부호 최종 확인. 부호 뒤집기 원하면 `UpdateDrag` 의 `_swayVel += -(dx)*swayImpulseScale` 부호 1줄.
- 파라미터(각도/스프링/감쇠/impulse) 실기 튜닝은 SerializeField 로 조정.
- 후속 후보(README): 배치 유닛 idle sway · 세로/전후 흔들림 · 드롭 bounce · fallback capsule 각도/sway.
