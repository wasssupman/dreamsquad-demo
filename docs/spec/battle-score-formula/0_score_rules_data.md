# 0 — 점수 규칙 SO + 유닛 킬 가치

## 목적

점수 산식이 쓸 상수를 데이터로 확정한다. 제약 6(하드코딩 금지)에 따라 초당점수·점당점수는
ScriptableObject 에서, 킬 가치는 각 적 유닛 정의에서 나온다. 이 단위는 **데이터만** 만든다 —
아직 아무도 읽지 않는다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/ScoreRulesData.cs`
- 신규 에셋 `Assets/_Project/Data/Config/ScoreRules.asset`
- 수정 `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `killScore` 필드 추가
- 수정 `Assets/_Project/Data/Enemies/*.asset` (10종) — `killScore` 값 기입

## 구현

### ScoreRulesData

`Assets/_Project/Scripts/Data/Config/` 이 아니라 `Data/` 직하에 둔다 (기존 `AttackDeck`, `WavePlanAsset` 과 같은 계층).
`[CreateAssetMenu]` 를 달고 에셋은 `Assets/_Project/Data/Config/ScoreRules.asset` 로 만든다
(`BattleConfig.asset`, `DefaultCostConfig.asset` 과 같은 폴더).

```csharp
public int timeScorePerSecond = 100;   // 남은 1초당
public int stressScorePerPoint = 900;  // 남은 스트레스 1점당
```

두 필드에 `[Tooltip]` 으로 예산 총량을 적어둔다 — 튜닝하는 사람이 총점 규모 변화를 즉시 알 수 있게.
`timeScorePerSecond` 는 11,930 을 넘으면 int 오버플로가 나므로 `[Range(1, 10000)]` 로 막는다.

### AttackUnitData.killScore

```csharp
[Tooltip("이 적을 처치했을 때 얻는 점수. 유출당하면 얻지 못한다.")]
public int killScore = 100;
```

기본값 100 이라 잡몹 9종은 에셋 수정 없이 기본값으로 커버된다.
**보스만 2000 으로 올린다** — `Assets/_Project/Data/Enemies/Enemy_Boss_Nightmare.asset`.

티어 enum 을 만들지 않는다 (계약 5). 잡몹/정예/보스는 이 필드의 **값 구간**으로만 표현된다.

## 완료 기준

- [ ] compile 통과. `refresh_unity` 후 `read_console` 에 에러 없음
- [ ] `ScoreRules.asset` 이 Project 창에 보이고 인스펙터에서 두 값이 편집 가능
- [ ] `Enemy_Boss_Nightmare.asset` 의 `killScore` = 2000, 나머지 9종 = 100
- [ ] 기존 EditMode 테스트 전부 통과 (필드 추가라 회귀가 없어야 정상)
- [ ] 어떤 코드도 아직 이 값들을 읽지 않는다 (다음 단위에서 연결)

> 유닛별 값을 갈라 쓰면 킬 예산이 마리수가 아니라 **타입 분포**에 의존하게 된다.
> README 의 10,300 은 "잡몹 전부 100" 을 전제한 값이므로, 잡몹 값을 갈라 쓰려면 README 예산 표를 함께 고쳐야 한다.
