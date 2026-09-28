using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7b — 카드 규칙 테스트의 공용 고정구.
    //
    // ⚠ 여기 수치는 **게임 값이 아니라 픽스처**다. 라이브 카드의 값은 SO → `CardDefinitionBuilder` 가 싣고,
    // 그 경로는 Assets lane(`CardBakeTests`)이 본다.
    public static class CoreCardFixtures
    {
        /// <summary>카드 규칙 한 줄(출처 = 카드). 실행자는 라우팅 표가 고른다.</summary>
        public static RuleRow CardRule(TriggerKind trigger, EffectKind payload)
        {
            var d = CoreTriggerFixtures.Rule(trigger, payload);
            d.Rule.Origin = BindingOrigin.Card;
            return d;
        }

        /// <summary>실행자를 직접 주는 카드 규칙(탐침 · 코어 concrete).</summary>
        public static RuleRow CardProbe(TriggerKind trigger, ISkill skill)
        {
            var d = CoreTriggerFixtures.Probe(trigger, skill);
            d.Rule.Origin = BindingOrigin.Card;
            return d;
        }

        /// <summary>규칙 줄을 표에 더하고 그 카드를 카드 표 끝에 붙인다. 반환 = 카드 줄.</summary>
        public static int AddAttachCard(MatchDefinition def, string id, int cost, params RuleRow[] rules)
        {
            var card = CardDef.Default();
            card.Id = id;
            card.Kind = CardKind.Attach;
            card.Cost = cost;
            card.Bindings = CoreTriggerFixtures.Add(def, rules);
            return AddCard(def, card);
        }

        public static int AddCard(MatchDefinition def, CardDef card)
        {
            var list = new List<CardDef>(def.Cards) { card };
            def.Cards = list.ToArray();
            return list.Count - 1;
        }

        /// <summary>액티브 카드 — `trigger None` 규칙 한 줄을 시전한다.</summary>
        public static int AddActiveCard(MatchDefinition def, string id, int cost, float cooldown, RuleRow rule,
                                        bool twoCells = false)
        {
            var card = CardDef.Default();
            card.Id = id;
            card.Kind = CardKind.Active;
            card.Cost = cost;
            card.CooldownSeconds = cooldown;
            card.NeedsTwoCells = twoCells;
            rule.Rule.Origin = BindingOrigin.Card;
            card.ActiveBinding = CoreTriggerFixtures.Add(def, rule)[0];
            return AddCard(def, card);
        }

        /// <summary>Squad 카드 — 축(직업 비트 · 코스트) 스탯 한 줄(배율 · 영구 · 소급 회수).</summary>
        public static int AddSquadCard(MatchDefinition def, string id, int cost, SkillStatKind stat, float mul,
                                       int classMask = 0, int subjectCost = 0)
        {
            var rule = CardRule(TriggerKind.OnPlace, EffectKind.SelfStatBuff);
            rule.Rule.Subject = BindingSubject.Any;
            rule.Rule.SubjectClassMask = classMask;
            rule.Rule.SubjectCost = subjectCost;
            rule.Effect.StatKind = (int)stat;
            rule.Effect.Magnitude = mul;
            rule.Rule.RevokeOnExpire = true;
            var card = CardDef.Default();
            card.Id = id;
            card.Kind = CardKind.Attach;
            card.Cost = cost;
            card.SquadBindings = CoreTriggerFixtures.Add(def, rule);
            return AddCard(def, card);
        }

        /// <summary>판 전투를 연다(각성 넉넉히 · 손패 = 덱 전부).</summary>
        public static BattleMatch CardBattle(MatchDefinition def, float awakening = 100f)
        {
            def.Mode.Awakening = new AwakeningDef { Start = awakening, Max = 100f };
            def.Mode.HandSize = math.max(1, def.Cards.Length);
            def.ConfigHash = def.ComputeConfigHash();
            return CoreMatchFixtures.BeginBattle(def);
        }

        public static int EntryOf(BattleMatch m, int cardIndex)
        {
            var hand = new List<HandDeck.Entry>();
            m.Hand.Hand(hand);
            foreach (var e in hand) if (e.CardIndex == cardIndex) return e.EntryId;
            return -1;
        }

        public static List<HandDeck.Entry> Hand(BattleMatch m)
        {
            var hand = new List<HandDeck.Entry>();
            m.Hand.Hand(hand);
            return hand;
        }

        /// <summary>방어유닛을 **배치 없이** 세운다(활성 상태 — 배치 사건 없음).</summary>
        public static Unit Defender(BattleMatch m, int2 cell, int defIndex = 0)
        {
            m.Apply(Command.DebugSpawnDefender(defIndex, cell));
            var u = m.World.Units;
            return u[u.Count - 1];
        }
    }
}
