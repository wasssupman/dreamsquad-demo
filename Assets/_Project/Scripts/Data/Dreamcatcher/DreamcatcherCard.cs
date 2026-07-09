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

    // dreamcatcher-unit-trigger Unit 0 — how a card binds to its targets.
    // Axis = existing axis-matched buff (current + future matching defenders);
    // Unit = attached to one individual defender. Default 0 = Axis preserves
    // every existing card asset's behavior.
    public enum CardBinding { Axis, Unit }

    // dreamcatcher-card-taxonomy — Squad(축 스탯 버프) / Unit(개별 부착 메커니즘).
    // The deck cap now keys on this (Squad ≤2), not on CardCategory. Coincides with
    // binding (Squad=Axis, Unit=Unit) but is its own authoritative field. Default 0
    // = Squad preserves existing stat cards without touching their assets.
    public enum CardType { Squad, Unit }

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
        // dreamcatcher-unit-trigger Unit 0 — appended last to keep serialization
        // order stable for existing card assets (binding deserializes as 0=Axis,
        // mechanics as empty). effects[] and mechanics[] may coexist, but the
        // current interpretation path consumes mechanics only for binding=Unit
        // cards (ApplyDreamcatcherCard stays Axis-only). Bake-time read only —
        // never iterate mechanics per-frame (managed array).
        public CardBinding binding;
        public DcMechanic[] mechanics;
        // dreamcatcher-attack-mod-bounce Unit 0 — card class (c): always-on
        // attack-output modifications (usually 0~1). Appended last; bake-time
        // read only, same rules as mechanics above.
        public DcAttackModSpec[] attackMods;
        // dreamcatcher-card-taxonomy — Squad/Unit type. Deck cap keys on this.
        // Appended last; zero-init = Squad for existing stat cards.
        public CardType type;
    }
}
