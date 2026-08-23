# 4 — 결과 화면·로그 어휘 정합

## 목적

**인게임을 스트레스 어휘로 바꾼 뒤 결과 화면만 옛 어휘로 남지 않게 한다.** 그리고 새 종료
라벨(`stress_full`)이 로그·기록에 정확히 남게 한다. **승/패는 여전히 만들지 않는다**(명제 3).

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `EndMatch("stress_full")` 로그 문구
- `Assets/_Project/Scripts/UI/ResultScreen.cs` — 「남은 마음」 줄(`:139`)
- `Assets/_Project/Scripts/Core/MatchTally.cs` — `Outcome` 주석(값 목록 3개로)

## 구현

**1. `Outcome` 은 라벨이지 판정이 아니다.** `complete` / `submitted` 옆에 `stress_full` 이
붙는다. `MatchTally` 에 승패 필드를 **만들지 않는다** — three-minute-kill-race 가 *"자리를 남기면
조용히 되살아난다"* 며 `Won` 을 은퇴시킨 이유가 그대로 유효하다. 주석의 「값은 둘」 서술을 셋으로 고친다.

**2. ⚠ 결과 화면의 「남은 마음」 줄.** 지금 `StatRow.State("남은 마음", StabilityText(Stability, StabilityMax))`
로 `650 / 1000` 같은 HP 를 그린다. 인게임이 스트레스(차오름)로 바뀌면 **어휘가 갈리고**,
`stress_full` 종료에서는 「남은 마음 0 / 1000」이 뜬다. 셋 중 하나로 정한다:
- **A(권장)**: 같은 줄을 **「스트레스」**로 바꾸고 `StressMath` 로 `0~100` 표기 — 인게임과 한 어휘
- B: 줄을 뺀다 — 점수(처치 수)만 남는다
- C: 그대로 둔다 — 어휘 분열을 수용

**3. `MatchTally.Leaks` 는 항상 0 이 된다.** 명제 1 의 귀결이다. 필드를 지우지 않는다(계약: 휴면).
다만 **화면·로그에 0 을 «정보»로 그리고 있는 자리가 있으면** 그건 거짓말이므로 이 unit 에서 확인한다.
로그 스키마는 유지한다(서버·히스토리 소비처가 있다).

**4. 서버 제출값은 무변경.** `SubmissionScore => Kills`. 종료 경로가 셋이 되어도 **값을 만드는
곳은 하나**라는 계약(three-minute-kill-race)은 그대로 승계한다.

## 완료 기준

- [ ] 컴파일 0 에러 · 콘솔 에러 0
- [ ] EditMode 전체 완주, 신규 실패 0건
- [ ] Play: 스트레스 100 종료 → 결과 화면에 **승/패 표기가 없다**
- [ ] Play: 결과 화면 총점 = 그때까지 처치 수 (3분 완주·유저 제출과 같은 값 경로)
- [ ] Play: 「남은 마음」 자리가 인게임과 **같은 어휘**다
- [ ] 로그에 `outcome=stress_full` 이 기록된다
- [ ] 서버 complete 에 실리는 값이 화면 숫자와 같다
