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
            PinNamesAndValues<Wassup.Battle.Effects.StatKind, Wassup.BattleCore.Effects.StatKind>();
            PinMapping<Wassup.Battle.Effects.StatKind, Wassup.BattleCore.Effects.StatKind>(
                CombatDefinitionBuilder.ToCoreStat);
        }

        [Test]
        public void 결합_연산자()
        {
            PinNamesAndValues<Wassup.Battle.Effects.CombineOp, Wassup.BattleCore.Effects.CombineOp>();
            PinMapping<Wassup.Battle.Effects.CombineOp, Wassup.BattleCore.Effects.CombineOp>(
                CombatDefinitionBuilder.ToCoreOp);
        }

        [Test]
        public void 스택_종류()
        {
            PinNamesAndValues<Wassup.Battle.Effects.StackKind, Wassup.BattleCore.Effects.StackKind>();
            PinMapping<Wassup.Battle.Effects.StackKind, Wassup.BattleCore.Effects.StackKind>(
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
    }
}
