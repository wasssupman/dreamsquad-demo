namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-C/1 — 모디파이어 어휘. 구 `Wassup.Battle.Effects` 의
    /// **값 순서까지 그대로** 옮긴 것이다.
    ///
    /// ⚠ **enum 값 순서가 계약이다.** 상태 해시가 enum 을 `Convert.ToInt64` 로 정수화해 찍는다
    /// (`BattleBridge.LegacyTrace.cs:336`). 멤버를 재정렬하거나 중간에 끼워 넣으면 같은 상태가
    /// 다른 문자열로 나가 A/B parity 가 깨진다. 추가는 **맨 뒤에만**(append-only).
    ///
    /// ⚠ `CombineOp` 의 **기본값(0)이 `Multiplicative`** 다. `op` 를 안 채운 생산자는 전부 곱셈으로
    /// 들어간다 — 구 sim 의 실존 동작이고(EffectTile 이 그 경로다) 재현 대상이다.
    /// </summary>
    public enum StatKind : byte
    {
        DamageMul, AttackSpeedMul, DmgTakenMul, RegenPerSec, MoveSpeedMul, DamageVsCcMul, MaxHealthMul
    }

    public enum StackKind : byte { None, Fire, Ice, Bleed, Poison, Fatigue }

    /// <summary>
    /// battle-sim-extraction unit 18-H/3 — 저작이 상한을 비워 둔 스택의 폴백.
    /// 구 `Wassup.Data.StackModifierSO.DefaultMaxStack` 이식.
    ///
    /// ⚠ 여러 생산자(공격 출력·배치 도포·투사체 착탄)가 같은 값을 복사해 쓰고 있었다 —
    /// 권위는 스택을 소유한 저작 SO 지만, sim 은 그걸 못 읽으므로 값을 **복제**한다.
    /// 저작 쪽이 바뀌면 여기도 바꿔야 한다(어휘 평행성 검사와 같은 성격의 결합).
    /// </summary>
    public static class StackDefaults
    {
        public const byte MaxStack = 5;
    }

    public enum CombineOp : byte { Multiplicative, Additive, Override }

    /// 모디파이어 출처(1급 태그). 크기·stat 이 같아도 출처를 슬롯 단위로 구분한다 —
    /// 집계 결과에선 소실되고 슬롯 버퍼에서만 유효하다. append-only.
    public enum ModifierOrigin : byte
    {
        Unspecified = 0,
        OnPlace,
        Skill,
        Synergy,
        Dreamcatcher,
        Dreamstone,
        Tile,
        Zone,
        Boss,
        HealthThreshold,
        OnHit,
        Stack,
        Gimmick,
        Burnout,
    }

    /// <summary>
    /// 슬롯 공통 헤더. **`IComponentData` 가 아니라 두 Slot struct 에 직접 임베딩**되는 것이
    /// 구 sim 의 컨벤션이고, 그 모양이 상태 해시에 그대로 나간다
    /// (`Wassup.Battle.Effects.ModifierHeader{origin=…,remaining=…,source=sim:N,stackId=…}`).
    /// 필드 이름 4개와 그 ordinal 정렬 순서가 계약이다 — `LegacyTraceKeyContractTests` 가 박제.
    /// </summary>
    public struct ModifierHeader
    {
        public float remaining;
        public SimEntityId source;
        public ushort stackId;
        public ModifierOrigin origin;
    }

    /// <summary>
    /// 병합 키는 **`(source, stat, op, stackId)` 4축**이다. `op` 가 키에 들어가는 이유:
    /// 한 채널이 1.0 경계를 넘나들면 Additive 슬롯과 Multiplicative 슬롯이 **공존**해
    /// refresh 가 아니라 누적이 된다(슬롯 누수). 채널은 단방향으로 유지한다.
    /// </summary>
    public struct StatModifierSlot
    {
        public ModifierHeader header;
        public StatKind stat;
        public CombineOp op;
        public float magnitude;
    }

    /// `header.remaining` 은 **perAppDuration 까지 남은 시간**이다(슬롯 수명이 아니다).
    public struct StackModifierSlot
    {
        public ModifierHeader header;
        public StackKind kind;
        public byte stackCount;
        public byte maxStack;
        public byte lastTriggeredStack;   // 엣지 교차 검출 캐시
    }

    /// <summary>
    /// 집계 결과. 기본값은 배율 1.0 / `regenPerSec` 0.0 이다 — `Aggregate` 가 **유일 writer** 이고
    /// dirty 인 엔티티만 다시 계산한다.
    /// </summary>
    public struct ModifierStats
    {
        public float damageMul;
        public float attackSpeedMul;
        public float dmgTakenMul;
        public float regenPerSec;
        public float moveSpeedMul;
        public float damageVsCcMul;
        public float maxHealthMul;

        /// 배율 5축을 1.0, `regenPerSec` 을 0 으로. 신규 엔티티는 이 값에서 출발한다.
        public static ModifierStats Identity => new ModifierStats
        {
            damageMul = 1f, attackSpeedMul = 1f, dmgTakenMul = 1f,
            regenPerSec = 0f, moveSpeedMul = 1f, damageVsCcMul = 1f, maxHealthMul = 1f,
        };
    }

    /// <summary>
    /// ECS 의 `IEnableableComponent` 대응. 신 sim 에는 활성/비활성 컴포넌트 개념이 없으므로
    /// **존재 = dirty** 로 접는다(`Aggregate` 가 처리 후 제거).
    ///
    /// ⚠ 구 `StatModifierTickSystem` 은 dirty 와 **무관하게** 모든 슬롯 보유자를 훑는다 —
    /// 한때 dirty 로 쿼리했다가 만료가 영영 안 오는 버그가 났던 자리다(그 주석이 코드에 남아 있다).
    /// 이식할 때 dirty 로 좁히지 말 것.
    /// </summary>
    public struct ModifierStatsDirty { }
}
