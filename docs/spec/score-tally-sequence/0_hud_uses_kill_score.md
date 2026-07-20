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

### 마일스톤 재조정 (필수)

`milestoneInterval` 이 **100 이라 잡몹 하나 잡을 때마다 플래시가 터진다.** 축척이 15배 커지면
연출이 무의미해진다.

**100 → 2,000.** 근거 두 가지:

- 매치당 플래시 횟수가 기존 감각과 비슷해진다 (740/100 ≈ 7회 → 11,200/2,000 ≈ 6회)
- **보스 killScore 가 정확히 2,000** 이라 보스 처치 = 마일스톤 1개가 보장된다. 의도한 정렬이다

> `pointsPerKill` / `milestoneInterval` 은 **씬에 직렬화돼 있다**(`BattleScene.unity:2413, 2451`).
> 코드 기본값만 고치면 안 먹는다 — 씬 값도 같이 바꿔야 한다.

## 완료 기준

- [ ] compile 통과, `read_console` 클린
- [ ] EditMode 전체 통과
- [ ] Play: 잡몹 처치 시 HUD 가 **+100**, 보스 처치 시 **+2,000** 오른다
- [ ] Play: 전투 종료 시 HUD 점수 == `ScoreMath` 의 킬축 값
- [ ] Play: 마일스톤 플래시가 잡몹마다 터지지 않는다 (2,000 단위)
- [ ] 배틀로그 `score_events[]` 의 `enemy_killed` 값이 유닛별 실제 킬점수
