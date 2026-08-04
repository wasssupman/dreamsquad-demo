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
3. **`Arm` 의 순번 리셋을 지우지 말 것.** 두 번째 판의 첫 커맨드가 갭으로 거절돼 배치가 통째로
   먹지 않는다.
4. **`DrainEvents()` 가 빈 목록인 것은 의도**다. 소비자(기록기)가 없는데 누적하면 무한히 자란다.
5. **코스트는 두 값**이다: `CostCurrent`(raw — 지불 판정) · `CostCurrentInt`(floor — 표시).
   합치면 max 근처에서 판정이 1 씩 어긋난다.
6. **`DefenderSelector` 의 폴백 방향이 두 곳에서 반대**다(`int.MinValue` vs `int.MaxValue`).
   의도된 것이며 통일하면 튜토리얼 힌트나 트레이 딤 중 하나가 뒤집힌다.
7. **골든은 bundle C 를 검출하지 못한다** — 하네스가 Bridge API 를 직접 부르고 뷰를 거치지 않는다.
   C 의 검출기는 PlayMode(특히 `DropDismountTest`)다.

## Follow-up

- **C2**(배치 컨트롤러: Deploy/SetFacing/Relocate) — `CommandReceipt.SubjectSimId` 를 이미
  추가해 뒀다(미커밋). 뷰는 뷰 작업용 Entity 를 `bridge.TryResolveSimEntity(simId, out e)` 로 얻는다.
  `_aimController.Begin` → `CleanupSession` 순서(슬로우모 lease 의존)를 보존해야 한다.
- **C3**(드림캐쳐 카드 4변종) — `DreamcatcherHandController` 는 커맨드 발신자로만 바꾸고
  소유권 이동은 unit 16.
- **B2**(`SetLeakStatus`·집계·결과) — unit 14 와 함께. 근거는 `13_consumer_rewiring.md`.
- **unit 14 정찰 완료**: 적출 대상이 `BattleBridge.cs` 두 구역(웨이브 1776~1955, 승패·점수
  4975~5171) + 필드 3개(`_goalReachedCount`·`_leakAllowancePenalty`·`_killScoreTotal`)에 국소화돼
  있다. `ScoreMath` 는 이미 순수 static(conform). `ForceNextWave` 만 public, 나머지는 private.
- **PlayMode 스위트 15 실패**는 이 spec 밖 — README 후속 후보의 "PlayMode 스위트 수리" 참조.
