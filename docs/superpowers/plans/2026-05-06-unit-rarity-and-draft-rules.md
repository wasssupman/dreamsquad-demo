# Unit Rarity & Draft Rules Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 유닛에 등급(Common/Rare/Epic/Ego)을 추가하고, 드래프트 풀을 슬롯 기반(Basic×3 + Meta×2 + Ego×1 + Collection×4 = 10)으로 전환하며, 카드에 2-layer 시각 데코레이션과 등급별 VFX를 추가한다.

**Architecture:** `DefenderRarity`/`DraftSlotType` enum → `DraftSession` 슬롯 기반 Reset 오버로드 → `DraftController` 슬롯 필드 교체 → `DraftCardFanView` 2-layer 시각 → `DraftCardVfxDriver` PrimeTween Yoyo + 파티클 → UnityMCP SO 배정 + Inspector 배선.

**Tech Stack:** Unity 6 (6000.4.3f1) / C# / URP 17.4 / PrimeTween / UnityMCP MCP tools / NUnit EditMode tests

**Spec 참조:** `docs/spec/unit-rarity-and-draft-rules/` (0~4 파일)

---

## File Map

| 상태 | 파일 | 역할 |
|---|---|---|
| 신규 | `Assets/_Project/Scripts/Data/DefenderRarity.cs` | Rarity enum |
| 신규 | `Assets/_Project/Scripts/Data/DraftSlotType.cs` | SlotType enum |
| 신규 | `Assets/_Project/Scripts/UI/Draft/DraftCardVfxDriver.cs` | 카드 VFX 드라이버 |
| 수정 | `Assets/_Project/Scripts/Data/DefenderUnitData.cs` | `rarity` 필드 추가 |
| 수정 | `Assets/_Project/Scripts/Core/DraftSession.cs` | 슬롯 기반 Reset 오버로드 + `_slotMap` |
| 수정 | `Assets/_Project/Scripts/Core/DraftController.cs` | `catalog`/`poolSize` → 슬롯 필드 교체 |
| 수정 | `Assets/_Project/Scripts/UI/Draft/DraftCardFanView.cs` | 2-layer 카드 시각 + VFX driver 부착 |
| 수정 | `Assets/_Project/Scripts/UI/Draft/DraftView.cs` | `Build()` 호출부 session 파라미터 추가 |
| 수정 | `Assets/_Project/Tests/EditMode/DraftSessionTests.cs` | 슬롯 기반 Reset 테스트 추가 |
| 수정 | `Assets/_Project/Tests/EditMode/DraftControllerMapRebuildTests.cs` | 슬롯 필드로 SetUp 수정 |
| 수정 | `Assets/_Project/Data/Defenders/Defender_*.asset` (15종) | rarity 값 배정 |

---

### Task 1: DefenderRarity + DraftSlotType enums + DefenderUnitData.rarity

**Files:**
- Create: `Assets/_Project/Scripts/Data/DefenderRarity.cs`
- Create: `Assets/_Project/Scripts/Data/DraftSlotType.cs`
- Modify: `Assets/_Project/Scripts/Data/DefenderUnitData.cs`

- [ ] **Step 1: DefenderRarity.cs 생성**

```csharp
// Assets/_Project/Scripts/Data/DefenderRarity.cs
namespace Wassup.Data
{
    public enum DefenderRarity { Common, Rare, Epic, Ego }
}
```

- [ ] **Step 2: DraftSlotType.cs 생성**

```csharp
// Assets/_Project/Scripts/Data/DraftSlotType.cs
namespace Wassup.Data
{
    public enum DraftSlotType { Basic, Meta, Collection, Ego }
}
```

- [ ] **Step 3: DefenderUnitData.cs에 rarity 필드 추가**

`[Header("Deployment Presentation")]` 바로 위에 삽입:

```csharp
[Header("Rarity")]
public DefenderRarity rarity = DefenderRarity.Common;
```

- [ ] **Step 4: 컴파일 확인**

UnityMCP `read_console` 호출 → 오류 없음 확인. 기존 defender SO 15종이 Editor에서 정상 로드되는지 `manage_asset` 으로 Scout SO 하나 열어 `rarity` 필드 존재 확인.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Data/DefenderRarity.cs \
        Assets/_Project/Scripts/Data/DefenderRarity.cs.meta \
        Assets/_Project/Scripts/Data/DraftSlotType.cs \
        Assets/_Project/Scripts/Data/DraftSlotType.cs.meta \
        Assets/_Project/Scripts/Data/DefenderUnitData.cs
git commit -m "feat(rarity): DefenderRarity + DraftSlotType enums, rarity field on DefenderUnitData"
```

---

### Task 2: DraftSession — 슬롯 기반 Reset 오버로드 + 슬롯 추적

**Files:**
- Modify: `Assets/_Project/Scripts/Core/DraftSession.cs`
- Modify: `Assets/_Project/Tests/EditMode/DraftSessionTests.cs`

**중요:** 기존 `Reset(catalog, poolSize, maxDiscards, seed)` 오버로드를 **절대 제거하지 않는다**. 기존 테스트 6개가 이 시그니처를 사용 중.

- [ ] **Step 1: 실패하는 테스트 작성**

`DraftSessionTests.cs` 클래스 맨 끝(닫는 `}` 앞)에 테스트 3개 추가:

```csharp
// ── Slot-based Reset tests ─────────────────────────────────────────

[Test]
public void SlotReset_Builds10CardPool()
{
    var basic = MakeUnits(3, "B");
    var meta  = MakeUnits(2, "M");
    var ego   = MakeUnit("Ego");
    var col   = MakeUnits(8, "C");
    var s = new DraftSession();

    s.Reset(basic, meta, ego, col, collectionCount: 4, maxDiscards: 3, seed: 7);

    Assert.AreEqual(10, s.PoolSize);
    DestroyAll(basic); DestroyAll(meta);
    Object.DestroyImmediate(ego); DestroyAll(col);
}

[Test]
public void SlotReset_GetSlotType_ReturnsCorrectSlot()
{
    var basic = MakeUnits(3, "B");
    var meta  = MakeUnits(2, "M");
    var ego   = MakeUnit("Ego");
    var col   = MakeUnits(8, "C");
    var s = new DraftSession();

    s.Reset(basic, meta, ego, col, collectionCount: 4, maxDiscards: 3, seed: 7);

    Assert.AreEqual(DraftSlotType.Basic, s.GetSlotType(basic[0]));
    Assert.AreEqual(DraftSlotType.Meta,  s.GetSlotType(meta[0]));
    Assert.AreEqual(DraftSlotType.Ego,   s.GetSlotType(ego));
    DestroyAll(basic); DestroyAll(meta);
    Object.DestroyImmediate(ego); DestroyAll(col);
}

[Test]
public void SlotReset_CollectionExcludesAlreadySlottedUnits()
{
    var basic = MakeUnits(3, "B");
    var meta  = MakeUnits(2, "M");
    var ego   = MakeUnit("Ego");
    // collectionPool contains basic/meta/ego as well — they must be skipped
    var col   = new List<DefenderUnitData>(basic);
    col.AddRange(meta); col.Add(ego);
    var extras = MakeUnits(6, "C");
    col.AddRange(extras);
    var s = new DraftSession();

    s.Reset(basic, meta, ego, col, collectionCount: 4, maxDiscards: 3, seed: 7);

    Assert.AreEqual(10, s.PoolSize);
    // basic[0] slot must remain Basic (not overwritten by collection pass)
    Assert.AreEqual(DraftSlotType.Basic, s.GetSlotType(basic[0]));
    DestroyAll(basic); DestroyAll(meta);
    Object.DestroyImmediate(ego); DestroyAll(extras);
}

// ── helpers ──────────────────────────────────────────────────────

private static DefenderUnitData MakeUnit(string name)
{
    var u = ScriptableObject.CreateInstance<DefenderUnitData>();
    u.displayName = name;
    return u;
}

private static DefenderUnitData[] MakeUnits(int count, string prefix)
{
    var arr = new DefenderUnitData[count];
    for (int i = 0; i < count; i++) arr[i] = MakeUnit($"{prefix}_{i}");
    return arr;
}

private static void DestroyAll(IEnumerable<DefenderUnitData> units)
{
    foreach (var u in units) if (u != null) Object.DestroyImmediate(u);
}
```

- [ ] **Step 2: 테스트 실패 확인**

`mcp__UnityMCP__run_tests` 실행 (filter: `DraftSessionTests`). 신규 3개 테스트가 컴파일 오류 또는 NotImplemented로 실패 확인.

- [ ] **Step 3: DraftSession.cs에 슬롯 기반 Reset 구현**

`DraftSession` 클래스 필드 및 메서드 추가 (기존 코드는 건드리지 않음):

```csharp
// 기존 필드들 아래에 추가
private readonly Dictionary<DefenderUnitData, DraftSlotType> _slotMap = new();

public DraftSlotType GetSlotType(DefenderUnitData unit) =>
    unit != null && _slotMap.TryGetValue(unit, out var t) ? t : DraftSlotType.Collection;

// 신규 오버로드 — 기존 Reset은 수정하지 않음
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

    foreach (var u in basicUnits) AddSlot(u, DraftSlotType.Basic);
    foreach (var u in metaUnits)  AddSlot(u, DraftSlotType.Meta);
    AddSlot(egoUnit, DraftSlotType.Ego);

    var candidates = new List<DefenderUnitData>();
    foreach (var u in collectionPool)
        if (u != null && !_slotMap.ContainsKey(u)) candidates.Add(u);

    var rng = new System.Random(seed);
    for (int i = 0; i < collectionCount && candidates.Count > 0; i++)
    {
        int j = rng.Next(candidates.Count);
        AddSlot(candidates[j], DraftSlotType.Collection);
        candidates.RemoveAt(j);
    }
}

private void AddSlot(DefenderUnitData unit, DraftSlotType slot)
{
    if (unit == null) return;
    _pool.Add(unit);
    _slotMap[unit] = slot;
}
```

파일 상단 `using` 에 `System.Collections.Generic` 이미 있는지 확인. 없으면 추가.

- [ ] **Step 4: 테스트 통과 확인**

`mcp__UnityMCP__run_tests` (filter: `DraftSessionTests`). 기존 6개 + 신규 3개 = 9개 전부 통과 확인.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Core/DraftSession.cs \
        Assets/_Project/Tests/EditMode/DraftSessionTests.cs
git commit -m "feat(draft): slot-based Reset overload + _slotMap tracking in DraftSession"
```

---

### Task 3: DraftController — 슬롯 필드 교체 + 영향 받는 테스트 수정

**Files:**
- Modify: `Assets/_Project/Scripts/Core/DraftController.cs`
- Modify: `Assets/_Project/Tests/EditMode/DraftControllerMapRebuildTests.cs`

**주의:** `DraftControllerMapRebuildTests`는 reflection으로 `"catalog"`, `"poolSize"` 필드명을 문자열로 참조. 필드명 변경 시 반드시 같이 수정.

- [ ] **Step 1: DraftController.cs 슬롯 필드 교체**

`[SerializeField] private DefenderUnitData[] catalog;` 와 `[SerializeField] private int poolSize = 10;` 를 아래로 교체:

```csharp
[SerializeField] private DefenderUnitData[] basicDeck;
[SerializeField] private DefenderUnitData[] metaDeck;
[SerializeField] private DefenderUnitData   egoUnit;
[SerializeField] private DefenderUnitData[] collectionPool;
```

`PoolSize` 프로퍼티와 `Catalog` 프로퍼티 교체:

```csharp
// 제거: public IReadOnlyList<DefenderUnitData> Catalog => catalog;
// 제거: public int PoolSize => poolSize;

public int PoolSize => 10; // 3 basic + 2 meta + 1 ego + 4 collection
public IReadOnlyList<DefenderUnitData> CollectionPool => collectionPool;
```

`BeginDraft(int seed)` 내부 `_session.Reset(...)` 호출 교체:

```csharp
// 기존
_session.Reset(catalog, poolSize, discardCount, seed);

// 교체
if (!ValidateSlots()) return;
_session.Reset(basicDeck, metaDeck, egoUnit, collectionPool,
               collectionCount: 4, maxDiscards: discardCount, seed: seed);
```

`ValidateSlots()` 메서드 추가 (`BeginDraft` 아래):

```csharp
private bool ValidateSlots()
{
    if (basicDeck == null || basicDeck.Length != 3)
    { Debug.LogError("[DraftController] basicDeck must have exactly 3 entries.", this); return false; }
    if (metaDeck == null || metaDeck.Length != 2)
    { Debug.LogError("[DraftController] metaDeck must have exactly 2 entries.", this); return false; }
    if (egoUnit == null)
    { Debug.LogError("[DraftController] egoUnit is not assigned.", this); return false; }
    if (collectionPool == null || collectionPool.Length < 4)
    { Debug.LogError("[DraftController] collectionPool needs at least 4 entries.", this); return false; }
    return true;
}
```

- [ ] **Step 2: DraftControllerMapRebuildTests.cs SetUp 수정**

`SetUp()` 에서 `_catalog = new DefenderUnitData[5]` 블록을 10개 유닛으로 확장하고 슬롯 필드로 배선:

```csharp
// 기존 catalog 5개 생성 블록 전체를 아래로 교체
_catalog = new DefenderUnitData[10];
for (int i = 0; i < _catalog.Length; i++)
{
    _catalog[i] = ScriptableObject.CreateInstance<DefenderUnitData>();
    _catalog[i].displayName = $"TestUnit_{i}";
}

var basicArr      = new[] { _catalog[0], _catalog[1], _catalog[2] };
var metaArr       = new[] { _catalog[3], _catalog[4] };
var egoArr        = _catalog[5];
var collectionArr = new[] { _catalog[6], _catalog[7], _catalog[8], _catalog[9] };
```

`SetPrivateField` 호출부 교체:

```csharp
// 제거
SetPrivateField(_controller, "catalog",  _catalog);
SetPrivateField(_controller, "poolSize", 5);

// 추가
SetPrivateField(_controller, "basicDeck",      basicArr);
SetPrivateField(_controller, "metaDeck",       metaArr);
SetPrivateField(_controller, "egoUnit",        egoArr);
SetPrivateField(_controller, "collectionPool", collectionArr);
```

`discardCount` 설정은 `2` 유지 (기존 그대로).

- [ ] **Step 3: 컴파일 + 테스트 확인**

`mcp__UnityMCP__read_console` → 오류 없음 확인.  
`mcp__UnityMCP__run_tests` (filter: `DraftControllerMapRebuildTests`) → 3개 테스트 통과.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/Core/DraftController.cs \
        Assets/_Project/Tests/EditMode/DraftControllerMapRebuildTests.cs
git commit -m "feat(draft): replace catalog/poolSize with slot fields in DraftController"
```

---

### Task 4: DraftCardFanView — 2-layer 카드 시각 데코레이션

**Files:**
- Modify: `Assets/_Project/Scripts/UI/Draft/DraftCardFanView.cs`
- Modify: `Assets/_Project/Scripts/UI/Draft/DraftView.cs`

- [ ] **Step 1: DraftCardFanView.cs 색상 헬퍼 추가**

클래스 내부 (private 상수 아래)에 추가:

```csharp
private static Color RarityBorderColor(DefenderRarity r) => r switch
{
    DefenderRarity.Common => new Color(0.55f, 0.55f, 0.55f),
    DefenderRarity.Rare   => new Color(0.35f, 0.61f, 0.97f),
    DefenderRarity.Epic   => new Color(1.00f, 0.55f, 0.26f),
    DefenderRarity.Ego    => new Color(0.80f, 0.27f, 1.00f),
    _                     => Color.white,
};

private static Color SlotBannerColor(DraftSlotType t) => t switch
{
    DraftSlotType.Basic      => new Color(0.29f, 0.48f, 0.78f),
    DraftSlotType.Meta       => new Color(0.79f, 0.64f, 0.15f),
    DraftSlotType.Collection => new Color(0.18f, 0.62f, 0.38f),
    DraftSlotType.Ego        => new Color(0.61f, 0.18f, 0.96f),
    _                        => Color.gray,
};

private static string SlotLabel(DraftSlotType t) => t switch
{
    DraftSlotType.Basic      => "BASIC",
    DraftSlotType.Meta       => "META",
    DraftSlotType.Collection => "COLLECT",
    DraftSlotType.Ego        => "EGO",
    _                        => "",
};
```

- [ ] **Step 2: Build() 시그니처 변경 + CreateCard() 시그니처 변경**

```csharp
// 변경 전
public void Build(IReadOnlyList<DefenderUnitData> pool)
private DraftCardView CreateCard(DefenderUnitData unit, int index)

// 변경 후
public void Build(IReadOnlyList<DefenderUnitData> pool, DraftSession session)
private DraftCardView CreateCard(DefenderUnitData unit, int index, DraftSession session)
```

`Build()` 내부 루프에서 `CreateCard(unit, i)` → `CreateCard(unit, i, session)` 로 수정.

- [ ] **Step 3: CreateCard() 레이아웃 변경**

기존 `go.GetComponent<Image>().color = new Color(0.18f, ...)` 한 줄을 아래로 교체:

```csharp
// 카드 외곽 Image = 등급 테두리 색
go.GetComponent<Image>().color = RarityBorderColor(unit.rarity);

// Inner background (테두리 두께 4px 확보)
var innerBg = new GameObject("InnerBg", typeof(RectTransform), typeof(Image));
innerBg.transform.SetParent(go.transform, false);
var innerRt = (RectTransform)innerBg.transform;
innerRt.anchorMin = Vector2.zero;
innerRt.anchorMax = Vector2.one;
innerRt.offsetMin = new Vector2(4f, 4f);
innerRt.offsetMax = new Vector2(-4f, -4f);
var innerImg = innerBg.GetComponent<Image>();
innerImg.color = new Color(0.11f, 0.11f, 0.14f, 1f);
innerImg.raycastTarget = false;
innerBg.transform.SetSiblingIndex(0);
```

기존 Swatch 생성 블록에서 `swatch.color = ...` 줄을 아래로 교체:

```csharp
var slotType = session != null ? session.GetSlotType(unit) : DraftSlotType.Collection;
swatch.color = SlotBannerColor(slotType);

// 배너 레이블
var bannerLabelGo = new GameObject("BannerLabel", typeof(RectTransform));
bannerLabelGo.transform.SetParent(swatchGo.transform, false);
var blRt = (RectTransform)bannerLabelGo.transform;
blRt.anchorMin = Vector2.zero; blRt.anchorMax = Vector2.one;
blRt.offsetMin = Vector2.zero; blRt.offsetMax = Vector2.zero;
var blTmp = bannerLabelGo.AddComponent<TextMeshProUGUI>();
blTmp.text = SlotLabel(slotType);
blTmp.fontSize = 16;
blTmp.color = slotType == DraftSlotType.Meta ? Color.black : Color.white;
blTmp.alignment = TextAlignmentOptions.Center;
blTmp.fontStyle = FontStyles.Bold;
blTmp.raycastTarget = false;
```

- [ ] **Step 4: DraftView.cs 호출부 수정**

`fan.Build(controller.Session.Pool)` → `fan.Build(controller.Session.Pool, controller.Session)`

- [ ] **Step 5: 컴파일 + PlayMode 확인**

`mcp__UnityMCP__read_console` → 오류 없음.  
Play 모드 진입 → 드래프트 화면에서 카드 10장 표시 → 각 카드의 테두리 색(등급)과 상단 배너(슬롯)가 README 표와 일치하는지 `manage_screenshot` 또는 눈으로 확인.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/Draft/DraftCardFanView.cs \
        Assets/_Project/Scripts/UI/Draft/DraftView.cs
git commit -m "feat(draft-ui): 2-layer card decoration (rarity border + slot banner)"
```

---

### Task 5: DraftCardVfxDriver — 등급별 PrimeTween + 파티클 VFX

**Files:**
- Create: `Assets/_Project/Scripts/UI/Draft/DraftCardVfxDriver.cs`
- Modify: `Assets/_Project/Scripts/UI/Draft/DraftCardFanView.cs`

- [ ] **Step 1: DraftCardVfxDriver.cs 생성**

```csharp
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI.Draft
{
    // Attached at runtime by DraftCardFanView.CreateCard().
    // Common/Rare: border color Yoyo pulse via PrimeTween.
    // Epic:  pulse + ember ParticleSystem child.
    // Ego:   pulse + ParticleSystem + banner shimmer.
    public class DraftCardVfxDriver : MonoBehaviour
    {
        private Tween _borderTween;
        private Tween _bannerTween;

        public void Configure(
            DefenderRarity rarity,
            Image borderImage,
            Image bannerImage,
            ParticleSystem epicParticlePrefab,
            ParticleSystem egoParticlePrefab)
        {
            Color borderBase   = borderImage.color;
            Color borderBright = Color.Lerp(borderBase, Color.white, 0.35f);

            float halfCycle = rarity switch
            {
                DefenderRarity.Common => 1.5f,
                DefenderRarity.Rare   => 1.0f,
                DefenderRarity.Epic   => 0.6f,
                DefenderRarity.Ego    => 0.45f,
                _                    => 1.5f,
            };

            _borderTween = Tween.Color(borderImage, borderBright, halfCycle,
                Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);

            if (rarity == DefenderRarity.Epic && epicParticlePrefab != null)
                SpawnParticle(epicParticlePrefab);

            if (rarity == DefenderRarity.Ego)
            {
                if (egoParticlePrefab != null) SpawnParticle(egoParticlePrefab);
                if (bannerImage != null)
                {
                    Color bannerBase   = bannerImage.color;
                    Color bannerBright = Color.Lerp(bannerBase, Color.white, 0.4f);
                    _bannerTween = Tween.Color(bannerImage, bannerBright, halfCycle,
                        Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);
                }
            }
        }

        private void SpawnParticle(ParticleSystem prefab)
        {
            var ps = Instantiate(prefab, transform);
            ps.transform.localPosition = Vector3.zero;
            ps.Play();
        }

        private void OnDisable() { _borderTween.Stop(); _bannerTween.Stop(); }
        private void OnDestroy() { _borderTween.Stop(); _bannerTween.Stop(); }
    }
}
```

- [ ] **Step 2: DraftCardFanView에 파티클 프리팹 SerializeField 추가**

기존 필드 선언 블록에 추가:

```csharp
[SerializeField] private ParticleSystem epicCardParticlePrefab;
[SerializeField] private ParticleSystem egoCardParticlePrefab;
```

- [ ] **Step 3: CreateCard() 마지막에 VFX driver 부착**

`return view;` 바로 위에 삽입:

```csharp
// VFX driver — borderImage = go.GetComponent<Image>(), bannerImage = swatch
var driver = go.AddComponent<DraftCardVfxDriver>();
driver.Configure(unit.rarity, go.GetComponent<Image>(), swatch,
                 epicCardParticlePrefab, egoCardParticlePrefab);
```

- [ ] **Step 4: 컴파일 확인**

`mcp__UnityMCP__read_console` → 오류 없음. 파티클 프리팹 없이도 graceful fallback 동작 확인 (null 체크 있음).

- [ ] **Step 5: 파티클 프리팹 생성 (null 방어)**

파티클 프리팹이 없어도 VFX 없이 pulse만 동작한다. 프리팹 authoring은 별도 작업.  
지금은 `DraftCardFanView` Inspector의 `epicCardParticlePrefab`, `egoCardParticlePrefab` 을 비워둔 상태로 진행.

- [ ] **Step 6: PlayMode 확인**

Play 모드 → 드래프트 카드 표시 → Common 카드 테두리가 천천히 pulse, Rare는 더 빠르게, Epic은 더 빠르게 pulse 확인. 카드 버리기 후 콘솔 오류 없음.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/UI/Draft/DraftCardVfxDriver.cs \
        Assets/_Project/Scripts/UI/Draft/DraftCardVfxDriver.cs.meta \
        Assets/_Project/Scripts/UI/Draft/DraftCardFanView.cs
git commit -m "feat(draft-vfx): DraftCardVfxDriver — rarity-tiered PrimeTween pulse + particle support"
```

---

### Task 6: SO rarity 배정 + DraftController Inspector 배선

**Files:**
- Modify: `Assets/_Project/Data/Defenders/Defender_*.asset` (15종)
- BattleScene DraftController Inspector

**배정표:**

| SO | rarity 값 |
|---|---|
| Defender_Scout | Common |
| Defender_Guardian | Common |
| Defender_Cannon | Common |
| Defender_Ranger | Common |
| Defender_Piercer | Common |
| Defender_Marksman | Common |
| Defender_Archer | Rare |
| Defender_Bastion | Rare |
| Defender_Healer | Rare |
| Defender_Sniper | Rare |
| Defender_FireCaster | Epic |
| Defender_IceCaster | Epic |
| Defender_PoisonCaster | Epic |
| Defender_BlockingCaster | Epic |
| Defender_Bruiser | Ego |

- [ ] **Step 1: SO rarity 일괄 배정**

UnityMCP `execute_code` 로 일괄 배정:

```csharp
// UnityMCP execute_code 내용
using UnityEditor;
using Wassup.Data;

var assignments = new (string path, DefenderRarity rarity)[]
{
    ("Assets/_Project/Data/Defenders/Defender_Scout.asset",         DefenderRarity.Common),
    ("Assets/_Project/Data/Defenders/Defender_Guardian.asset",      DefenderRarity.Common),
    ("Assets/_Project/Data/Defenders/Defender_Cannon.asset",        DefenderRarity.Common),
    ("Assets/_Project/Data/Defenders/Defender_Ranger.asset",        DefenderRarity.Common),
    ("Assets/_Project/Data/Defenders/Defender_Piercer.asset",       DefenderRarity.Common),
    ("Assets/_Project/Data/Defenders/Defender_Marksman.asset",      DefenderRarity.Common),
    ("Assets/_Project/Data/Defenders/Defender_Archer.asset",        DefenderRarity.Rare),
    ("Assets/_Project/Data/Defenders/Defender_Bastion.asset",       DefenderRarity.Rare),
    ("Assets/_Project/Data/Defenders/Defender_Healer.asset",        DefenderRarity.Rare),
    ("Assets/_Project/Data/Defenders/Defender_Sniper.asset",        DefenderRarity.Rare),
    ("Assets/_Project/Data/Defenders/Defender_FireCaster.asset",    DefenderRarity.Epic),
    ("Assets/_Project/Data/Defenders/Defender_IceCaster.asset",     DefenderRarity.Epic),
    ("Assets/_Project/Data/Defenders/Defender_PoisonCaster.asset",  DefenderRarity.Epic),
    ("Assets/_Project/Data/Defenders/Defender_BlockingCaster.asset",DefenderRarity.Epic),
    ("Assets/_Project/Data/Defenders/Defender_Bruiser.asset",       DefenderRarity.Ego),
};

foreach (var (path, rarity) in assignments)
{
    var so = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(path);
    if (so == null) { Debug.LogError($"Not found: {path}"); continue; }
    so.rarity = rarity;
    EditorUtility.SetDirty(so);
}
AssetDatabase.SaveAssets();
Debug.Log("Rarity assignment complete.");
```

- [ ] **Step 2: SO 저장 확인**

`mcp__UnityMCP__read_console` → "Rarity assignment complete." 로그 확인. 오류 없음.

- [ ] **Step 3: DraftController Inspector 배선**

BattleScene 열기 → DraftController GameObject 선택 → Inspector에서:

| 필드 | 배정 |
|---|---|
| `basicDeck` (크기 3) | Scout, Guardian, Cannon |
| `metaDeck` (크기 2) | Sniper, Archer |
| `egoUnit` | Bruiser |
| `collectionPool` (크기 9) | Ranger, Piercer, Marksman, Bastion, Healer, FireCaster, IceCaster, PoisonCaster, BlockingCaster |

UnityMCP `manage_components` 또는 `execute_code` 로 자동 배선 가능:

```csharp
// execute_code — DraftController 슬롯 배선
using UnityEditor;
using UnityEditor.SceneManagement;
using Wassup.Core;
using Wassup.Data;

var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/BattleScene.unity");
var controller = Object.FindFirstObjectByType<DraftController>();
if (controller == null) { Debug.LogError("DraftController not found"); return; }

DefenderUnitData Load(string name) =>
    AssetDatabase.LoadAssetAtPath<DefenderUnitData>($"Assets/_Project/Data/Defenders/Defender_{name}.asset");

var so = new UnityEditor.SerializedObject(controller);
// basicDeck
var basicProp = so.FindProperty("basicDeck");
basicProp.arraySize = 3;
basicProp.GetArrayElementAtIndex(0).objectReferenceValue = Load("Scout");
basicProp.GetArrayElementAtIndex(1).objectReferenceValue = Load("Guardian");
basicProp.GetArrayElementAtIndex(2).objectReferenceValue = Load("Cannon");
// metaDeck
var metaProp = so.FindProperty("metaDeck");
metaProp.arraySize = 2;
metaProp.GetArrayElementAtIndex(0).objectReferenceValue = Load("Sniper");
metaProp.GetArrayElementAtIndex(1).objectReferenceValue = Load("Archer");
// egoUnit
so.FindProperty("egoUnit").objectReferenceValue = Load("Bruiser");
// collectionPool
string[] colNames = { "Ranger","Piercer","Marksman","Bastion","Healer",
                      "FireCaster","IceCaster","PoisonCaster","BlockingCaster" };
var colProp = so.FindProperty("collectionPool");
colProp.arraySize = colNames.Length;
for (int i = 0; i < colNames.Length; i++)
    colProp.GetArrayElementAtIndex(i).objectReferenceValue = Load(colNames[i]);

so.ApplyModifiedProperties();
EditorSceneManager.SaveScene(scene);
Debug.Log("DraftController slots wired.");
```

- [ ] **Step 4: PlayMode 전체 통합 확인**

Play 모드 진입 → 드래프트 시작 → 콘솔 확인:
- `[DraftController]` 오류 없음
- Pool 10장 구성: Basic×3(Scout/Guardian/Cannon), Meta×2(Sniper/Archer), Ego×1(Bruiser), Collection×4
- 테두리: Scout/Guardian/Cannon = 회색(Common), Sniper/Archer = 파랑(Rare), Bruiser = 보라(Ego)
- 배너: Basic 3장 = 파란 "BASIC", Meta 2장 = 골드 "META", Ego 1장 = 보라 "EGO", Collection 4장 = 초록 "COLLECT"
- 카드 3장 버리기 → 전투 진입 정상

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Data/Defenders/ \
        Assets/_Project/Scenes/BattleScene.unity
git commit -m "feat(rarity): assign rarity to 15 defender SOs + wire DraftController slots in BattleScene"
```

---

## 완료 기준 요약

- [ ] Task 1~6 모든 커밋 완료
- [ ] 전체 EditMode 테스트 통과 (`DraftSessionTests` 9개, `DraftControllerMapRebuildTests` 3개)
- [ ] PlayMode: 드래프트 10장 풀 = Basic×3 + Meta×2 + Ego×1 + Collection×4
- [ ] PlayMode: 카드 테두리 색상 = 등급 색상 일치
- [ ] PlayMode: 배너 레이블/색상 = 슬롯 타입 일치
- [ ] PlayMode: 카드 pulse VFX 동작 (Common~Ego 속도 차이 확인)
- [ ] PlayMode: 3장 버리기 → 전투 정상 진입
- [ ] 콘솔 오류 없음
