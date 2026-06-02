using System;
using UnityEngine;

namespace Wassup.Data
{
    // ingame-dreamcatcher Unit 1 — which allied units a card targets.
    public enum CardTargetAxis { ClassRanger, ClassGuardian, Cost1 }

    // ingame-dreamcatcher Unit 1 — what a card buffs. Maps to StatModifier in
    // Unit 2: AttackDamage→DamageMul, AttackSpeed→AttackSpeedMul,
    // EffectiveHealth→DmgTakenMul (damage-taken reduction proxy), MoveSpeed→MoveSpeedMul.
    public enum CardBuffKind { AttackDamage, AttackSpeed, EffectiveHealth, MoveSpeed }

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
        public CardEffect[] effects; // usually 1; fortress has 2
    }
}
