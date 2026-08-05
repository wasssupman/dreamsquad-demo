// battle-sim-extraction unit 18-E/6 — 환경 클러스터 조립 계약.
// 개별 규칙은 각 복제 테스트가 진다. 여기서 보는 것은 **배치**다 — phase 매핑과,
// 두 클러스터가 같은 phase 에 섞여도 캡처 번호가 순서를 정하는지.
using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Effects;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    public class SimEnvironmentClusterTests
    {
        [Test]
        public void RegistersSixInFieldsAndPeriodic_AndOneInPreCombat()
        {
            var tick = new SimPipeline().Add(new EnvironmentCluster(new SimChannels()).Steps()).Build();
            Assert.AreEqual(6, tick.StepCount(SimPhase.FieldsAndPeriodic), "#1·#2·#3·#5·#6·#7");
            Assert.AreEqual(1, tick.StepCount(SimPhase.PreCombat), "#16 PatrolField");
        }

        [Test]
        public void EveryStep_LandsInThePhaseItsCaptureNumberImplies()
        {
            foreach (var s in new EnvironmentCluster(new SimChannels()).Steps())
                Assert.AreEqual(SimPipeline.PhaseForOrder(s.Order), s.Phase,
                    $"#{s.Order} {s.Name} 의 phase 가 캡처 번호와 어긋난다.");
        }

        [Test]
        public void HazardCastIsAbsent_BecauseItMovedTo18I()
        {
            // #18 은 `DcTriggerSlot`(쓰기 소유자 = AttackSystem)의 버퍼 존재를 본다.
            // 그 타입을 추측으로 먼저 옮기면 필드 하나만 틀려도 parity 가 깨지므로 18-I 가 가져간다.
            // 이 테스트는 **누락이 사고가 아니라 결정**임을 코드에 남긴다 — 18-I 가 여기를 고친다.
            var orders = new List<int>();
            foreach (var s in new EnvironmentCluster(new SimChannels()).Steps()) orders.Add(s.Order);
            CollectionAssert.DoesNotContain(orders, 18,
                "#18 HazardCast 는 의도적으로 18-I 소속이다(클러스터 주석 참조).");
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 5, 6, 7, 16 }, orders,
                "18-E 가 소유하는 캡처 번호 7개. #4 는 18-J 라 여기 없다.");
        }

        [Test]
        public void TwoClusters_InterleaveByCaptureNumber_NotByAddOrder()
        {
            // 이것이 18-D 가 `SimPipeline` 을 만든 이유다. 클러스터를 **뒤에** 얹어도
            // 캡처 번호가 앞이면 먼저 돈다 — 순서의 정본이 캡처 표 하나로 유지된다.
            var ch = new SimChannels();
            var modifier = new ModifierCluster(ch);      // P2 #9 · P7 #28~32
            var environment = new EnvironmentCluster(ch); // P1 #1~7 · P3 #16

            var pipeline = new SimPipeline().Add(modifier.Steps()).Add(environment.Steps());
            SimTick tick = pipeline.Build();

            Assert.AreEqual(6, tick.StepCount(SimPhase.FieldsAndPeriodic));
            Assert.AreEqual(1, tick.StepCount(SimPhase.Intake), "#9");
            Assert.AreEqual(1, tick.StepCount(SimPhase.PreCombat), "#16");
            Assert.AreEqual(5, tick.StepCount(SimPhase.ModifierTick), "#28~32");
        }

        [Test]
        public void DuplicateCaptureNumber_Throws_RatherThanSilentlyReordering()
        {
            var ch = new SimChannels();
            var a = new EnvironmentCluster(ch);
            var b = new EnvironmentCluster(ch);   // 같은 번호를 두 번 신고
            Assert.Throws<System.InvalidOperationException>(
                () => new SimPipeline().Add(a.Steps()).Add(b.Steps()));
        }

        [Test]
        public void HazardIndexIsBuiltBeforeZoneReadsIt_WithinTheSameTick()
        {
            // #2 → #5 순서의 관측점. 뒤집히면 존이 **한 틱 낡은 인덱스**를 본다.
            var ch = new SimChannels();
            var world = new SimWorld(new SimConfig(1u, 1u));
            var index = new HazardCellIndex();
            world.Set(world.Create(), new HazardSingleton { cellToEffects = index });

            var grid = new SimInt2(8, 8);
            int n = grid.x * grid.y;
            world.Set(world.Create(), new FlowFieldSingleton
            {
                flow = new SimVec2[n], dist = new int[n],
                gridSize = grid, tileSize = 1f, origin = SimVec3.Zero, goalCell = new SimInt2(7, 7),
            });

            var cell = new SimInt2(3, 3);
            var hazard = world.Create();
            world.Set(hazard, new Hazard { remainingLife = 10f });
            world.AddBuffer<HazardCellsBuffer>(hazard).Add(new HazardCellsBuffer { cell = cell });
            world.AddBuffer<HazardEffectsBuffer>(hazard).Add(new HazardEffectsBuffer
            {
                effect = new HazardEffect { kind = CcKind.Slow, param1 = 0.5f, restDuration = 1f },
            });

            var victim = world.Create();
            world.Set(victim, new FactionTag { value = Faction.Enemy });
            world.Set(victim, Wassup.Sim.Movement.SimTransform.FromPosition(new SimVec3(3, 0, 3)));
            world.Set(victim, new Wassup.Sim.Movement.PathFollowState { speed = 1f });

            SimTick tick = new SimPipeline().Add(new EnvironmentCluster(ch).Steps()).Build();
            tick.Run(world, 0.016f);

            Assert.AreEqual(1, ch.StatApply.Count,
                "해저드를 만든 **첫 틱에** 존이 먹는다 — #2 가 #5 보다 앞이라서다.");
        }
    }
}
