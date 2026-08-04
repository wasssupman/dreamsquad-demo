# M1 unit 14 세션 인계 (2026-08-05)

feature 종료 handoff 가 아니다 — units 15~20 이 남아 있다. 최신 계약은
[14_rule_extraction_wave_outcome.md](14_rule_extraction_wave_outcome.md) 와 [README.md](README.md) 가 정본이다.

## Commit

| 커밋 | 내용 |
|---|---|
| `6f1bf77f` | **골든 게이트 복구** — 스킬 로드아웃 벽시계 시드 제거 + 블롭 진단 덤프 |
| `773e57b2` | unit 14 — 웨이브·승패·점수 규칙을 `Scripts/Sim/Match/` 로 적출 |

## Implemented

- **골든 게이트가 살아 있다.** 7종 two-run diff 0 + 커밋된 코퍼스와 byte 동일. units 15~20 은
  증인을 갖고 진행할 수 있다.
- `MatchWaveSchedule` — 플랜·인덱스·`_waveTimeShift`·대기열·스폰 예고·클리어 래치 소유.
- `MatchOutcomeRules` — 유출·선불차감·킬점수·결과 래치·제한시간·점수 산출 소유.
- 두 모듈 **부작용 0**(로그·HUD·연출·엔티티 생성 없음). Bridge 가 호출자 + 서술자.
- 읽기 모델이 점수·유출·스트레스를 **실제 값**으로 서빙(`SupportedScore: true`).
- `ScoreHudView` 가 `ReadModel.ScoreKill` 을 따라간다(Battle 구간 한정).
- 웨이브 테스트 20건이 씬·리플렉션을 버리고 모듈 직접 테스트로 전환.

## Key Files

- `Assets/_Project/Scripts/Sim/Match/` — `MatchWaveSchedule` · `MatchOutcomeRules` ·
  `MatchOutcomeNames`. **`UnityEngine` 을 직접 `using` 하지 않는다** = unit 17 asmdef 분리의 출발점.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ResolveAndInitializeWavePlan`(SO 해석 seam) ·
  `ConcludeMatch`(종료 단일 경로) · `NarrateQueuedWaves`(서술) · `Outcome*` internal 읽기 창구.
- `Assets/_Project/Editor/LegacyTraceGoldenRunner.cs` — `DumpCanonicalBlob`. **해시가 흔들리면
  추론하지 말고 `Library/LegacyTraceV0/*.blob.txt` 두 개를 diff 한다.**

## Verified

- 4어셈블리 `dotnet build` 오류 0.
- 전체 EditMode **1910 통과 / 실패 0 / skip 1**(의도적 `[Ignore]`).
- **골든 7종 two-run diff 0 · `git diff` 빈칸**(백업 대비 `cmp` 7/7 동일).
- PlayMode 관련 5클래스(Tally·Endless·NextWaveClear·Kindler·DropDismount) **6 통과 / 실패 0**,
  2회 연속 동일.

## Notes (되돌리면 안 되는 의도)

1. **`TakeDueSpawns` 의 역순 순회**를 유지할 것 — 그 순서가 프레임 내 스폰 순서이고, 엔티티 생성
   순서를 통해 sim 결과에 들어간다(골든이 고정하고 있다).
2. **`MatchOutcome` enum 을 sim 쪽에 다시 만들지 말 것.** 세션 계약(`Wassup.Core.Session`)의
   그것을 쓴다 — 어휘가 갈리면 이벤트·커맨드로그·리플레이가 서로 다른 말을 한다.
3. **`ResolveAndInitializeWavePlan` 을 모듈로 밀어넣지 말 것**(unit 18 까지). SO 해석이라
   규칙 안에 넣으면 sim lib 이 에셋 계층을 물고 온다.
4. **`ScoreHudView` 의 `AddScore` 누적을 지우지 말 것.** 세션 없는 경로에서는 그것이 유일한 값이고,
   스냅샷 동기화는 덮어쓰기로 수렴을 보장한다. 동기화는 **Battle 구간에서만** — 연출이 시간·
   스트레스 축을 킬 점수 위에 더해 올린다.
5. **`ConfigureOutcomeRules(logMissingScoreRules:)` 의 플래그를 지우지 말 것.** 배치 진입마다
   `scoreRules` 미배선 에러가 울리면 SO 미배선 테스트 씬이 배치만 해도 실패한다(원래는 판당 1회).
6. **`NextWaveClearReadyTests` 를 모듈 테스트로 옮기지 말 것.** 검증 대상이 대기열(모듈)과 생존
   적(ECS 질의)의 **합집합**이라 두 소유자가 만나는 지점이 곧 Bridge 다.
7. **골든 검증 세션에서 PlayMode 스위트를 먼저 돌리지 않는다.** 돌렸으면 러너 `ReimportData` 후
   골든. 도메인 리로드로는 낫지 않는다(에셋 인스턴스가 리로드를 넘어 산다).

## Follow-up

- **B2** — `ScoreHudView.SetLeakStatus` push→pull 역전, `ScoreTallyView.Play`,
  `ResultScreen.ShowVictory/ShowDefeat`. 뷰 소유권 문제라 한 묶음. 근거는 `13_consumer_rewiring.md`.
- **unit 15**(배치·통화) 이 다음이다. 소유권 이동 대상: 코스트/쿨타임 쓰기(`CostRuntime`·
  `PlacementCooldownRuntime` — unit 13-A3 은 **번역**만 해 뒀다), 방향 없는 활성화 3종,
  `StartBattle` 재시도 의미(unit 13 이 `FinishPlacement` 를 커맨드로 바꾸지 않은 이유).
- **unit 18(context port) 은 이 spec 의 실질적 본체**다 — ECS 시스템 전체와 27개 이벤트 채널을
  엔진-프리로 옮기는 작업이라 단일 세션 규모가 아니다. units 15~17 을 먼저 끝내고 별도 계획으로
  쪼갤 것을 권한다.
- **unit 20 은 실기기 게이트**다(ARM64 IL2CPP A/B parity). 장비 없이는 완료 불가.
- **골든의 사각지대 2개**(코퍼스 변경 = unit 19 권한이라 미조치): ① 뷰→커맨드 경로를 하네스가
  우회한다(검출기는 PlayMode) ② 코퍼스는 draft 경로를 녹음해 `_skillLoadout` 이 null 이라
  로드아웃 결정론을 증인할 수 없다(회귀 방지는 EditMode 6건이 진다).
