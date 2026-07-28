# 4 — handoff summary

## Commit

- `0aa709b5` feat(defenders): 넉업 심 + on-place 변종 2종 (units 0·1)
- `b770b797` feat(defenders): 난도질꾼·말파이트 유닛 에셋 + 카탈로그 등록 (unit 2)
- `1851d848` feat(knockup-fighter-defender): unit 3 — 넉업 띄우기 연출 (전용 채널 + view 수직 호핑)
- `c8808843` refactor(defenders): on-place 분기 중복 제거 + 호핑 타이머 부작용 분리

## Implemented

- 말파이트(`malphite`) — Fighter·Epic·코스트 5, HP 600 · 사거리 1 · 쿨다운 2.0 · 3체 동시 타격 · 직격 20
- `knockupOnHitSec 0.8` — 히트한 **전 대상**에 Stun. 기존 `sleepOnHitSec`(주 타겟 1체)과 스코프가 다르다
- 배치 스킬 "착지 충격" = `OnPlaceEffectType.StunNearby` 신설 (반경 1, 0.8s)
- 띄우기 연출: `KnockupVisualEventsSingleton`(24번째 채널, Combat→Bridge) → `SpineUnitView.PlayKnockupHop`
- 호핑은 `BoardSpace.ToView` **뒤에** view 공간 Y 가산 · 포물선 `4t(1−t)` · 시간은 `_battleScale` 추종

## Key Files

- `Assets/_Project/Data/Defenders/Defender_Malphite.asset`
- `Assets/_Project/Scripts/Battle/Combat/KnockupVisualEvents.cs` (신규 채널)
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — RESOLVE 히트 루프의 knockup enqueue
- `Assets/_Project/Scripts/Battle/Combat/DefenderCcData.cs` — `knockupOnHitSec` · `knockupVisualHeight`
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — `PlayKnockupHop` / `AdvanceHop` / `CurrentHopOffset`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 큐 lifecycle 3지점 + `DrainKnockupVisualEvents`
- `Assets/_Project/Tests/PlayMode/KnockupOnHitTest.cs`

## Verified

- 리그 PlayMode 7/7 green — 3체 **동시** 스턴(순차 회전 오답 배제) · 배치 반경 필터 · 리팩토링 후 무회귀
- **ecs-reviewer 리뷰 통과 — 전 심각도 결함 0건.** hitTargets 스코프/Dispose, ParallelWriter 안전성, 맥락 경계, Burst 호환, 기존 baked 값 무영향 전부 확인
- 에디터 EditMode 1543건 중 사전 실패 2건만(본 작업 무관)

## Notes (되돌리지 말 것)

- **`knockupOnHitSec` 과 `sleepOnHitSec` 을 하나로 합치지 말 것.** 전자는 `hitTargets` 전원, 후자는 `bestTarget` 1체. 합치면 투머치토커가 깨진다. 그래서 코드 위치도 다르다(전자는 hitTargets 스코프 안, 후자는 Dispose 뒤).
- **연출을 `CcEffect.kind == Stun` 으로 구동하지 말 것.** frost_arrow 등 일반 스턴까지 떠오른다. 그래서 "누구를 띄웠는가"를 띄운 쪽이 직접 신호하는 전용 채널을 뒀다.
- **호핑을 sim-Y 에 넣지 말 것.** 평면 tilemap 보드라 `BoardSpace.ToView` 가 sim-Y 를 버려 화면에 아무 변화가 없다.
- 호핑 시간 진행은 `UpdatePosition` 에서만(`AdvanceHop`). `ApplyRenderPosition` 은 Spawn 에서도 불려서 거기서 진행시키면 이중 진행된다.
- 재신호 = 타이머 재시작(연속 히트로 계속 떠 있는 것이 의도 — 스턴 `remainingTime=max` 와 대칭).
- `knockupVisualHeight` 가 ECS 를 경유하는 건 의도다 — `ProjectileRef.visualScale` 과 같은 형태로, 심은 읽지도 쓰지도 않고 실어보내기만 한다.

## Follow-up

- **사용자 Play 시각 확인** — 떠오름이 "띄운다"로 읽히는지, 높이 1.2 / 0.8초가 적당한지. 애니메이션이라 정지 스크린샷으로는 판정 불가
- 오버헤드 체력바·그림자·정렬이 호핑 중 어색하지 않은지
- frost_arrow(일반 스턴) 대상은 뜨지 않는지 교차 확인
- 보스 상대 광역 넉업 밸런스 감각(면역은 README 후속 후보)
