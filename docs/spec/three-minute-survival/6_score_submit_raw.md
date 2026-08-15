# 6 — 점수 제출: 생값 (인코딩·안정도 tie-break 제거)

> 2026-08-15 사용자 결정. unit 3 의 「제출값 인코딩」 절을 **폐기**한다.

## 목적

**잡은 만큼이 곧 점수다.** 서버에 보내는 숫자와 화면에 보이는 숫자를 같게 만든다.
unit 3 이 넣은 `1,000,000,000 + 처치점수 × 1000 + 안정도permille` 인코딩과, 그것을 되꺼내는
디코딩 3곳을 전부 걷어낸다. 남은 마음의 안정도는 **점수에 일절 관여하지 않는다** — 패배 조건과
결과 화면의 정보 줄로만 남는다.

동점을 값으로 가르는 장치가 사라지므로 **동점은 그냥 동점**이다(서버 정렬 규칙에 맡긴다).

## 변경 대상

- `Assets/_Project/Scripts/Core/ScoreMath.cs` — 인코딩/디코딩 API 전량 삭제
  (`SubmissionBase`·`KillScoreScale`·`MaxEncodableKillScore`·`EncodeSubmission`·
  `StabilityPermille`·`IsEncodedSubmission`·`DecodeKillScore`·`DecodeStabilityPermille`·
  `DisplayScore`). 남는 것은 `BattleScore` 와 `Evaluate` 뿐
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BeginTally` 가 `score.Total` 을 그대로 제출
- `Assets/_Project/Scripts/UI/ResultScreen.cs` · `UI/LeaderboardList.cs` ·
  `UI/Outgame/TournamentHistoryPanel.cs` — 서버가 준 `score` 를 그대로 표시
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — 인코딩 여유를 근거로 들던 주석 정리
- 테스트: `ScoreMathTests`(인코딩 절 삭제) · `EndlessScoreTests`(`StabilityRidesInSubmissionOnly` 삭제)
- `docs/reference/score-formula.md` — 「동점은 남은 골 안정도로 가른다」 절 교체

## 구현

**1. 산식은 손대지 않는다.** 총점 = 처치한 적의 `killScore` 합(일반 1 / 엘리트 3 / 보스 10)은
그대로다. 이 unit 이 바꾸는 것은 **그 값을 어떻게 실어 보내는가** 하나뿐이다.

```
제출값 = 처치한 적의 killScore 합        // 전투 중 HUD 숫자와 완전히 같은 수
표시값 = 서버가 준 score 그대로          // 변환 없음
```

**2. 구 기록은 변환하지 않는다**(사용자 결정). 이미 서버에 쌓인 인코딩 값은 리더보드·히스토리에
`1,000,047,599` 같은 원값으로 뜬다. 판별 오프셋을 읽기 전용으로 남기면 «제거» 가 아니라
«반쪽 유지»가 되고, 그 분기가 다음 세대까지 따라다닌다. 데모 기록은 버릴 수 있는 값이다.

**3. 안정도는 제출 경로에서 완전히 빠진다.** `_goalStability` 를 읽던 유일한 제출 지점이
사라지므로, unit 3 이 만들었던 **순서 의존**(붕괴 프레임에 미러를 먼저 0 으로 놓고 유출을
열어야 한다는 `SyncGoalStability` 의 제약)도 제출 측에서는 해소된다. 다만 그 순서는 HUD 미러
정확성 자체를 위해 여전히 필요하므로 **코드는 그대로 두고 주석의 근거만 갱신**한다.

**4. 결과 화면의 `남은 안정도 X / Max (Y%)` 줄은 유지한다.** 백분율을 함께 내던 근거가
«tie-break 가 비율이라 화면에서 검산돼야 한다» 였는데 그 근거는 사라진다. 그래도 얼마나
버텼는지는 판을 읽는 정보라 줄 자체는 남기고 주석만 정리한다(범위 밖 UI 변경 금지).

## 완료 기준

- [x] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러 0
- [x] 코드베이스에 `EncodeSubmission`/`DisplayScore`/`permille` 참조가 0건
- [x] EditMode: 점수 산식 테스트 초록 (unit 7 에서 `MatchTallyTests` 로 이관)
- [x] 안정도가 달라도 같은 처치 점수면 제출값이 같다
      (`MatchTallyTests.SubmissionScore_IgnoresStability`)
- [ ] **Play 육안 미확인**: 판 종료 후 콘솔 `[TournamentReporter] complete ok — score=N` 의
      `N` 이 전투 중 마지막 HUD 숫자·결과 화면 총점과 셋 다 같은 수
- [ ] **Play 육안 미확인**: 결과 화면 리더보드에 내 점수가 그 수 그대로(10억대 숫자 없음)

> 2026-08-15 구현. 컴파일·EditMode 검증까지 완료, 실기 Play 육안 확인은 대기.
