using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wassup.Sim;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-K/5 — **조립 지점의 계약.**
    ///
    /// 조립이 두 벌이 되면 A/B 가 서로 다른 파이프라인을 비교하게 되고, 그 차이는 골든이
    /// 갈릴 때까지 보이지 않는다. 그래서 `{1..44}` 전수 단정을 **`SimRuntime` 위에서** 다시
    /// 세운다 — 테스트가 자기만의 조립을 들고 있으면 그 단정이 프로덕션을 증언하지 않는다.
    /// </summary>
    public class SimRuntimeAssemblyTests
    {
        private static SimRuntime New() => new SimRuntime(new SimConfig(1u, 1u));

        [Test]
        public void 조립_지점이_1부터_44까지_정확히_한_번씩_등록한다()
        {
            // 🎯 `SimPipeline` 은 번호 **중복만** 막고 누락은 못 막는다 — 전수 단정이 유일한 증인.
            int[] orders = New().Pipeline.Steps.Select(s => s.Order).OrderBy(o => o).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(1, 44).ToArray(), orders);
        }

        [Test]
        public void 모든_스텝의_phase_가_캡처_번호_구간과_맞는다()
        {
            foreach (SimStep s in New().Pipeline.Steps)
                Assert.AreEqual(SimPipeline.PhaseForOrder(s.Order), s.Phase,
                    $"#{s.Order}({s.Name}) 의 phase 가 캡처 번호 구간과 어긋난다");
        }

        [Test]
        public void 월드와_채널은_런타임이_소유한다()
        {
            var rt = New();
            Assert.IsNotNull(rt.World);
            Assert.IsNotNull(rt.Channels);
            Assert.AreEqual(0, rt.World.Tick);
            Assert.AreEqual(0.0, rt.World.BattleClock);
        }

        [Test]
        public void 두_런타임은_상태를_공유하지_않는다()
        {
            // 채널을 정적으로 두면 두 판이 서로의 이벤트를 먹는다 — A/B 는 그 상태로 돌아간다.
            var a = New();
            var b = New();
            a.World.Create();
            Assert.AreEqual(1, a.World.SpawnedCount);
            Assert.AreEqual(0, b.World.SpawnedCount);
            Assert.AreNotSame(a.Channels, b.Channels);
        }

        // ── 호스트 스텝 (P0/P13) ──────────────────────────────────────────────

        [Test]
        public void 호스트_스텝은_P0_두_조각과_P13_에만_들어간다()
        {
            var rt = New();
            Assert.DoesNotThrow(() => rt.RegisterHostStep(SimPhase.CommandIntake, _ => { }));
            Assert.DoesNotThrow(() => rt.RegisterHostStep(SimPhase.FramePrologue, _ => { }));
            Assert.DoesNotThrow(() => rt.RegisterHostStep(SimPhase.PostSim, _ => { }));

            foreach (SimPhase p in SimTick.PhaseOrder.Where(
                         p => p != SimPhase.CommandIntake && p != SimPhase.FramePrologue
                              && p != SimPhase.PostSim))
                Assert.Throws<InvalidOperationException>(() => rt.RegisterHostStep(p, _ => { }),
                    $"{p} 는 클러스터 몫이다 — 호스트가 끼면 캡처 표가 정본이 아니게 된다");
        }

        [Test]
        public void 틱이_시작되면_스텝을_더_받지_않는다()
        {
            // ⚠ 판 도중에 파이프라인이 바뀌면 A/B 가 "다른 판" 으로 갈린다.
            var rt = New();
            rt.StepOneTick(0.016f);
            Assert.Throws<InvalidOperationException>(
                () => rt.RegisterHostStep(SimPhase.PostSim, _ => { }));
        }

        [Test]
        public void 호스트_스텝이_실제로_양_끝에서_돈다()
        {
            var rt = New();
            var log = new List<string>();
            rt.RegisterHostStep(SimPhase.CommandIntake, _ => log.Add("P0a"));
            rt.RegisterHostStep(SimPhase.FramePrologue, _ => log.Add("P0b"));
            rt.RegisterHostStep(SimPhase.PostSim, _ => log.Add("P13"));

            rt.StepOneTick(0.016f);
            CollectionAssert.AreEqual(new[] { "P0a", "P0b", "P13" }, log);
        }

        // ── 틱 ────────────────────────────────────────────────────────────────

        [Test]
        public void 빈_판을_돌려도_던지지_않는다()
        {
            // 44 시스템 전부가 게이트 없이 도는 첫 자리다 — 분류 C 가 다 닫혀 있어도 안전해야 한다.
            var rt = New();
            Assert.DoesNotThrow(() => { for (int i = 0; i < 5; i++) rt.StepOneTick(0.016f); });
            Assert.AreEqual(5, rt.World.Tick);

            // ⚠ `5 * 0.016f` 가 아니다 — 시계는 **double 로 다섯 번 누적**한다(18-K/3).
            //   곱셈으로 기대값을 쓰면 이 단정이 곧바로 어긋난다(실제로 어긋났다).
            double expected = 0.0;
            for (int i = 0; i < 5; i++) expected += 0.016f;
            Assert.AreEqual(expected, rt.World.BattleClock);
        }

        [Test]
        public void 상태_원문이_같은_입력에서_같다()
        {
            // A/B 비교의 최소 성질 — 같은 씨앗·같은 스텝이면 같은 문자열이다.
            var a = New();
            var b = New();
            for (int i = 0; i < 3; i++) { a.StepOneTick(0.016f); b.StepOneTick(0.016f); }
            Assert.AreEqual(a.BuildStateCanonical(default), b.BuildStateCanonical(default));
        }

        [Test]
        public void 상태_원문이_헤더와_월드를_모두_싣는다()
        {
            var rt = New();
            rt.World.Create();
            rt.StepOneTick(0.25f);
            string s = rt.BuildStateCanonical(new SimLegacyTraceHeader { goals = 3 });

            StringAssert.Contains("battleClock=0.25\n", s, "월드가 소유하는 값");
            StringAssert.Contains("simEntityIdCounter=1\n", s, "월드가 소유하는 값");
            StringAssert.Contains("goals=3\n", s, "헤더가 나르는 값");
            StringAssert.Contains("entity+0\n", s, "첫 스폰은 구 simId 0");
        }
    }
}
