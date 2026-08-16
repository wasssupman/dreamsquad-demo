# 0 — 패배·강제 종료 축 은퇴

## 목적

**시스템이 판을 끝내는 경로를 전부 없앤다.** 이 unit 이 끝나면 판을 끝내는 것은 3분 만료
하나뿐이고, 유저 제출(unit 3)이 두 번째로 붙는다. 승/패라는 개념이 코드에서 사라진다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 판정 4개 제거, `EndMatch` 축소
- `Assets/_Project/Scripts/Core/MatchTally.cs` — `Won` 은퇴
- `Assets/_Project/Scripts/UI/ResultScreen.cs` — 승/패 두 입구 → 하나
- 테스트: `MatchTallyTests` · `EndlessScoreTests`(생성자 인자)

## 구현

**1. 판정 4개 제거**

| 제거 | 위치 | 남는 것 |
|---|---|---|
| 골 붕괴 즉시 패배 | `SyncGoalStability` 말미 `StressLimit <= 0` 분기 | 셀 열기(`OpenGoalCellAfterBreach`)만 |
| `CheckStressDefeat()` | 정의 + 호출 2곳(`DrainGoalEvents`·`LeakSiegingEnemy`) | 유출 카운터 `_goalReachedCount` 는 유지 |
| `CheckEnemyCoreDestroyed()` | 정의 + `Update` 호출 | `_enemyCoreCurrent` 미러는 유지(연출·로그) |
| `CheckVictory()` | 정의 + `Update` 호출 | `NoQueuedAttackersRemain()` 은 **유지** — 웨이브 케이던스 동력이다 |

`StressLimit` 분기가 사라지면 골 붕괴는 **로그 한 줄 + 셀 열기**로 끝난다. `EffectiveLeakLimit()`
은 HUD 만 쓰게 되지만 이 unit 에서 지우지 않는다(unit 2 의 배지 정리 몫).

**2. `CheckTimer` 에서 승패 비교 제거**

```
전: bool win = _goalStability >= _enemyCoreCurrent;
    EndMatch(win ? "victory_timeout" : "defeat_timeout", win);
후: EndMatch("complete");
```

만료 = 완주다. 두 마음의 체력을 비교하던 «버틴다» 계약(battle-structures 계약 15)은 여기서
끝난다 — 적 마음이 무엇을 하는지는 README 후속 후보.

**3. `Won` 은퇴**

`EndMatch(outcome, win)` → `EndMatch(outcome)`, `BuildTally(outcome, win)` → `BuildTally(outcome)`,
`MatchTally` 생성자에서 `won` 인자 제거. **승패를 담는 자리가 없어야** 나중에 조용히
되살아나지 않는다.

`outcome` 은 배틀 로그 라벨로 남긴다 — 이 unit 이후 값은 `"complete"` 하나이고,
unit 3 이 `"submitted"` 를 더한다.

**4. 결과 화면 입구 통합** (`Won` 제거의 컴파일 귀결)

`ShowVictory`/`ShowDefeat` → `Show(MatchTally)` 하나. 라벨은 `결과`, 색은 승리색(gold) 고정.
**줄 구성·문구 다듬기는 unit 4** 가 한다 — 여기서는 컴파일이 서게만 만든다.

**5. 옛 규칙을 단언하던 테스트 2개**

- `StructureSpawnAndBreachTests.SiegeCoreAlive_AllWavesCleared_DoesNotUseLegacyVictory`
  → **삭제**. 「살아 있는 적 마음이 구 전멸 승리를 억제한다」를 고정했는데 억제할 경로가
  사라졌다(주석으로 이유를 남긴다).
- `StructureLivePlayTest` (4) 「잔여 0 → 승리 축」 → **단언을 뒤집는다**: 적 마음을 부숴도
  `GamePhase.Result` 로 가지 않아야 한다. 붕괴 후 30프레임을 더 돌려 «한 프레임 늦은 종료»
  도 잡는다. 이게 계약을 지키는 실질 가드다.

## 주의

- **`GamePhase` 값을 건드리지 않는다.** `Tally` 는 그대로 둔다(전투 HUD 게이팅이 읽고,
  `CameraDirectionConfig.asset` 이 이 enum 을 정수로 직렬화한다).
- **엔드리스는 이제 스스로 끝나지 않는다.** `_timerDuration <= 0` 이라 만료가 없고 판정도
  전부 사라져, unit 3 의 제출이 붙기 전까지 종료 수단이 없다. 의도된 중간 상태다
  (모드의 정체 자체가 README 후속 후보).
- `EndlessModeSmokeTest` 는 `if (defeatSeen)` 조건부라 패배가 안 나도 깨지지 않는다(확인함).
  `sawStabilityDrain` 단언은 공성이 여전히 안정도를 깎으므로 유효하다.

## 완료 기준

- [x] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러 0
- [x] `EndMatch` 호출부가 **1곳**(`CheckTimer`)만 남는다
- [x] 코드베이스에 `Won`·`ShowVictory`·`ShowDefeat`·`CheckVictory`·`CheckStressDefeat`·
      `CheckEnemyCoreDestroyed`·`defeatColor` 참조가 0건
- [x] EditMode 19/19 — `MatchTallyTests`·`EndlessScoreTests`·`StructureSpawnAndBreachTests`.
      로그에 `골 붕괴 — 1개 셀 유출 전환. 판은 계속된다.` 확인
- [x] PlayMode `TallyFlowTest` 초록(7.7s) — 로그 마지막 줄 `COMPLETE — 3분 완주`
- [x] PlayMode `GoalStabilityTest` 초록(19.3s) — 마음이 무너진 뒤 5초를 더 돌려도 Result 가 아니다
- [x] PlayMode `StructureLivePlayTest.SiegeMap_DefendersBreakEnemyCore...` 초록 —
      적 마음을 부숴도 판이 안 끝난다
- [ ] **Play 육안 미확인**: 마음을 전부 내주고도 3분을 채우고 결과 화면이 뜬다

### 관측된 사전 실패 (이 unit 과 무관 — 고치지 않았다)

- `StructureLivePlayTest.Structures_BootOnDevMap_SpawnBlockAndSurviveConnectivity` —
  「배치 페이즈에 거점 프랍이 보여야」 0. **단독 실행에서도 동일하게 실패**해 순서/상태 누수가
  아님을 확인했다. 이 unit 의 diff 는 프랍 뷰·배치 진입에 접점이 없다(마감 축만 건드린다).
- EditMode `MultiGoalPoolSeparationTests` 4건(맵 복도 폭) · `WhirlpotAuthoringTests`
  (`MissingReferenceException`) — `gift-phase-removal` handoff 가 이미 기존 실패로 기록.
