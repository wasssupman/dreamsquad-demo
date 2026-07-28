using System;
using UnityEngine;
using Wassup.Battle.Effects;

namespace Wassup.Data
{
    public enum StackPolicy : byte
    {
        RefreshAll,      // S1 디폴트 — 매 적용 시 remaining = perAppDuration
        PerStackInline,  // 향후 확장 여지
        DecayTick,       // 향후 확장 여지
    }

    public enum ThresholdMode : byte
    {
        Edge,    // 임계 도달 시 1회 발화, 스택 유지
        Consume, // 발화 후 atStack 만큼 stackCount 차감
    }

    public enum DerivedEffectKind : byte
    {
        ApplyDot,   // magnitude = dps, duration = 지속 시간
        ApplyStun,  // magnitude = stun 지속 시간 (duration 무시)
        ApplyStat,  // magnitude = stat magnitude, duration = 지속 시간, stat/op 사용
    }

    [Serializable]
    public struct ThresholdRule
    {
        public byte atStack;
        public ThresholdMode mode;
        public DerivedEffectKind derivedKind;
        /// <summary>
        /// ApplyDot: DPS, ApplyStun: stun duration (seconds), ApplyStat: stat magnitude
        /// </summary>
        public float magnitude;
        /// <summary>
        /// ApplyDot/ApplyStat: effect duration. ApplyStun: ignored (magnitude serves as duration).
        /// </summary>
        public float duration;
        /// <summary>ApplyStat 만 의미 있음.</summary>
        public StatKind stat;
        /// <summary>ApplyStat 만 의미 있음.</summary>
        public CombineOp op;
    }

    [CreateAssetMenu(fileName = "StackModifier", menuName = "Wassup/StackModifier", order = 30)]
    public class StackModifierSO : ScriptableObject
    {
        // 미등록 StackKind 의 상한 폴백. 여러 producer(AttackSystem outputs, on-place 도포)가
        // 같은 기본값을 복사해 쓰고 있었다 — 권위는 스택을 소유한 이 SO 다.
        public const byte DefaultMaxStack = 5;

        public StackKind kind;
        public byte maxStack = DefaultMaxStack;
        public float perAppDuration = 5f;
        public StackPolicy policy = StackPolicy.RefreshAll;
        public ThresholdRule[] thresholds;
    }
}
