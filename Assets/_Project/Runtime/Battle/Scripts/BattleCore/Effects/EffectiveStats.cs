namespace Somnia.Battle.BattleCore.Effects
{
    // battle-core-rebuild unit 6a — 슬롯들이 **접힌** 값. 옛 `ModifierStats` 의 후계다.
    //
    // 소비처가 곧 규칙이다:
    //   · `DamageMul`·`DamageVsCcMul` — 공격 산출물(때리는 쪽)
    //   · `AttackSpeedMul` — 쿨다운(실주기 = `max(간격 × 1/공속, 선딜)`)
    //   · `DmgTakenMul`·`RegenPerSec` — 피해 단계(`Unit` 의 미러 필드로 밀어 넣는다)
    //   · `MoveSpeedMul` — 이동 스텝
    //   · `MaxHealthMul` — 최대 체력 재계산(`MaxHealthScale`)
    //
    // ⚠ **struct 다.** 읽는 쪽이 들고 있다가 나중에 쓰면 그 사이의 만료가 반영되지 않으므로,
    // 매 읽기에서 `ModifierSet.Effective` 를 다시 묻는 것이 규약이다(늦게 접는다 = 구현 5).
    public struct EffectiveStats
    {
        public float DamageMul;
        public float AttackSpeedMul;
        public float DmgTakenMul;
        public float RegenPerSec;
        public float MoveSpeedMul;
        public float DamageVsCcMul;
        public float MaxHealthMul;

        /// <summary>슬롯이 하나도 없을 때의 값. 배율은 1, 재생은 0 이다.</summary>
        public static EffectiveStats Identity => new EffectiveStats
        {
            DamageMul = 1f,
            AttackSpeedMul = 1f,
            DmgTakenMul = 1f,
            RegenPerSec = 0f,
            MoveSpeedMul = 1f,
            DamageVsCcMul = 1f,
            MaxHealthMul = 1f,
        };

        public float Of(StatKind stat)
        {
            switch (stat)
            {
                case StatKind.DamageMul: return DamageMul;
                case StatKind.AttackSpeedMul: return AttackSpeedMul;
                case StatKind.DmgTakenMul: return DmgTakenMul;
                case StatKind.RegenPerSec: return RegenPerSec;
                case StatKind.MoveSpeedMul: return MoveSpeedMul;
                case StatKind.DamageVsCcMul: return DamageVsCcMul;
                case StatKind.MaxHealthMul: return MaxHealthMul;
                default: return 0f;
            }
        }
    }
}
