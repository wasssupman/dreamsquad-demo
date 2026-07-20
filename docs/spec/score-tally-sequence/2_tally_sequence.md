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

## 인지 설계 (rev — 2026-07-21)

첫 구현은 마지막 적이 죽자마자 합산이 시작돼 **첫 축을 통째로 놓쳤다.** 원인은 시간이
아니라 위치다 — 시선은 보드 중앙(마지막 킬)에 있는데 점수는 우상단이다.

지연만 늘리는 대신 **인지 단계를 쪼갰다**:

| 구간 | 목적 |
|---|---|
| `preRollSec` 0.6 | 전투 화면 그대로. 마지막 킬을 눈으로 마무리 |
| `dimFadeSec` 0.4 | 딤 = "전투는 끝났다" 상태 전환 신호 |
| `postDimHoldSec` 0.3 | 화면이 바뀐 걸 인지할 쉼 |
| **`PulseAttention()`** | 값 없는 배지 펀치 — 시선을 우상단으로 끌어온다 |
| `labelLeadSec` 0.25 | **라벨이 먼저, 숫자는 나중.** 동시에 하면 어디를 볼지 모르는 사이 롤업이 끝난다 |

가장 큰 개선은 **라벨 선행**이다. 실측에서 라벨(2.16s) → 숫자(2.42s) 로 0.26초 리드가 생긴다.

총 4.5초(이전 3.2초). 늘어난 1.3초가 전부 인지에 쓰인다. 전부 인스펙터 값이라 조정이 싸다.

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
- [x] 라벨이 스트레스 배지에 안 가린다 (배지 하단 278 → `labelTopOffset` 296)
- [x] 딤이 전장을 덮고 HUD·라벨은 그 위에 뜬다 (스크린샷)
- [ ] 타이밍·딤 농도 체감 미확인

## 코드 리뷰 반영 (2026-07-21)

| 심각도 | 발견 | 조치 |
|---|---|---|
| 심각 | **판을 끝낸 마지막 킬의 연출이 통째로 사라짐.** 같은 Update 안에서 `DrainEnemyKilledEvents → CheckVictory → SetPhase(Tally)` 가 도는데, Tally 분기의 `_pendingKills = 0` 이 `LateUpdate` 게이트를 막았다. 하필 `preRollSec`("마지막 킬을 눈으로 마무리할 시간") 동안 그게 노출된다 | 그 줄 삭제. 실측 `_pendingKills` 보존 확인 |
| 중간 | **축 라벨이 스트레스 배지에 가림.** 배지 하단 278(= 36+20+148+10+64) 인데 `labelTopOffset` 250 이었고, Tally(5)가 ScoreHud(6) 아래라 배지가 위에 그려진다 | 296 으로. 코드·씬 동시 수정 |
| 중간 | order 5 에 `CostDisplay`·`DreamcatcherHandView`·`DraftView` 가 공존 — 동순위라 그리기 순서가 계층 의존 | `overrideSorting` 으로 독립 단위 확정 |
| 낮음 | `onDone` → `SetActive(false)` 순서 취약. 지금은 사이에 `yield` 가 없어 동작하지만 훗날 한 줄 끼면 결과 화면이 영영 안 뜬다 | 순서 교체 |
| 낮음 | `PulseAttention` 이 직전 킬의 버스트 창을 읽어 **값 변화 없이 플래시 오발** 가능(여유 0.15초뿐) | 펄스 전 창 비우기. 실측 억제 확인 |
| 낮음 | 라벨이 TMP 기본 폰트 — 옆 HUD 는 Kanit | `labelFont`/`labelMaterial` 추가 + HUD 와 동일 배선 |
| 낮음 | 씬 직렬화값이 코드 기본값과 불일치(`dimFadeSec` 0.25 vs 0.4 등) | 씬 재저장으로 동기화 |

**리뷰가 "확인됨"으로 판정한 것**: `onDone` 유실 경로 없음 · 스킵 시 총점 보존(계약 2) · Tally 도입으로 BGM/카메라 회귀 없음 · 씬 배선 정상 · `StopFeedbackTweens` 미호출은 의도대로.

**미조치 (제품 판단 필요)**: Tally 진입 즉시 전투 HUD(손패·코스트·다음웨이브)가 전부 사라져, `preRollSec` 의 "전투 화면 그대로"가 실제로는 "HUD 걷힌 화면"이다. 코드를 고칠지(SetPhase 를 preRoll 뒤로) 문서를 고칠지 결정 필요.

확인: 2026-07-21

```
t=0.0~0.6   전투 여운 (딤 없음)
t=0.6~1.0   딤 차오름 → 1.0~1.9 유지 + 배지 펄스
t=2.16      "시간  +11,500"      ← 라벨 먼저
t=2.42      HUD 6,400 → 17,900   ← 0.26초 뒤 숫자
t=3.36      "스트레스  +7,200"
t=3.62      HUD → 25,100
t=4.54      → onDone
```
