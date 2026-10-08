namespace Somnia.Battle.Data
{
    // ingame-dreamcatcher Unit 1 — what a card buffs. Maps to StatModifier in
    // Unit 2: AttackDamage→DamageMul, AttackSpeed→AttackSpeedMul,
    // EffectiveHealth→DmgTakenMul (damage-taken reduction proxy), MoveSpeed→MoveSpeedMul.
    // dreamstone-loadout Unit 6 — CostRate appended at the end (existing card/stone
    // assets serialize kind as int 0~3; inserting earlier would relabel them).
    // CostRate has no StatModifier/entity mapping (old battle: BattleBridge.MapDcEffect
    // no-op'd it and GameManager -> CostRuntime.SetRegenRateMultiplier consumed it — history).
    // dreamcatcher-new-abilities unit 0 — DamageVsCc: 활성 CcEffect(기절/수면/DoT/넉백)가
    // 걸린 적에게 추가 피해 %. ⚠ 이동감속(Slow)은 이 엔진에서 CcEffect 가 아니라 MoveSpeedMul
    // StatModifier 라 여기 해당 없음(카드 문안에 "둔화" 표기 금지). StatKind.DamageVsCcMul
    // 로 매핑(unit 2). append-only.
    public enum CardBuffKind { AttackDamage, AttackSpeed, EffectiveHealth, MoveSpeed, CostRate, DamageVsCc }
}
