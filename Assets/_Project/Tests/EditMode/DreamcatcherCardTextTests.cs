using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    public class DreamcatcherCardTextTests
    {
        private readonly List<Object> _cleanup = new List<Object>();

        private DreamcatcherCard Card(CardType type, CardTargetAxis axis = CardTargetAxis.All,
            CardEffect[] effects = null, string description = "")
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.type = type;
            card.axis = axis;
            card.effects = effects;
            card.description = description;
            _cleanup.Add(card);
            return card;
        }

        private SkillData Skill(SkillEffectType effect, float magnitude, float duration,
            float cooldown, int cost)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.effect = effect;
            skill.magnitude = magnitude;
            skill.durationSec = duration;
            skill.cooldownSec = cooldown;
            skill.cost = cost;
            _cleanup.Add(skill);
            return skill;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _cleanup)
                if (obj != null) Object.DestroyImmediate(obj);
            _cleanup.Clear();
        }

        [Test]
        public void SquadEffects_UseKoreanTargetAndStandardTerms()
        {
            var card = Card(CardType.Squad, CardTargetAxis.ClassRanger, new[]
            {
                new CardEffect { kind = CardBuffKind.AttackDamage, percent = 20f },
                new CardEffect { kind = CardBuffKind.MoveSpeed, percent = -10f },
            });

            StringAssert.Contains(
                "항상 → 레인저 아군 공격력 +20% · 레인저 아군 이동 속도 -10%",
                DreamcatcherCardText.Body(card));
            StringAssert.DoesNotContain("Attack", DreamcatcherCardText.Body(card));
            StringAssert.DoesNotContain("RANGER", DreamcatcherCardText.Body(card));
        }

        [Test]
        public void SquadEffects_UseAlwaysTrigger()
        {
            var card = Card(CardType.Squad, CardTargetAxis.All, new[]
            {
                new CardEffect { kind = CardBuffKind.AttackDamage, percent = 70f },
                new CardEffect { kind = CardBuffKind.EffectiveHealth, percent = -40f },
            });
            StringAssert.Contains(
                "항상 → 모든 아군 공격력 +70% · 모든 아군 체력 -40%",
                DreamcatcherCardText.Body(card));
        }

        [Test]
        public void BodyCompact_ChangesOnlyBlockSpacing()
        {
            var card = Card(CardType.Squad, CardTargetAxis.ClassRanger, new[]
            {
                new CardEffect { kind = CardBuffKind.AttackDamage, percent = 20f },
            });

            Assert.AreEqual(
                DreamcatcherCardText.Body(card).Replace("\n\n", "\n").Replace("<size=22>", "<size=115%>"),
                DreamcatcherCardText.BodyCompact(card));
        }

        [Test]
        public void UnitMechanic_FormatsTriggerPayloadAndNumbers()
        {
            var card = Card(CardType.Unit);
            card.mechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.AttackN, period = 3 },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.ApplyStackToTarget,
                        magnitude = 1f,
                        duration = 4f,
                        stackKind = DcStackKind.Bleed,
                    },
                },
            };

            StringAssert.Contains(
                "3번째 공격마다 → 대상에게 출혈 1스택 · 4초",
                DreamcatcherCardText.Body(card));
        }

        [Test]
        public void UnitMechanic_FormatsImpulseDuration()
        {
            var card = Card(CardType.Unit);
            card.mechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.AttackN, period = 3 },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.ApplyCcToTarget,
                        ccKind = DcCcKind.Impulse,
                        magnitude = 4f,
                        duration = 0.5f,
                    },
                },
            };

            StringAssert.Contains(
                "3번째 공격마다 → 대상에게 넉백 속도 4 · 0.5초",
                DreamcatcherCardText.Body(card));
        }

        [Test]
        public void UnitMechanic_FormatsSleepWithWakeNote()
        {
            var card = Card(CardType.Unit);
            card.mechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.AttackN, period = 5 },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.ApplyCcToTarget,
                        ccKind = DcCcKind.Sleep,
                        duration = 2.5f,
                    },
                },
            };

            StringAssert.Contains(
                "5번째 공격마다 → 대상에게 수면 2.5초 (피격 시 해제)",
                DreamcatcherCardText.Body(card));
        }

        // dreamcatcher-trigger-gates unit 1 — 게이트 접두 문안 (배선 조합 골든).
        [Test]
        public void UnitMechanic_FormatsGatePrefix_ForWiredCombos()
        {
            var card = Card(CardType.Unit);
            card.mechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec
                    {
                        kind = DcTriggerKind.AttackN, period = 1,
                        gate = DcGateKind.HpBelow, gateSubject = DcGateSubject.EventTarget, gateValue = 0.25f,
                    },
                    payload = new DcPayloadSpec { kind = DcPayloadKind.HeavyStrike, magnitude = 2f },
                },
                new DcMechanic
                {
                    trigger = new DcTriggerSpec
                    {
                        kind = DcTriggerKind.OnDamagedN, period = 2,
                        gate = DcGateKind.HpBelow, gateSubject = DcGateSubject.Self, gateValue = 0.30f,
                    },
                    payload = new DcPayloadSpec { kind = DcPayloadKind.SelfTileAoe, magnitude = 20f, tileRange = 1 },
                },
            };

            string body = DreamcatcherCardText.Body(card);
            StringAssert.Contains("HP 25% 이하인 적에게 공격마다 → 피해 x2", body);
            StringAssert.Contains("HP 30% 이하일 때 2번째 피격마다 → 반경 1칸 피해 20", body);
        }

        [Test]
        public void UnitMechanic_UnsupportedGateCombo_UsesDescriptionFallback()
        {
            var card = Card(CardType.Unit, description: "미지원 게이트 설명");
            card.mechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec
                    {
                        kind = DcTriggerKind.OnDamagedN, period = 2,
                        gate = DcGateKind.HpBelow, gateSubject = DcGateSubject.EventTarget, gateValue = 0.30f,
                    },
                    payload = new DcPayloadSpec { kind = DcPayloadKind.SelfTileAoe, magnitude = 20f, tileRange = 1 },
                },
            };

            string body = DreamcatcherCardText.Body(card);
            StringAssert.Contains("미지원 게이트 설명", body);
            StringAssert.DoesNotContain("2번째 피격마다", body);
        }

        [Test]
        public void UnitMechanic_FormatsThresholdAndPermanentEffect()
        {
            var card = Card(CardType.Unit);
            card.mechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec
                    {
                        kind = DcTriggerKind.HealthThreshold,
                        fraction = 0.7f,
                    },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.SelfStatBuff,
                        magnitude = 30f,
                        buffStat = CardBuffKind.AttackDamage,
                    },
                },
            };

            StringAssert.Contains(
                "HP 30% 이하 → 공격력 +30% · 전투 중 1회",
                DreamcatcherCardText.Body(card));
        }

        [Test]
        public void UnitAttackMod_FormatsBounceValues()
        {
            var card = Card(CardType.Unit);
            card.attackMods = new[]
            {
                new DcAttackModSpec
                {
                    kind = DcAttackModKind.ProjectileBounce,
                    count = 2,
                    tileRange = 3,
                    damageMul = 1f,
                },
            };

            StringAssert.Contains(
                "항상 → 공격 투사체가 최대 3칸 범위 내 2회 튕김 (감쇠 없음)",
                DreamcatcherCardText.Body(card));
        }

        [Test]
        public void ActiveSkill_FormatsMultiplierDurationCostAndCooldown()
        {
            var card = Card(CardType.Active, description: "레거시 설명");
            card.skill = Skill(SkillEffectType.RapidFire, 2f, 6f, 25f, 2);

            StringAssert.Contains(
                "아군 유닛 지정 → 공격 속도 x2 · 6초 · 비용 2 · 재사용 25초",
                DreamcatcherCardText.Body(card));
            StringAssert.DoesNotContain("레거시 설명", DreamcatcherCardText.Body(card));
        }

        [Test]
        public void ActiveSkill_FormatsTornadoPullSpeed()
        {
            var card = Card(CardType.Active);
            card.skill = Skill(SkillEffectType.Tornado, 12.5f, 3f, 20f, 2);
            card.skill.range = 2f;

            StringAssert.Contains(
                "타일 지정 → 반경 2칸 적을 중심으로 끌어당김 · 끌어당김 속도 12.5 · 3초 · 비용 2 · 재사용 20초",
                DreamcatcherCardText.Body(card));
        }

        [Test]
        public void DecimalNumbers_TrimTrailingZeros()
        {
            var card = Card(CardType.Squad, CardTargetAxis.All, new[]
            {
                new CardEffect { kind = CardBuffKind.AttackDamage, percent = 7.5f },
            });

            StringAssert.Contains("공격력 +7.5%", DreamcatcherCardText.Body(card));
            StringAssert.DoesNotContain("+7.50%", DreamcatcherCardText.Body(card));
        }

        [Test]
        public void LegacyDescription_RemainsFallbackWithoutStructuredData()
        {
            var card = Card(CardType.Unit, description: "레거시 설명");

            StringAssert.Contains("레거시 설명", DreamcatcherCardText.Body(card));
            StringAssert.Contains("유닛 부착", DreamcatcherCardText.Body(card));
        }

        [Test]
        public void UnsupportedMechanic_UsesDescriptionFallbackInsteadOfPartialSummary()
        {
            var card = Card(CardType.Unit, description: "구형 설명 fallback");
            card.mechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.AttackN, period = 3 },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.ApplyStackToTarget,
                        magnitude = 1f,
                        duration = 4f,
                        stackKind = DcStackKind.Bleed,
                    },
                },
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.None },
                    payload = new DcPayloadSpec { kind = DcPayloadKind.SelfWarmupBuff },
                },
            };

            string body = DreamcatcherCardText.Body(card);
            StringAssert.Contains("구형 설명 fallback", body);
            StringAssert.DoesNotContain("출혈 1스택", body);
        }

        [Test]
        public void CardAssets_UseStructuredSummaryWhenDataExists()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:DreamcatcherCard", new[] { "Assets/_Project/Data/Dreamcatcher" });
            Assert.IsNotEmpty(guids);

            int structuredCount = 0;
            foreach (var guid in guids)
            {
                var card = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (card == null) continue;

                bool hasStructuredData = card.type == CardType.Squad
                    ? card.effects != null && card.effects.Length > 0
                    : card.type == CardType.Unit
                        ? (card.mechanics != null && card.mechanics.Length > 0)
                          || (card.attackMods != null && card.attackMods.Length > 0)
                        : card.skill != null;
                if (!hasStructuredData) continue;

                structuredCount++;
                string body = DreamcatcherCardText.Body(card);
                Assert.IsFalse(string.IsNullOrEmpty(body), $"empty body: {card.id}");
                if (!string.IsNullOrEmpty(card.description))
                {
                    int first = body.IndexOf(card.description, System.StringComparison.Ordinal);
                    int last = body.LastIndexOf(card.description, System.StringComparison.Ordinal);
                    Assert.GreaterOrEqual(first, 0, card.id);
                    Assert.AreEqual(first, last, card.id + " description must not be duplicated");
                }

            }

            Assert.AreEqual(44, structuredCount, "all current Dreamcatcher cards should be data-formatted");
        }

        // ── dreamcatcher-attach-requirement unit 4: 부착 제한 접두 ────────────────

        private DreamcatcherCard RequireCard(DcAttachRequireKind kind,
            DefenderClass cls = DefenderClass.None, string unitId = null)
        {
            var card = Card(CardType.Unit, description: "부착 즉시 → 뭔가 한다");
            card.attachRequire = kind;
            card.attachRequireClass = cls;
            card.attachRequireUnitId = unitId;
            return card;
        }

        private static string FirstLine(string body)
        {
            var lines = body.Split('\n');
            return lines[lines.Length > 0 ? 0 : 0];
        }

        [Test]
        public void AttachRequirement_ClassPrefix_IsFirstLine()
        {
            var card = RequireCard(DcAttachRequireKind.Class, cls: DefenderClass.Guardian);
            Assert.AreEqual("가디언 전용", FirstLine(DreamcatcherCardText.BodyLinesOnly(card)));
            Assert.That(DreamcatcherCardText.Body(card), Does.Contain("가디언 전용"));
        }

        [Test]
        public void AttachRequirement_UnitIdPrefix_UsesResolverThenFallsBackToId()
        {
            var card = RequireCard(DcAttachRequireKind.UnitId, unitId: "shield_shuttle");

            Assert.AreEqual("실드셔틀 전용", FirstLine(DreamcatcherCardText.BodyLinesOnly(
                card, id => id == "shield_shuttle" ? "실드셔틀" : null)));
            Assert.AreEqual("shield_shuttle 전용", FirstLine(DreamcatcherCardText.BodyLinesOnly(card)),
                "resolver 미주입 시 id 폴백");
            Assert.AreEqual("shield_shuttle 전용", FirstLine(DreamcatcherCardText.BodyLinesOnly(
                card, id => null)), "resolver 가 못 찾으면 id 폴백");
        }

        [Test]
        public void AttachRequirement_InvalidOrNone_AddsNoPrefix()
        {
            // 무효 설정에 "None 전용" 같은 문구를 보이지 않는다 — fail-closed 는 게이트/validator 담당.
            Assert.That(DreamcatcherCardText.BodyLinesOnly(
                RequireCard(DcAttachRequireKind.Class, cls: DefenderClass.None)),
                Does.Not.Contain("전용"));
            Assert.That(DreamcatcherCardText.BodyLinesOnly(
                RequireCard(DcAttachRequireKind.UnitId, unitId: "")),
                Does.Not.Contain("전용"));
        }

        [Test]
        public void AttachRequirement_UnrestrictedCard_BodyUnchanged()
        {
            var card = RequireCard(DcAttachRequireKind.None);
            Assert.AreEqual("부착 즉시 → 뭔가 한다".Replace(" → ", " →\n"),
                DreamcatcherCardText.BodyLinesOnly(card), "제한 없는 카드 문안은 무변화");
        }
    }
}
