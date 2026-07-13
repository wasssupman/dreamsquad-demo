using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.StatImport;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // dreamcatcher-sheet-sync unit 2 — regression coverage for the DC tab DTOs and
    // the two array-sync semantics (sheet-SoT rebuild vs Unity-SoT overlay).
    public class DcSheetImportTests
    {
        private DreamcatcherCard NewCard(string id)
        {
            var so = ScriptableObject.CreateInstance<DreamcatcherCard>();
            so.id = id;
            return so;
        }

        private static string Apply(DcSheetPayload payload,
            Dictionary<string, DreamcatcherCard> cards,
            Dictionary<string, SkillData> skills = null,
            Dictionary<string, ScriptableObject> configs = null,
            StringBuilder log = null)
        {
            return DcSheetApplier.Apply(payload, cards,
                skills ?? new Dictionary<string, SkillData>(),
                configs ?? new Dictionary<string, ScriptableObject>(),
                null, log ?? new StringBuilder());
        }

        // -------- deserialization --------

        [Test]
        public void Deserialize_CardDto_ParsesStringEnumsAndLeavesOmittedNull()
        {
            const string json = @"[{ ""id"": ""poke_needle"", ""type"": ""Unit"",
                ""axis"": ""All"" }]";

            var rows = JsonConvert.DeserializeObject<DcCardDto[]>(json);

            Assert.AreEqual(CardType.Unit, rows[0].type);
            Assert.AreEqual(CardTargetAxis.All, rows[0].axis);
            Assert.IsNull(rows[0].description, "omitted column must stay null");
        }

        [Test]
        public void Deserialize_UnknownEnumMember_Throws()
        {
            const string json = @"[{ ""cardId"": ""x"", ""slot"": 0, ""kind"": ""AttackDamge"" }]";
            Assert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<DcCardEffectDto[]>(json));
        }

        // -------- flat tabs --------

        [Test]
        public void ApplyCards_UpdatesFieldsAndKeepsOmitted()
        {
            var so = NewCard("ranger_atk_10");
            so.displayName = "old";
            so.axis = CardTargetAxis.ClassRanger;
            so.description = "keep";
            var payload = new DcSheetPayload
            {
                cards = new[] { new DcCardDto { id = "ranger_atk_10", displayName = "new", axis = CardTargetAxis.All } },
            };

            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["ranger_atk_10"] = so });

            Assert.AreEqual("new", so.displayName);
            Assert.AreEqual(CardTargetAxis.All, so.axis);
            Assert.AreEqual("keep", so.description, "omitted column must keep the SO value");
        }

        [Test]
        public void ApplyConfigs_UnionRow_TouchesOnlyItsOwnSo()
        {
            var awakening = ScriptableObject.CreateInstance<AwakeningConfig>();
            awakening.id = "awakening_default";
            awakening.costUnit = 15;
            var deckRule = ScriptableObject.CreateInstance<DeckRuleConfig>();
            deckRule.id = "deck_rule_default";
            deckRule.maxSquad = -1;
            var configs = new Dictionary<string, ScriptableObject>
            { ["awakening_default"] = awakening, ["deck_rule_default"] = deckRule };

            var payload = new DcSheetPayload
            {
                configs = new[] { new DcConfigDto { id = "deck_rule_default", maxSquad = 2 } },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard>(), configs: configs);

            Assert.AreEqual(2, deckRule.maxSquad);
            Assert.AreEqual(15, awakening.costUnit, "the other config SO must stay untouched");
        }

        [Test]
        public void ApplySkills_UpdatesBalanceScalars()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.id = "meteor";
            skill.cooldownSec = 18f;
            skill.cost = 4;

            var payload = new DcSheetPayload
            {
                skills = new[] { new DcSkillDto { id = "meteor", cooldownSec = 22f } },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard>(),
                skills: new Dictionary<string, SkillData> { ["meteor"] = skill });

            Assert.AreEqual(22f, skill.cooldownSec);
            Assert.AreEqual(4, skill.cost, "omitted column must keep the SO value");
        }

        // -------- sheet-SoT tabs: effects rebuild --------

        [Test]
        public void RebuildEffects_RowAdded_GrowsArray()
        {
            var so = NewCard("card_a");
            so.effects = new[] { new CardEffect { kind = CardBuffKind.AttackDamage, percent = 10 } };
            var log = new StringBuilder();

            var payload = new DcSheetPayload
            {
                cardEffects = new[]
                {
                    new DcCardEffectDto { cardId = "card_a", slot = 0, percent = 12 },
                    new DcCardEffectDto { cardId = "card_a", slot = 1, kind = CardBuffKind.AttackSpeed, percent = 10 },
                },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["card_a"] = so }, log: log);

            Assert.AreEqual(2, so.effects.Length);
            Assert.AreEqual(CardBuffKind.AttackDamage, so.effects[0].kind, "slot 0 keeps its kind (blank cell)");
            Assert.AreEqual(12f, so.effects[0].percent);
            Assert.AreEqual(CardBuffKind.AttackSpeed, so.effects[1].kind);
            StringAssert.Contains("effects 1→2", log.ToString());
        }

        [Test]
        public void RebuildEffects_RowRemoved_ShrinksArrayAndReports()
        {
            var so = NewCard("card_a");
            so.effects = new[]
            {
                new CardEffect { kind = CardBuffKind.EffectiveHealth, percent = 50 },
                new CardEffect { kind = CardBuffKind.AttackSpeed, percent = -50 },
            };
            var log = new StringBuilder();

            var payload = new DcSheetPayload
            {
                cardEffects = new[] { new DcCardEffectDto { cardId = "card_a", slot = 0 } },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["card_a"] = so }, log: log);

            Assert.AreEqual(1, so.effects.Length);
            Assert.AreEqual(CardBuffKind.EffectiveHealth, so.effects[0].kind);
            StringAssert.Contains("effects 2→1", log.ToString());
        }

        [Test]
        public void RebuildEffects_CardAbsentFromTab_KeepsArray()
        {
            var touched = NewCard("card_a");
            touched.effects = new CardEffect[0];
            var untouched = NewCard("card_b");
            untouched.effects = new[] { new CardEffect { kind = CardBuffKind.MoveSpeed, percent = 10 } };
            var cards = new Dictionary<string, DreamcatcherCard>
            { ["card_a"] = touched, ["card_b"] = untouched };

            var payload = new DcSheetPayload
            {
                cardEffects = new[]
                {
                    new DcCardEffectDto { cardId = "card_a", slot = 0, kind = CardBuffKind.AttackDamage, percent = 5 },
                },
            };
            Apply(payload, cards);

            Assert.AreEqual(1, untouched.effects.Length, "cards absent from the tab keep their arrays");
            Assert.AreEqual(1, touched.effects.Length);
        }

        [Test]
        public void RebuildEffects_DuplicateSlot_SkipsWholeCard()
        {
            var so = NewCard("card_a");
            so.effects = new[] { new CardEffect { kind = CardBuffKind.AttackDamage, percent = 10 } };
            var log = new StringBuilder();

            var payload = new DcSheetPayload
            {
                cardEffects = new[]
                {
                    new DcCardEffectDto { cardId = "card_a", slot = 0, percent = 1 },
                    new DcCardEffectDto { cardId = "card_a", slot = 0, percent = 2 },
                },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["card_a"] = so }, log: log);

            Assert.AreEqual(10f, so.effects[0].percent, "duplicate slots must poison the whole card");
            StringAssert.Contains("duplicate slots", log.ToString());
        }

        [Test]
        public void RebuildEffects_NewSlotWithoutKind_SkipsWholeCard()
        {
            var so = NewCard("card_a");
            so.effects = new CardEffect[0];

            var payload = new DcSheetPayload
            {
                cardEffects = new[] { new DcCardEffectDto { cardId = "card_a", slot = 0, percent = 5 } },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["card_a"] = so });

            Assert.AreEqual(0, so.effects.Length, "a new row without kind must not fabricate an effect");
        }

        [Test]
        public void RebuildAttackMods_RowAdded_GrowsArray()
        {
            var so = NewCard("bouncy_bead");
            so.attackMods = new DcAttackModSpec[0];

            var payload = new DcSheetPayload
            {
                attackMods = new[]
                {
                    new DcAttackModDto { cardId = "bouncy_bead", slot = 0, kind = DcAttackModKind.ProjectileBounce, count = 2, tileRange = 3, damageMul = 1f },
                },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["bouncy_bead"] = so });

            Assert.AreEqual(1, so.attackMods.Length);
            Assert.AreEqual(2, so.attackMods[0].count);
        }

        // -------- Unity-SoT tab: mechanics overlay --------

        private DreamcatcherCard NewMechanicCard(string id, ProjectileData projectile)
        {
            var so = NewCard(id);
            so.mechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.AttackN, period = 5 },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.ProjectileToTarget, magnitude = 20, projectile = projectile,
                    },
                },
            };
            return so;
        }

        [Test]
        public void OverlayMechanics_UpdatesValuesAndPreservesProjectileRef()
        {
            var projectile = ScriptableObject.CreateInstance<ProjectileData>();
            var so = NewMechanicCard("poke_needle", projectile);

            var payload = new DcSheetPayload
            {
                mechanics = new[]
                {
                    new DcMechanicDto { cardId = "poke_needle", slot = 0, triggerPeriod = 4, magnitude = 25 },
                },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["poke_needle"] = so });

            Assert.AreEqual(4, so.mechanics[0].trigger.period);
            Assert.AreEqual(25f, so.mechanics[0].payload.magnitude);
            Assert.AreEqual(DcTriggerKind.AttackN, so.mechanics[0].trigger.kind, "omitted column keeps value");
            Assert.AreSame(projectile, so.mechanics[0].payload.projectile, "asset ref must survive the overlay");
        }

        // unit 7 — Spec A/B 신필드(triggerFraction/ccKind/stackKind/buffStat) overlay 라운드트립.
        [Test]
        public void OverlayMechanics_AppliesSpecABNewFields()
        {
            var so = NewMechanicCard("last_stand", null);
            var payload = new DcSheetPayload
            {
                mechanics = new[]
                {
                    new DcMechanicDto
                    {
                        cardId = "last_stand", slot = 0,
                        triggerKind = DcTriggerKind.HealthThreshold, triggerFraction = 0.7f,
                        payloadKind = DcPayloadKind.SelfStatBuff, buffStat = CardBuffKind.AttackDamage,
                        ccKind = DcCcKind.Stun, stackKind = DcStackKind.Bleed,
                    },
                },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["last_stand"] = so });

            Assert.AreEqual(0.7f, so.mechanics[0].trigger.fraction, "triggerFraction overlaid");
            Assert.AreEqual(CardBuffKind.AttackDamage, so.mechanics[0].payload.buffStat, "buffStat overlaid");
            Assert.AreEqual(DcCcKind.Stun, so.mechanics[0].payload.ccKind, "ccKind overlaid");
            Assert.AreEqual(DcStackKind.Bleed, so.mechanics[0].payload.stackKind, "stackKind overlaid");
        }

        // 신필드 omit(null) 시 기존 SO 값 유지 — partial-update 컨벤션 가드.
        [Test]
        public void OverlayMechanics_OmittedNewFields_KeepExistingValues()
        {
            var so = NewMechanicCard("last_stand", null);
            so.mechanics[0].trigger.fraction = 0.5f;
            so.mechanics[0].payload.buffStat = CardBuffKind.AttackSpeed;
            so.mechanics[0].payload.ccKind = DcCcKind.Impulse;
            so.mechanics[0].payload.stackKind = DcStackKind.Poison;

            var payload = new DcSheetPayload
            {
                mechanics = new[] { new DcMechanicDto { cardId = "last_stand", slot = 0, magnitude = 12 } },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["last_stand"] = so });

            Assert.AreEqual(12f, so.mechanics[0].payload.magnitude);
            Assert.AreEqual(0.5f, so.mechanics[0].trigger.fraction, "omitted triggerFraction keeps value");
            Assert.AreEqual(CardBuffKind.AttackSpeed, so.mechanics[0].payload.buffStat, "omitted buffStat keeps value");
            Assert.AreEqual(DcCcKind.Impulse, so.mechanics[0].payload.ccKind, "omitted ccKind keeps value");
            Assert.AreEqual(DcStackKind.Poison, so.mechanics[0].payload.stackKind, "omitted stackKind keeps value");
        }

        [Test]
        public void OverlayMechanics_SlotOutOfRange_SkipsAndReports()
        {
            var so = NewMechanicCard("poke_needle", null);
            var log = new StringBuilder();

            var payload = new DcSheetPayload
            {
                mechanics = new[] { new DcMechanicDto { cardId = "poke_needle", slot = 1, magnitude = 99 } },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["poke_needle"] = so }, log: log);

            Assert.AreEqual(1, so.mechanics.Length);
            Assert.AreEqual(20f, so.mechanics[0].payload.magnitude);
            StringAssert.Contains("out of range", log.ToString());
        }

        // ---- unit 6 (review fixes) — edge cases from the two-track review ----

        [Test]
        public void RebuildEffects_NegativeSlot_SkipsCardWithoutThrowing()
        {
            var so = NewCard("card_a");
            so.effects = new[] { new CardEffect { kind = CardBuffKind.AttackDamage, percent = 10 } };
            var log = new StringBuilder();

            var payload = new DcSheetPayload
            {
                cardEffects = new[] { new DcCardEffectDto { cardId = "card_a", slot = -1, percent = 99 } },
            };
            Assert.DoesNotThrow(() =>
                Apply(payload, new Dictionary<string, DreamcatcherCard> { ["card_a"] = so }, log: log));

            Assert.AreEqual(10f, so.effects[0].percent, "negative slot must poison the card, not crash");
            StringAssert.Contains("without valid slot", log.ToString());
        }

        [Test]
        public void RebuildEffects_NullSlot_SkipsCard()
        {
            var so = NewCard("card_a");
            so.effects = new[] { new CardEffect { kind = CardBuffKind.AttackDamage, percent = 10 } };

            var payload = new DcSheetPayload
            {
                cardEffects = new[] { new DcCardEffectDto { cardId = "card_a", percent = 99 } },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["card_a"] = so });

            Assert.AreEqual(10f, so.effects[0].percent);
        }

        // Pins the reorder semantics (review M1): a blank cell inherits the old
        // entry AT THAT SLOT NUMBER, and the rebuilt array is slot-ordered rows —
        // renumbering rows while leaving cells blank moves values by slot label.
        [Test]
        public void RebuildEffects_SlotGap_BlankCellsInheritBySlotNumber()
        {
            var so = NewCard("card_a");
            so.effects = new[]
            {
                new CardEffect { kind = CardBuffKind.AttackDamage, percent = 10 },
                new CardEffect { kind = CardBuffKind.AttackSpeed, percent = 20 },
            };

            var payload = new DcSheetPayload
            {
                cardEffects = new[] { new DcCardEffectDto { cardId = "card_a", slot = 1 } }, // blank kind/percent
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["card_a"] = so });

            Assert.AreEqual(1, so.effects.Length);
            Assert.AreEqual(CardBuffKind.AttackSpeed, so.effects[0].kind, "blank cells inherit old[slot], not old[position]");
            Assert.AreEqual(20f, so.effects[0].percent);
        }

        [Test]
        public void RebuildAttackMods_RowRemoved_ShrinksAndReports()
        {
            var so = NewCard("bouncy_bead");
            so.attackMods = new[]
            {
                new DcAttackModSpec { kind = DcAttackModKind.ProjectileBounce, count = 2 },
                new DcAttackModSpec { kind = DcAttackModKind.ProjectileBounce, count = 3 },
            };
            var log = new StringBuilder();

            var payload = new DcSheetPayload
            {
                attackMods = new[] { new DcAttackModDto { cardId = "bouncy_bead", slot = 0 } },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["bouncy_bead"] = so }, log: log);

            Assert.AreEqual(1, so.attackMods.Length);
            Assert.AreEqual(2, so.attackMods[0].count);
            StringAssert.Contains("attackMods 2→1", log.ToString());
        }

        [Test]
        public void OverlayMechanics_DuplicateRow_AppliesFirstSkipsRest()
        {
            var so = NewMechanicCard("poke_needle", null);
            var log = new StringBuilder();

            var payload = new DcSheetPayload
            {
                mechanics = new[]
                {
                    new DcMechanicDto { cardId = "poke_needle", slot = 0, magnitude = 30 },
                    new DcMechanicDto { cardId = "poke_needle", slot = 0, magnitude = 40 },
                },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["poke_needle"] = so }, log: log);

            Assert.AreEqual(30f, so.mechanics[0].payload.magnitude, "first row applies, duplicates skip");
            StringAssert.Contains("duplicate row", log.ToString());
        }

        [Test]
        public void ChildTab_UnknownCardId_ReportsNoMatch()
        {
            var log = new StringBuilder();
            var payload = new DcSheetPayload
            {
                cardEffects = new[] { new DcCardEffectDto { cardId = "ghost", slot = 0, percent = 1 } },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard>(), log: log);

            StringAssert.Contains("no match for cardId='ghost'", log.ToString());
        }

        [Test]
        public void Apply_EmptyPayload_ReportsZeroCounts()
        {
            var log = new StringBuilder();
            Apply(new DcSheetPayload(), new Dictionary<string, DreamcatcherCard>(), log: log);
            StringAssert.Contains("Matched 0, unmatched 0", log.ToString());
        }

        // review 3b — a renamed column must be reported, or edits in it vanish
        // silently under the "blank cell = keep" rule. `_` columns stay exempt.
        [Test]
        public void ParseSheetLogged_UnknownHeader_IsReportedAndUnderscoreIsNot()
        {
            const string body = @"{ ""success"": true, ""data"": [
                { ""id"": ""x"", ""displayNam"": ""oops"", ""_memo"": ""y"" }
            ] }";
            var log = new StringBuilder();

            var rows = SheetEnvelopeParser.ParseSheetLogged<DcCardDto>(body, null, "DcCards", log);

            Assert.AreEqual(1, rows.Length);
            StringAssert.Contains("displayNam", log.ToString());
            StringAssert.DoesNotContain("_memo", log.ToString());
        }

        // dreamcatcher-sheet-sync unit 4 — awakeningReward rides the existing
        // reflection contract both ways (new column = one DTO field).
        [Test]
        public void AwakeningReward_RoundTripsThroughUnitDtos()
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            so.awakeningReward = 7;

            var exported = new DefenderStatDto();
            UnitStatFieldMapper.ReadFieldsToDto(so, exported);
            Assert.AreEqual(7, exported.awakeningReward, "export must read the SO value");

            UnitStatFieldMapper.ApplyNonNullFields(new DefenderStatDto { awakeningReward = 9 }, so);
            Assert.AreEqual(9, so.awakeningReward, "import must write the sheet value");
        }

        [Test]
        public void OverlayMechanics_ProjectileToTargetWithoutRef_Warns()
        {
            var so = NewMechanicCard("poke_needle", null);
            var log = new StringBuilder();

            var payload = new DcSheetPayload
            {
                mechanics = new[] { new DcMechanicDto { cardId = "poke_needle", slot = 0, magnitude = 25 } },
            };
            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["poke_needle"] = so }, log: log);

            Assert.AreEqual(25f, so.mechanics[0].payload.magnitude, "warning must not block the apply");
            StringAssert.Contains("projectile is unassigned", log.ToString());
        }
    }
}
