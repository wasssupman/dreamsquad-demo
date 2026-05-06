# 4 — SO Rarity 배정 + Inspector 배선

## 목적

15종 defender SO에 `rarity` 필드를 배정하고, `DraftController` Inspector에 `basicDeck`, `metaDeck`, `egoUnit`, `collectionPool`을 배선한다.

## 변경 대상

- `Assets/_Project/Data/Defenders/Defender_*.asset` (15종)
- BattleScene 의 `DraftController` GameObject Inspector

## Rarity 배정표

| SO 파일 | 등급 |
|---|---|
| Defender_Scout.asset | Common |
| Defender_Guardian.asset | Common |
| Defender_Cannon.asset | Common |
| Defender_Ranger.asset | Common |
| Defender_Piercer.asset | Common |
| Defender_Marksman.asset | Common |
| Defender_Archer.asset | Rare |
| Defender_Bastion.asset | Rare |
| Defender_Healer.asset | Rare |
| Defender_Sniper.asset | Rare |
| Defender_FireCaster.asset | Epic |
| Defender_IceCaster.asset | Epic |
| Defender_PoisonCaster.asset | Epic |
| Defender_BlockingCaster.asset | Epic |
| Defender_Bruiser.asset | Ego |

UnityMCP `execute_code` 로 일괄 배정한다. **method body 실행 환경이므로 top-level `using` 없이 완전한 형식명 사용.**

```csharp
// tuple deconstruction 없이 병렬 배열로 작성
string[] paths = new string[]
{
    "Assets/_Project/Data/Defenders/Defender_Scout.asset",
    "Assets/_Project/Data/Defenders/Defender_Guardian.asset",
    "Assets/_Project/Data/Defenders/Defender_Cannon.asset",
    "Assets/_Project/Data/Defenders/Defender_Ranger.asset",
    "Assets/_Project/Data/Defenders/Defender_Piercer.asset",
    "Assets/_Project/Data/Defenders/Defender_Marksman.asset",
    "Assets/_Project/Data/Defenders/Defender_Archer.asset",
    "Assets/_Project/Data/Defenders/Defender_Bastion.asset",
    "Assets/_Project/Data/Defenders/Defender_Healer.asset",
    "Assets/_Project/Data/Defenders/Defender_Sniper.asset",
    "Assets/_Project/Data/Defenders/Defender_FireCaster.asset",
    "Assets/_Project/Data/Defenders/Defender_IceCaster.asset",
    "Assets/_Project/Data/Defenders/Defender_PoisonCaster.asset",
    "Assets/_Project/Data/Defenders/Defender_BlockingCaster.asset",
    "Assets/_Project/Data/Defenders/Defender_Bruiser.asset",
};
Wassup.Data.DefenderRarity[] rarities = new Wassup.Data.DefenderRarity[]
{
    Wassup.Data.DefenderRarity.Common,
    Wassup.Data.DefenderRarity.Common,
    Wassup.Data.DefenderRarity.Common,
    Wassup.Data.DefenderRarity.Common,
    Wassup.Data.DefenderRarity.Common,
    Wassup.Data.DefenderRarity.Common,
    Wassup.Data.DefenderRarity.Rare,
    Wassup.Data.DefenderRarity.Rare,
    Wassup.Data.DefenderRarity.Rare,
    Wassup.Data.DefenderRarity.Rare,
    Wassup.Data.DefenderRarity.Epic,
    Wassup.Data.DefenderRarity.Epic,
    Wassup.Data.DefenderRarity.Epic,
    Wassup.Data.DefenderRarity.Epic,
    Wassup.Data.DefenderRarity.Ego,
};
for (int i = 0; i < paths.Length; i++)
{
    var so = UnityEditor.AssetDatabase.LoadAssetAtPath<Wassup.Data.DefenderUnitData>(paths[i]);
    if (so == null) { UnityEngine.Debug.LogError("Not found: " + paths[i]); continue; }
    so.rarity = rarities[i];
    UnityEditor.EditorUtility.SetDirty(so);
}
UnityEditor.AssetDatabase.SaveAssets();
UnityEngine.Debug.Log("Rarity assignment complete.");
```

## DraftController Inspector 배선

BattleScene > DraftController GO:

| 필드 | 값 |
|---|---|
| `basicDeck` (3) | Scout, Guardian, Cannon |
| `metaDeck` (2) | Sniper, Archer |
| `egoUnit` | Bruiser |
| `collectionPool` | 나머지 전체 11종 (Ranger, Piercer, Marksman, Bastion, Healer, FireCaster, IceCaster, PoisonCaster, BlockingCaster, + 메타에서 빠진 유닛) |

`collectionPool` = basicDeck + metaDeck + egoUnit 을 제외한 **모든** defender SO.  
현재 11종: Ranger, Piercer, Marksman, Bastion, Healer, FireCaster, IceCaster, PoisonCaster, BlockingCaster, Archer→메타이면 제외, Sniper→메타이면 제외.

> metaDeck = [Sniper, Archer] 기준으로 collectionPool = {Ranger, Piercer, Marksman, Bastion, Healer, FireCaster, IceCaster, PoisonCaster, BlockingCaster} 9종.  
> collectionCount=4 이므로 9종 중 4장 랜덤.

구 `catalog` 필드는 DraftController에서 제거됐으므로 씬에서 해당 배열 레퍼런스가 missing으로 표시될 수 있다 — 저장 후 확인.

## 완료 기준

- [ ] Inspector: 15종 SO 각각의 Rarity 드롭다운 값이 배정표와 일치
- [ ] PlayMode: `BeginDraft()` → 풀 10장 (Basic×3, Meta×2, Ego×1, Collection×4) 검증
- [ ] PlayMode: Basic 카드 3장이 Scout/Guardian/Cannon 임을 로그로 확인 (`[DraftController] Pool: ...`)
- [ ] PlayMode: Ego 카드 1장이 Bruiser임을 확인
- [ ] 콘솔: missing reference, validation 오류 없음
