# 3 — BattleBridge 웨이브 시드를 matchSeed 파생으로

## 목적

웨이브 시드 출처를 `deck.ResolveWaveSeed()`(고정 1) 에서 `MatchSeed.DeriveWaveSeed(_matchSeed)` 로 교체한다. 덱의 나머지 설정(풀, 웨이브 수, spacing)은 그대로 유지.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`TryInitializeGeneratedWaves`, 974 부근)
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` (명시 시드 오버로드)
- `Assets/_Project/Scripts/Data/AttackDeck.cs` (deprecated 주석)

## 구현

WavePatternGenerator — 명시 시드 오버로드 추가(기존 `Generate(deck)` 는 유지하되 시드만 외부 주입 가능하게):

```csharp
public static GeneratedWavePlan Generate(AttackDeck deck, int seedOverride)
{
    if (deck == null) throw new ArgumentNullException(nameof(deck));
    return Generate(
        seedOverride,                 // 기존: deck.ResolveWaveSeed()
        deck.waveGeneratorVersion,
        deck.timerDurationSec,
        deck.minWaveCount, deck.maxWaveCount,
        deck.minUnitsPerWave, deck.maxUnitsPerWave,
        deck.intraWaveSpacingSec,
        deck.ResolveAttackUnitPool());
}
```

- 기존 `Generate(deck)` 는 다른 호출처/테스트 호환 위해 남겨둔다(시드 = `deck.ResolveWaveSeed()`).
- 내부 `Generate(int seed, ...)` 의 `resolvedSeed = seed != 0 ? seed : 1` 폴백은 그대로 두어도 무방(파생 시드는 0 이 아님).

BattleBridge `TryInitializeGeneratedWaves` (현재 974):

```csharp
// 기존: _wavePlan = WavePatternGenerator.Generate(deck);
int waveSeed = Wassup.Core.MatchSeed.DeriveWaveSeed(_matchSeed != 0 ? _matchSeed : 1);
_wavePlan = WavePatternGenerator.Generate(deck, waveSeed);
```

AttackDeck:

```csharp
[System.Obsolete("라이브 웨이브 시드는 GameManager.matchSeed 에서 파생. waveSeed/ResolveWaveSeed 는 라이브 경로 미사용. 필드는 직렬화 호환 위해 유지.")]
public int ResolveWaveSeed() => ...; // 본문 유지, 라이브 호출처 제거
```

## 완료 기준

- [ ] compile green, 콘솔 에러 0.
- [ ] `debugFixedMatchSeed` 고정 후 두 번 Play → `_wavePlan.seed` 및 웨이브 구성(유닛/카운트) 동일.
- [ ] `debugFixedMatchSeed=0` 으로 두 번 Play → 웨이브 구성 서로 다름(고정 해소 — 이번 spec 핵심 관찰).
- [ ] 같은 matchSeed 의 맵 시드와 웨이브 시드가 서로 다른 값(decorrelation 로그/확인).
- [ ] 기존 `WavePatternGenerator.Generate(deck)` 호출처/테스트 영향 없음.
- [ ] EditMode 전부 통과(회귀 0).
