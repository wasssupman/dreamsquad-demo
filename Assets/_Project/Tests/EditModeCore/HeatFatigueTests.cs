using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Effects;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6b2 — **시간으로 쌓이는 두 기믹**(온천 열기 · 번아웃 피로).
    //
    // 주기와 대상 필터는 unit 7 의 바인딩이라, 여기서는 그 바인딩이 붙을 자리(`[Periodic]` seam)에
    // 테스트가 직접 핸들러를 달아 **한 주기의 걸음**만 증언한다.
    [TestFixture]
    public class HeatFatigueTests
    {
        private static (BattleMatch m, Unit defender) Board(MatchDefinition def)
        {
            var m = new BattleMatch(def);
            m.Begin();
            m.Apply(Command.DebugSpawnDefender(0, new int2(5, 2)));
            return (m, CoreCombatFixtures.First(m, UnitKind.Defender));
        }

        private static MatchDefinition OnsenDef(int flip = 2)
            => CoreGimmickFixtures.With(CoreCombatFixtures.Definition(defenderDamage: 0f),
                                        CoreGimmickFixtures.Onsen(flip: flip, heal: 0.1f, loss: 0.1f));

        // 한 틱에 한 번 열기를 올리는 가짜 바인딩(unit 7 의 자리).
        private static void HeatEveryTick(BattleMatch m, Unit u)
        {
            var spec = m.Definition.Gimmicks[0].Onsen;
            m.Seams.Register(Seam.Periodic, _ => GimmickStacks.AccrueHeat(u, in spec));
        }

        [Test]
        public void 열기_회복은_넘치는_만큼_잘라낸다_만피면_아무것도_안_들어온다()
        {
            var (m, d) = Board(OnsenDef());
            var heals = CoreCombatFixtures.Listen(m, CoreEventKind.HealApplied);
            HeatEveryTick(m, d);
            m.Tick();
            Assert.AreEqual(d.MaxHealth, d.Health, 1e-4f);
            Assert.AreEqual(0, heals.Count, "F10 — 만피 유닛에 회복 펄스가 안 난다");

            d.Health = d.MaxHealth - 20f;   // 10% = 50 인데 빈칸은 20
            m.Tick();
            Assert.AreEqual(d.MaxHealth, d.Health, 1e-3f, "오버힐 없음");
        }

        [Test]
        public void 열기가_반전_임계를_넘으면_손실이고_체력_1_밑으로는_안_내린다()
        {
            var (m, d) = Board(OnsenDef(flip: 2));
            m.Apply(Command.DebugSetHeat(d.Id, 2));   // 다음 누적이 3 → 반전
            HeatEveryTick(m, d);
            d.Health = 3f;
            m.Tick();
            Assert.AreEqual(1f, d.Health, 1e-4f, "F10 — 과열은 체력 1 을 남긴다");
            CoreCombatFixtures.Tick(m, 30);
            Assert.IsFalse(d.Dead, "열기는 사망 원인이 될 수 없다");
            Assert.AreEqual(1f, d.Health, 1e-4f);
        }

        [Test]
        public void 열기는_상한에서_멈춘다()
        {
            var (m, d) = Board(OnsenDef());
            HeatEveryTick(m, d);
            CoreCombatFixtures.Tick(m, 20);
            Assert.AreEqual(m.Definition.Gimmicks[0].Onsen.HeatMaxStack, d.Stacks.Heat);
        }

        // 피로 저작 — 라이브 모양(같은 임계 5 에 스탯 여러 줄, 마지막 줄이 소비형)을 줄여 옮긴 픽스처.
        private static MatchDefinition BurnoutDef()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f);
            def.StackRules = new[]
            {
                new StackRuleDef
                {
                    Id = "fixture_fatigue",
                    Kind = (int)StackKind.Fatigue,
                    MaxStack = 5,
                    PerAppDuration = 25f,
                    Thresholds = new[]
                    {
                        new StackThresholdDef
                        {
                            AtStack = 5, Mode = StackThresholdMode.Edge, Derived = StackDerivedKind.ApplyStat,
                            Magnitude = 0.8f, Duration = 15f, Stat = (int)StatKind.DamageMul, Op = (int)CombineOp.Multiplicative,
                        },
                        new StackThresholdDef
                        {
                            AtStack = 5, Mode = StackThresholdMode.Consume, Derived = StackDerivedKind.ApplyStat,
                            Magnitude = 0.8f, Duration = 15f, Stat = (int)StatKind.AttackSpeedMul, Op = (int)CombineOp.Multiplicative,
                        },
                    },
                },
            };
            return CoreGimmickFixtures.With(def, CoreGimmickFixtures.Burnout(amount: 5, rule: 0));
        }

        [Test]
        public void 피로_임계는_번아웃_출처로_스탯을_건다()
        {
            var (m, d) = Board(BurnoutDef());
            m.Apply(Command.DebugSetStack(d.Id, StackKind.Fatigue, 5));
            m.Tick();

            Assert.AreEqual(0.8f, d.Modifiers.Effective.DamageMul, 1e-5f);
            bool burnout = false;
            foreach (var slot in d.Modifiers.Slots) burnout |= slot.Origin == ModifierOrigin.Burnout;
            Assert.IsTrue(burnout, "피로 → ModifierOrigin.Burnout");
            Assert.AreEqual(0, d.Stacks.CountOf(StackKind.Fatigue), "마지막 줄이 소비형 — 다시 쌓인다");
        }

        [Test]
        public void 증상_피로_누적은_스탯_적용_뒤라_한_틱_뒤에_번아웃이_든다()
        {
            // 옛 `FatigueAccrualSystem` 은 `[UpdateAfter(ModifierApplySystem)]` — 그 1프레임이 현행이다.
            var (m, d) = Board(BurnoutDef());
            var spec = m.Definition.Gimmicks[0].Burnout;
            bool once = false;
            m.Seams.Register(Seam.Periodic, ctx =>
            {
                if (once) return;
                once = true;
                GimmickStacks.RequestFatigue(ctx.World, d, in spec);
            });

            m.Tick();
            Assert.AreEqual(5, d.Stacks.CountOf(StackKind.Fatigue), "누적은 이번 틱에 섰다");
            Assert.AreEqual(1f, d.Modifiers.Effective.DamageMul, 1e-5f, "그러나 임계는 아직 — 스탯 적용 뒤라서");

            m.Tick();
            Assert.AreEqual(0.8f, d.Modifiers.Effective.DamageMul, 1e-5f, "한 틱 뒤에 번아웃");
        }

        [Test]
        public void 헤드리스_하네스에서_커맨드_예약만으로_스택이_선다()
        {
            var def = BurnoutDef();
            // 방어유닛이 판의 첫 유닛이 아닐 수 있다(마음 타워) — 스폰 뒤에 id 를 찾아 넣는다.
            var probe = new BattleMatch(def);
            probe.Begin();
            probe.Apply(Command.DebugSpawnDefender(0, new int2(5, 2)));
            var id = CoreCombatFixtures.First(probe, UnitKind.Defender).Id;

            var schedule = new CommandSchedule()
                .Add(0, Command.DebugSpawnDefender(0, new int2(5, 2)))
                .Add(2, Command.DebugSetStack(id, StackKind.Fatigue, 3));
            var result = CoreHarness.Run(def, schedule, 5, "stack_debug");
            var d = CoreCombatFixtures.First(result.Match, UnitKind.Defender);
            Assert.AreEqual(id, d.Id, "같은 정의표·같은 순서 = 같은 id(결정론)");
            Assert.AreEqual(3, d.Stacks.CountOf(StackKind.Fatigue));
        }
    }
}
