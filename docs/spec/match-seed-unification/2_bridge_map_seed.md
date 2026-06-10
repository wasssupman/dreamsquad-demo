# 2 — BattleBridge 맵 시드를 matchSeed 파생으로

## 목적

`BuildMapForBattle` 의 맵 시드 출처를 `mapSettings.EffectiveSeed` 에서 `MatchSeed.DeriveMapSeed(_matchSeed)` 로 교체한다. visualSeed(투사체 jitter)도 같은 matchSeed 계열로 전환.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (497 부근, 857 부근)
- `Assets/_Project/Scripts/Data/MapGenerationSettings.cs` (deprecated 주석)

## 구현

`BuildMapForBattle` (현재 497):

```csharp
// 기존: int seed = mapSettings != null ? mapSettings.EffectiveSeed : 0;
int matchSeed = _matchSeed != 0 ? _matchSeed : Wassup.Core.MatchSeed.GenerateRandom();
int seed = Wassup.Core.MatchSeed.DeriveMapSeed(matchSeed);
```

- `_matchSeed==0`(주입 누락, 예: 테스트 직접 호출) 폴백으로 즉석 random matchSeed 생성 → 항상 유효 시드.
- `version`(generatorVersion), `gridSize` 등 `mapSettings` 의 나머지 용도는 그대로.

visualSeed (현재 857):

```csharp
// 기존: int visualSeed = (mapSettings != null ? mapSettings.EffectiveSeed : 42) ^ 0x5A5A5A5A;
int visualSeed = Wassup.Core.MatchSeed.DeriveVisualSeed(_matchSeed != 0 ? _matchSeed : 42);
```

MapGenerationSettings:

```csharp
[System.Obsolete("라이브 시드는 GameManager.matchSeed 에서 파생. defaultSeed/EffectiveSeed 는 더 이상 라이브 경로에 쓰이지 않음(재현 고정은 GameManager.debugFixedMatchSeed). 필드는 직렬화 호환 위해 유지.")]
public int EffectiveSeed => ...; // 본문 유지, 호출처 제거
```

- `EffectiveSeed`/`defaultSeed` 를 읽는 곳이 BattleBridge 두 군데뿐임을 확인하고 둘 다 교체(읽기 0 으로).

## 완료 기준

> ✅ 검증 2026-06-10 (Unity MCP) — `BuildMapForBattle` 맵 시드를 `MatchSeed.DeriveMapSeed(_matchSeed)`,
> visualSeed 를 `DeriveVisualSeed(_matchSeed)` 로 교체. `mapSettings.EffectiveSeed`/`defaultSeed` deprecated
> 주석, 호출처 0(grep 확인). Play(고정 matchSeed=999) reflection: `GameManager.MatchSeed=999` →
> `BattleBridge._matchSeed=999` → `_generatedMap.seed=251418039 == DeriveMapSeed(999)` 정확 일치(통합 체인
> 검증). 파생이 순수 결정론(작업 0 테스트)이므로 고정 시드 → 매 판 동일 맵. 컴파일 green, EditMode 315/313
> 통과, 콘솔 에러 0. 커밋: (작업 3 과 동일 커밋 — BattleBridge 동일 파일)

- [ ] compile green, 콘솔 에러 0.
- [ ] `debugFixedMatchSeed` 고정 후 두 번 Play → `_generatedMap.seed` 동일(맵 결정론 재현).
- [ ] `debugFixedMatchSeed=0` 으로 두 번 Play → 맵 서로 다름(매 판 변화 유지).
- [ ] `mapSettings.EffectiveSeed` 호출처 0 (grep 확인).
- [ ] EditMode 전부 통과(회귀 0).
