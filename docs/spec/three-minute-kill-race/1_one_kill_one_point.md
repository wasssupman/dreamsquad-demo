# 1 — 1킬 = 1점 (killScore 티어 은퇴)

## 목적

**개체 1킬 = 1점, 예외 없음.** 점수를 티어로 가중하던 축(`killScore` 일반 1 / 엘리트 3 /
보스 10)을 통째로 없앤다. 점수와 처치 수가 같은 값이 되므로 두 축을 하나로 합친다.

사용자 결정 A(2026-08-16): **분열체(`Enemy_Slime_Mid`·`Enemy_Slime_Small`)도 1점**이다.
지금은 `killScore: 0` 이라 파생체가 점수를 안 주지만, 「예외 없음」이 규칙이다. 슬라임이
점수 효율 1위가 되는 것은 **웨이브 편성량**으로 조정할 문제이지 점수 산식의 예외가 아니다.

## 변경 대상

- **삭제** `Assets/_Project/Scripts/Battle/Units/KillScore.cs` (ECS 컴포넌트)
- `Battle/Units/EnemyKilledEvent.cs` — `killScore` 필드 제거
- `Battle/Units/DamageApplicationSystem.cs` — `KillScore` 룩업·스탬프 제거
- `Data/AttackUnitData.cs` — `killScore` 필드 제거
- `Bridge/BattleBridge.cs` — `_killScoreTotal` 삭제(`_killCount` 하나로), bake 제거,
  HUD·로거 가산 1 고정
- `Core/MatchTally.cs` — `KillScore`/`KillCount` → **`Kills` 한 축**
- `Core/PaceBaseline.cs` — `WaveKillScore`(가중 합) → `WaveKillCount`(마리 수 합)
- `UI/ScoreHudView.cs` — `OnEnemyKilled(int)` → `OnEnemyKilled()`
- `UI/ResultScreen.cs` — `Kills` 로 읽기(두 줄 통합은 unit 4)
- `Data/EnemyTier.cs` — `killScore` 를 근거로 들던 주석
- 테스트: `MatchTallyTests` · `EndlessScoreTests`
- `docs/reference/score-formula.md` — 티어 표 폐기
- `.claude/skills/enemy-wave-integration/SKILL.md` — 「분열체는 `killScore 0`」 규칙 갱신

## 구현

**1. 축을 지운다.** `KillScore` 컴포넌트와 `EnemyKilledEvent.killScore` 는 **실을 값이 없어서**
사라진다 — 이벤트 1건이 곧 1점이다. `AwakeningReward` 는 그대로 둔다(각성치는 여전히 적별로
다르다). 이 둘이 나란히 있던 대칭이 깨지는 것이 이 unit 의 요점이다.

**2. `_killScoreTotal` 삭제.** `_killCount` 와 항상 같은 값이 되므로 브리지의 누적은 하나다.
리셋 지점 3곳(`_battleClock` 이 0 이 되는 자리)은 `_killCount` 가 그대로 이어받는다.

**3. `MatchTally.Kills` 한 축.** `Total`·`SubmissionScore` 모두 `Kills` 를 가리킨다.
로그 스키마(`result.score` / `result.kill_score`)는 **건드리지 않는다** — 두 필드에 같은 값이
들어갈 뿐이고, `TallyFlowTest` 의 `total == kill_score` 단언이 그대로 유효하다.

**4. HUD 버스트 임계는 4를 유지한다.** 「잡몹 4기 ≈ 보스 절반」이라는 근거는 사라지지만,
1킬 1점에서 4는 **「4마리 동시 처치」** 라는 더 곧은 의미가 된다. 값이 그대로라 씬 에셋을
건드릴 필요도 없다(워크트리 공유 중 — 씬 편집은 비용이 크다).

**5. 적 SO 23종의 `killScore` 키는 그대로 둔다.** 직렬화 필드가 사라지면 Unity 가 다음 저장
때 알아서 흘린다. 23개를 지금 만지면 diff 만 커지고 병행 세션과 충돌 면적이 넓어진다.

## 완료 기준

- [x] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러 0
- [x] 코드베이스에 `killScore`·`KillScore`·`_killScoreTotal`·`WaveKillScore` **코드** 참조 0건
      (이력을 남긴 주석만 잔존)
- [x] EditMode 39/39 — `MatchTallyTests`·`EndlessScoreTests`·`PaceBaselineTests`·
      `SlimeSplitAuthoringTests`·`WaveKillBudgetPinTests`·`StructureSpawnAndBreachTests`
- [x] PlayMode `TallyFlowTest` 초록(6.8s)
- [ ] **Play 육안 미확인**: 보스를 잡아도 점수가 **1** 오른다. 슬라임을 끝까지 쪼개 잡으면 **원본+파생 전부**
      1점씩 들어온다
- [ ] **Play 육안 미확인**: 결과 화면의 「점수」와 「처치 N기」가 같은 수다(두 줄 통합은 unit 4)

### 티어 규칙을 단언하던 테스트 4개 (같은 unit 에서 이관)

- `WaveKillBudgetPinTests` — 「모든 유닛이 killScore > 0」·「보스 > 잡몹」 단언 삭제.
  **그 단언을 되살리면 그게 곧 티어 가중의 부활이다.** 예산 = 스폰 마리 수.
- `SlimeSplitAuthoringTests` — 「분열체 0점」 삭제(결정 A). 각성 0 은 그대로 유지 —
  처치 7회짜리 각성 농장 방지는 여전히 유효한 이유다.
- `PaceBaselineTests` — 가중치 존중 → 마릿수 합. 3웨이브를 탱커 10기(=30점)에서
  30기로 바꿔 기존 수치 단언(누적 60)을 그대로 살렸다.
- `WaveConceptAuthoringTests` — 분열체를 정규 풀에서 배제하는 **이유**를 갱신
  (「점수 없는 적」 → 「부모 없이 튀어나오는 파생물 + 보상 0」).
