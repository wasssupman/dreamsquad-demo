namespace Wassup.Data.Authoring
{
    // battle-core-rebuild unit 8c — 이 타입의 **집**만 옮겼다(옛 `Battle/Effects/Modifiers/ModifierTypes.cs` 에서 떼어냄). 네임스페이스·값·번호 무변 —
    // 새 층 저작 SO 가 이것을 부르는데 옛 폴더는 unit 9 가 통째로 지운다. 이름 정리는 unit 9.

    // dreamcatcher-new-abilities unit 0 — DamageVsCcMul: 활성 CcEffect(기절/수면/DoT/넉백)
    // 걸린 적 대상 데미지 배율 (base 1). Slow(이동감속)은 CcEffect 아니라 미포함. append-only.
    // season-gimmick-overwork unit 1 — MaxHealthMul: 최대체력 배율 (base 1). Effects 가 배율 결정,
    // Health 쓰기는 Units 의 MaxHealthScaleSystem 만 수행.
    public enum StatKind : byte { DamageMul, AttackSpeedMul, DmgTakenMul, RegenPerSec, MoveSpeedMul, DamageVsCcMul, MaxHealthMul }

    // season-gimmick-overwork unit 0 — Fatigue: 야근 기믹 피로도 스택. append-only.
    public enum StackKind : byte { None, Fire, Ice, Bleed, Poison, Fatigue }

    public enum CombineOp : byte { Multiplicative, Additive, Override }
}
