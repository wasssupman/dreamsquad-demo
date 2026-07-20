# 2 — 합산 연출

## 목적

우상단 HUD 점수(= 킬점수)에 **시간 → 스트레스**를 순차로 더하고, 끝나면 결과 화면으로 넘긴다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/ScoreTallyView.cs`
- 수정 `Assets/_Project/Scripts/UI/ScoreHudView.cs` — `AddScore` / `RollSettled` 공개
- 수정 `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `scoreTallyView` 배선 + 호출
- 수정 `Assets/_Project/Scenes/BattleScene.unity` — 뷰 GameObject + 배선

## 구현

### 롤업을 새로 만들지 않는다

`ScoreHudView` 가 이미 `_shownScore → _targetScore` 보간(`rollLerp`)·펀치·플래시를 갖고 있다.
연출은 **값만 밀어준다**:

```csharp
public void AddScore(int points)   // 처치가 아닌 가산
public bool RollSettled            // 표시 숫자가 목표에 붙었는가
```

`AddScore` 는 처치와 **똑같은 경로**를 탄다(펀치·플래시·버스트 판정). 큰 값이 한 번에
들어오면 버스트 임계(300)를 넘겨 가장자리 플래시가 터지는데, 합산 순간의 타격감으로 알맞다.

### 정렬 순서 5

배틀(0) 위, `ScoreHudView`(6) 아래. 딤이 전장을 덮되 **점수는 그 위에 뜬다.**
딤은 0.55 — 결과 화면의 `UiOverlay.Dim`(0.92)보다 옅게 해서 전투 화면이 비친다.

### 시간은 전부 unscaled

전투 종료 시 `_running=false` 가 ECS `BattleRunning` 으로 흘러 **시뮬이 이미 멈춘다**
(`PushBattleRunningToEcs`). 별도 시간 제어가 필요 없고, 연출은 그와 무관하게 흘러야 한다.

### 0점 축은 건너뛴다

패배 시 시간·스트레스가 0이라 "0을 굴리는" 민망한 장면이 남는다. 그 경우 딤만 스치고
곧장 결과로 간다 — 실측 0.78초(딤 0.25 + 여운 0.5).

### 스킵

딤 자체가 버튼이라 **어디를 눌러도** 넘어간다. 재시작을 자주 하는 게임이라 필수다.

### onDone 은 반드시 호출된다

스킵·0점 축·중단 어느 경로로 끝나도 콜백이 나간다. 여기서 끊기면 **결과 화면이 영영 안 뜬다.**
같은 이유로 `BattleBridge` 는 `scoreTallyView` 미배선 시 즉시 `FinishTally` 로 넘어간다 —
연출은 곁가지, 결과 화면은 필수다.

## 완료 기준

- [x] compile 통과, `read_console` 클린
- [x] EditMode 전체 통과 (1125 / 0 실패)
- [x] 씬 배선 (`ScoreTallyView` GameObject + `BattleBridge.scoreTallyView`)
- [x] 합산 시퀀스 실측 — 킬 6,400 → `시간 +11,500` → `스트레스 +7,200` → **25,100**, `onDone` 도달
- [x] 패배(0점 축) 경로 실측 — 두 축 모두 건너뛰고 0.78초에 종료
- [ ] **실전 승리 경로 미확인** — 밸런스상 승리 유도가 어려워 `Play()` 직접 호출로 검증했다.
      `BeginTally` → `Play` 배선은 패배 경로로 확인됐으므로 조합은 성립하지만, 승리로 끝나는
      실제 판을 끝까지 본 적은 없다
- [ ] 스킵(탭) 미확인 — 코드 경로는 있으나 실제 입력으로 눌러보지 않았다
- [ ] 타이밍·딤 농도 체감 미확인

확인: 2026-07-21

```
t=1.37s   HUD 6,400                      딤 0.55
t=1.40s   HUD 17,900   "시간  +11,500"
t=2.33s   HUD 25,100   "스트레스  +7,200"
t=3.25s   HUD 25,100   → onDone
```
