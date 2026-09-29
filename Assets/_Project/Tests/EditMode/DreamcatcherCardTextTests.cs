using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data.Authoring;
using Wassup.Data;
using Wassup.UI;
using Wassup.BattleCore.Trigger;

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
            card.description = description;
            _cleanup.Add(card);
            // skill-data-table unit 8 — 스쿼드 스탯 효과 = 진영 버프 효과 줄(트리거 None · 수혜 대상 = `allyFilter`). 픽스처는 읽기 쉬운
            // (종류, %) 모양으로 적고 여기서 소유 줄로 옮긴다(수혜 대상 = 넘긴 축 — 이전 규칙과 같다).
            if (effects != null)
            {
                card.bindings = new BindingSpec[effects.Length];
                for (int i = 0; i < effects.Length; i++)
                    card.bindings[i] = Always(AlwaysOnEffect(new EffectValues
                    {
                        kind = EffectKind.FactionStatBuff, buffStat = effects[i].kind, percent = effects[i].percent, allyFilter = axis,
                    }));
            }
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

        // skill-data-table unit 8 — 액티브 문안의 수치 = 시전 줄 효과 값 + 카드 `cooldownSec`(굽기와 한 원천).
        private void AttachCast(DreamcatcherCard card, EffectValues v, float cooldownSec)
        {
            var e = ScriptableObject.CreateInstance<EffectData>();
            e.id = "fixture_cast";
            e.values = v;
            _cleanup.Add(e);
            card.bindings = new[] { new BindingSpec { trigger = new TriggerSpec { kind = TriggerKind.Cast }, fireCap = 1, effect = e } };
            card.cooldownSec = cooldownSec;
        }

        private EffectData AlwaysOnEffect(EffectValues v)
        {
            var e = ScriptableObject.CreateInstance<EffectData>();
            e.id = "fixture_" + v.kind.ToString().ToLowerInvariant();
            e.values = v;
            _cleanup.Add(e);
            return e;
        }

        private static BindingSpec Always(EffectData e) => new BindingSpec { trigger = new TriggerSpec { kind = TriggerKind.None }, effect = e };

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
            TestBindings.Attach(card, new[]
            {
                new DcMechanic
                {
                    trigger = new TriggerSpec { kind = TriggerKind.AttackN, period = 3 },
                    payload = new DcPayloadSpec
                    {
                        kind = EffectKind.ApplyStackToTarget,
                        magnitude = 1f,
                        duration = 4f,
                        stackKind = DcStackKind.Bleed,
                    },
                },
            });

            StringAssert.Contains(
                "3번째 공격마다 → 대상에게 출혈 1스택 · 4초",
                DreamcatcherCardText.Body(card));
        }

        [Test]
        public void UnitMechanic_FormatsImpulseDuration()
        {
            var card = Card(CardType.Unit);
            TestBindings.Attach(card, new[]
            {
                new DcMechanic
                {
                    trigger = new TriggerSpec { kind = TriggerKind.AttackN, period = 3 },
                    payload = new DcPayloadSpec
                    {
                        kind = EffectKind.ApplyCcToTarget,
                        ccKind = DcCcKind.Impulse,
                        magnitude = 4f,
                        duration = 0.5f,
                    },
                },
            });

            StringAssert.Contains(
                "3번째 공격마다 → 대상에게 넉백 속도 4 · 0.5초",
                DreamcatcherCardText.Body(card));
        }

        [Test]
        public void UnitMechanic_FormatsSleepWithWakeNote()
        {
            var card = Card(CardType.Unit);
            TestBindings.Attach(card, new[]
            {
                new DcMechanic
                {
                    trigger = new TriggerSpec { kind = TriggerKind.AttackN, period = 5 },
                    payload = new DcPayloadSpec
                    {
                        kind = EffectKind.ApplyCcToTarget,
                        ccKind = DcCcKind.Sleep,
                        duration = 2.5f,
                    },
                },
            });

            StringAssert.Contains(
                "5번째 공격마다 → 대상에게 수면 2.5초 (피격 시 해제)",
                DreamcatcherCardText.Body(card));
        }

        // dreamcatcher-trigger-gates unit 1 — 게이트 접두 문안 (배선 조합 골든).
        [Test]
        public void UnitMechanic_FormatsGatePrefix_ForWiredCombos()
        {
            var card = Card(CardType.Unit);
            TestBindings.Attach(card, new[]
            {
                new DcMechanic
                {
                    trigger = new TriggerSpec
                    {
                        kind = TriggerKind.AttackN, period = 1,
                        gate = GateKind.HpBelow, gateSubject = GateSubject.EventTarget, gateValue = 0.25f,
                    },
                    payload = new DcPayloadSpec { kind = EffectKind.HeavyStrike, magnitude = 2f },
                },
                new DcMechanic
                {
                    trigger = new TriggerSpec
                    {
                        kind = TriggerKind.OnDamagedN, period = 2,
                        gate = GateKind.HpBelow, gateSubject = GateSubject.Self, gateValue = 0.30f,
                    },
                    payload = new DcPayloadSpec { kind = EffectKind.SelfTileAoe, magnitude = 20f, tileRange = 1 },
                },
            });

            string body = DreamcatcherCardText.Body(card);
            StringAssert.Contains("HP 25% 이하인 적에게 공격마다 → 피해 x2", body);
            StringAssert.Contains("HP 30% 이하일 때 2번째 피격마다 → 반경 1칸 피해 20", body);
        }

        [Test]
        public void UnitMechanic_UnsupportedGateCombo_UsesDescriptionFallback()
        {
            var card = Card(CardType.Unit, description: "미지원 게이트 설명");
            TestBindings.Attach(card, new[]
            {
                new DcMechanic
                {
                    trigger = new TriggerSpec
                    {
                        kind = TriggerKind.OnDamagedN, period = 2,
                        gate = GateKind.HpBelow, gateSubject = GateSubject.EventTarget, gateValue = 0.30f,
                    },
                    payload = new DcPayloadSpec { kind = EffectKind.SelfTileAoe, magnitude = 20f, tileRange = 1 },
                },
            });

            string body = DreamcatcherCardText.Body(card);
            StringAssert.Contains("미지원 게이트 설명", body);
            StringAssert.DoesNotContain("2번째 피격마다", body);
        }

        [Test]
        public void UnitMechanic_FormatsThresholdAndPermanentEffect()
        {
            var card = Card(CardType.Unit);
            TestBindings.Attach(card, new[]
            {
                new DcMechanic
                {
                    trigger = new TriggerSpec
                    {
                        kind = TriggerKind.HealthThreshold,
                        fraction = 0.7f,
                    },
                    payload = new DcPayloadSpec
                    {
                        kind = EffectKind.SelfStatBuff,
                        magnitude = 30f,
                        buffStat = CardBuffKind.AttackDamage,
                    },
                },
            });

            StringAssert.Contains(
                "HP 30% 이하 → 공격력 +30% · 전투 중 1회",
                DreamcatcherCardText.Body(card));
        }

        [Test]
        public void UnitAttackMod_FormatsBounceValues()
        {
            var card = Card(CardType.Unit);
            card.bindings = new[] { Always(AlwaysOnEffect(new EffectValues { kind = EffectKind.ProjectileBounce, count = 2, rangeTiles = 3, mul = 1f })) };

            StringAssert.Contains(
                "항상 → 공격 투사체가 최대 3칸 범위 내 2회 튕김 (감쇠 없음)",
                DreamcatcherCardText.Body(card));
        }

        // content-4 unit 0 — 수면 특효(악몽 사냥). 상시 변조라 "항상" 접두를 쓰고,
        // "잠든 적에게" 라는 대상 조건이 문안에 반드시 남아야 한다 — 이 조건이 빠지면
        // 플레이어가 상시 피해 2배 카드로 읽는다.
        [Test]
        public void UnitAttackMod_FormatsDamageVsSleeping()
        {
            var card = Card(CardType.Unit);
            card.bindings = new[] { Always(AlwaysOnEffect(new EffectValues { kind = EffectKind.DamageVsSleeping, mul = 2f })) };

            StringAssert.Contains(
                "항상 → 잠든 적에게 주는 피해 x2",
                DreamcatcherCardText.Body(card));
        }

        // content-4 unit 0 — 궤도 화염구(불꽃 팽이). 재타격 간격은 탄 SO 소유라
        // 문안이 그 수치를 복제하지 않는다(제약 6) — 지속과 피해만 나온다.
        [Test]
        public void UnitMechanic_FormatsSelfOrbitProjectile()
        {
            var card = Card(CardType.Unit);
            TestBindings.Attach(card, new[]
            {
                new DcMechanic
                {
                    trigger = new TriggerSpec { kind = TriggerKind.PeriodicTimer, periodSeconds = 6f },
                    payload = new DcPayloadSpec
                    {
                        kind = EffectKind.SelfOrbitProjectile,
                        magnitude = 20f,
                        duration = 3f,
                        tileRange = 1,
                    },
                },
            });

            StringAssert.Contains(
                "6초마다 → 주위를 도는 화염구 3초 · 스치는 적에게 피해 20",
                DreamcatcherCardText.Body(card));
        }

        // content-4 unit 0 — 퇴근 운석(퇴직 위로금). **사망 문안과 갈라져야 한다** —
        // 두 트리거가 교차 발동하지 않는 것이 이 카드의 계약이라, 문안이 흐리면
        // 플레이어가 죽어도 터질 거라 기대한다.
        [Test]
        public void UnitMechanic_FormatsOnRetireMeteor_AndIsNotDeathWording()
        {
            var card = Card(CardType.Unit);
            TestBindings.Attach(card, new[]
            {
                new DcMechanic
                {
                    trigger = new TriggerSpec { kind = TriggerKind.OnRetire },
                    payload = new DcPayloadSpec
                    {
                        kind = EffectKind.SelfTileAoe,
                        magnitude = 120f,
                        tileRange = 1,
                        duration = 0.8f, // 낙하 예고
                    },
                },
            });

            string body = DreamcatcherCardText.Body(card);
            StringAssert.Contains("이 유닛이 철수하면 → 0.8초 후 반경 1칸 피해 120", body);
            StringAssert.DoesNotContain("사망", body);
        }

        // retire-recall unit 0 — 인수인계. **"다른"이 load-bearing 단어다**: 선언한 카드
        // 자신은 맨 뒤로 가므로, 단독 부착이면 아무 일도 일어나지 않는다. 문안에서 그 단어가
        // 빠지면 화면이 거짓말을 한다. 수치는 한 칸도 읽지 않으므로 숫자가 새어나오면 안 된다.
        [Test]
        public void UnitMechanic_FormatsRetireRecall_AndSaysOthersOnly()
        {
            var card = Card(CardType.Unit);
            TestBindings.Attach(card, new[]
            {
                new DcMechanic
                {
                    trigger = new TriggerSpec { kind = TriggerKind.OnRetire },
                    payload = new DcPayloadSpec
                    {
                        kind = EffectKind.RecallAttachedToFront,
                        // 저작 실수로 값이 들어와도 문안이 그것을 읽지 않는다는 것까지 고정한다.
                        magnitude = 2f,
                        tileRange = 3,
                    },
                },
            });

            string body = DreamcatcherCardText.Body(card);
            StringAssert.Contains("이 유닛이 철수하면 → 함께 붙은 다른 드림캐쳐가 손패 맨 앞으로", body);
            StringAssert.DoesNotContain("2장", body);
            StringAssert.DoesNotContain("사망", body);
        }

        // content-4 unit 0 — 무회귀: duration 0 인 기존 SelfTileAoe 카드(작별 선물 등)는
        // 예고 접두 없이 종전 문안 그대로여야 한다.
        [Test]
        public void UnitMechanic_SelfTileAoe_WithoutDuration_KeepsLegacyWording()
        {
            var card = Card(CardType.Unit);
            TestBindings.Attach(card, new[]
            {
                new DcMechanic
                {
                    trigger = new TriggerSpec { kind = TriggerKind.OnDeath },
                    payload = new DcPayloadSpec
                    {
                        kind = EffectKind.SelfTileAoe,
                        magnitude = 50f,
                        tileRange = 1,
                    },
                },
            });

            StringAssert.Contains(
                "이 유닛이 사망하면 → 반경 1칸 피해 50",
                DreamcatcherCardText.Body(card));
        }

        // skill-data-table unit 5 — 칸 결합 낙하 탄(SkyFall)의 ProjectileToTarget 은 «추가 투사체» 가 아니라 맞은 적 자리의 반경 피해다.
        [Test]
        public void UnitMechanic_ProjectileToTarget_SkyFall_DescribesStrikeAtHitEnemy()
        {
            var meteor = ScriptableObject.CreateInstance<ProjectileData>();
            meteor.flightMode = ProjectileFlightMode.SkyFall;
            _cleanup.Add(meteor);
            var card = Card(CardType.Unit);
            TestBindings.Attach(card, new[]
            {
                new DcMechanic
                {
                    trigger = new TriggerSpec { kind = TriggerKind.AttackN, period = 1 },
                    payload = new DcPayloadSpec
                    {
                        kind = EffectKind.ProjectileToTarget, magnitude = 30f, tileRange = 1, duration = 0.5f, projectile = meteor,
                    },
                },
            }, _cleanup);
            StringAssert.Contains("공격마다 → 맞은 적 자리에 운석 낙하 · 0.5초 후 반경 1칸 피해 30", DreamcatcherCardText.Body(card));

            card.bindings[0].effect.values.flightSec = 0f;
            StringAssert.Contains("공격마다 → 맞은 적 자리에 운석 낙하 · 반경 1칸 피해 30", DreamcatcherCardText.Body(card));

            // 유도탄은 옛 문안 그대로.
            meteor.flightMode = ProjectileFlightMode.Homing;
            StringAssert.Contains("공격마다 → 대상에게 추가 투사체 피해 30", DreamcatcherCardText.Body(card));
        }

        [Test]
        public void ActiveSkill_FormatsMultiplierDurationCostAndCooldown()
        {
            var card = Card(CardType.Active, description: "레거시 설명");
            // active-dreamcatcher-tile-aim unit 0 — 아군 버프도 타일 반경
            AttachCast(card, new EffectValues { kind = EffectKind.ActiveRapidFire, mul = 2f, radiusTiles = 1, durationSec = 6f }, 25f);

            // skill-data-table U18 — 비용 칸 = 실제 비용(호출처가 넘기는 카드 값 · `AwakeningConfig.costActive`). `SkillData.cost`(2)는 안 읽는다.
            StringAssert.Contains(
                "타일 지정 → 반경 1칸 아군 공격 속도 x2 · 6초 · 비용 20 · 재사용 25초",
                DreamcatcherCardText.Body(card, activeCost: 20));
            StringAssert.DoesNotContain("레거시 설명", DreamcatcherCardText.Body(card, activeCost: 20));
        }

        [Test]
        public void ActiveSkill_CostUnknown_OmitsCost_AndNeverShowsSkillDataCost()
        {
            // U18 — 비용을 모르는 호출처는 비용 칸을 뺀다(옛 `SkillData.cost` 로 떨어지지 않는다).
            var card = Card(CardType.Active);
            AttachCast(card, new EffectValues { kind = EffectKind.ActiveRapidFire, mul = 2f, radiusTiles = 1, durationSec = 6f }, 25f);

            StringAssert.Contains("타일 지정 → 반경 1칸 아군 공격 속도 x2 · 6초 · 재사용 25초", DreamcatcherCardText.Body(card));
            StringAssert.DoesNotContain("비용", DreamcatcherCardText.Body(card));
            StringAssert.Contains("비용 20", DreamcatcherCardText.BodyLinesOnly(card, null, 20));
        }

        [Test]
        public void ActiveSkill_FormatsTornadoPullSpeed()
        {
            var card = Card(CardType.Active);
            AttachCast(card, new EffectValues { kind = EffectKind.ActiveTornado, speed = 12.5f, radiusTiles = 2, durationSec = 3f }, 20f);

            StringAssert.Contains(
                "타일 지정 → 반경 2칸 적을 중심으로 끌어당김 · 끌어당김 속도 12.5 · 3초 · 비용 20 · 재사용 20초",
                DreamcatcherCardText.Body(card, activeCost: 20));
        }

        [Test]
        public void ActiveSkill_ReadsCastEffectAndCardCooldown_NotSkillDataNumbers()
        {
            // skill-data-table unit 8 — 쿨다운 두 원천 해소: 문안은 굽기와 같은 값(시전 줄 효과 + 카드 cooldownSec)만 읽는다.
            var card = Card(CardType.Active);
            AttachCast(card, new EffectValues { kind = EffectKind.ActiveMeteor, damage = 40f, radiusTiles = 2, flightSec = 1.5f }, 18f);
            card.skill = Skill(SkillEffectType.Meteor, 99f, 0f, 77f, 4);   // 옛 두 번째 원천 — 다른 숫자
            card.skill.range = 5f;
            card.skill.warningSec = 9f;

            string body = DreamcatcherCardText.Body(card, activeCost: 20);
            StringAssert.Contains("타일 지정 → 1.5초 후 반경 2칸 피해 40 · 비용 20 · 재사용 18초", body);
            StringAssert.DoesNotContain("99", body);
            StringAssert.DoesNotContain("77", body);
        }

        [Test]
        public void ActiveSkill_WithoutCastRow_FallsBackToDescription()
        {
            var card = Card(CardType.Active, description: "레거시 설명");
            card.skill = Skill(SkillEffectType.RapidFire, 2f, 6f, 25f, 2);
            StringAssert.Contains("레거시 설명", DreamcatcherCardText.Body(card));
            StringAssert.DoesNotContain("재사용", DreamcatcherCardText.Body(card));
        }

        // ── skill-data-table unit 8 — 상시 효과 줄(진영 버프 · 공격 수식자) ─────────────────────────────

        [Test]
        public void SquadRows_EachRowCarriesItsAllyFilter_HeaderAndChipFromFirstRow()
        {
            // 수혜 대상 = 효과의 `allyFilter`(계약 12) — 카드 axis 는 읽지 않는다(단계 B). 머리 · 칩 = 첫 진영 버프 줄.
            var card = Card(CardType.Squad, CardTargetAxis.All);
            card.bindings = new[]
            {
                Always(AlwaysOnEffect(new EffectValues { kind = EffectKind.FactionStatBuff, buffStat = CardBuffKind.AttackDamage, percent = 20f, allyFilter = CardTargetAxis.ClassRanger })),
                Always(AlwaysOnEffect(new EffectValues { kind = EffectKind.FactionStatBuff, buffStat = CardBuffKind.MoveSpeed, percent = -10f, allyFilter = CardTargetAxis.Cost1 })),
            };
            string body = DreamcatcherCardText.Body(card);
            StringAssert.Contains("항상 → 레인저 아군 공격력 +20% · 1코스트 유닛 이동 속도 -10%", body);
            StringAssert.Contains("레인저", body.Split('\n')[0], "머리 축 = 첫 진영 버프 줄의 수혜 대상(카드 axis 가 아니다)");
            Assert.AreEqual("레인저 버프", CardCategoryStyle.TargetTag(card), "손패 칩도 같은 원천");
        }

        [Test]
        public void AttackModRows_FormatAllThree_InRowOrder_BeforeRuleLines()
        {
            var card = Card(CardType.Unit);
            card.bindings = new[]
            {
                Always(AlwaysOnEffect(new EffectValues { kind = EffectKind.ProjectileBounce, count = 2, rangeTiles = 3, mul = 0.5f })),
                Always(AlwaysOnEffect(new EffectValues { kind = EffectKind.FrontmostTarget, mul = 1.2f })),
                Always(AlwaysOnEffect(new EffectValues { kind = EffectKind.DamageVsSleeping, mul = 2f })),
            };
            var lines = DreamcatcherCardText.BodyLinesOnly(card).Replace("항상 →\n", "항상 → ").Split('\n');
            Assert.AreEqual(3, lines.Length);
            Assert.AreEqual("항상 → 공격 투사체가 최대 3칸 범위 내 2회 튕김 (튕길 때마다 피해 x0.5)", lines[0]);
            Assert.AreEqual("항상 → 목표 지점에 가장 가까운 적 우선 공격 · 해당 적 직접 피해 +20%", lines[1]);
            Assert.AreEqual("항상 → 잠든 적에게 주는 피해 x2", lines[2]);
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
            TestBindings.Attach(card, new[]
            {
                new DcMechanic
                {
                    trigger = new TriggerSpec { kind = TriggerKind.AttackN, period = 3 },
                    payload = new DcPayloadSpec
                    {
                        kind = EffectKind.ApplyStackToTarget,
                        magnitude = 1f,
                        duration = 4f,
                        stackKind = DcStackKind.Bleed,
                    },
                },
                new DcMechanic
                {
                    trigger = new TriggerSpec { kind = TriggerKind.None },
                    payload = new DcPayloadSpec { kind = EffectKind.SelfWarmupBuff },
                },
            });

            string body = DreamcatcherCardText.Body(card);
            StringAssert.Contains("구형 설명 fallback", body);
            StringAssert.DoesNotContain("출혈 1스택", body);
        }

        // 실카탈로그 전수 검사(`CardAssets_UseStructuredSummaryWhenDataExists`)는
        // EditModeAssets/DreamcatcherCardAssetTextTests.cs 에 있다. 그 과정에서 개수 pin
        // (44 → content-4 에서 47 로 올려야 했던 그 수)은 «비정형 카드 목록이 빈다» 는
        // 직접 단언으로 바뀌었다 — 이제 카드를 추가해도 그 테스트를 고칠 필요가 없다
        // (test-suite-fast-lane units 0·1).

        // ── dreamcatcher-attach-requirement unit 4: 부착 제한 접두 ────────────────

        private DreamcatcherCard RequireCard(DcAttachType type, string value = null)
        {
            var card = Card(CardType.Unit, description: "부착 즉시 → 뭔가 한다");
            card.attachType = type;
            card.attachValue = value;
            return card;
        }

        private static string FirstLine(string body) => body.Split('\n')[0];

        [Test]
        public void AttachRequirement_ClassPrefix_IsFirstLine()
        {
            var card = RequireCard(DcAttachType.Class, "Guardian");
            Assert.AreEqual("가디언 전용", FirstLine(DreamcatcherCardText.BodyLinesOnly(card)));
            Assert.That(DreamcatcherCardText.Body(card), Does.Contain("가디언 전용"));
        }

        [Test]
        public void AttachRequirement_UnitIdPrefix_UsesResolverThenFallsBackToId()
        {
            var card = RequireCard(DcAttachType.UnitId, "shield_shuttle");

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
                RequireCard(DcAttachType.Class, "")),
                Does.Not.Contain("전용"));
            Assert.That(DreamcatcherCardText.BodyLinesOnly(
                RequireCard(DcAttachType.UnitId, "")),
                Does.Not.Contain("전용"));
        }

        [Test]
        public void AttachRequirement_UnrestrictedCard_BodyUnchanged()
        {
            var card = RequireCard(DcAttachType.None);
            Assert.AreEqual("부착 즉시 → 뭔가 한다".Replace(" → ", " →\n"),
                DreamcatcherCardText.BodyLinesOnly(card), "제한 없는 카드 문안은 무변화");
        }

        // --- content-3 unit 6: 스택 임계 요약 ---

        private StackModifierSO StackSO(StackKind kind, params ThresholdRule[] rules)
        {
            var so = ScriptableObject.CreateInstance<StackModifierSO>();
            so.kind = kind;
            so.thresholds = rules;
            _cleanup.Add(so);
            return so;
        }

        private static ThresholdRule SlowRule(byte atStack, float multiplier)
            => new ThresholdRule
            {
                atStack = atStack,
                mode = ThresholdMode.Edge,
                derivedKind = DerivedEffectKind.ApplyStat,
                magnitude = multiplier,
                duration = 4f,
                stat = StatKind.MoveSpeedMul,
                op = CombineOp.Multiplicative,
            };

        private DreamcatcherCard StackCard(DcStackKind stackKind, StackModifierSO so)
        {
            var card = Card(CardType.Unit);
            TestBindings.Attach(card, new[]
            {
                new DcMechanic
                {
                    trigger = new TriggerSpec { kind = TriggerKind.AttackN, period = 1 },
                    payload = new DcPayloadSpec
                    {
                        kind = EffectKind.ApplyStackToTarget,
                        magnitude = 1f,
                        duration = 4f,
                        stackKind = stackKind,
                        stackModifier = so,
                    },
                },
            });
            return card;
        }

        [Test]
        public void StackThresholds_EvenRamp_FoldsIntoPerStackLine()
        {
            // 동상 형태 — 1~4중첩 계단 감속 + 5중첩 Consume 기절.
            var so = StackSO(StackKind.Ice,
                SlowRule(1, 0.9f), SlowRule(2, 0.8f), SlowRule(3, 0.7f), SlowRule(4, 0.6f),
                new ThresholdRule
                {
                    atStack = 5,
                    mode = ThresholdMode.Consume,
                    derivedKind = DerivedEffectKind.ApplyStun,
                    magnitude = 1f,
                });

            Assert.AreEqual("빙결 중첩당 이동 속도 -10% · 5중첩 기절 1초 (중첩 소모)",
                DreamcatcherCardText.StackThresholdSummary(so, DcStackKind.Ice));
        }

        [Test]
        public void StackThresholds_DotRule_ConvertsTickDamageToPerSecond()
        {
            // 화상물기 형태 — tickInterval>0 이면 magnitude 는 틱당 피해다(초당으로 환산해 표기).
            var so = StackSO(StackKind.Bleed, new ThresholdRule
            {
                atStack = 5,
                mode = ThresholdMode.Consume,
                derivedKind = DerivedEffectKind.ApplyDot,
                magnitude = 5f,
                duration = 4.85f,
                tickInterval = 0.5f,
            });

            Assert.AreEqual("출혈 5중첩 초당 피해 10 · 4.85초 (중첩 소모)",
                DreamcatcherCardText.StackThresholdSummary(so, DcStackKind.Bleed));
        }

        [Test]
        public void StackThresholds_UnevenRamp_ListsEachStack()
        {
            // 등차가 아니면 접지 않고 중첩별로 나열한다(수치를 왜곡하지 않는다).
            var so = StackSO(StackKind.Ice, SlowRule(1, 0.9f), SlowRule(2, 0.5f));

            Assert.AreEqual("빙결 1중첩 이동 속도 -10% · 2중첩 이동 속도 -50%",
                DreamcatcherCardText.StackThresholdSummary(so, DcStackKind.Ice));
        }

        [Test]
        public void StackThresholds_NotStartingAtOne_ListsEachStack()
        {
            var so = StackSO(StackKind.Ice, SlowRule(2, 0.8f), SlowRule(3, 0.7f));

            Assert.AreEqual("빙결 2중첩 이동 속도 -20% · 3중첩 이동 속도 -30%",
                DreamcatcherCardText.StackThresholdSummary(so, DcStackKind.Ice));
        }

        [Test]
        public void StackThresholds_MissingSo_OmitsLine()
        {
            Assert.IsNull(DreamcatcherCardText.StackThresholdSummary(null, DcStackKind.Ice));
            Assert.IsNull(DreamcatcherCardText.StackThresholdSummary(
                StackSO(StackKind.Ice), DcStackKind.Ice), "규칙 0개면 라인 없음");

            // 카드 문안에도 트리거 줄만 남는다(기존 카드 무회귀).
            var card = StackCard(DcStackKind.Ice, null);
            Assert.AreEqual("공격마다 → 대상에게 빙결 1스택 · 4초",
                DreamcatcherCardText.EffectOnly(card));
        }

        [Test]
        public void StackCard_Body_AppendsSummaryLine()
        {
            var so = StackSO(StackKind.Ice, SlowRule(1, 0.9f), SlowRule(2, 0.8f));
            var card = StackCard(DcStackKind.Ice, so);

            Assert.AreEqual(
                "공격마다 → 대상에게 빙결 1스택 · 4초\n빙결 중첩당 이동 속도 -10%",
                DreamcatcherCardText.EffectOnly(card));
        }
    }
}
