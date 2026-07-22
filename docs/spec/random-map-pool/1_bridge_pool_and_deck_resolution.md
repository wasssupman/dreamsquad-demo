# 1. BattleBridge — 풀에서 (맵, 덱) 인코운터 resolve

## 목적

`BuildMapForBattle` 이 단일 `mapDocument` 를 소비하던 것을, **풀에서 seed 로 (맵, 덱) 쌍을 한 번 resolve** 해 소비하도록 바꾼다. 맵과 덱이 같은 인덱스로 잠겨야 "맵마다 그 맵의 적 패턴"이 성립한다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

- 새 필드 `[SerializeField] private MapDocumentPool mapPool;`. 기존 `mapDocument`/`deck` 필드는 **레거시 폴백**으로 유지(풀 비면 현행 동작).
- `BuildMapForBattle` 에서 `seed` 계산 직후 인코운터 resolve(선택 인덱스는 맵 seed 와 동일 로컬 `seed` 사용 → `fixedMapSeed` 가 인덱스도 핀):
  ```
  MapDocument activeDoc = mapDocument;   // 폴백
  AttackDeck  activeDeck = deck;         // 폴백
  if (mapPool != null && mapPool.Count > 0) {
      var e = mapPool.Get(MapPoolSelect.SelectIndex(seed, mapPool.Count));
      if (MapGridBattleAdapter.IsUsableDocument(e.document)) {
          activeDoc = e.document;
          if (e.deck != null) activeDeck = e.deck;
      }
  }
  _resolvedDeck = activeDeck;
  ```
- MapGrid 분기: `MapGridBattleAdapter.Build(seed, mapGridSettings, activeDoc, _mapGridGridSizeOverride)`.
- guard: `validatorBacked = mapSource == MapSource.MapGrid && !MapGridBattleAdapter.IsUsableDocument(activeDoc);`
- **deck 소비 라우팅**: `private AttackDeck _resolvedDeck;` + `public AttackDeck ActiveDeck => _resolvedDeck != null ? _resolvedDeck : deck;`. 기존 `deck.` 소비 지점(스폰 큐잉·`_timerDuration`·`GoalReachedLimit`·stress limit·wave `Generate`·logger deckId 등 ~10곳, `BattleBridge.cs` 1091/1148/1155~1174/1453/1564~1574/3857/4018)을 `ActiveDeck` 로 교체. **필드 선언·폴백 표현식은 제외**. `ActiveDeck` 은 브리핑 스트립이 읽도록 public.
- 웨이브 스폰 분배 코드는 **무변경** — 이미 `_generatedMap.spawns.Length`(선택된 맵의 스폰 수) 로 `EffectiveSpawnIndex`/`FirstSpawnTimesPerLane` 호출.

## 완료 기준

- [ ] compile 0 errors
- [ ] 기존 EditMode green (deck 라우팅 회귀 없음)
- [ ] Play: `debugFixedMatchSeed` 를 인덱스 0·1 로 매핑되는 두 값으로 각각 실행 → 각기 다른 맵 + 다른 덱 로드. 콘솔 `Battle started with generated deck '{id}'` 의 deckId 가 선택 맵과 일치
- [ ] 풀 미배선(null) 시 기존 단일 `mapDocument`+`deck` 그대로 동작(폴백 무회귀)
