using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // dreamcatcher-new-abilities unit 0 — DamageVsCcMul: 활성 CcEffect(기절/수면/DoT/넉백)
    // 걸린 적 대상 데미지 배율 (base 1). Slow(이동감속)은 CcEffect 아니라 미포함. append-only.
    // season-gimmick-overwork unit 1 — MaxHealthMul: 최대체력 배율 (base 1). Effects 가 배율 결정,
    // Health 쓰기는 Units 의 MaxHealthScaleSystem 만 수행.
    public enum StatKind : byte { DamageMul, AttackSpeedMul, DmgTakenMul, RegenPerSec, MoveSpeedMul, DamageVsCcMul, MaxHealthMul }

    // season-gimmick-overwork unit 0 — Fatigue: 야근 기믹 피로도 스택. append-only.
    public enum StackKind : byte { None, Fire, Ice, Bleed, Poison, Fatigue }

    public enum CombineOp : byte { Multiplicative, Additive, Override }

    // dreamcatcher-empower-aura unit 1 — 모디파이어 출처(1급 태그). 크기·stat 이 같아도
    // 출처를 슬롯 단위로 구분(집계 ModifierStats 에선 소실 — 슬롯 버퍼에서만 유효).
    // 소비: 오라(Dreamcatcher 필터). 향후 dispel/UI/로깅 재사용. append-only(직렬화 안전).
    // 각 값은 실존 생산자에 매핑 — 임의 확장 금지(제약 8).
    public enum ModifierOrigin : byte
    {
        Unspecified = 0,
        OnPlace,          // 유닛 배치 스킬(BattleBridge onPlace*)
        Skill,            // 플레이어 액티브 스킬 카드
        Synergy,          // 인접 동일유닛 시너지
        Dreamcatcher,     // 드림캐쳐 카드/유닛 트리거(OnKill 자가버프 포함)
        Dreamstone,       // 드림스톤 로드아웃(영구 baseline)
        Tile,             // EffectTile
        Zone,             // 해저드 존(Slow 등)
        Boss,             // 보스 주기 트리거(AllyMoveSpeedAura)
        HealthThreshold,  // 체력 임계 자가버프
        OnHit,            // 공격 산출물 ApplyStat(공격 시 대상에 부여)
        Stack,            // 스택(Fire/Ice/Bleed/Poison/Fatigue) 파생 스탯
        Gimmick,          // 시즌 기믹 효과(season-gimmick-overwork 야근 라스트런 등)
    }

    // 임베딩 컨벤션 — IComponentData/IBufferElementData 아님. 두 Slot struct 에 직접 임베딩.
    public struct ModifierHeader
    {
        public float remaining;
        public Entity source;
        public ushort stackId;
        // dreamcatcher-empower-aura unit 1 — 이 모디파이어의 출처(머지 키 아님, 메타데이터).
        public ModifierOrigin origin;
    }
}
