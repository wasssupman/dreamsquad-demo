# 0 — Data Model: DefenderRarity / DraftSlotType

## 목적

유닛 등급과 드래프트 슬롯 타입을 enum으로 정의하고, `DefenderUnitData`에 `rarity` 필드를 추가한다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Data/DefenderRarity.cs`
- 신규: `Assets/_Project/Scripts/Data/DraftSlotType.cs`
- 수정: `Assets/_Project/Scripts/Data/DefenderUnitData.cs`

## 구현

### DefenderRarity.cs

```csharp
namespace Wassup.Data
{
    public enum DefenderRarity { Common, Rare, Epic, Ego }
}
```

### DraftSlotType.cs

```csharp
namespace Wassup.Data
{
    public enum DraftSlotType { Basic, Meta, Collection, Ego }
}
```

### DefenderUnitData.cs 변경

`Header("Deployment Presentation")` 위에 다음 추가:

```csharp
[Header("Rarity")]
public DefenderRarity rarity = DefenderRarity.Common;
```

기본값 `Common`으로 설정해 기존 SO가 재직렬화 없이 Common으로 읽힌다.

## 완료 기준

- [ ] 컴파일 오류 없음
- [ ] Unity Inspector에서 `DefenderUnitData` 에 `Rarity` 드롭다운이 표시됨
- [ ] 기존 defender SO 15종이 재직렬화 없이 Editor에서 정상 로드됨
