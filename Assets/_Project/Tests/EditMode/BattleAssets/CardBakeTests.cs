using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Trigger;
using Somnia.Battle.BattleCoreUnity;
using Somnia.Battle.Data;

namespace Somnia.Battle.Tests.EditModeAssets
{
    // battle-core-rebuild unit 7b — **라이브 카드 전량**이 정의표로 구워지는가(`CardDefinitionBuilder`).
    //
    // 값은 에셋에서 읽어 대조한다 — 밸런스 수치를 리터럴로 박지 않는다(test-procedure 규율). 여기서 보는 것은 **모양**이다:
    // 규칙 0 줄 카드가 없고, 배치 오라가 둘로 펴지고, 표식이 적 전용이고, 액티브가 실행자를 든다.
    public class CardBakeTests
    {
        private const string CardsRoot = "Assets/_Project/Runtime/Battle/Data/Dreamcatcher";

        private static List<DreamcatcherCard> LoadCards()
        {
            var result = new List<DreamcatcherCard>();
            foreach (var guid in AssetDatabase.FindAssets("t:DreamcatcherCard", new[] { CardsRoot }))
            {
                var c = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null) result.Add(c);
            }
            Assert.IsNotEmpty(result);
            return result;
        }

        private static AwakeningConfig Awakening()
        {
            var guids = AssetDatabase.FindAssets("t:AwakeningConfig");
            Assert.IsNotEmpty(guids);
            return AssetDatabase.LoadAssetAtPath<AwakeningConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static (MatchDefinition def, List<DreamcatcherCard> cards) Bake()
        {
            var cards = LoadCards();
            var def = new MatchDefinition();
            CardDefinitionBuilder.Fill(def, new CardAuthoring { Cards = cards, Awakening = Awakening() },
                                       new List<ProjectileData>(), new List<ProjectilePatternData>(),
                                       CardDefinitionBuilder.WithCardHazards(null, cards));
            Assert.AreEqual(cards.Count, def.Cards.Length, "덱 순서 = 카드 줄 순서");
            return (def, cards);
        }

        [Test]
        public void 라이브_카드는_전부_무언가를_싣는다()
        {
            var (def, cards) = Bake();
            var awakening = Awakening();
            for (int i = 0; i < cards.Count; i++)
            {
                ref var c = ref def.Cards[i];
                Assert.AreEqual(awakening.CostFor(cards[i].type), c.Cost, cards[i].id + " — 값은 카드 종류별 저작");
                if (cards[i].type == CardType.Active)
                {
                    Assert.AreEqual(CardKind.Active, c.Kind);
                    Assert.GreaterOrEqual(c.ActiveBinding, 0, cards[i].id);
                    Assert.IsNotNull(def.Bindings[c.ActiveBinding].Skill, cards[i].id + " — 실행자");
                    Assert.AreEqual(cards[i].skill.NeedsTwoTiles, c.NeedsTwoCells, cards[i].id);
                    continue;
                }
                bool any = (c.Bindings?.Length ?? 0) + (c.SquadBindings?.Length ?? 0) + (c.AttackMods?.Length ?? 0) > 0
                           || c.DeclaresRetireRecall;
                Assert.IsTrue(any, cards[i].id + " — 규칙 0 줄 카드는 어떤 유닛에도 안 붙는다");
                foreach (int r in c.Bindings ?? System.Array.Empty<int>())
                {
                    Assert.AreEqual(BindingOrigin.Card, def.Bindings[r].Origin);
                    Assert.IsNotNull(def.Bindings[r].Skill, def.Bindings[r].Label);
                }
            }
        }

        [Test]
        public void 배치_오라는_규칙_둘로_펴지고_회수가_비대칭이다()
        {
            var (def, cards) = Bake();
            int found = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].RuleView() == null) continue;
                bool aura = false;
                foreach (var m in cards[i].RuleView()) if (m.payload.kind == EffectKind.PlacementAura) aura = true;
                if (!aura) continue;
                found++;
                var rows = def.Cards[i].Bindings;
                Assert.GreaterOrEqual(rows.Length, 2, cards[i].id);
                var speed = def.Bindings[rows[0]];
                var sleep = def.Bindings[rows[1]];
                Assert.AreEqual(BindingSubject.Any, speed.Subject);
                Assert.AreEqual(TriggerKind.OnPlace, speed.Trigger);
                Assert.IsTrue(speed.RevokeOnExpire, "공속 — 소급 중화");
                Assert.IsFalse(sleep.RevokeOnExpire, "수면 — 등록부에서만 빠진다");
                Assert.IsInstanceOf<PlacementSleepSkill>(sleep.Skill);
            }
            Assert.Greater(found, 0, "라이브에 배치 오라 카드가 있다(느린 각성)");
        }

        [Test]
        public void 표식_카드는_적_전용이고_표식_규칙만_싣는다()
        {
            var (def, cards) = Bake();
            int found = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (!cards[i].HasBountyMark()) { Assert.IsFalse(def.Cards[i].TargetsEnemies, cards[i].id); continue; }
                found++;
                Assert.IsTrue(def.Cards[i].TargetsEnemies);
                foreach (int r in def.Cards[i].Bindings)
                {
                    Assert.AreEqual(EffectKind.BountyMark, def.EffectOf(in def.Bindings[r]).Kind);
                    Assert.AreEqual(1, def.Bindings[r].FireCap, "fireCap 1 — 수명은 소유자 소멸(다른 축)");
                    Assert.Greater(def.EffectOf(in def.Bindings[r]).Magnitude, 1f);
                }
            }
            Assert.Greater(found, 0, "라이브에 표식 카드가 있다(살찌운 제물)");
        }

        [Test]
        public void 인수인계_선언은_카드_줄_한_칸이다()
        {
            var (def, cards) = Bake();
            for (int i = 0; i < cards.Count; i++)
            {
                bool declares = false;
                if (cards[i].type == CardType.Unit && cards[i].RuleView() != null)
                    foreach (var m in cards[i].RuleView())
                        if (m.payload.kind == EffectKind.RecallAttachedToFront && m.trigger.kind == TriggerKind.OnRetire)
                            declares = true;
                Assert.AreEqual(declares, def.Cards[i].DeclaresRetireRecall, cards[i].id + " — 옛 `DeclaresRetireRecall` 과 같은 판정");
            }
        }

        [Test]
        public void 드림스톤은_판_규칙_줄이고_코스트_돌은_재생_배율이다()
        {
            var stones = new List<DreamstoneData>();
            foreach (var guid in AssetDatabase.FindAssets("t:DreamstoneData"))
            {
                var s = AssetDatabase.LoadAssetAtPath<DreamstoneData>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null) stones.Add(s);
            }
            Assert.IsNotEmpty(stones);
            var def = new MatchDefinition();
            CardDefinitionBuilder.Fill(def, new CardAuthoring { Dreamstones = stones },
                                       new List<ProjectileData>(), new List<ProjectilePatternData>(), null);
            int stat = 0; float sum = 0f;
            foreach (var s in stones)
                if (s.effect.kind == CardBuffKind.CostRate) sum += s.effect.percent; else stat++;
            Assert.AreEqual(stat, def.MatchBindings.Length);
            Assert.AreEqual(1f + sum / 100f, CardDefinitionBuilder.CostRateOf(stones), 1e-5f);
            foreach (int r in def.MatchBindings)
                Assert.IsInstanceOf<DreamstoneStatSkill>(def.Bindings[r].Skill, "출처가 드림스톤 — 강화 오라가 안 켜진다");
        }
    }
}
