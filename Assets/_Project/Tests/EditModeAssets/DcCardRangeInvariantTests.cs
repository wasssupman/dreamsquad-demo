using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.Cards;
using Wassup.Data;
using Wassup.Skills;

namespace Wassup.Tests.EditModeAssets
{
    // dreamcatcher-attach-range-preview unit 1 — 실제 카드 전부에 대한 **단일 도형 불변식**(README 계약 6).
    //
    // 범위 채널이 하나라 카드당 공간 효과는 최대 1개여야 한다. 위반은 loud 로 잡아 결정을 강제한다
    // (조용히 첫 것만 그리면 두 번째 범위가 없는 것처럼 보인다). 공격 수식자의 tileRange(팅김 탐색 반경)는 착탄점 기준이라
    // host 중심 범위가 아니고, 규칙 줄이 아니라서 도형을 만들지 않는다 — 그것도 여기서 못박는다.
    //
    // skill-data-table 4-정리(B21) — 판정 기준을 옛 사본(`DcRangeCatalog` · `DcSkillRouting`)에서 **코어 `RangeCatalog`**(정의표 규칙 줄 —
    // 카드 화면 `CoreCardDragSlot.CardRangeOf` 와 같은 함수)으로 옮겼다. 카드를 한 번 굽고 그 카드의 규칙 줄을 본다.
    // ⚠ 오늘 공간 카드 목록은 **로그로만** 남긴다 — 카드가 늘어도 이 테스트가 빨개지면 안 된다(기대값 표는 `CardViewAssetTests`).
    public class DcCardRangeInvariantTests
    {
        private const string CardsRoot = "Assets/_Project/Data/Dreamcatcher";

        private static List<DreamcatcherCard> LoadCards()
        {
            var result = new List<DreamcatcherCard>();
            foreach (var guid in AssetDatabase.FindAssets("t:DreamcatcherCard", new[] { CardsRoot }))
            {
                var card = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(AssetDatabase.GUIDToAssetPath(guid));
                if (card != null) result.Add(card);
            }
            Assert.IsNotEmpty(result, "DreamcatcherCard 에셋을 찾지 못했다 — 경로 규약이 바뀌었나?");
            return result;
        }

        private static MatchDefinition Bake(List<DreamcatcherCard> cards)
        {
            var guids = AssetDatabase.FindAssets("t:AwakeningConfig");
            Assert.IsNotEmpty(guids);
            var awakening = AssetDatabase.LoadAssetAtPath<AwakeningConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var def = new MatchDefinition();
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                CardDefinitionBuilder.Fill(def, new CardAuthoring { Cards = cards, Awakening = awakening },
                                           new List<ProjectileData>(), new List<ProjectilePatternData>(),
                                           CardDefinitionBuilder.WithCardHazards(null, cards));
            }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
            Assert.AreEqual(cards.Count, def.Cards.Length, "덱 순서 = 카드 줄 순서");
            return def;
        }

        [Test]
        public void EveryCard_HasAtMostOneSpatialShape()
        {
            var cards = LoadCards();
            var def = Bake(cards);
            var drawable = new StringBuilder();
            for (int c = 0; c < cards.Count; c++)
            {
                var distinct = new HashSet<(RangeShape, float, RangeMetric)>();
                var rows = def.Cards[c].Bindings;
                for (int i = 0; rows != null && i < rows.Length; i++)
                {
                    ref var b = ref def.Bindings[rows[i]];
                    var fx = def.EffectOf(in b);
                    var spec = RangeCatalog.Resolve(b.Trigger, fx.Kind, fx.TileRange);
                    if (spec.Shape != RangeShape.None) distinct.Add((spec.Shape, spec.RadiusTiles, spec.Metric));
                }
                Assert.LessOrEqual(distinct.Count, 1,
                    $"{cards[c].id}: 공간 효과가 {distinct.Count}개 — 범위 채널은 하나다. 카드 분할 또는 표기 결정이 필요하다.");

                var resolved = CoreCardDragSlot.CardRangeOf(def, c);
                if (resolved.Shape != RangeShape.None)
                    drawable.Append(cards[c].id).Append('(').Append(resolved.RadiusTiles).Append(") ");
            }
            Debug.Log($"[DcCardRangeInvariant] 오늘 범위를 그리는 카드: {drawable}");
        }

        [Test]
        public void AttackModOnlyCards_DrawNothing()
        {
            // 팅김 반경(bouncy_bead 3) 등은 착탄점 기준이라 host 중심 범위가 아니다.
            var cards = LoadCards();
            var def = Bake(cards);
            int checkedCards = 0;
            for (int c = 0; c < cards.Count; c++)
            {
                // skill-data-table unit 8 — 공격 수식자도 소유 줄이다(트리거 None). 「수식자만」 = 소유 줄 전부가 수식자 종류.
                if (!OnlyAttackModRows(cards[c])) continue;
                checkedCards++;
                Assert.AreEqual(RangeShape.None, CoreCardDragSlot.CardRangeOf(def, c).Shape,
                    $"{cards[c].id}: 공격 수식자 줄만 있는 카드가 범위를 그리면 안 된다");
            }
            Assert.Greater(checkedCards, 0, "공격 수식자만 있는 카드가 없다면 테스트가 공허하다");
        }

        private static bool OnlyAttackModRows(DreamcatcherCard card)
        {
            if (card.bindings == null || card.bindings.Length == 0) return false;
            foreach (var b in card.bindings)
            {
                var k = b.effect != null ? b.effect.values.kind : EffectKind.None;
                if (k != EffectKind.ProjectileBounce && k != EffectKind.FrontmostTarget && k != EffectKind.DamageVsSleeping) return false;
            }
            return true;
        }

        [Test]
        public void CardRangeOf_NeverThrows_ForAnyAuthoredCard()
        {
            var cards = LoadCards();
            var def = Bake(cards);
            for (int c = 0; c < cards.Count; c++)
                Assert.DoesNotThrow(() => CoreCardDragSlot.CardRangeOf(def, c), cards[c].id);
        }
    }
}
