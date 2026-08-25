# 2b — 진영 상대화

## 목적

**「누구든 쓸 수 있다」를 막고 있는 실제 벽.** arm 이 진영을 리터럴로 알고 있어서,
트리거 화이트리스트를 여는 순간 자기 진영을 때린다.

`Battle/Combat/DcTrigger.cs:100~106` 이 그 위험을 직접 적어놨다 —
*"누가 이 줄을 완화하면 브리지의 파열 드레인(`CollectShieldBreakTargets` — 대상 풀이
`AttackUnitTag` 하드코딩)이 돌아 **보스의 파열 폭발이 자기 진영을 때린다**."*

즉 화이트리스트는 증상이고 원인은 arm 이다. **동작 무변경 리팩터다** — 오늘 라이브 경로는
전부 결과가 같다.

## 변경 대상

진영 리터럴 **56개** (`AttackUnitTag` 29 + `DefenderUnitTag` 27):

| 파일 | 적 | 방 |
|---|---|---|
| `Scripts/Bridge/BattleBridge.cs` | 16 | 11 |
| `Battle/Combat/BossPeriodicTriggerSystem.cs` | 7 | 5 |
| `Battle/Units/DamageApplicationSystem.cs` | 4 | 2 |
| `Scripts/Bridge/BattleBridge.Dreamcatcher.cs` | 1 | 3 |
| `Battle/Combat/AttackSystem.cs` | 1 | 3 |
| `Battle/Combat/HealthThresholdSystem.cs` | 0 | 3 |

## 구현

1. **`Opponents(caster)` / `Allies(caster)` 를 만든다.** 현재 프로젝트에 이 헬퍼가 **하나도 없다**.
   축은 **유닛 태그**다 — `FactionTag` 은 거점을 포함하는데 거점은 CC·실드 버퍼가 없어
   예외가 된다(`skill-fire-dispatch` 계약 6 계승).
2. **파일 단위로 쪼개 커밋한다.** 56곳을 한 커밋에 넣으면 회귀가 났을 때 어느 쪽인지 못 가른다.
   `unit 2a`(핸들)와도 분리한다 — 성격이 다른 두 변경이 한 커밋에 있으면 그물이 빨개졌을 때
   원인을 못 가른다.
3. **`CollectShieldBreakTargets` 를 반드시 포함한다.** 이게 `DcTrigger.cs` 주석이 지목한
   그 드레인이다. unit 0 의 56곳 전수표에 들어 있는지 먼저 확인한다.
4. **화이트리스트는 이 unit 에서 건드리지 않는다.** 철거는 `skill-layer-migration` 에서,
   그것도 **해당 가족의 이전이 끝난 뒤**에 한다. 지금 열면 legacy enum 경로인 payload 와
   개방된 조합이 공존하는 창이 생긴다.
5. **핀 테스트를 갱신한다** — `Tests/EditMode/DcTriggerTests.cs` · `DcTriggerArmedTests.cs` 가
   현행 술어를 고정하고 있다.

## 완료 기준

- [ ] 리터럴 56곳이 `Opponents/Allies(caster)` 로 대체됐고 남은 리터럴이 0이다 (grep 확인)
- [ ] **파일 단위 커밋**으로 분리됐다 (한 커밋에 56곳 금지)
- [ ] `CollectShieldBreakTargets` 가 포함됐다
- [ ] unit 1 이 깐 그물 전건 초록 — 오늘 라이브 경로의 결과가 바뀌지 않았다
- [ ] 화이트리스트 2술어(`EnemyTriggerArmed`·`DefenderTriggerArmed`)는 **그대로**다
- [ ] EditMode 코어 lane + Assets lane 초록
