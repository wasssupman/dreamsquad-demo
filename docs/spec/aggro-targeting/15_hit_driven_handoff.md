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

## Follow-up (잔여)
- **씬 배선 + Play 스모크(Unit 13/14 잔여)**: BattleScene 에 `AggroIconSpawner` GameObject 추가 → `style`=`AggroIconStyle.asset` 할당 → `BattleBridge.aggroIconSpawner` 에 연결 → 씬 저장. 그 후 Play: 가디언 공격 명중→어그로→겹침→가디언 사망 해제, 아이콘 표시/해제, capacity 초과 데미지-only 육안 확인. (에디터 포커스·씬 저장 사용자 필요.)
- 투사체 가디언 지원(ProjectileHit emit arm), 도발 에픽 가디언 — 별도 spec.
- `object-pipeline-map.md` 에 "오버헤드 View" 아키타입 추가 여부 판정.
