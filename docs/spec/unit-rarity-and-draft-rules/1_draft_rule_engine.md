# 1 — Draft Rule Engine

## 목적

`DraftController`에 슬롯 배열 필드를 추가하고, `DraftSession`의 풀 구성 방식을 슬롯 기반(3+2+1+4=10)으로 전환한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/DraftController.cs`
- `Assets/_Project/Scripts/Core/DraftSession.cs`

## 구현

### DraftController.cs

기존 `catalog` 배열과 `poolSize` 필드를 **슬롯 필드로 교체**한다.

```csharp
// 제거
// [SerializeField] private DefenderUnitData[] catalog;
// [SerializeField] private int poolSize = 10;

// 추가
[SerializeField] private DefenderUnitData[] basicDeck;        // 3장 고정 Common
[SerializeField] private DefenderUnitData[] metaDeck;         // 2장 로테이션
[SerializeField] private DefenderUnitData   egoUnit;          // 1장 고정 Ego
[SerializeField] private DefenderUnitData[] collectionPool;   // 랜덤 후보 전체
```

`BeginDraft(int seed)` 변경:

```csharp
public void BeginDraft(int seed)
{
    if (!ValidateSlots()) return;
    _session.Reset(basicDeck, metaDeck, egoUnit, collectionPool,
                   collectionCount: 4, maxDiscards: discardCount, seed: seed);
    // 기존 skill loadout roll 로직 유지
    ...
    DraftStarted?.Invoke();
}

private bool ValidateSlots()
{
    if (basicDeck == null || basicDeck.Length != 3) { Debug.LogError("[DraftController] basicDeck must have 3 entries."); return false; }
    if (metaDeck == null || metaDeck.Length != 2)   { Debug.LogError("[DraftController] metaDeck must have 2 entries.");  return false; }
    if (egoUnit == null)                             { Debug.LogError("[DraftController] egoUnit is not assigned.");        return false; }
    if (collectionPool == null || collectionPool.Length < 4) { Debug.LogError("[DraftController] collectionPool needs 4+ entries."); return false; }
    return true;
}
```

`Catalog` 프로퍼티 대신 `CollectionPool` 프로퍼티로 교체 (외부 참조 확인 필요):

```csharp
public IReadOnlyList<DefenderUnitData> CollectionPool => collectionPool;
```

### DraftSession.cs

기존 `Reset(catalog, poolSize, maxDiscards, seed)` 오버로드를 유지하고 새 오버로드 추가.  
(기존 EditMode 테스트가 구 시그니처를 사용하므로 제거하지 않는다.)

```csharp
// 슬롯 추적
private readonly Dictionary<DefenderUnitData, DraftSlotType> _slotMap = new();

public DraftSlotType GetSlotType(DefenderUnitData unit) =>
    unit != null && _slotMap.TryGetValue(unit, out var t) ? t : DraftSlotType.Collection;

public void Reset(
    IReadOnlyList<DefenderUnitData> basicUnits,
    IReadOnlyList<DefenderUnitData> metaUnits,
    DefenderUnitData egoUnit,
    IReadOnlyList<DefenderUnitData> collectionPool,
    int collectionCount,
    int maxDiscards,
    int seed)
{
    Seed = seed;
    MaxDiscards = maxDiscards;
    _discarded.Clear();
    _pool.Clear();
    _slotMap.Clear();

    // Basic 3
    foreach (var u in basicUnits) AddToPool(u, DraftSlotType.Basic);
    // Meta 2
    foreach (var u in metaUnits) AddToPool(u, DraftSlotType.Meta);
    // Ego 1
    AddToPool(egoUnit, DraftSlotType.Ego);

    // Collection — seed 기반 랜덤, 이미 풀에 있는 유닛 제외
    var candidates = new List<DefenderUnitData>();
    foreach (var u in collectionPool)
        if (u != null && !_slotMap.ContainsKey(u)) candidates.Add(u);

    var rng = new System.Random(seed);
    for (int i = 0; i < collectionCount && candidates.Count > 0; i++)
    {
        int j = rng.Next(candidates.Count);
        AddToPool(candidates[j], DraftSlotType.Collection);
        candidates.RemoveAt(j);
    }
}

private void AddToPool(DefenderUnitData unit, DraftSlotType slot)
{
    if (unit == null) return;
    _pool.Add(unit);
    _slotMap[unit] = slot;
}
```

## 완료 기준

- [ ] 컴파일 오류 없음
- [ ] 기존 DraftSession EditMode 테스트 통과 (구 Reset 오버로드 유지이므로 변경 없이)
- [ ] PlayMode: DraftController 슬롯 배열 배선 후 `BeginDraft()` 호출 → 풀 10장 구성 확인
- [ ] 콘솔: `[DraftController]` 오류 없음
