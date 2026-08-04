# M1 unit 13 세션 인계 (2026-08-05)

feature 종료 handoff 가 아니다 — units 14~20 이 남아 있다. 이 문서는 **unit 13 진행 중** 세션이
넘어갈 때의 지도다. 최신 계약은 `13_consumer_rewiring.md` 와 `README.md` 가 정본이다.

## Commit

| 커밋 | 내용 |
|---|---|
| `b0681da6` | HEAD 골든 parity 확인 — 머지 2 행동 중립 증명 + 러너 stale 방지 |
| `1ce4407c` | unit 13 **A1** — 세션 로케이터 + Bridge 공급 + `NextWaveDock` |
| `cbd830c3` | unit 13 **A2** — 스폰 예보 `ReadOnlySpan` |
| `0cd3e04c` | unit 13 **A3** — 코스트·쿨타임 표면 + 소비자 4개 |
| `e588d6b5` | unit 13 **B1** — push 역전(킬 점수·보스 경보) + 무인 리컴파일 |
| `18ed2315` | unit 13 **C1** — 단순 3동사 커맨드화 + C2 검출기 복구 |
| `9ba84fc1` | 이 인계 문서 |
| `a71e8088` | **리뷰 반영** — 순번 소유를 세션으로, 라우터 게이트, tick 부재 명시 |

## Implemented

- **세션 획득 = 정적 로케이터** `MatchSession.Current`(사용자 결정). Bridge 가 `BeginPlacement`
  에서 `Arm`, `OnDestroy` 에서 `Release`. `Bridge.Session` 공개 프로퍼티는 **의도적으로 없다**.
- 읽기 모델: 웨이브·타이머(A1) · 스폰 예보 span(A2) · 코스트·쿨타임(A3). 점수·게이지는 미지원
  플래그 유지(unit 14·16).
- 이벤트 fan-out = **정적** `MatchSession.Events` + `Publish`. 세션이 매치마다 교체되므로
  인스턴스 구독은 부적합하다. 뷰는 `OnEnable`/`OnDisable` 에서 붙이고 뗀다.
- 커맨드 발신 = `MatchSession.Send(build)` 단일 창구. 순번은 여기서만 움직이고 `Arm` 에서 0 리셋.
- 커맨드화 완료: `ForceNextWave` · `SetPaused`(MenuPopup lease 소유 이전) · `FinishPlacement`.

## Key Files

- `Assets/_Project/Scripts/Core/Session/` — `MatchSession`(로케이터·이벤트·순번) ·
  `IMatchSession` · `MatchReadModel` · `MatchSessionContract`(커맨드·receipt) · `MatchSessionEvents`
- `Assets/_Project/Scripts/Bridge/LegacyMatchSessionAdapter.cs` — 구 sim 위 구현체(번역만)
- `Assets/_Project/Editor/SimTestAutoRunner.cs` — 락 하 테스트/골든/리컴파일 러너
- 재배선된 뷰: `UI/{NextWaveDock,CostDisplay,DefenderSelector,DefenderDragSlot,ScoreHudView,
  BossWarningView,MenuPopup,PlacementPhaseView}` · `Presentation/SpawnAlertPresenter`

## Verified

매 조각마다 동일 절차: 4어셈블리 `dotnet build` 오류 0 → 전체 EditMode → 골든 7종 byte diff.

- 전체 EditMode **1898 통과 / 실패 0 / skip 1**(의도적 `[Ignore]` 1건).
- **골든 7종 byte diff 0** — A1·A2·A3·B1·C1 전부에서 확인(승격 mtime 변화 + 백업 대비 `cmp` 7/7).
- PlayMode 배치/재배치 집중 **passed=8 failed=3** — 실패 3건은 전부 선행 파손 `PlacementAuraTest`.
- `Refresh` 모드로 **무인 리컴파일→검증** 경로가 실전 검증됨(사용자 개입 불필요).

## Notes (되돌리면 안 되는 의도)

1. **`BeginPlacement` 는 `Dispose` → 생성 → `Arm` 순서다.** 옛 세션이 잡은 pause lease 를
   반납하지 않으면 그 판이 영구 정지한다.
2. **`Release(expected)` 의 신분 확인을 지우지 말 것.** 씬 전환에서 새 Bridge 가 무장한 뒤 옛
   Bridge 의 `OnDestroy` 가 늦게 오면 무조건 null 대입이 살아 있는 새 세션을 지운다.
3. **순번은 세션이 소유한다**(`NextClientSeq`) — 호출자 쪽에 카운터를 다시 만들지 말 것. 어댑터의
   갭 분기는 기대값을 전진시키지 않으므로 두 값이 어긋나면 **재수렴이 없고**, receipt 를 보는
   호출부가 없어 콘솔이 깨끗한 채로 입력 전체가 죽는다. 커맨드는 `MatchSession.Send` 로만 보낸다.
4. **`Publish(sender, evt)` 의 발신자 게이트를 지우지 말 것.** Ghost(남의 판)·Replay(seek)가 같은
   정적 창구를 쓰면 `ScoreHudView` 가 누적식이라 상대 킬이 내 점수를 부풀린다.
5. **`ResetForTests()` 는 `Events` 를 지우지 않는다.** Play 진입 도메인 리로드가 꺼져 있어 뷰의
   `OnEnable` 이 재실행되지 않는다 — 끊으면 그 세션 내내 HUD·배너가 되살아나지 못한다.
6. **라이브 tick 은 `-1`(모른다)이다.** `_harnessTick` 은 하네스만 증가시킨다. 0 으로 바꾸면
   unit 19·20 이 거짓 tick 위에 세워지고 **골든으로는 잡히지 않는다**(골든은 하네스로 녹음된다).
7. **`FinishPlacement` 를 커맨드로 바꾸지 말 것**(unit 14 까지). `StartBattle` 의 자기치유 재시도가
   사라져 배치가 소프트락된다 — 근거는 `13_consumer_rewiring.md`.
8. **`DrainEvents()` 가 빈 목록인 것은 의도**다. 소비자(기록기)가 없는데 누적하면 무한히 자란다.
9. **코스트는 두 값**이다: `CostCurrent`(raw — 지불 판정) · `CostCurrentInt`(floor — 표시).
   합치면 max 근처에서 판정이 1 씩 어긋난다.
10. **`DefenderSelector` 의 폴백 방향이 두 곳에서 반대**다(`int.MinValue` vs `int.MaxValue`).
    의도된 것이며 통일하면 튜토리얼 힌트나 트레이 딤 중 하나가 뒤집힌다.
11. **골든은 bundle C 를 검출하지 못한다** — 하네스가 Bridge API 를 직접 부르고 뷰를 거치지 않는다.
    C 의 검출기는 PlayMode(특히 `DropDismountTest`)다.
12. **`BossSpawned` 의 simId 단정은 `_session != null` 안에 있어야 한다.** 밖으로 내면
    `PatternBakeTests`(리플렉션 단독 호출, `AttachSimEntityId` 미경유)가 6건 깨진다 — 실측했다.

## Follow-up

- **C2**(배치 컨트롤러: Deploy/SetFacing/Relocate) — `CommandReceipt.SubjectSimId` 는 이미 있다
  (`a71e8088`). 뷰는 뷰 작업용 Entity 를 `bridge.TryResolveSimEntity(simId, out e)` 로 얻는다.
  `_aimController.Begin` → `CleanupSession` 순서(슬로우모 lease 의존)를 보존해야 한다.
  진행 전 `DropDismountTest` 가 초록인지 확인하고, 끝난 뒤 다시 돌린다 — 유일한 자동 검출기다.
  드래그/조준은 매 프레임 발신이므로 `Send(Func<uint,MatchCommand>)` 의 클로저 할당이 문제가 된다
  (리뷰 minor 6) — 그때 상태 인자를 받는 오버로드를 추가한다.
- **C3**(드림캐쳐 카드 4변종) — `DreamcatcherHandController` 는 커맨드 발신자로만 바꾸고
  소유권 이동은 unit 16.
- **B2**(`SetLeakStatus`·집계·결과) — unit 14 와 함께. 근거는 `13_consumer_rewiring.md`.
- **unit 14 정찰 완료**: 적출 대상이 `BattleBridge.cs` 두 구역(웨이브 1776~1955, 승패·점수
  4975~5171) + 필드 3개(`_goalReachedCount`·`_leakAllowancePenalty`·`_killScoreTotal`)에 국소화돼
  있다. `ScoreMath` 는 이미 순수 static(conform). `ForceNextWave` 만 public, 나머지는 private.
- **PlayMode 스위트 15 실패**는 이 spec 밖 — README 후속 후보의 "PlayMode 스위트 수리" 참조.
