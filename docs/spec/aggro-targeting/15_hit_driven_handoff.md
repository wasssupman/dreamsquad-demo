# Unit 15 — 히트 구동 재설계 handoff

> 근접 즉시 배정 → 가디언 공격 명중 구동 전환(Unit 9~14). 다음 작업자용 지도.

## Commit
- `77203ac1` docs(spec): 히트 구동 재설계 스펙 (Unit 9~14 + README)
- `1f8246d6` feat(aggro): 정의 계층 순수함수 AggroPolicy/AggroTargeting + EditMode [9]
- `b84b6887` feat(aggro): 히트 구동 코어 — AggroCapacity/AggroStateSystem/히트 채널 [10-13]
- (이 커밋) docs + AggroIconStyle.asset + 절차적 아이콘 폴백

## Implemented
- 정의 계층 순수함수 3(ECS 무참조): `AggroPolicy.CanAcquire`/`ShouldRelease`, `AggroTargeting.SelectTargets`(여유 시 비-어그로 최근접 우선, 상한 시 최근접).
- `AggroProvider`(+`aggroRange`) 폐기 → `AggroCapacity{max,held}`(존재=가디언, held는 Effects full recompute).
- `AggroAssignmentSystem` → `AggroStateSystem`(Effects): 해제(사망 3중판정)/held 재계산/히트 드레인(claimed+runningHeld 로 capacity·선점 게이트, critic H1).
- `AggroHitEventsSingleton` NativeQueue(Combat→Effects, 소비자-Effects 소유). BattleBridge lifecycle. 채널 15개.
- AttackSystem: 가디언 aggro-aware 타겟팅(SelectTargets)+명중분 emit, primary 를 실제 히트 대상으로 정렬(critic H1 넉백/DC 일관).
- Guardian `attackTargetCount 1→2`.
- 어그로 아이콘(Mono 소비): `AggroIconSpawner`/`AggroIconView`/`AggroIconStyle` + BattleBridge 상태 구동 reconcile. 아트 미할당 시 절차적 "!" 폴백(붉은 주황).

## Key Files
- `Assets/_Project/Scripts/Battle/Combat/{AggroPolicy,AggroTargeting}.cs`
- `Assets/_Project/Scripts/Battle/Effects/{AggroCapacity,AggroHitEvents,AggroStateSystem}.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` (가디언 분기 + emit)
- `Assets/_Project/Scripts/Presentation/{AggroIconSpawner,AggroIconView}.cs`, `Data/AggroIconStyle.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (베이크 + 채널 + reconcile)
- Tests: `AggroPolicyTests.cs`, `AggroStateSystemTests.cs`

## Verified
- 컴파일 클린(에러 0). EditMode 604 통과 — 무관 사전실패 `ResultLeaderboardModelTests`(한글화 stale) 1건만.
- ecs-reviewer 2회(설계+코드): CRITICAL 0. H1(primary 정렬)/M2(경계 테스트)/M4(drain Exists) 반영. H2(채널 목록) 반영.

## Notes (되돌리면 안 됨)
- held 는 **1-tick 지연**(Pass2 재계산은 드레인 전 커밋분 기준) — 의도. AggroStateSystemTests 가 이 수렴을 고정.
- 채널은 **소비자(Effects) 소유** — Combat 은 enqueue만, Aggroed/AggroCapacity 쓰기는 AggroStateSystem 단독.
- 근접 전용(투사체 가디언 없음). bestTarget=null 이면 RESOLVE 진입 안 함 → hitCount 0 안전.

## 상태: 완료 2026-07-09
씬 배선(`5ea07f6c`) + 밸런스 capacity 2(`8c892d3d`) + 사용자 Play 확인. Play 진입 에러 0.

## Follow-up (별도 spec, 순차 진행)
- **어그로 아이콘 → 상태 연출(unit-status-fx)**: 현 "!" 는 플레이스홀더. 어그로는 "느끼게 할 상태"라 아이콘 배지가 아니라 연출(온-바디 VFX/마커/가디언 tether/틴트). `AggroIcon*` 를 그 첫 케이스로 재편. **다음 우선순위**.
- **모디파이어 인디케이터 스트립(unit-modifier-indicators)**: 버프/디버프(`ModifierStats`·DoT 스택)/드림캐쳐(`DreamcatcherCard.art`) 아이콘 행. 어그로 연출과 다른 축(정보 배지).
- 투사체 가디언 지원(ProjectileHit emit arm), 도발 에픽 가디언 — 별도 spec.
