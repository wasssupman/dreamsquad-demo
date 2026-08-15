# 점수 산식 — 어디서 나오고 얼마인가

> 한 판 끝나면 나오는 최종 점수의 전부. 판의 성적은 `Scripts/Core/MatchTally.cs` 값 하나로
> 취합되고(조립 지점 = `BattleBridge.BuildTally`), 서버로 가는 수는 그 `SubmissionScore` 하나다.
> 값은 적 SO(`Data/Enemies/*.asset` → `killScore`)에서 온다 — 코드에 숫자가 박혀 있지 않다.
> 상세 설계·결정 이력은 `docs/spec/three-minute-survival/`. 구 3축 산식은 `docs/spec/battle-score-formula/`.

## 한 줄 요약

**잡은 만큼 받는다.** 점수원은 처치 하나뿐이고, 져도 남는다. 서버에도 이 수가 그대로 간다.

```
총점 = 제출값 = 처치한 적의 killScore 합
```

| 티어 | 자산 | `killScore` |
|---|---|---|
| 일반 | Basic · Debuffer · Kindler · Needler · Rootcaster · Runner · Sniper · Swift | 1 |
| 엘리트 | Tanker · Vanguard | 3 |
| 보스 | Boss_Jjangssen · Boss_Nightmare · Boss_Mamemo | 10 |

- **놓친 적은 0점**이다. 골을 뚫은 적은 "잡은 것"으로 안 친다(`EnemyKilledEvent` 미발화).
- **져도 깎이지 않는다.** 산식에 분기가 하나도 없다.
- **만점이 고정값이 아니다.** 웨이브 구성이 바뀌면 총합도 바뀌므로 이 숫자에 의존하는 코드를
  만들면 안 된다.

> 점수는 티어 가중이라 **"처치 마리 수"와 같지 않다**(잡몹 10기 + 보스 1기 = 20점).
> 화면 라벨은 `점수`이고, `처치 N기` 는 결과 화면의 별도 줄이다.

## 은퇴한 축 (2026-08-07)

| 축 | 구 산식 | 지금 |
|---|---|---|
| 시간 | 남은 초 × 100 (최대 18,000) | **없음.** 3분은 지갑이 아니라 판의 길이다 |
| 스트레스 | (한계 − 누적) × 900 | **없음.** 스트레스는 집계 지표로만 남고 패배도 점수도 만들지 않는다 |

`Data/Config/ScoreRules.asset`(`ScoreRulesData`)은 공급할 값이 남지 않아 빈 SO 다 —
씬 참조가 있어 타입만 남겼고, 실제 삭제는 에디터에서 에셋·씬을 함께 정리할 때 한다.

## 서버에 보내는 수 = 화면에 보이는 수 (2026-08-15)

가공이 없다. 서버는 int 점수 하나만 받고(`TournamentApi`), 거기 들어가는 값이 곧 총점이다.

```
제출값 = killScore합          // 전투 중 HUD 숫자와 완전히 같은 수
표시값 = 서버가 준 score      // 변환 없음
```

- **동점은 그냥 동점이다.** 정렬은 서버 규칙에 맡긴다.
- 상한·saturate 도 없다 — 인코딩이 만들던 제약이었다.
- 구 인코딩 기록(`1,000,047,599` 같은 값)은 **변환하지 않는다**(사용자 결정).
  리더보드·히스토리에 원값 그대로 뜨고, 룰이 다른 데모 기록이라 그대로 둔다.

> **은퇴한 인코딩(2026-08-07 ~ 08-15)**: `1,000,000,000 + killScore합 × 1000 +
> 안정도permille` 로 동점 판정을 값에 실었다. 안정도가 점수에 섞이는 것을 없애면서
> 디코딩 3곳(`ScoreMath.DisplayScore`)과 함께 통째로 제거했다 — `docs/spec/three-minute-survival/6_score_submit_raw.md`.
> 이어 unit 7 이 `ScoreMath` 자체를 `MatchTally` 로 흡수했다(점수 정본이 둘이 되지 않게).

## 마감 파이프라인 — 취합 → 기록 → 통보 → 표시

종료 5경로(골붕괴 즉사·스트레스 상한·적 마음 붕괴·타이머 만료·전멸)는 **판정만** 하고
`BattleBridge.EndMatch(outcome, win)` 한 곳으로 들어온다.

| 단계 | 어디 | 하는 일 |
|---|---|---|
| 취합 | `BattleBridge.BuildTally` | 흩어진 재료(처치 점수·마리 수·안정도·도달 웨이브·유출)를 `MatchTally` 하나로 |
| 기록 | `BattleLogger.SetResult/SetScore` | 로컬 `GameLogs` 배틀 로그 |
| 통보 | `TournamentMatchReporter.ReportResult` | `tally.SubmissionScore` 를 서버로 |
| 표시 | `ResultScreen.ShowVictory/ShowDefeat` | 총점 + 3줄 |

**제출이 표시보다 앞이라는 순서는 계약이다** — 화면을 기다리다 앱이 죽으면 기록이 사라진다.

## 골 안정도 (패배 조건)

점수는 아니지만 같은 판정에 얽혀 있다.

- 적이 골을 뚫으면 **거기 남아 타워를 때린다**(그 적의 공격력으로 지속 피해).
- 예외: 공격 수단이 없는 돌격형(`Enemy_Runner`·`Enemy_Swift`)은 몸을 부딪고 사라지며 그 적의
  `stabilityDamage`(일반 1 / 엘리트 2 / 보스 5)만큼 1회 피해를 준다.
- **0 = 패배.** 유일한 패배 조건이다(스트레스 한계 패배는 폐기).
- 최대치는 `Deck_*.asset` → `goalStabilityMax`(현재 전 덱 20). 정본은 `GoalTowerHealth` 싱글턴.
- 설계·함정은 `docs/spec/goal-tower-siege/`.

## 결과별 정리

| 결과 | 점수 |
|---|---|
| 3분 완주(승리) | 잡은 만큼 |
| 안정도 0(패배) | 잡은 만큼 — 깎이지 않는다 |

## 값 바꾸는 곳

| 값 | 위치 |
|---|---|
| 적별 처치 점수 | `Data/Enemies/*.asset` → `killScore` (코드 기본값 1) |
| 적별 안정도 피해 | 같은 파일 → `stabilityDamage` (코드 기본값 1) |
| 안정도 최대치 | `Scripts/Data/Decks/Deck_*.asset` → `goalStabilityMax` |
| 제한시간 180초 | 같은 파일 → `timerDurationSec` |
| 스트레스 한계(계약 카드 지불 대상) | 같은 파일 → `defeatGoalReachedCount` — **패배와 무관** |

## 전투 중 화면 위 점수

**우상단 HUD 숫자 = 최종 점수**다. 잡을 때마다 그 적의 `killScore` 만큼 오르고, 전투가
끝나면 **그 숫자가 그대로 결과 화면의 총점**이다. 합산 연출(탤리)은 제거됐다 — 더할 축이 없다.

버스트 플래시 임계(`ScoreHudView.burstScoreThreshold`)는 점수 단위가 100 → 1 로 바뀌면서
4 로 재조정됐다(잡몹 4기 동시 처치 ≈ 보스 1기의 절반).

## 결과 화면에 뭐가 뜨나

총점 아래 세 줄:

```
처치          47기
남은 안정도    12 / 20 (60%)
도달 웨이브    14
```

안정도 백분율은 «얼마나 버텼나» 를 읽히게 하는 정보 줄이다. 점수와는 무관하다
(동점 판정에 쓰이던 시절의 근거는 2026-08-15 에 사라졌다).
