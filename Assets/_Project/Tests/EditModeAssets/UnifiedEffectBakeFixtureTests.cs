using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;
using Wassup.BattleCoreUnity;
using Wassup.Data;
using Wassup.Skills;
using Wassup.Tests.EditMode.Core;

namespace Wassup.Tests.EditModeAssets
{
    // unified-effect-layer unit 5 — **하드 케이스 둘을 저작 경로로 굽는다**(가이드 §5 · 5_single_validator 완료 기준 3).
    //
    // 탐침(`HardCaseUnifiedSkillProbeTests` · `HardCaseMeteorProbeTests`)은 정의표를 **손으로** 조립했다 — 빌더가 그 조합을
    // 거절했기 때문이다. 검증이 한 함수가 된 뒤 같은 저작을 메모리 SO 로 만들어 카드 빌더에 넣으면 경고 0 으로 굽히고,
    // 굽힌 줄이 탐침의 손조립 줄과 **필드 동치**여야 한다. 에셋은 만들지 않는다(메모리에서 만들고 버린다).
    //
    // 동치에서 빼는 것: 진단 이름(`Label`) · 실행자 참조(`Skill` — 같은 스킬 번호인지만 본다) · 코어 효과(카드는 안 든다).
    // 빌더는 선택자 셋(CC · 스택 · 부채꼴)을 **명시 기본값**으로 옮긴다(`BindingDefinitionBuilder` 머리말 — 0 이 진짜처럼
    // 보이는 함정) — 두 효과는 그 셋을 안 읽으므로 기대값에 빌더의 기본값을 얹는다.
    public class UnifiedEffectBakeFixtureTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void Cleanup()
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

        private static AwakeningConfig Awakening()
        {
            var guids = AssetDatabase.FindAssets("t:AwakeningConfig");
            Assert.IsNotEmpty(guids);
            return AssetDatabase.LoadAssetAtPath<AwakeningConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // 카드 한 장을 굽고 그 카드의 규칙 줄 하나(+ 그 줄이 가리키는 효과 줄 — skill-data-table 1a) + 빌더가 낸 로그를 돌려준다.
        private BindingDef BakeOne(DcMechanic mechanic, out EffectDef effect, out List<string> logs)
        {
            var card = Make<DreamcatcherCard>();
            card.id = "fixture_card";
            card.type = CardType.Unit;
            card.mechanics = new[] { mechanic };
            var cards = new List<DreamcatcherCard> { card };
            var def = new MatchDefinition();
            var lines = new List<string>();
            void Tap(string message, string stack, LogType type)
            {
                if (message.StartsWith("[CardDefinitionBuilder]") || message.StartsWith("[BindingDefinitionBuilder]"))
                    lines.Add(type + " " + message);
            }
            Application.logMessageReceived += Tap;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                CardDefinitionBuilder.Fill(def, new CardAuthoring { Cards = cards, Awakening = Awakening() },
                                           new List<ProjectileData>(), new List<ProjectilePatternData>(),
                                           CardDefinitionBuilder.WithCardHazards(null, cards));
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
                Application.logMessageReceived -= Tap;
            }
            logs = lines;
            Assert.AreEqual(1, def.Cards.Length);
            Assert.IsNotNull(def.Cards[0].Bindings, "규칙 줄이 안 구워졌다: " + string.Join(" / ", lines));
            Assert.AreEqual(1, def.Cards[0].Bindings.Length);
            var row = def.Bindings[def.Cards[0].Bindings[0]];
            effect = def.EffectOf(in row);
            return row;
        }

        // 빌더가 규칙 줄에 명시로 옮기는 선택자 기본값(저작 기본 = Stun · Bleed · 반각 0 → (sin, cos) = (0, 1)).
        private static void WithBuilderSelectorDefaults(ref BindingDef b)
        {
            b.CcKind = (int)BindingDefinitionBuilder.ToSkillCc(default(DcCcKind));
            b.StackKind = (int)BindingDefinitionBuilder.ToSkillStack(default(DcStackKind));
            b.StatKind = (int)(BindingDefinitionBuilder.TryToSkillStat(default(CardBuffKind), out var st) ? st : SkillStatKind.DamageMul);
            b.ConeSinHalf = 0f; b.ConeCosHalf = 1f;
        }

        // skill-data-table 1a — 탐침 줄은 효과를 인라인으로 들고(-1), 구운 줄은 효과 표를 가리킨다. 규칙 칸은 규칙 칸끼리,
        // 효과 값은 **해석한 효과 줄끼리**(탐침 = `InlineEffect()` · 구움 = `EffectOf`) 대조한다 — id 는 빼고.
        private static void AssertFieldEqual(BindingDef expected, BindingDef actual, in EffectDef actualEffect)
        {
            Assert.AreEqual(expected.SkillId, actual.SkillId, "실행자(스킬 번호)");
            var effectFields = new HashSet<string> { "Payload", nameof(BindingDef.EffectIndex) };
            foreach (var f in typeof(EffectDef).GetFields(BindingFlags.Public | BindingFlags.Instance)) effectFields.Add(f.Name);
            foreach (var f in typeof(BindingDef).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.Name == nameof(BindingDef.Label) || f.Name == nameof(BindingDef.Skill) || f.Name == nameof(BindingDef.CoreSkill)) continue;
                if (effectFields.Contains(f.Name)) continue;
                Assert.AreEqual(f.GetValue(expected), f.GetValue(actual), $"필드 {f.Name} 이 탐침의 손조립 줄과 다르다");
            }
            object want = expected.InlineEffect();
            object got = actualEffect;
            foreach (var f in typeof(EffectDef).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.Name == nameof(EffectDef.Id)) continue;
                Assert.AreEqual(f.GetValue(want), f.GetValue(got), $"효과 칸 {f.Name} 이 탐침의 손조립 줄과 다르다");
            }
        }

        [Test]
        public void AA_남의_배치_융단폭격은_경고_0_으로_굽고_탐침_줄과_같다()
        {
            var barrel = Make<ProjectileData>();
            barrel.flightMode = ProjectileFlightMode.Homing;   // 대상 결합 — 「반경 안 전원」의 전제
            var pattern = Make<ProjectilePatternData>();
            pattern.barrel = barrel;
            pattern.damage = HardCaseUnifiedSkillProbeTests.Hit;
            pattern.scopeTileRange = HardCaseUnifiedSkillProbeTests.N;
            pattern.fanOutToAllCandidates = true;
            var m = new DcMechanic
            {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.OnPlace, subject = DcTriggerSubject.OthersPlacement },
                payload = new DcPayloadSpec
                {
                    kind = DcPayloadKind.EmitProjectilePattern, pattern = pattern,
                    tileRange = HardCaseUnifiedSkillProbeTests.N, magnitude = HardCaseUnifiedSkillProbeTests.Hit,
                },
            };

            var baked = BakeOne(m, out var bakedEffect, out var logs);
            Assert.IsEmpty(logs, "경고·오류 0 이어야 한다: " + string.Join(" / ", logs));

            var expected = HardCaseUnifiedSkillProbeTests.CardRow();
            WithBuilderSelectorDefaults(ref expected);
            AssertFieldEqual(expected, baked, in bakedEffect);
        }

        [Test]
        public void 타격_운석은_경고_0_으로_굽고_탐침_줄과_같다()
        {
            var meteor = Make<ProjectileData>();
            meteor.flightMode = ProjectileFlightMode.SkyFall;   // 칸 결합 — 옛 카드 빌더가 거절하던 탄
            meteor.speed = 0f;          // 낙하는 비행 시간(duration)이 정한다 — 탐침 줄도 속도·굵기 0
            meteor.hitThreshold = 0f;
            meteor.visualScale = 1f;
            var m = new DcMechanic
            {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.AttackN, period = 1 },
                payload = new DcPayloadSpec
                {
                    kind = DcPayloadKind.ProjectileToTarget, projectile = meteor,
                    magnitude = HardCaseMeteorProbeTests.OnHitHit, tileRange = HardCaseMeteorProbeTests.N,
                    duration = HardCaseMeteorProbeTests.WarningSec, telegraph = true,
                },
            };

            var baked = BakeOne(m, out var bakedEffect, out var logs);
            Assert.IsEmpty(logs, "경고·오류 0 이어야 한다: " + string.Join(" / ", logs));

            var (movement, payload) = CombatDefinitionBuilder.Translate(ProjectileFlightMode.SkyFall);
            var expected = HardCaseMeteorProbeTests.OnHitMeteorRule(HardCaseMeteorProbeTests.N, movement, payload);
            // 탐침의 예고 판(`…예고`)과 같은 두 칸 — 낙하 = 효과 지속 · 예고 켬(U1).
            expected.Duration = HardCaseMeteorProbeTests.WarningSec;
            expected.Telegraph = true;
            WithBuilderSelectorDefaults(ref expected);
            AssertFieldEqual(expected, baked, in bakedEffect);
        }
    }
}
