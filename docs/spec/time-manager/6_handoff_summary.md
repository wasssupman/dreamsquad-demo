# 6 — Handoff Summary

## Commit
`feat(time-manager)` — main 직접 커밋(2026-07-06). 사용자 정지 육안 확인 완료.
워크트리 무관 dirty 다수라 time-manager 파일 39개만 개별 스테이징(`git add -A` 금지). 해시는 git log 참조.

## Implemented (Units 0–5)
- `TimeManager`(예외 싱글턴, `Wassup.Core.TimeControl`) — 도메인별 시간 스케일 소유. `Request/ScaleOf/DeltaTime/ScaleChanged/ResetAll` + 멱등 `TimeLease`(단조 id, 이중 dispose 안전).
- arbitration: 승자 = priority desc, 동률 scale asc, 요청 없으면 1. 글로벌 `Time.timeScale` 은 코드에서 절대 write 안 함(0건).
- `BattleSimGroup` 신설 + 배틀 시스템 24개 `[UpdateInGroup]` 재타겟, `[UpdateBefore(TransformSystemGroup)]`.
- `BattleScaledRateManager`(IRateManager): scale≤0 → 그룹 skip(완전 정지), scale>0 → 스케일된 delta 로 1회 update. elapsed 는 로컬 누산기(정지 후 점프 없음).
- `BattleBridge`: `BattleTimeScale` singleton 매 프레임 write(경계 유지) + `_battleClock`(double) 이 웨이브 스케줄·ForceNextWave·CheckTimer·TimerRemaining·CalculatePlayerScore 를 실시간 대신 구동. 이벤트/로그 타임스탬프는 의도적으로 실시간 유지.
- 전투 Spine 유닛: 스폰 시 `ScaleOf(Battle)` pull + `ScaleChanged` fan-out(`SpineUnitView`/`SpineUnitPool`). (Quad 유닛은 애니 없음=ECS 위치만이라 불필요.)
- DreamcatcherController: 구 `Time.timeScale=0` → TimeManager lease(pri 100), OnDisable/OnPicked 해제.
- DefenderDragPlacementController: BeginDrag 에서 슬로우모 lease(`dragSlowmoScale` SerializeField 0.2), CleanupSession 에서 해제(전 종료 경로 커버).

## Key Files
- `Assets/_Project/Scripts/Core/TimeControl/TimeManager.cs`, `TimeDomain.cs`
- `Assets/_Project/Scripts/Battle/BattleSimGroup.cs`, `BattleScaledRateManager.cs`, `BattleTimeScale.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`PushBattleTimeScaleToEcs`, `_battleClock`, ResetAll 배선, H1 destroy)
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs`, `SpineUnitPool.cs`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherController.cs`, `UI/DefenderDragPlacementController.cs`
- 테스트: `Tests/EditMode/TimeManagerTests.cs`, `BattleScaledRateManagerTests.cs`

## Verified
- **컴파일**: 클린(에러 0).
- **EditMode**: 477개 중 13개 신규(TimeManager 7 + RateManager 6) 전부 통과. 유일 실패 `ObstaclePlacerTests`는 변경 전부터 존재하는 무관 사전실패.
- **런타임(포커스 불필요)**: default world 에 `BattleSimGroup`(children=24) + `RateManager=BattleScaledRateManager` + `BattleTimeScale` singleton 1개. Mono→ECS 경로: Request(0.2)→singleton 0.2, 정지(pri100) 겹침→0(arbitration), 해제→복귀. H1: destroy→recreate 후 singleton 정확히 1(orphan 없음), TryGetSingleton=True.
- **미검증(사용자 Play 필요, 포커스 요구)**: 실제 화면에서 슬로우모/정지 시각 육안 — 드래그 중 전투 0.2배·드래그 유닛/프리뷰 정상속도, Dreamcatcher 선택 시 전투 완전 정지·재개.

## Notes (되돌리면 안 되는 의도)
- 글로벌 `Time.timeScale` 은 1 고정. 다시 `Time.timeScale=0/x` 로 정지 구현하지 말 것(도메인 분리가 깨짐). TRD §5.2 에 TimeManager 싱글턴 예외 기록됨.
- `BattleTimeScale` 은 `DestroyEcsInfrastructureEntities` 에서 반드시 파괴(H1). 빼면 StopBattle 후 orphan→2개→시간제어 영구 무력화.
- RateManager elapsed 는 로컬 `_elapsedTime` 누산(월드 elapsed 읽기 금지 — 정지 후 점프).
- 드래그 프리뷰 sway 는 `unscaledDeltaTime`(Interaction 도메인) — 스케일 대상 아님.

## Follow-up
- 사용자 Play 확인 → 통과 시 각 작업단위 "완료 기준" 에 확인일자+커밋해시 기재 후 커밋.
- README "후속 후보": 투사체 VFX 파티클 스케일 · 독립 정지 메뉴 UI · RateManager allocator swap(M2, internal API).
