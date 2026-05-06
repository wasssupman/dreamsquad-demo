# 1 — Draft Rule Engine

## 목적

`DraftController`에 슬롯 배열 필드를 추가하고, `DraftSession`의 풀 구성 방식을 슬롯 기반(3+2+1+4=10)으로 전환한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/DraftController.cs`
- `Assets/_Project/Scripts/Core/DraftSession.cs`
- `Assets/_Project/Tests/PlayMode/DraftFlowSmokeTest.cs` (reflection 키 변경)

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

기존 `Reset(catalog, poolSize, maxDiscards, seed)` 오버로드를 유지하되, **`_slotMap.Clear()`를 반드시 추가**한다. 새 오버로드를 추가한다.

```csharp
// 슬롯 추적 (기존 _pool, _discarded, _picked 아래에 추가)
private readonly Dictionary<DefenderUnitData, DraftSlotType> _slotMap = new();

public DraftSlotType GetSlotType(DefenderUnitData unit) =>
    unit != null && _slotMap.TryGetValue(unit, out var t) ? t : DraftSlotType.Collection;

// 기존 Reset — _slotMap.Clear() 한 줄 추가
public void Reset(IReadOnlyList<DefenderUnitData> catalog, int poolSize, int maxDiscards, int seed)
{
    // ... 기존 코드 유지 ...
    _discarded.Clear();
    _pool.Clear();
    _slotMap.Clear();   // ← 추가: 이전 슬롯 정보 정리
    // ... shuffle + pool build 기존 로직 ...
}

// 신규 오버로드
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

    foreach (var u in basicUnits) AddToPool(u, DraftSlotType.Basic);
    foreach (var u in metaUnits)  AddToPool(u, DraftSlotType.Meta);
    AddToPool(egoUnit, DraftSlotType.Ego);

    // collection 후보 — 이미 슬롯에 배정된 유닛 제외
    var candidates = new List<DefenderUnitData>();
    foreach (var u in collectionPool)
        if (u != null && !_slotMap.ContainsKey(u)) candidates.Add(u);

    // 후보 부족 시 오류 — 10장 계약 미달 방지
    if (candidates.Count < collectionCount)
        Debug.LogError($"[DraftSession] collectionPool 후보 부족: {candidates.Count} < {collectionCount}. 10장 계약 미달.");

    var rng = new System.Random(seed);
    for (int i = 0; i < collectionCount && candidates.Count > 0; i++)
    {
        int j = rng.Next(candidates.Count);
        AddToPool(candidates[j], DraftSlotType.Collection);
        candidates.RemoveAt(j);
    }

    // 10장 총합 검증
    int expected = basicUnits.Count + metaUnits.Count + 1 + collectionCount;
    if (_pool.Count != expected)
        Debug.LogError($"[DraftSession] 풀 {_pool.Count}장 ≠ 기대 {expected}장. fixed slot 중복 여부 확인.");
}

private void AddToPool(DefenderUnitData unit, DraftSlotType slot)
{
    if (unit == null) return;
    // fixed slot 중복 방지 — 동일 유닛이 basic/meta/ego에 두 번 들어오면 두 번째는 무시
    if (_slotMap.ContainsKey(unit))
    {
        Debug.LogError($"[DraftSession] fixed slot 중복: {unit.displayName}. Inspector 배열을 확인하세요.");
        return;
    }
    _pool.Add(unit);
    _slotMap[unit] = slot;
}
```

### DraftFlowSmokeTest.cs 수정

`catalog` reflection을 슬롯 필드로 교체한다. 유닛 수는 기존 10개 그대로 유지.

```csharp
// 제거
// var catalogField = typeof(DraftController).GetField("catalog", ...);
// catalogField?.SetValue(_controller, _catalog.ToArray());

// 교체 — 10개 유닛을 슬롯별로 분배
var bDeck = new DefenderUnitData[] { _catalog[0], _catalog[1], _catalog[2] };
var mDeck = new DefenderUnitData[] { _catalog[3], _catalog[4] };
var ego   = _catalog[5];
var cPool = new DefenderUnitData[] { _catalog[6], _catalog[7], _catalog[8], _catalog[9] };

SetField(_controller, "basicDeck",      bDeck);
SetField(_controller, "metaDeck",       mDeck);
SetField(_controller, "egoUnit",        ego);
SetField(_controller, "collectionPool", cPool);

static void SetField(object obj, string name, object val)
{
    var f = obj.GetType().GetField(name,
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    f?.SetValue(obj, val);
}
```

테스트 assertion은 변경 없음: pool 10장, discard 3, picked 7, TryConfirm 성공.

## 완료 기준

- [ ] 컴파일 오류 없음
- [ ] DraftSession 기존 EditMode 테스트 전체 통과 (구 Reset 오버로드 + _slotMap.Clear 추가 후)
- [ ] 신규 EditMode 테스트 통과:
  - fixed slot 중복 유닛 → pool에 1회만 추가되고 LogError 발생
  - collectionPool 후보 부족 (후보 2개, collectionCount 4) → LogError 발생, pool < 10
  - legacy Reset 호출 후 `GetSlotType(unit)` → `Collection` 반환 (슬롯 맵 초기화 확인)
  - 정상 입력 → pool 정확히 10장, 각 유닛 GetSlotType 올바름
- [ ] DraftFlowSmokeTest (PlayMode) 통과: 10장 pool, 3 discard, 7 picked, TryConfirm 성공
- [ ] 콘솔: `[DraftController]` / `[DraftSession]` 오류 없음
