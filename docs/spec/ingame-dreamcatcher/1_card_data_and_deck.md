# 1 — 드림캐쳐 카드 데이터 + 기본 덱

## 목적

카드 효과 모델과 고정 기본 덱(10장)을 정의한다. 효과는 4채널 스탯%로 한정.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs`
- 신규 `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherDeck.cs`
- 신규 에셋 6개 카드 + `DreamcatcherDeck_Default.asset` (10장 참조, 중복 허용)

## 구현

```csharp
public enum CardTargetAxis { ClassRanger, ClassGuardian, Cost1 }
public enum CardBuffKind { AttackDamage, AttackSpeed, EffectiveHealth, MoveSpeed }

[Serializable] public struct CardEffect { public CardBuffKind kind; public float percent; } // +10 = +10%

[CreateAssetMenu(menuName="Wassup/DreamcatcherCard")]
public class DreamcatcherCard : ScriptableObject
{
    public string id;
    public string displayName;
    public CardTargetAxis axis;
    public CardEffect[] effects;   // 대개 1개, fortress 는 2개
}

[CreateAssetMenu(menuName="Wassup/DreamcatcherDeck")]
public class DreamcatcherDeck : ScriptableObject
{
    public DreamcatcherCard[] cards;  // 길이 10, 중복 참조 허용
}
```

효과→StatModifier 매핑(Unit 2에서 사용, 여기선 모델만):
- AttackDamage +p → DamageMul ×(1+p/100)
- AttackSpeed +p → AttackSpeedMul ×(1+p/100)
- EffectiveHealth +p → DmgTakenMul ×(1/(1+p/100))
- MoveSpeed ±p → MoveSpeedMul ×(1+p/100)

6 카드 에셋:
| id | axis | effects |
|---|---|---|
| ranger_atk_10 | ClassRanger | AttackDamage +10 |
| ranger_as_10 | ClassRanger | AttackSpeed +10 |
| cost1_as_5 | Cost1 | AttackSpeed +5 |
| cost1_hp_10 | Cost1 | EffectiveHealth +10 |
| guardian_hp_15 | ClassGuardian | EffectiveHealth +15 |
| guardian_fortress | ClassGuardian | EffectiveHealth +50, MoveSpeed -50 |

기본 덱 10장(중복 허용): ranger_atk_10 ×2, ranger_as_10 ×2, cost1_as_5 ×2, cost1_hp_10, guardian_hp_15 ×2, guardian_fortress ×1.

에셋 생성은 execute_code(ScriptableObject.CreateInstance + CreateAsset).

## 완료 기준

- compile + read_console clean.
- 6 카드 + 덱 에셋 생성, 덱 cards.Length==10, 각 카드 effects 유효.
- 런타임 점검: 덱 로드 + 카드 axis/effects 읽힘.
