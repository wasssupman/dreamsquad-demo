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
        public static string Body(DreamcatcherCard card) => Assemble(card, compact: false);

        // hand-drag-tooltip unit 4 — 인게임 손패 툴팁 전용. 블록 사이 빈 줄을 뺀 것만
        // 다르다(내용·라벨·색은 동일). 툴팁은 화면 상단에 떠서 보드를 가리므로 세로
        // 예산이 빡빡한 반면, 덱빌더 팝업/덱 페이지는 넓어서 빈 줄이 가독성에 이롭다.
        // 그래서 Body() 를 바꾸지 않고 변형을 둔다(unit 0 "덱빌더 회귀 금지" 계약).
        public static string BodyCompact(DreamcatcherCard card) => Assemble(card, compact: true);

        private static string Assemble(DreamcatcherCard card, bool compact)
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
            string gap = compact ? "\n" : "\n\n";
            string body = $"<size=22>{header}</size>";
            if (lines.Count > 0) body += gap + string.Join("\n", lines);
            if (!string.IsNullOrEmpty(card.description)) body += $"{gap}<color=#D4DAE8>{card.description}</color>";
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
