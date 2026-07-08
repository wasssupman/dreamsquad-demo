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
    // dreamstone-loadout Unit 6 — CostRate appended at the end (existing card/stone
    // assets serialize kind as int 0~3; inserting earlier would relabel them).
    // CostRate has no StatModifier/entity mapping — BattleBridge.MapDcEffect's
    // switch has no case for it and safely no-ops via its default branch; the value
    // is consumed entirely by GameManager -> CostRuntime.SetRegenRateMultiplier.
    public enum CardBuffKind { AttackDamage, AttackSpeed, EffectiveHealth, MoveSpeed, CostRate }

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
        // dreamcatcher-card-art Unit 0 — tarot-style card art shown on the deck
        // page (image + effect text column). Nullable: view falls back to a
        // category color when unassigned. Appended last to keep serialization
        // order stable for existing card assets.
        public Sprite art;
    }
}
