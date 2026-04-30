using System;
using Wassup.Battle.Effects;

namespace Wassup.Data
{
    [Serializable]
    public enum AttackOutputKind { Damage, Heal, ApplyStat, ApplyStack }

    [Serializable]
    public struct AttackOutput
    {
        public AttackOutputKind kind;   // 출력 종류: 4가지 분기 중 하나
        public float magnitude;         // 모든 kind — Damage/Heal 양 / Stat magnitude / Stack countDelta
        public float duration;          // ApplyStat / ApplyStack 만 의미 (Damage/Heal 은 무시)
        public StatKind stat;           // ApplyStat 만 — 적용할 스탯 종류
        public CombineOp op;            // ApplyStat 만 — 합성 방식
        public StackKind stackKind;     // ApplyStack 만 — 적용할 스택 종류
    }
}
