# 0 — HUD 점수를 킬점수로 통일

## 목적

전투 중 우상단 HUD 점수를 **최종 점수의 킬축과 같은 값**으로 만든다.
연출(unit 2)이 이 숫자에서 이어서 합산하므로, 축척이 다르면 합산이 성립하지 않는다.

```
현재  HUD 740   ← 처치 74기 × 10
      킬점수 11,200 ← 잡몹 72×100 + 보스 2×2,000     15배 차이

이후  HUD = 킬점수 = 11,200
```

## 변경 대상

- 수정 `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- 수정 `Assets/_Project/Scripts/UI/ScoreHudView.cs`
- 수정 `Assets/_Project/Scenes/BattleScene.unity` (직렬화된 튜닝값)

## 구현

### 값 교체

`DrainEnemyKilledEvents` 가 HUD·로그에 넘기는 값을 고정 10 에서 **이벤트가 실어온 `killScore`** 로 바꾼다.
`EnemyKilledEvent.killScore` 는 이미 `battle-score-formula` unit 2 에서 스폰 베이크로 채워지고 있다 —
새 배관은 필요 없다.

```csharp
scoreHud?.OnEnemyKilled(evt.killScore);
logger?.AddScoreEvent("enemy_killed", evt.killScore, time);
```

`EnemyKillScoreDelta` 상수는 삭제한다. 이 값을 쓰는 곳이 사라진다.

### 죽은 코드 정리

`ScoreHudView.OnEnemyKilled()` (무인자)와 그것만 쓰던 `pointsPerKill` 필드를 삭제한다.
호출처가 없다 — Bridge 는 int 오버로드만 쓴다.

### 가장자리 플래시 — 누계 → 순간 화력 (필수)

원래는 `milestoneInterval` 누계 N 점마다 터졌다. 축척이 15배 커지면서 두 번 흔들렸다:

1. 간격 100 그대로 → **잡몹 하나마다** 터짐
2. 간격 2,000 으로 키움 → **보스에서만** 터져 거의 안 보임

누계 기준 자체가 킬점수 축척과 안 맞는다. **순간 화력 기준으로 바꾼다** — 최근
`burstWindowSec`(1초) 안에 번 점수가 `burstScoreThreshold`(300) 이상이면 터진다.
잡몹 3기 동시처치나 보스 1기가 같은 무게로 터진다.

**쿨다운이 필요하다.** "1초에 300점" 조건만 그대로 두면 몰아치는 동안 매 프레임 참이라
60fps 로 터진다. 한 번 터지면 플래시 지속시간(`milestoneDuration` 0.55초)만큼 재무장을
막아 지속 사격 중 초당 ~2회로 정리한다.

기록은 `OnEnemyKilled` 유입 시점, 판정은 프레임당 1회 flush(`TriggerHit`)에서 한다 —
AoE 동시처치가 한 번의 판정으로 합쳐진다.

> `milestoneInterval` 은 삭제되고 `burstWindowSec` / `burstScoreThreshold` 로 대체된다.
> 셋 다 **씬에 직렬화**되므로(`BattleScene.unity`) 코드만 고치면 안 먹는다.

## 완료 기준

- [x] compile 통과, `read_console` 클린
- [x] EditMode 전체 통과 (1125 / 0 실패)
- [x] Play: 잡몹 처치 시 HUD 가 **+100** (2,400 샘플 전부 `HUD == _killScoreTotal`, 불일치 0)
- [x] 플래시 발화 조건 격리 검증 — 잡몹 2기(200) 안 터짐 / 3기(300) 터짐 / 보스 1기 터짐 /
      1초 밖 만료분 안 터짐 / 터진 직후 쿨다운 억제
- [ ] **보스 처치 +2,000 은 미확인** — 5웨이브째 등장이라 검증 구간에 안 나왔다. 같은 채널이라
      구조상 성립하지만 눈으로는 못 봤다
- [ ] 배틀로그 `score_events[]` 값 확인 미실시

확인: 2026-07-21
