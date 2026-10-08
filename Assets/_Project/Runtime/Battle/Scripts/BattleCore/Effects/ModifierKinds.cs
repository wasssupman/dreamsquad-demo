using System;

namespace Somnia.Battle.BattleCore.Effects
{
    // battle-core-rebuild unit 6a — 효과 어휘.
    //
    // `StatKind`·`CombineOp`·`StackKind` 는 `Somnia.Battle.Skills` 의 `Skill*` 미러와 **값이 같아야
    // 한다**(어댑터가 캐스트로 번역한다). 어셈블리가 갈려 컴파일러가 못 잡는 자리라
    // `CoreSkillEnumPinTests` 가 유일한 그물이다 — 옛 `SkillModifierKindPinTests` 의 후계다.
    //
    // ⚠ 전부 **append-only**. 번호를 재사용하면 이미 구운 골든과 저작 에셋이 다른 뜻으로 읽힌다.

    /// <summary>모디파이어가 움직이는 값. 기본은 배율 1(재생만 0)이다.</summary>
    public enum StatKind : byte
    {
        DamageMul = 0,
        AttackSpeedMul = 1,
        DmgTakenMul = 2,
        RegenPerSec = 3,
        MoveSpeedMul = 4,
        /// <summary>군중 제어에 걸린 대상을 때릴 때의 추가 배율. **감속은 여기 안 센다**.</summary>
        DamageVsCcMul = 5,
        MaxHealthMul = 6,
    }

    public enum CombineOp : byte
    {
        Multiplicative = 0,
        Additive = 1,
        Override = 2,
    }

    /// <summary>쌓이는 것. 값은 `Somnia.Battle.Skills.SkillStackKind` 와 같다.</summary>
    public enum StackKind : byte
    {
        None = 0,
        Fire = 1,
        Ice = 2,
        Bleed = 3,
        Poison = 4,
        Fatigue = 5,
    }

    /// <summary>
    /// 이 모디파이어가 **어디서 왔나**. 꼬리표이고 **병합 키가 아니다**(F26 — 병합 키는
    /// `SlotTag` 다). 오라 판정·로그·상태 그림이 이것으로 거른다.
    /// skill-data-table unit 2(U15 — 연출은 효과 기준): 연출이 읽는 값(`Dreamcatcher` = 강화 오라 · `Burnout`)은 **효과가** 박는다 —
    /// 소유자(카드 · 유닛 · 적)에서 파생하지 않는다. 병합 칸 규칙은 이것을 읽지 않는다(`SimIntent.PerBindingSlot`).
    ///
    /// ⚠ 3(`Synergy`)은 **은퇴 슬롯**이다(2026-09-03 기능 제거). 지우면 뒤 값이 밀려
    /// `SkillModifierOrigin` 캐스트가 조용히 어긋난다 — 번호만 보존한다.
    /// </summary>
    public enum ModifierOrigin : byte
    {
        Unspecified = 0,
        OnPlace = 1,
        Skill = 2,
        Synergy = 3,
        Dreamcatcher = 4,
        Dreamstone = 5,
        Tile = 6,
        Zone = 7,
        Boss = 8,
        HealthThreshold = 9,
        OnHit = 10,
        Stack = 11,
        Gimmick = 12,
        Burnout = 13,
    }

    /// <summary>
    /// 슬롯을 **가르는** 축. 옛 전역 `stackId` 번호판의 후계이고 그 번호판이 **수정본**이었다
    /// (F26) — 4키가 전부 겹쳐 강한 배치 감속이 약한 스택 감속으로 깎이던 버그의 수정.
    /// </summary>
    public enum SlotKind : byte
    {
        /// <summary>일반. 옛 `stackId = 0`.</summary>
        Default = 0,
        OnPlace = 1,
        Tile = 2,
        AllyField = 3,
        /// <summary>규칙 인스턴스 소유 버프(오늘 = 드림캐쳐 카드). 판별자 = 바인딩 `instanceId`(인스턴스마다 자기 칸). 정수 4 유지.</summary>
        BindingInstance = 4,
        /// <summary>스택 임계 파생. 판별자 = `StackKind`(불과 얼음이 서로 다른 칸).</summary>
        StackDerived = 5,
        Gimmick = 6,
        /// <summary>
        /// unit 6b — 존 장판. **판별자가 없다**(전부 0) — 겹친 장판이 «한 슬롯을 나눠 쓰는
        /// 것»이 규칙이기 때문이다. 장판마다 칸을 주면 겹칠 때 배율이 곱으로 누적된다.
        /// 대신 누가 이기는지를 순회 순서가 아니라 `FieldFold.Strongest` 가 정한다(F23).
        /// </summary>
        Zone = 7,
    }

    /// <summary>
    /// **enum 하나가 아니라 (종류, 판별자) 짝이다.** 옛 번호판 여섯 자리 중 둘이 매개변수였다 —
    /// 스택 파생은 `100 + (int)StackKind`, 드림캐쳐는 카드마다 늘어나는 카운터였다.
    /// 판별자를 접어 종류 하나로 만들면 그 버그가 그대로 재현된다(6a 구현 3).
    /// </summary>
    public readonly struct SlotTag : IEquatable<SlotTag>
    {
        public readonly SlotKind Kind;

        /// <summary>종류 안에서 칸을 가르는 값. 쓰지 않는 종류는 0 이다.</summary>
        public readonly int Discriminator;

        public SlotTag(SlotKind kind, int discriminator = 0)
        {
            Kind = kind;
            Discriminator = discriminator;
        }

        /// <summary>일반 칸. 공격 산출물·존처럼 「그 출처가 하나만 갖는」 효과가 쓴다.</summary>
        public static readonly SlotTag Default = new SlotTag(SlotKind.Default);

        /// <summary>스택 임계 파생 — 종류마다 자기 칸(불 스택이 얼음 스택을 안 덮는다).</summary>
        public static SlotTag OfStack(StackKind kind) => new SlotTag(SlotKind.StackDerived, (int)kind);

        /// <summary>규칙 인스턴스 — 부착 인스턴스마다 자기 칸(같은 카드 두 장이 서로 안 덮는다).</summary>
        public static SlotTag OfBinding(int instanceId) => new SlotTag(SlotKind.BindingInstance, instanceId);

        public bool Equals(SlotTag other) => Kind == other.Kind && Discriminator == other.Discriminator;

        public override bool Equals(object obj) => obj is SlotTag o && Equals(o);

        public override int GetHashCode() => ((int)Kind * 397) ^ Discriminator;

        public static bool operator ==(SlotTag a, SlotTag b) => a.Equals(b);

        public static bool operator !=(SlotTag a, SlotTag b) => !a.Equals(b);
    }
}
