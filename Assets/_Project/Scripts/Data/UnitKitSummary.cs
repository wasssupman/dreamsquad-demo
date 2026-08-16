using System.Collections.Generic;

namespace Wassup.Data
{
    // squad-character-page Unit 0 — the detail card's "설명문" is assembled purely
    // from existing DefenderUnitData fields (class + attack archetype + traits),
    // so no per-unit lore has to be authored to give the character page a
    // description. Pure projection helper, mirrors AttackOutputStats: plain input
    // -> plain string, EditMode tested (UnitKitSummaryTests fixes phrasing/order).
    // enum -> Korean label is presentation, not a hardcoded stat; every number in
    // the sentence is read from the SO, never literalised here.
    public static class UnitKitSummary
    {
        // squad-character-page unit 6 — display accessor. Authored `desc` (sheet-synced)
        // wins; empty falls back to the generated summary so new/unauthored units are
        // never blank. Build stays the generator (also used to seed desc).
        public static string Describe(DefenderUnitData u)
        {
            if (u == null) return "";
            return string.IsNullOrEmpty(u.desc) ? Build(u) : u.desc;
        }

        public static string Build(DefenderUnitData u)
        {
            if (u == null) return "";

            string cls = UnitLabels.ClassLabel(u.role);
            string archetype = Archetype(u);
            string head = string.IsNullOrEmpty(cls) ? archetype : $"{cls} · {archetype}";

            var traits = new List<string>();
            if (u.attackTargetCount > 1)
                traits.Add($"최대 {u.attackTargetCount}체 동시 타격");
            // defender-ability-assets unit 2 — trait 문구의 소스가 능력 서브에셋으로 이동.
            // 문구 텍스트/순서는 불변(테스트 고정). 사격 문구는 볼리 능력에 귀속 —
            // 폭탄 등 다른 facing 능력의 문구는 후속(현재 해당 유닛은 authored desc 사용).
            var volley = u.GetAbility<DirectionalVolleyAbility>();
            if (volley != null)
            {
                int shotCount = volley.pattern?.shots?.Length ?? 0;
                string aim = volley.RequiresFacing ? "지정 방향" : "대상 방향";
                traits.Add(shotCount > 1 ? $"{aim}으로 {shotCount}연발 사격" : $"{aim} 사격");
            }
            if (u.aggroCapacity > 0)
                traits.Add($"최대 {u.aggroCapacity}체 도발 유지");
            // on-place-skill-rework unit 6 — 배치 스킬의 출처가 둘이다: 레거시 enum 과
            // 규칙(UnitSkillAbility). 규칙을 먼저 보고, 없을 때만 enum 으로 떨어진다 —
            // 둘 다 선언된 유닛은 bake 가 경고하므로 여기선 한 줄만 낸다.
            string onPlace = OnPlaceRuleClause(u);
            if (string.IsNullOrEmpty(onPlace)) onPlace = OnPlaceClause(u.onPlaceEffect);
            if (!string.IsNullOrEmpty(onPlace))
                traits.Add(onPlace);
            if (u.GetAbility<HazardCastAbility>() != null)
                traits.Add("지속 해저드 설치");

            if (traits.Count == 0) return head + ".";
            return head + ". " + string.Join(", ", traits) + ".";
        }

        private static string Archetype(DefenderUnitData u)
        {
            if (u.targetAllies)
                return HasOutput(u.outputs, AttackOutputKind.Heal) ? "아군 치유형" : "아군 강화형";
            return u.projectile != null ? "원거리형" : "근접형";
        }

        private static bool HasOutput(AttackOutput[] outputs, AttackOutputKind kind)
        {
            if (outputs == null) return false;
            for (int i = 0; i < outputs.Length; i++)
                if (outputs[i].kind == kind) return true;
            return false;
        }

        private static string OnPlaceClause(OnPlaceEffectType effect)
        {
            switch (effect)
            {
                case OnPlaceEffectType.SlowPulse: return "배치 시 주변 둔화";
                case OnPlaceEffectType.BoostNearbyDefenders: return "배치 시 주변 아군 강화";
                case OnPlaceEffectType.BindNearby: return "배치 시 주변 속박";
                case OnPlaceEffectType.MeleeBurst: return "배치 즉시 광역 폭발";
                case OnPlaceEffectType.ForwardProjectile: return "배치 시 전방 발사";
                case OnPlaceEffectType.GainCost: return "배치 시 코스트 획득";
                case OnPlaceEffectType.ReduceSkillCooldown: return "배치 시 스킬 쿨다운 감소";
                case OnPlaceEffectType.ApplyStackNearby: return "배치 시 주변 상태 중첩";
                case OnPlaceEffectType.StunNearby: return "배치 시 주변 기절";
                case OnPlaceEffectType.DotNearby: return "배치 시 주변 지속 피해";
                // default 가 빈 문자열이라 신규 enum 멤버는 **조용히 설명이 빈다**.
                // OnPlaceEffectType 을 늘리면 여기도 같이 늘릴 것.
                default: return "";
            }
        }

        // on-place-skill-rework unit 6 — 규칙(트리거 × 페이로드)으로 선언된 배치 스킬의 문안.
        //
        // 「어그로」가 아니라 **「도발」** 계열 어휘를 쓴다(게임 문안의 기존 표현).
        // ⚠ 배스티온은 **두 도발을 화면에서 갈라야 한다** — 이미 `특수 효과: 공격한 적 도발`
        // 이 있고, 같은 단어·같은 상태(Aggroed)·같은 아이콘이라 플레이어가 구분할 수 없다.
        // sim 상 차이(상한 무시·즉시·시한)는 전부 숫자고 화면에 그 어휘가 없다. 눈에 보이는
        // 유일한 차이가 **「전원 / 한꺼번에」** 이므로 그 축으로 가른다.
        private static string OnPlaceRuleClause(DefenderUnitData u)
        {
            var ability = u.GetAbility<UnitSkillAbility>();
            if (ability?.mechanics == null) return "";
            for (int i = 0; i < ability.mechanics.Length; i++)
            {
                var m = ability.mechanics[i];
                if (m.trigger.kind != DcTriggerKind.OnPlace) continue;
                switch (m.payload.kind)
                {
                    case DcPayloadKind.EmitProjectilePattern: return "배치 시 주변 적 전원에게 낙하 폭격";
                    case DcPayloadKind.AreaTaunt:             return "배치 시 주변 적을 한꺼번에 끌어모음";
                    // ⚠ 위 default 와 같은 함정이다 — 배선하지 않은 payload 는 조용히 문안이
                    // 빈다. `UnitKitSummaryTests` 의 전수 순회가 이걸 잡는다.
                    default: return "";
                }
            }
            return "";
        }
    }
}
