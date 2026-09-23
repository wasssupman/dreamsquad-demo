using NUnit.Framework;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6a — 스택 저작을 읽는 규칙.
    [TestFixture]
    public class StackRuleTests
    {
        private static StackRuleDef Rule(string id, StackKind kind, int maxStack,
                                         float perApp, params StackThresholdDef[] thresholds)
            => new StackRuleDef
            {
                Id = id,
                Kind = (int)kind,
                MaxStack = maxStack,
                PerAppDuration = perApp,
                Thresholds = thresholds ?? System.Array.Empty<StackThresholdDef>(),
            };

        private static StackThresholdDef At(int atStack, StackThresholdMode mode)
            => new StackThresholdDef
            {
                AtStack = atStack,
                Mode = mode,
                Derived = StackDerivedKind.ApplyStun,
                Magnitude = 1f,
            };

        // ── 자산별 규칙 ──────────────────────────────────────────────────────

        [Test]
        public void 같은_종류라도_저작_자산마다_줄이_갈린다()
        {
            // F31 — 옛 전투는 종류당 전역 한 벌이라 드래곤을 4→10 올렸더니 킨들러가 같이
            // 올라갔다. 줄이 갈리면 그 결합이 사라진다.
            var rules = new[]
            {
                Rule("dragon_fire", StackKind.Fire, maxStack: 10, perApp: 3f),
                Rule("kindler_fire", StackKind.Fire, maxStack: 4, perApp: 3f),
            };

            Assert.AreEqual(10, StackRules.MaxStackOf(rules, 0));
            Assert.AreEqual(4, StackRules.MaxStackOf(rules, 1));
        }

        [Test]
        public void 미지정은_그_종류의_첫_줄로_떨어진다()
        {
            var rules = new[]
            {
                Rule("bleed", StackKind.Bleed, 5, 2f),
                Rule("dragon_fire", StackKind.Fire, 10, 3f),
                Rule("kindler_fire", StackKind.Fire, 4, 3f),
            };

            Assert.AreEqual(1, StackRules.Resolve(rules, StackKind.Fire, -1), "미지정 → 첫 불 줄");
            Assert.AreEqual(2, StackRules.Resolve(rules, StackKind.Fire, 2), "번호를 실으면 그 줄");
            Assert.AreEqual(1, StackRules.Resolve(rules, StackKind.Fire, 0),
                "번호가 남의 종류를 가리키면 폴백 — 기본값 0 이 조용히 남의 줄을 밟지 않는다");
            Assert.AreEqual(-1, StackRules.Resolve(rules, StackKind.Poison, -1), "없으면 없다");
        }

        [Test]
        public void 미등록_종류의_최대치는_5_다()
        {
            // F14 — 폴백이 없으면 미등록 스택이 무한히 증가한다.
            Assert.AreEqual(5, StackRules.DefaultMaxStack);
            Assert.AreEqual(5, StackRules.MaxStackOf(System.Array.Empty<StackRuleDef>(), -1));
            Assert.AreEqual(5, StackRules.MaxStackOf(new[] { Rule("x", StackKind.Fire, 0, 1f) }, 0),
                "0 저작도 폴백으로 접힌다");
        }

        // ── 임계 ─────────────────────────────────────────────────────────────

        [Test]
        public void 임계는_올라가는_길에만_발화한다()
        {
            var rule = At(5, StackThresholdMode.Edge);
            Assert.IsTrue(StackRules.Fires(in rule, prevStack: 4, stackCount: 5));
            Assert.IsFalse(StackRules.Fires(in rule, prevStack: 5, stackCount: 5), "이미 발화했다");
            Assert.IsFalse(StackRules.Fires(in rule, prevStack: 5, stackCount: 3), "내려온 것은 발화가 아니다");
            Assert.IsTrue(StackRules.Fires(in rule, prevStack: 2, stackCount: 7), "건너뛴 임계도 난다");
        }

        [Test]
        public void 소비형은_차감된_최종_중첩에_기준을_맞춘다()
        {
            // F13 — 안 맞추면 같은 임계가 재발화한다.
            var consume = At(5, StackThresholdMode.Consume);
            Assert.AreEqual(2, StackRules.AfterConsume(in consume, 7));
            Assert.AreEqual(0, StackRules.AfterConsume(in consume, 3), "0 아래로는 안 내려간다");

            var edge = At(5, StackThresholdMode.Edge);
            Assert.AreEqual(7, StackRules.AfterConsume(in edge, 7), "Edge 는 스택을 안 깎는다");
        }

        [Test]
        public void 임계_배열은_비내림차순이어야_한다()
        {
            // 「엄격 증가」가 아니다 — 라이브 피로도 저작이 5·5·5 로 세 스탯을 한꺼번에 건다.
            Assert.IsTrue(StackRules.IsAscending(new[]
            {
                At(1, StackThresholdMode.Edge), At(2, StackThresholdMode.Edge),
                At(5, StackThresholdMode.Consume),
            }));
            Assert.IsTrue(StackRules.IsAscending(new[]
            {
                At(5, StackThresholdMode.Edge), At(5, StackThresholdMode.Edge),
                At(5, StackThresholdMode.Consume),
            }), "같은 임계 여러 줄은 정상 저작이다");
            Assert.IsFalse(StackRules.IsAscending(new[]
            {
                At(5, StackThresholdMode.Edge), At(2, StackThresholdMode.Edge),
            }), "어긋난 저작은 임계 일부를 조용히 건너뛴다 — 빌더가 loud 하게 거절한다");
            Assert.IsTrue(StackRules.IsAscending(null));
        }

        // ── 슬롯 ─────────────────────────────────────────────────────────────

        [Test]
        public void 스택은_출처와_종류_2축으로_갈린다()
        {
            // F2 — 꼬리표(origin)를 안 싣는 것이 스탯 슬롯과의 **의도된 비대칭**이다.
            var set = new StackSet();
            set.Add(new SimEntityId(1), StackKind.Fire, -1, 1, 5, 3f);
            set.Add(new SimEntityId(2), StackKind.Fire, -1, 1, 5, 3f);
            set.Add(new SimEntityId(1), StackKind.Ice, -1, 1, 5, 3f);
            Assert.AreEqual(3, set.Count);

            set.Add(new SimEntityId(1), StackKind.Fire, -1, 2, 5, 3f);
            Assert.AreEqual(3, set.Count, "같은 (출처, 종류)는 한 슬롯");
            Assert.AreEqual(3, set.Slots[0].Count);
        }

        [Test]
        public void 중첩은_상한에서_멈추고_지속은_매_적용이_갱신한다()
        {
            var set = new StackSet();
            for (int i = 0; i < 20; i++) set.Add(new SimEntityId(1), StackKind.Fire, -1, 1, 5, 3f);
            Assert.AreEqual(5, set.Slots[0].Count);
            Assert.AreEqual(3f, set.Slots[0].Remaining, 1e-4f);
        }
    }
}
