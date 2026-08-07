# 3 — 점수: 처치만 · 동점은 안정도

## 목적

점수를 **처치로만** 벌게 하고, 동점을 남은 안정도로 가른다. 시간·스트레스 축과 합산 연출을
제거한다. 선행: unit 0(안정도 값).

## 변경 대상

- `Assets/_Project/Scripts/Core/ScoreMath.cs` — 산식 축소 + 제출값 인코딩/디코딩
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` · `Data/Enemies/*.asset` — `killScore` 재장전
- `Assets/_Project/Scripts/Data/ScoreRulesData.cs` — 초당·점당 배점 은퇴
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 종료 3경로, 제출, 탤리 생략, 처치 수 집계
- `Assets/_Project/Scripts/UI/ScoreHudView.cs` — 버스트 임계 재조정
- `Assets/_Project/Scripts/UI/ResultScreen.cs` · `LeaderboardList.cs` ·
  `UI/Outgame/TournamentHistoryPanel.cs` — 디코딩 표시
- `Assets/_Project/Scripts/Logging/BattleLogger.cs` — `time_score`/`stress_score` 은퇴
- `Assets/_Project/Scripts/UI/ScoreTallyView.cs` + 씬 배선 — 제거
- `docs/reference/score-formula.md` — **전문 재작성**(3원천 표·시간/스트레스 절·결과 화면 5줄이
  전부 무효). 루트 `CLAUDE.md` 가 이 문서를 점수의 정본으로 지정한다
- 테스트: `ScoreMathTests` · **`EndlessScoreTests`(`:18,33,47` 이 `Evaluate` 를 직접 호출)** ·
  `Tests/PlayMode/TallyFlowTest`(`:171-172` 가 리플렉션으로 `time_score` 를 읽는다) ·
  `Tests/PlayMode/EndlessModeSmokeTest`(`:121` 동일)

## 구현

**1. 산식** — `ScoreMath.Evaluate` 의 시간·스트레스 인자와 계산을 삭제한다. 총점 = 처치한 적의
`killScore` 합. 패배해도 깎이지 않으므로 산식의 유일한 분기였던 `defeated` 처리도 사라진다.

**2. 제출값 인코딩** — 서버는 int 하나만 받으므로 동점 판정을 값에 싣는다. 새 타입을 만들지
않고 `ScoreMath` 에 순수 함수 2개를 둔다:

```
BASE        = 1_000_000_000
submitted   = BASE + killScoreTotal × 1000 + clamp(round(stability / stabilityMax × 999), 0, 999)
decode(v)   = v >= BASE ? (v − BASE) / 1000 : LEGACY
```

**`BASE` 오프셋이 있는 이유**: 구 총점은 현실적으로 1~3만이라 `v / 1000` 로 디코딩하면
**10~30 이라는 그럴듯한 가짜 점수**가 되어 신규 기록과 구분할 수 없다. 오프셋 미만은 구 포맷
으로 판정해 디코딩하지 않고 그대로 표시한다. 신규 기록이 구 기록보다 항상 위에 정렬되는 것은
의도다(룰이 다른 기록이다). 오버플로 상한: `killScoreTotal ≤ 1,147,483`.

**3. `killScore` 재장전** — 이름 유지, 값만 티어로: 일반 1 / 엘리트 3 / 보스 10(초기값).
코드 기본값(`AttackUnitData.killScore = 100`)도 1 로 내린다. **엘리트 라벨은 아직 없다** —
`EnemyClass` 는 행동 아키타입(Tanker/Runner/Bruiser/Shooter)이라 강함 티어가 아니므로, 자산
11종을 체력·공격력 기준으로 분류 제안하고 확인받은 뒤 값을 넣는다.

**4. 용어** — 점수는 티어 가중이라 "처치 수"와 같지 않다(잡몹 10 + 보스 1 = 20점). 화면 라벨은
`점수`로 통일하고, **`처치 N기` 는 결과 화면의 별도 줄**로 둔다. 이를 위해 브리지가
`killScore` 합과 **처치 마리 수를 따로** 집계한다.

**5. 전투 중 HUD** — 숫자 단위가 100 → 1 로 바뀌므로 `ScoreHudView` 의 `burstScoreThreshold` 등
임계를 함께 재조정한다(안 하면 버스트 플래시가 영원히 안 터진다).

**6. 탤리 제거** — 합산 연출의 재료가 없다. `BeginTally` 의 시퀀스 재생을 없애고 종료 즉시 결과
화면으로 간다. **서버 제출 지점은 그 자리에 유지**한다(연출과 기록 전송은 독립이라는 기존
계약). `ScoreTallyView` 는 호출자가 사라지므로 스크립트·씬 오브젝트를 함께 정리한다.
`GamePhase.Tally` 전이는 유지한다 — HUD 게이팅이 그 페이즈를 읽는다.

**7. 결과 화면** — 총점(=점수) 아래 3줄: `처치 N기` / `남은 안정도 X / Max (Y%)` /
`도달 웨이브 N`. 기존 5줄(남은 시간·스트레스·시간점수·스트레스점수·처치점수)을 대체한다.
승/패 표기는 unit 0 의 정의를 따른다(3분 완주 = 승리, 안정도 0 = 패배).

## 완료 기준

- [ ] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러/경고 0
- [ ] EditMode: `Encode`/`Decode` 왕복, 안정도 경계(0·max), 점수 0, 상한 `1,147,483` 에서
      오버플로 없음, **`BASE` 미만 값이 LEGACY 로 판정**
- [ ] EditMode: `ScoreMathTests`·`EndlessScoreTests` 를 새 산식으로 재작성
- [ ] Play: 적 1기 처치 → HUD 가 정확히 그 적의 `killScore` 만큼 오른다
- [ ] Play: 종료 시 탤리 없이 바로 결과 화면이 뜨고 총점 == 전투 중 마지막 HUD 숫자
- [ ] Play: 결과 3줄이 실제 상태(처치 수·안정도·도달 웨이브)와 일치
- [ ] 적 asset 13종 **전부** `killScore` 재저작 확인 — 하나라도 100/2000 이 남으면 그 적 1기가
      점수를 100~2000 점프시킨다
- [ ] 제출 로그 확인: 점수 47 + 안정도 62% → `1_000_047_619`, 리더보드 표시는 `47`
- [ ] 구 포맷 기록(예 26,000)이 히스토리에서 가짜 점수로 디코딩되지 않는다
