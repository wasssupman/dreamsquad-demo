using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;
using Wassup.BattleCoreUnity;
using Wassup.Data;
using Wassup.Data.StatImport;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // skill-data-table unit 9 완료 기준 — **액티브 쿨다운 = 한 원천**: 시트 `Cards.cooldown_sec` 를 고치면 굽기 값(`CardDef.CooldownSeconds`)과
    // 카드 문안의 「재사용」이 같이 바뀐다(옛 `DcSkills.cooldownSec` 은 문안만 움직이던 두 번째 원천 — 은퇴). 합성 SO · 디스크 0 · 네트워크 0.
    public class CardsTabCooldownTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }

        private DreamcatcherCard RapidFireCard(float cooldownSec)
        {
            var effect = Make<EffectData>();
            effect.id = "fixture_rapid_fire";
            effect.values = new EffectValues { kind = EffectKind.ActiveRapidFire, mul = 2f, radiusTiles = 1, durationSec = 6f };
            var card = Make<DreamcatcherCard>();
            card.id = "fixture_active_rapid_fire";
            card.type = CardType.Active;
            card.cooldownSec = cooldownSec;
            card.bindings = new[] { new BindingSpec { trigger = new TriggerSpec { kind = TriggerKind.Cast }, fireCap = 1, effect = effect } };
            return card;
        }

        private MatchDefinition Bake(DreamcatcherCard card)
        {
            var awakening = Make<AwakeningConfig>();
            var def = new MatchDefinition();
            CardDefinitionBuilder.Fill(def, new CardAuthoring { Cards = new List<DreamcatcherCard> { card }, Awakening = awakening },
                                       new List<ProjectileData>(), new List<ProjectilePatternData>(), System.Array.Empty<HazardSO>());
            return def;
        }

        [Test]
        public void CardsTab_CooldownSec_MovesBakedCooldown_AndCardText()
        {
            var card = RapidFireCard(25f);
            Assert.AreEqual(25f, Bake(card).Cards[0].CooldownSeconds);
            StringAssert.Contains("재사용 25초", DreamcatcherCardText.Body(card));

            const string body = @"{ ""success"": true, ""data"": [ { ""id"": ""fixture_active_rapid_fire"", ""cooldown_sec"": 12.5 } ] }";
            var log = new StringBuilder();
            var rows = SheetEnvelopeParser.ParseSheetLogged<DcCardDto>(body, null, DcSheetTabs.Cards, log);
            DcSheetApplier.Apply(new DcSheetPayload { cards = rows },
                new Dictionary<string, DreamcatcherCard> { [card.id] = card }, new Dictionary<string, SkillData>(),
                new Dictionary<string, ScriptableObject>(), null, log);

            Assert.AreEqual(12.5f, card.cooldownSec, log.ToString());
            Assert.AreEqual(12.5f, Bake(card).Cards[0].CooldownSeconds, "굽기 = 카드 칸");
            StringAssert.Contains("재사용 12.5초", DreamcatcherCardText.Body(card), "문안 = 같은 카드 칸(한 원천)");
            StringAssert.DoesNotContain("재사용 25초", DreamcatcherCardText.Body(card));
        }

        [Test]
        public void CardsTab_NeedsTwoTiles_And_HostKinds_Bake()
        {
            var card = RapidFireCard(10f);
            const string body = @"{ ""success"": true, ""data"": [ { ""id"": ""fixture_active_rapid_fire"", ""needs_two_tiles"": true } ] }";
            var log = new StringBuilder();
            DcSheetApplier.Apply(new DcSheetPayload { cards = SheetEnvelopeParser.ParseSheetLogged<DcCardDto>(body, null, DcSheetTabs.Cards, log) },
                new Dictionary<string, DreamcatcherCard> { [card.id] = card }, new Dictionary<string, SkillData>(),
                new Dictionary<string, ScriptableObject>(), null, log);
            Assert.IsTrue(Bake(card).Cards[0].NeedsTwoCells, "두 칸 조준 = 카드 칸");

            var unit = Make<DreamcatcherCard>();
            unit.id = "fixture_unit";
            unit.type = CardType.Unit;
            var mark = Make<EffectData>();
            mark.id = "fixture_mark";
            mark.values = new EffectValues { kind = EffectKind.BountyMark, mul = 2f, percent = 10f };
            unit.bindings = new[] { new BindingSpec { trigger = new TriggerSpec { kind = TriggerKind.None }, effect = mark } };
            const string hosts = @"{ ""success"": true, ""data"": [ { ""id"": ""fixture_unit"", ""host_kinds"": ""Enemy"" } ] }";
            DcSheetApplier.Apply(new DcSheetPayload { cards = SheetEnvelopeParser.ParseSheetLogged<DcCardDto>(hosts, null, DcSheetTabs.Cards, log) },
                new Dictionary<string, DreamcatcherCard> { [unit.id] = unit }, new Dictionary<string, SkillData>(),
                new Dictionary<string, ScriptableObject>(), null, log);
            Assert.AreEqual(HostKinds.Enemy, unit.hostKinds);
            Assert.IsTrue(Bake(unit).Cards[0].TargetsEnemies, "숙주 종류 = 카드 칸(적 표식 카드)");
        }
    }
}
