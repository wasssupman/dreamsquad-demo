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
            if (u.directionalAttack)
                traits.Add(u.shotCount > 1 ? $"지정 방향으로 {u.shotCount}연발 사격" : "지정 방향 사격");
            if (u.aggroCapacity > 0)
                traits.Add($"최대 {u.aggroCapacity}체 도발 유지");
            string onPlace = OnPlaceClause(u.onPlaceEffect);
            if (!string.IsNullOrEmpty(onPlace))
                traits.Add(onPlace);
            if (u.hazardCastEnabled)
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
                default: return "";
            }
        }
    }
}
