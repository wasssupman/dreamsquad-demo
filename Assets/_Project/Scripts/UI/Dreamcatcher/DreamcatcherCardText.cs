using System.Collections.Generic;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-hand-drag-tooltip unit 0 — shared card-performance body text
    // for the deck-builder detail popup and the in-battle drag tooltip. Pure
    // string assembly (card in → rich text out), EditMode-tested.
    public static class DreamcatcherCardText
    {
        // Rich effect body: gold axis header (Squad only) + type label + one
        // line per buff + authored description block (omitted when empty).
        public static string Body(DreamcatcherCard card)
        {
            string axis = card.axis == CardTargetAxis.ClassRanger ? "RANGER"
                        : card.axis == CardTargetAxis.ClassGuardian ? "GUARDIAN"
                        : card.axis == CardTargetAxis.Cost1 ? "COST-1 UNITS"
                        : "ALL UNITS";
            var lines = new List<string>();
            if (card.effects != null)
            {
                foreach (var e in card.effects)
                {
                    string sign = e.percent >= 0 ? "+" : "";
                    string col = e.percent >= 0 ? "#8BE28B" : "#E28B8B";
                    lines.Add($"{KindLabel(e.kind)}  <color={col}>{sign}{e.percent:0}%</color>");
                }
            }
            // dreamcatcher-card-taxonomy — label shows TYPE, retired grade.
            // hand-drag-tooltip unit 0 — Active gets its own label (the old
            // Unit/Squad split fell back to SQUAD; Active never reached the
            // deck builder, but the in-battle hand does show Active cards).
            string typeLabel = card.type == CardType.Unit ? "<color=#F0B44E>UNIT</color>"
                             : card.type == CardType.Active ? "<color=#7ED0E8>ACTIVE</color>"
                             : "<color=#9AA6C0>SQUAD</color>";
            // dreamcatcher-card-description Unit 1 — header(축·타입) + 자동 수치라인
            // (effects[], 있을 때만) + authored description(있을 때만). Unit/Active
            // 카드는 effects 가 비어 description 이 유일한 본문이 된다. axis 칩은
            // Squad 전용: axis 는 축 스탯 버프의 대상 필터라 다른 타입에는 무의미.
            string header = card.type == CardType.Squad
                ? $"<color=#F5D480><b>{axis}</b></color>  ·  {typeLabel}"
                : typeLabel;
            string body = $"<size=22>{header}</size>";
            if (lines.Count > 0) body += "\n\n" + string.Join("\n", lines);
            if (!string.IsNullOrEmpty(card.description)) body += $"\n\n<color=#D4DAE8>{card.description}</color>";
            return body;
        }

        // Exhaustive kind→label map. The legacy ternary chain fell through to
        // "Cost Rate" for DamageVsCc — keep every kind explicit so a future
        // enum append fails loudly here instead of mislabeling.
        private static string KindLabel(CardBuffKind kind)
        {
            switch (kind)
            {
                case CardBuffKind.AttackDamage: return "Attack";
                case CardBuffKind.AttackSpeed: return "Attack Speed";
                case CardBuffKind.EffectiveHealth: return "Health";
                case CardBuffKind.MoveSpeed: return "Move Speed";
                case CardBuffKind.CostRate: return "Cost Rate";
                case CardBuffKind.DamageVsCc: return "Damage vs CC";
                default: return kind.ToString();
            }
        }
    }
}
