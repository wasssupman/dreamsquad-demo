using System;

namespace Wassup.Data
{
    /// <summary>
    /// skill-data-table unit 4 — **소유 줄**(`tables.md` §4 `Skills` 한 줄): 언제(트리거 · 주체 · 게이트) → 효과 에셋. 카드 · 방어유닛 · 적이
    /// 같은 형식을 `bindings` 배열로 든다(계약 2 — 옛 저장처 넷 `mechanics` · `UnitSkillAbility` · `nightmareMechanics` · `ShieldCastAbility` 를 합친다).
    ///
    /// **수명은 칸이 아니다** — 소유자 종류에서 파생한다(카드 · 유닛 · 적 = 주인 수명 · 시전 = 발동 상한까지). 저작 손잡이는 `fireCap` 하나.
    /// </summary>
    [Serializable]
    public struct BindingSpec
    {
        public TriggerSpec trigger;
        [UnityEngine.Tooltip("발동 상한 — 0 = 무제한. 궁극기(UltimateLeap) = 1(「생존당 1회」). 시전(Cast) = 1.")]
        public int fireCap;
        public EffectData effect;
    }

    /// <summary>
    /// skill-data-table unit 4(U5) — 카드가 **붙을 수 있는 숙주 종류**(부여 게이트 — 계약 4). 굽기가 켜진 종류마다 조합 검증을 돌린다.
    /// 기본 = 방어유닛. 적 표식 카드(`BountyMark`) = 적만(옛 `HasBountyMark()` 파생을 값으로). append-only(에셋이 정수로 직렬화).
    /// </summary>
    [Flags]
    public enum HostKinds
    {
        None = 0,
        Defender = 1,
        Enemy = 2,
    }
}
