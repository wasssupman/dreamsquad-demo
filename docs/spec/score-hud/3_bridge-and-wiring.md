# 3 — 브리지·씬 (드레인 + wiring)

## 목적

`EnemyKilledEvent` 를 점수 HUD 증가로 연결한다. BattleBridge 스텁 드레인을 실 드레인으로 교체하고, BattleScene 에 ScoreHudView 를 wiring + Play 검증.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `scoreHud` 필드 + 스텁 드레인 교체
- BattleScene wiring (UnityMCP)

## 구현 (완료)

- 필드: `[SerializeField] private Wassup.UI.ScoreHudView scoreHud;` (단일 asmdef 라 참조 OK, BattleBridge 이미 `using Wassup.UI`).
- 드레인:
  ```csharp
  private void DrainEnemyKilledEvents()
  {
      if (!_enemyKilledEventQueue.IsCreated) return;
      if (scoreHud == null) { _enemyKilledEventQueue.Clear(); return; }
      while (_enemyKilledEventQueue.TryDequeue(out _))
          scoreHud.OnEnemyKilled();
  }
  ```
- 씬 wiring(execute_code): BattleScene 에 `ScoreHud` GameObject + `ScoreHudView` 추가 → `scoreFont`=Bangers SDF, `scoreMaterial`=DamageNumber Outline Mat, `BattleBridge.scoreHud`=hud. BattleScene 저장 후 OutgameScene 복원.

## 완료 기준

- ✅ compile: CS 에러/경고 0.
- Play (Squad): 전투 시작 시 상단 중앙 타이머 아래 `SCORE 0` 표시.
- 적 처치마다 점수가 +pointsPerKill 만큼 카운트업 롤 + 펀치 + 골드 플래시로 증가.
- 디펜더 사망/골 도달은 점수에 영향 없음.
- 전투 종료/다른 phase 에서 HUD 숨김, 재전투 시 0 리셋.
- console 에러 0.
- 사용자 Play 확인 후 일자 + 커밋 해시 기재.

✅ 2026-06-05 구현·wiring·compile 클린, 사용자 Play 확인 후 마무리. 커밋: 9549e55
