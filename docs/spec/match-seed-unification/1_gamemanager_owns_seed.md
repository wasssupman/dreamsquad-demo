# 1 — GameManager 가 matchSeed 소유·주입

## 목적

매치당 단일 matchSeed 를 GameManager 가 생성·보유하고, **맵을 빌드하는 `PrepareDraftMap()` 호출 이전에** BattleBridge 로 주입한다. Draft 경로와 Squad 경로 양쪽 모두 동일하게 적용.

## 변경 대상

- `Assets/_Project/Scripts/Core/GameManager.cs` (Start, StartSquadMatch)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`SetMatchSeed` API — 저장만, 소비는 작업 2/3)

## 구현

GameManager:

```csharp
[Header("Match Seed")]
[Tooltip("0 이면 매 판 새 시드. 0 이 아니면 재현용 고정 — 맵·웨이브가 매 판 동일.")]
[SerializeField] private int debugFixedMatchSeed = 0;

public int MatchSeed { get; private set; }

// Start() / StartSquadMatch() 진입 직후, PrepareDraftMap 보다 먼저 1회 호출.
private void EnsureMatchSeed()
{
    MatchSeed = debugFixedMatchSeed != 0
        ? debugFixedMatchSeed
        : Wassup.Core.MatchSeed.GenerateRandom();
    if (battleBridge != null) battleBridge.SetMatchSeed(MatchSeed);
    Debug.Log($"[GameManager] matchSeed={MatchSeed} (fixed={debugFixedMatchSeed != 0})");
}
```

- `Start()`: squad 분기 전 최상단에서 `EnsureMatchSeed()` 호출(두 경로 공통 보장). squad 경로도 `StartSquadMatch` 진입 시 이미 주입됨.
- `StartSquadMatch` 내부의 추가 `PrepareDraftMap()` 들도 주입 이후이므로 안전.
- 주입은 **저장만** — 실제 맵/웨이브 소비는 작업 2/3 에서. 이 단위만으로는 동작 변화 없음(주입값 미사용).

BattleBridge:

```csharp
private int _matchSeed; // 0 = 미설정(작업 2/3 에서 폴백 처리)
public void SetMatchSeed(int seed) => _matchSeed = seed;
```

## 완료 기준

- [ ] compile green, 콘솔 에러 0.
- [ ] Play 시 콘솔에 `[GameManager] matchSeed=...` 1회 출력(Draft·Squad 경로 모두).
- [ ] `debugFixedMatchSeed` 고정값으로 두 번 Play → 로그의 matchSeed 동일.
- [ ] 이 단위 단독으로는 맵/웨이브 결과 불변(주입값 아직 미소비) — 회귀 0.
