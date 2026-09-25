using System;
using NUnit.Framework;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild unit 6a 리뷰 HIGH-1 — **저작 어휘 → 코어 어휘의 핀.**
    //
    // 빌더가 두 어휘를 옮기는데 **어셈블리가 갈려 컴파일러가 어긋남을 못 잡는다.** 저작 값은
    // 이미 에셋에 byte 로 구워져 있으므로, 한쪽이 앞에 값을 끼우면 조용히 전부 밀린다 —
    // `PatternSelectionRule` 이 실제로 그렇게 당했다(12개 중 11개가 다른 규칙으로 읽힘).
    //
    // 리뷰가 든 다음 시나리오: `ThresholdMode` 앞에 값이 끼면 `Consume` 이 `Edge` 로 읽혀
    // 소비형 임계가 스택을 안 깎고 **무한 발화**한다.
    //
    // 그래서 두 가지를 고정한다:
    //   ① 두 enum 의 **이름 집합과 값**이 같다 — 한쪽에만 늘면 빨개진다.
    //   ② 매핑 함수가 **이름을 보존한다** — 번호 캐스트로 되돌리면 ①이 맞아도 여기서 잡힌다.
    [TestFixture]
    public class BuilderEnumPinTests
    {
        /// <summary>두 enum 의 이름 집합·값이 같은가(①).</summary>
        private static void PinNamesAndValues<TAuthored, TCore>()
            where TAuthored : Enum where TCore : Enum
        {
            var authored = Enum.GetNames(typeof(TAuthored));
            var core = Enum.GetNames(typeof(TCore));
            CollectionAssert.AreEquivalent(core, authored,
                $"{typeof(TAuthored).Name} ↔ {typeof(TCore).Name} — 한쪽에만 값이 늘었다");

            foreach (var name in authored)
                Assert.AreEqual(Convert.ToInt32(Enum.Parse(typeof(TAuthored), name)),
                                Convert.ToInt32(Enum.Parse(typeof(TCore), name)),
                                $"{typeof(TAuthored).Name}.{name} 의 번호가 갈렸다");
        }

        /// <summary>매핑이 이름을 보존하는가(②) — 저작 값 전부에 대해.</summary>
        private static void PinMapping<TAuthored, TCore>(Func<TAuthored, TCore> map)
            where TAuthored : Enum where TCore : Enum
        {
            foreach (TAuthored authored in Enum.GetValues(typeof(TAuthored)))
                Assert.AreEqual(authored.ToString(), map(authored).ToString(),
                    $"{typeof(TAuthored).Name}.{authored}({Convert.ToInt32(authored)}) 가 "
                    + "다른 이름으로 옮겨진다 — 번호 캐스트로 되돌아갔는지 본다");
        }

        [Test]
        public void 산출물_종류()
        {
            PinNamesAndValues<Wassup.Data.AttackOutputKind, Wassup.BattleCore.AttackOutputKind>();
            PinMapping<Wassup.Data.AttackOutputKind, Wassup.BattleCore.AttackOutputKind>(
                CombatDefinitionBuilder.ToCoreOutputKind);
        }

        [Test]
        public void 스탯_종류()
        {
            PinNamesAndValues<Wassup.Data.Authoring.StatKind, Wassup.BattleCore.Effects.StatKind>();
            PinMapping<Wassup.Data.Authoring.StatKind, Wassup.BattleCore.Effects.StatKind>(
                CombatDefinitionBuilder.ToCoreStat);
        }

        [Test]
        public void 결합_연산자()
        {
            PinNamesAndValues<Wassup.Data.Authoring.CombineOp, Wassup.BattleCore.Effects.CombineOp>();
            PinMapping<Wassup.Data.Authoring.CombineOp, Wassup.BattleCore.Effects.CombineOp>(
                CombatDefinitionBuilder.ToCoreOp);
        }

        [Test]
        public void 스택_종류()
        {
            PinNamesAndValues<Wassup.Data.Authoring.StackKind, Wassup.BattleCore.Effects.StackKind>();
            PinMapping<Wassup.Data.Authoring.StackKind, Wassup.BattleCore.Effects.StackKind>(
                CombatDefinitionBuilder.ToCoreStackKind);
        }

        [Test]
        public void 임계_모드()
        {
            PinNamesAndValues<ThresholdMode, StackThresholdMode>();
            PinMapping<ThresholdMode, StackThresholdMode>(MatchDefinitionBuilder.ToCoreThresholdMode);

            // 리뷰가 든 시나리오를 이름으로 못박는다 — 소비형이 Edge 로 읽히면 무한 발화다.
            Assert.AreEqual(StackThresholdMode.Consume,
                            MatchDefinitionBuilder.ToCoreThresholdMode(ThresholdMode.Consume));
        }

        [Test]
        public void 파생_효과()
        {
            PinNamesAndValues<DerivedEffectKind, StackDerivedKind>();
            PinMapping<DerivedEffectKind, StackDerivedKind>(MatchDefinitionBuilder.ToCoreDerivedKind);
        }

        [Test]
        public void 선정_규칙()
        {
            // 일곱 번째 쌍. 매핑의 상세 단언은 `PatternSelectionRulePinTests` 가 진다 —
            // 여기서는 **같은 그물에 걸린다**는 사실만 남긴다.
            PinNamesAndValues<PatternSelectionRule,
                              Wassup.BattleCore.Combat.Emission.PatternSelectionRule>();
            PinMapping<PatternSelectionRule, Wassup.BattleCore.Combat.Emission.PatternSelectionRule>(
                CombatDefinitionBuilder.ToCoreSelection);
        }

        // ── 2026-09-24 드리프트 감사 — 캐스트로 건너던 나머지 쌍 ──────────────

        [Test]
        public void 칸_종류()
        {
            PinNamesAndValues<MapTileType, Wassup.BattleCore.Map.MapTile>();
            PinMapping<MapTileType, Wassup.BattleCore.Map.MapTile>(MatchDefinitionBuilder.ToCoreTile);
        }

        [Test]
        public void 지속_락_모드()
        {
            PinNamesAndValues<EnemyTargetMode, TargetMode>();
            PinMapping<EnemyTargetMode, TargetMode>(CombatDefinitionBuilder.ToCoreTargetMode);
        }

        [Test]
        public void 교전_이동()
        {
            PinNamesAndValues<Wassup.Data.EngageMovement, Wassup.BattleCore.EngageMovement>();
            PinMapping<Wassup.Data.EngageMovement, Wassup.BattleCore.EngageMovement>(
                MatchDefinitionBuilder.ToCoreEngage);
        }

        // 상수 집합은 enum 반사로 못 잡는다 — **값으로** 핀한다. 한쪽이 비트를 옮기면 빨개진다.

        [Test]
        public void 층_비트()
        {
            Assert.AreEqual((int)PlacementLayer.None, Wassup.BattleCore.Map.LayerBits.None);
            Assert.AreEqual((int)PlacementLayer.Ground, Wassup.BattleCore.Map.LayerBits.Ground);
            Assert.AreEqual((int)PlacementLayer.Path, Wassup.BattleCore.Map.LayerBits.Path);
            Assert.AreEqual((int)PlacementLayer.Air, Wassup.BattleCore.Map.LayerBits.Air);
            Assert.AreEqual((int)PlacementLayer.All, Wassup.BattleCore.Map.LayerBits.All);
            // 이름 집합도 본다 — 저작 쪽에 층이 늘면 코어 상수도 늘어야 한다.
            CollectionAssert.AreEquivalent(new[] { "None", "Ground", "Path", "Air", "All" },
                                           Enum.GetNames(typeof(PlacementLayer)),
                                           "PlacementLayer 에 층이 늘었다 — LayerBits 도 같이 늘린다");
        }

        [Test]
        public void 도형_종류()
        {
            Assert.AreEqual(Wassup.Data.AttackShapeBaked.OmniKind, Wassup.BattleCore.Combat.AttackShapeBaked.OmniKind);
            Assert.AreEqual(Wassup.Data.AttackShapeBaked.SectorKind, Wassup.BattleCore.Combat.AttackShapeBaked.SectorKind);
            Assert.AreEqual(Wassup.Data.AttackShapeBaked.BandKind, Wassup.BattleCore.Combat.AttackShapeBaked.BandKind);
            Assert.AreEqual(Wassup.BattleCore.Combat.AttackShapeBaked.SectorKind,
                            CombatDefinitionBuilder.ToCoreShapeKind(Wassup.Data.AttackShapeBaked.SectorKind));
            Assert.AreEqual(Wassup.BattleCore.Combat.AttackShapeBaked.BandKind,
                            CombatDefinitionBuilder.ToCoreShapeKind(Wassup.Data.AttackShapeBaked.BandKind));
            Assert.AreEqual(Wassup.BattleCore.Combat.AttackShapeBaked.OmniKind,
                            CombatDefinitionBuilder.ToCoreShapeKind(Wassup.Data.AttackShapeBaked.OmniKind));
        }

        [Test]
        public void 스택_저작의_종류는_산출물과_같은_매핑을_지난다()
        {
            // `StackModifierSO.kind` 가 통짜 캐스트로 건너던 자리. 매핑 자체는 `스택_종류` 가 핀하고,
            // 여기서는 **스택 저작 표가 그 매핑을 지나는가**를 본다.
            var so = UnityEngine.ScriptableObject.CreateInstance<StackModifierSO>();
            try
            {
                foreach (Wassup.Data.Authoring.StackKind k in Enum.GetValues(typeof(Wassup.Data.Authoring.StackKind)))
                {
                    so.kind = k;
                    var rows = MatchDefinitionBuilder.ToStackRuleDefs(new[] { so });
                    Assert.AreEqual(1, rows.Length);
                    Assert.AreEqual((int)CombatDefinitionBuilder.ToCoreStackKind(k), rows[0].Kind, $"스택 저작 {k}");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }
    }
}
