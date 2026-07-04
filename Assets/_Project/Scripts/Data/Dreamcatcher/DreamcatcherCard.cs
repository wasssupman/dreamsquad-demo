using System;
using UnityEngine;

namespace Wassup.Data
{
    // ingame-dreamcatcher Unit 1 — which allied units a card targets.
    // dreamstone-loadout Unit 3 — All appended at the end (existing DreamcatcherCard
    // assets serialize axis as int 0~2; inserting earlier would relabel them).
    public enum CardTargetAxis { ClassRanger, ClassGuardian, Cost1, All }

    // ingame-dreamcatcher Unit 1 — what a card buffs. Maps to StatModifier in
    // Unit 2: AttackDamage→DamageMul, AttackSpeed→AttackSpeedMul,
    // EffectiveHealth→DmgTakenMul (damage-taken reduction proxy), MoveSpeed→MoveSpeedMul.
    public enum CardBuffKind { AttackDamage, AttackSpeed, EffectiveHealth, MoveSpeed }

    // dreamcatcher-deck-builder Unit 0 — deck-rule category. Unique cards are
    // capped per deck (<=2); Normal cards may repeat.
    public enum CardCategory { Normal, Unique }

    [Serializable]
    public struct CardEffect
    {
        public CardBuffKind kind;
        public float percent; // +10 = +10%, -50 = -50%
    }

    [CreateAssetMenu(fileName = "DreamcatcherCard", menuName = "Wassup/DreamcatcherCard", order = 20)]
    public class DreamcatcherCard : ScriptableObject
    {
        public string id;
        public string displayName;
        public CardTargetAxis axis;
        public CardCategory category = CardCategory.Normal;
        public CardEffect[] effects; // usually 1; fortress has 2
    }
}
