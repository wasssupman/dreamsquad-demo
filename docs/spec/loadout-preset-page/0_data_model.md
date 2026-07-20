# 0 — 프리셋 데이터 모델 (SO)

## 목적

기획자가 authoring 하는 프리셋 집합 SO 와 개별 프리셋 자료구조를 정의한다. 이후 모든 단위의 토대.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Data/Preset/SquadPresetCollection.cs`
  (`SquadPreset` + `SquadPresetCollection` 두 타입을 한 파일에 둔다 — `PlayerProfile.cs` 가 `SquadSave`/
  `DeckSave` 를 한 파일에 두는 것과 동형)

## 구현

`Wassup.Data` 네임스페이스(`DefenderUnitData`/`DreamcatcherCard` 와 동일 어셈블리).

```csharp
[Serializable]
public class SquadPreset
{
    public string presetName = "프리셋 1";   // 목록 아이템에 표시
    public DefenderUnitData[] units;          // ≤ SquadSave.SlotCount(7). SO 직접 참조
    public DreamcatcherCard[] cards;          // ≤ 덱 크기(10). SO 직접 참조
}

[CreateAssetMenu(fileName = "SquadPresetCollection", menuName = "Wassup/SquadPresetCollection", order = 25)]
public class SquadPresetCollection : ScriptableObject
{
    public List<SquadPreset> presets = new List<SquadPreset>();
}
```

원칙:

- **SO 직접 참조** — id 필드 없음. id 는 적용 단계(unit 1)에서 `unit.id`/`card.id` 로 읽는다.
- `units`/`cards` 는 배열 길이를 강제하지 않는다(기획 authoring 편의). 표시(unit 3)와 적용(unit 1)에서
  각각 7·필요분으로 정규화/캡한다.
- 순수 데이터 SO — MonoBehaviour/ECS 의존 없음. 계층 규칙(제약 4 authoring/runtime 분리) 준수.
- `order = 25` — 20~24 는 이미 점유(DreamcatcherCard=20, DreamcatcherDeck=21, DreamcatcherCardCatalog=22,
  DreamstoneData·DeckRuleConfig=23, DreamstoneCatalog·AwakeningConfig=24). order 는 Assets/Create 정렬
  힌트일 뿐이라 중복도 컴파일 무해하지만, 다음 빈 자리(25)를 쓴다. menuName 은 고유해 충돌 없음.

## 완료 기준

- [ ] `dotnet build` 또는 Unity 컴파일 무오류 (`Wassup.Data` 어셈블리).
- [ ] Unity `Assets > Create > Wassup > SquadPresetCollection` 메뉴로 에셋 생성 가능.
- [ ] 인스펙터에서 `presets` 리스트에 항목 추가 → 각 항목의 `units`/`cards` 에 SO 드래그 할당 가능.
- 확인 2026-07-20 (커밋 05c7c7b8): 컴파일 그린, `Assets/Create/Wassup/SquadPresetCollection` 로 에셋 authoring 확인.
