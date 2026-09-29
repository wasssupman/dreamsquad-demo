using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.StatImport;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // dreamcatcher-sheet-sync unit 2 — regression coverage for the DC tab DTOs(flat tabs — cards · skills · configs).
    // 시트-정본 자식 탭 둘은 skill-data-table unit 8 단계 B 에서 은퇴(아래 표시).
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
            const string json = @"[{ ""id"": ""x"", ""axis"": ""Alll"" }]";
            Assert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<DcCardDto[]>(json));
        }

        // -------- flat tabs --------

        [Test]
        public void ApplyCards_UpdatesFieldsAndKeepsOmitted()
        {
            var so = NewCard("ranger_atk");
            so.displayName = "old";
            so.axis = CardTargetAxis.ClassRanger;
            so.description = "keep";
            var payload = new DcSheetPayload
            {
                cards = new[] { new DcCardDto { id = "ranger_atk", displayName = "new", axis = CardTargetAxis.All } },
            };

            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["ranger_atk"] = so });

            Assert.AreEqual("new", so.displayName);
            Assert.AreEqual(CardTargetAxis.All, so.axis);
            Assert.AreEqual("keep", so.description, "omitted column must keep the SO value");
        }

        // -------- dreamcatcher-attach-requirement unit 2: 부착 제한 3열 --------

        [Test]
        public void Deserialize_CardDto_ParsesAttachTypeByName_AndValueAsString()
        {
            const string json = @"[{ ""id"": ""x"", ""attachType"": ""Class"",
                ""attachValue"": ""Guardian"" }]";

            var rows = JsonConvert.DeserializeObject<DcCardDto[]>(json);

            Assert.AreEqual(DcAttachType.Class, rows[0].attachType);
            Assert.AreEqual("Guardian", rows[0].attachValue);
        }

        [Test]
        public void ApplyCards_SetsClassAndUnitIdRestriction()
        {
            var cls = NewCard("c_cls");
            var uid = NewCard("c_uid");
            var payload = new DcSheetPayload
            {
                cards = new[]
                {
                    new DcCardDto { id = "c_cls", attachType = DcAttachType.Class,
                        attachValue = "Guardian" },
                    new DcCardDto { id = "c_uid", attachType = DcAttachType.UnitId,
                        attachValue = "shield_shuttle" },
                },
            };

            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["c_cls"] = cls, ["c_uid"] = uid });

            Assert.AreEqual(DcAttachType.Class, cls.attachType);
            Assert.AreEqual("Guardian", cls.attachValue);
            Assert.AreEqual(DcAttachType.UnitId, uid.attachType);
            Assert.AreEqual("shield_shuttle", uid.attachValue);
        }

        [Test]
        public void ApplyCards_BlankAttachColumns_KeepExistingRestriction()
        {
            var so = NewCard("c");
            so.attachType = DcAttachType.Class;
            so.attachValue = "Guardian";
            var payload = new DcSheetPayload
            {
                // 부착 열 전부 빈 셀(null) — 다른 열만 갱신한다.
                cards = new[] { new DcCardDto { id = "c", displayName = "new" } },
            };

            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["c"] = so });

            Assert.AreEqual("new", so.displayName);
            Assert.AreEqual(DcAttachType.Class, so.attachType, "빈 셀은 blank=keep");
            Assert.AreEqual("Guardian", so.attachValue);
        }

        [Test]
        public void ApplyCards_ExplicitNone_ClearsRestriction()
        {
            // 제한 해제의 **유일한** 수단 — 빈 셀이 아니라 None 명시.
            var so = NewCard("c");
            so.attachType = DcAttachType.UnitId;
            so.attachValue = "shield_shuttle";
            var payload = new DcSheetPayload
            {
                cards = new[] { new DcCardDto { id = "c", attachType = DcAttachType.None } },
            };

            Apply(payload, new Dictionary<string, DreamcatcherCard> { ["c"] = so });

            Assert.AreEqual(DcAttachType.None, so.attachType);
            Assert.AreEqual("shield_shuttle", so.attachValue,
                "kind 가 판별자 — 잔존 unitId 는 inert 이므로 청소하지 않는다");
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

        // skill-data-table unit 8 단계 B — 시트-정본 자식 탭 둘(`DcCardEffects` · `DcAttackMods` — 카드 `effects[]` · `attackMods[]` 재구성)은
        // 은퇴했다. 스쿼드 스탯 효과 · 공격 수식자는 효과 줄 + 소유 줄이다(탭 `Skills` · `SkillOwners` — `SkillSheetImportTests`).

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

    }
}
