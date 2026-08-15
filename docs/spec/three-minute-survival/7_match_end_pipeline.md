# 7 — 판 마감 파이프라인: 취합 → 기록 → 통보 → 표시

> 2026-08-15 사용자 지시(리팩토링). unit 6 의 연장 — 점수가 **어디서 취합되어** 서버로 가는지를
> 코드 구조로 드러낸다. 동작 변경 0.

## 진단

**아래층(서버 통보)은 이미 잘 분리돼 있다.** `TournamentApi`(HTTP 계약) → `TournamentMatchReporter`
(attempt 수명·중복 가드·락 복구·pending 재시도) → 브리지는 한 줄 호출. 여기는 손대지 않는다.

**문제는 그 위다 — 「취합」이라는 단계가 코드에 없다.**

- 판의 성적 재료 5개(`_killScoreTotal`·`_killCount`·`_goalStability(+Max)`·`ReachedWaveNumber`·
  `_goalReachedCount`)가 브리지 필드로 흩어져 있고, **값 하나로 존재하지 않는다.**
- 종료 경로 5곳(골붕괴 즉사·스트레스 상한·적 마음 붕괴·타이머 만료·전멸)이 같은 마감 의식
  6줄(`_resultShown`/`_running`/점수 계산/로거 2회/`BeginTally`)을 **각자 복붙**한다. 한 줄만
  빠뜨려도 조용히 어긋난다.
- `CalculateBattleScore(bool defeated)` 의 `defeated` 는 **죽은 인자**다(산식이 읽지 않는다).
  `CheckTimer` 의 주석은 이미 없는 산식(스트레스 점수)을 근거로 그 인자를 설명하고 있다.
- `FinishTally(win, score, remainingSec)` 의 `remainingSec` 도 **버려진다** — 4곳이 계산해서
  넘기지만 소비처(구 결과 화면 2줄)는 이미 죽었다.
- `ResultScreen` 에 호출자 0인 레거시가 남아 있다: 오버로드 6개 · `MatchStats(float,int)` ·
  `HasBreakdown` 분기.

## 변경 대상

- **신규** `Assets/_Project/Scripts/Core/MatchTally.cs` — 판 성적 값 하나(순수)
- **삭제** `Assets/_Project/Scripts/Core/ScoreMath.cs` — `MatchTally` 가 흡수. 남기면 점수 정본이
  둘이 되어 지금보다 나빠진다
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `EndMatch` 단일 관문, `BuildTally` 조립 1곳.
  `BeginTally`/`FinishTally`/`CalculateBattleScore`/`RemainingBattleSeconds` 제거
- `Assets/_Project/Scripts/UI/ResultScreen.cs` — `MatchStats` → `MatchTally`, 레거시 제거
- 테스트: `ScoreMathTests` → `MatchTallyTests`, `EndlessScoreTests` 갱신

## 구현

**1. `MatchTally` = 판이 끝난 시점의 성적 하나.** 아키텍처 무참조 순수 값(제약 10) —
`UnityEngine`/`Entities` 를 모른다. 담는 것: `Outcome`·`Won`·`KillScore`·`KillCount`·
`Stability`/`Max`·`WaveReached`·`Leaks`. 노출하는 것:

```
Total            = KillScore     // 총점. 점수원이 처치 하나뿐
SubmissionScore  = KillScore     // 서버에 보내는 수. 가공하지 않는다(unit 6)
```

`SubmissionScore` 가 **「서버에 뭘 보내나」의 유일한 답**이다. 점수 축이 다시 늘어나면 이 두
프로퍼티만 갈린다. `RemainingSec` 은 **담지 않는다** — 소비처가 없다(제약 8).

**2. `EndMatch(outcome, win)` 단일 관문.** 종료 5경로는 **판정만** 하고 이 한 줄을 부른다.
순서가 여기 한 곳에만 있다:

```
_resultShown/_running  →  BuildTally(취합)  →  Logger(기록)
                       →  Tally 페이즈 → ReportMatchResult(통보) → Result 페이즈 → 표시
```

**제출이 표시보다 앞이라는 계약은 유지한다**(score-tally-sequence 계약 3): 화면을 기다리다
앱이 죽으면 기록이 통째로 사라진다. `GamePhase.Battle → Tally → Result` 전이도 그대로다 —
전투 HUD 게이팅과 `TallyFlowTest` 가 그 순서를 읽는다.

**3. 조립은 `BuildTally` 한 곳.** 브리지 필드 → 값 하나로 옮기는 지점이 유일해야, 재료가
늘거나 줄 때 고칠 곳이 하나다. 종료 경로는 재료를 만지지 않는다.

**4. `ReportMatchResult(MatchTally)`** — 엔드리스 게이트 + 덱 스냅샷 + 제출 + 랭킹/에러 UI 는
그대로 두되 입력이 `int` 에서 tally 로 바뀐다. 서버로 가는 값은 `tally.SubmissionScore` 뿐.

**5. 동작은 하나도 바뀌지 않는다.** 이 unit 은 순수 구조 변경이다.

**남긴 것**: `ResultScreen.ClockText`(public static)는 호출자가 0이 됐지만 남긴다 — 구 「남은
시간」 줄이 죽으면서 이미 실질적으로 도달 불가였고, 전용 EditMode 테스트 2개를 갖고 있다.
순수 포매터라 위험이 없으므로 테스트까지 함께 정리할 때 지운다(후속 후보).

## 완료 기준

- [x] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러 0
- [x] 종료 5경로가 각각 `EndMatch(...)` 한 줄 + 로그 한 줄로 줄었다
- [x] `ScoreMath` 참조 0건, 점수 취합 지점이 `BuildTally` **하나**
- [x] EditMode 56/56 초록(전 어셈블리) — `MatchTallyTests`·`EndlessScoreTests`·
      `ResultScreenStatTextTests` 포함
- [x] PlayMode `TallyFlowTest.BattleEnd_GoesThroughTally_AndReachesResult` 초록(17초).
      로그상 스트레스 상한 패배가 `EndMatch` 를 타고 Result 도달
- [ ] **Play 육안 미확인**: 승리/패배 각 1회 — 결과 3줄(처치·안정도·도달 웨이브)이
      리팩토링 전과 같고 `complete ok — score=N` 의 `N` 이 총점과 같다

> 2026-08-15 구현 · 커밋 `1e8c1a90`. 자동 검증(컴파일·EditMode·PlayMode)까지 완료, 실기 Play 육안 확인은 대기.
