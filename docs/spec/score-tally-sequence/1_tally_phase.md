# 1 — Tally 페이즈 + 종료 3종 라우팅

## 목적

전투 종료와 결과 화면 사이에 **연출 구간(`GamePhase.Tally`)** 을 만들고, 종료 3종을
단일 관문으로 모은다. 연출 본체는 unit 2 — 이 단위는 **seam 만 만들고 동작은 현행과 동일**하다.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Core/GameManager.cs` — `GamePhase.Tally`
- 수정 `Assets/_Project/Scripts/UI/ScoreHudView.cs` — Tally 에서 패널 유지
- 수정 `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BeginTally` / `FinishTally`

## 구현

### enum 값은 반드시 맨 뒤에

```csharp
public enum GamePhase { None, Draft, Gift, Placement, Battle, Result, Tally }
```

흐름상 Tally 는 Battle 과 Result **사이**지만, 값을 중간에 끼우면 안 된다.

`CameraDirectionConfig` 의 `CameraPhasePose.phase` 와 `breathPhases` 가 이 enum 을
**int 로 직렬화**하고 있다 — `Assets/_Project/Data/Camera/CameraDirectionConfig.asset` 이
`phase: 1 / 3 / 4 / 5` 를 들고 있다. 중간 삽입 시 `Result` 가 5→6 으로 밀려 카메라 설정이 통째로 어긋난다.

> `git grep "GamePhase" -- '*.asset'` 은 이걸 못 잡는다. enum 은 int 로 저장돼 이름이 안 남는다.
> **직렬화 여부는 필드 선언으로 확인해야 한다.**

### HUD 게이팅

`ScoreHudView.OnPhaseChanged` 에 Tally 분기를 추가한다. **패널을 유지하고 리셋하지 않는다** —
전투에서 쌓인 킬점수를 그대로 이어받아야 연출이 성립한다.

나머지 전투 HUD(NextWaveDock·CostDisplay·DefenderSelector·핸드뷰)는 `== GamePhase.Battle` 을
보므로 **자동으로 꺼진다.** 별도 작업이 필요 없다.

### 단일 관문

```
BeginTally(win, score, remainingSec, timeBudget, stressBudget)
  → SetPhase(Tally)
  → ReportMatchResult(score.Total)     ← 서버 제출은 여기서 (계약 3)
  → [unit 2 가 연출을 넣을 자리]
  → FinishTally(...)  → SetPhase(Result) → ShowVictory/ShowDefeat
```

**서버 제출을 연출 시작 시점에 두는 이유**: 연출이 끝나길 기다리면 그 2~3초 사이에 앱이
죽거나 홈으로 나갔을 때 기록이 통째로 사라진다. 화면 연출과 기록 전송은 독립이어야 한다.

`SetResult`/`SetScore` 는 `BeginTally` **호출 전**에 유지한다 — 서버 제출이 로그 스냅샷을
쓰므로 순서가 load-bearing 이다.

## 완료 기준

- [x] compile 통과, `read_console` 에 신규 에러 없음
- [x] EditMode 전체 통과 (1125 / 0 실패)
- [x] Play: `Battle → Tally → Result` 순으로 전이한다
- [x] Play: Tally 동안 점수 HUD 패널이 **보인다**
- [x] Play: Result 로 넘어가면 숨는다
- [x] 종료 3종이 전부 `BeginTally` 를 거친다 (패배/버팀승리/전멸승리)
- [ ] 승리 경로 Play 미확인 — 검증은 패배(디펜더 0기)로만 했다. 라우팅은 같은 관문이라
      구조상 성립하지만 눈으로는 못 봤다

확인: 2026-07-21

```
phase=Tally   HUD패널=보임
phase=Result  HUD패널=숨김
```

> 반복 Play 후 `Leak Detected : Persistent allocates ...` 경고가 콘솔에 뜬다. DOTS 월드를
> 여러 번 세우고 부순 흔적으로 보이며 이 단위의 변경과 연결점은 확인되지 않았다.
> 재현되면 별도 추적이 필요하다.
