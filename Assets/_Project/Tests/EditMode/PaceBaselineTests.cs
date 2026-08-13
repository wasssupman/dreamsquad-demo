using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // wave-pull-revival unit 3 — 진출 예상선(가짜 par).
    //
    // 고정하는 것: ①저작이 없으면 «표시 안 함»이지 0 이 아니다 ②경사로 오른다(계단 금지)
    // ③범위 밖 시각에서 값이 튀지 않는다 ④killScore 가중치를 그대로 존중한다(계약 7).
    //
    // ①이 중요한 이유: par 0 을 그리면 화면에 「+1,240점」이 떠서 **항상 앞서고 있다는
    // 거짓말**이 된다. 실패가 조용하고 그럴듯해서 눈으로는 안 잡힌다.
    public class PaceBaselineTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private AttackUnitData Enemy(string id, int killScore)
        {
            var unit = ScriptableObject.CreateInstance<AttackUnitData>();
            _created.Add(unit);
            unit.id = id;
            unit.displayName = id;
            unit.killScore = killScore;
            return unit;
        }

        // 웨이브 3개: 20초 간격, 각각 10점 / 20점 / 30점.
        private GeneratedWavePlan Plan()
        {
            var basic = Enemy("basic", 1);
            var tanker = Enemy("tanker", 3);
            var waves = new List<GeneratedWave>
            {
                new(0, 0f, new List<WaveSpawnGroup> { new(basic, 10, 0f, 0) }, 0f,
                    WaveExpandMode.RoundRobin, "a"),
                new(1, 20f, new List<WaveSpawnGroup> { new(basic, 20, 0f, 0) }, 0f,
                    WaveExpandMode.RoundRobin, "a"),
                new(2, 40f, new List<WaveSpawnGroup> { new(tanker, 10, 0f, 0) }, 0f,
                    WaveExpandMode.RoundRobin, "a"),
            };
            return new GeneratedWavePlan(1, 4, 180f, 20f, 0.5f, waves, 2f);
        }

        [Test]
        public void 저작이_0이하면_표시하지_않는다()
        {
            var plan = Plan();
            Assert.IsFalse(PaceBaseline.TryExpectedScore(plan, 30f, 0f, out _),
                "par 비율 0 인데 값을 내놨다 — 화면에 «항상 앞섬» 거짓말이 뜬다");
            Assert.IsFalse(PaceBaseline.TryExpectedScore(plan, 30f, -1f, out _),
                "음수 par 비율인데 값을 내놨다");
        }

        [Test]
        public void 플랜이_비면_표시하지_않는다()
        {
            var empty = new GeneratedWavePlan(1, 4, 180f, 20f, 0.5f,
                new List<GeneratedWave>(), 2f);
            Assert.IsFalse(PaceBaseline.TryExpectedScore(empty, 10f, 1f, out _));
            Assert.IsFalse(PaceBaseline.TryExpectedScore(default, 10f, 1f, out _));
        }

        [Test]
        public void 웨이브_창_안에서_경사로_오른다()
        {
            var plan = Plan();

            Assert.IsTrue(PaceBaseline.TryExpectedScore(plan, 0f, 1f, out int atStart));
            Assert.IsTrue(PaceBaseline.TryExpectedScore(plan, 10f, 1f, out int atMid));
            Assert.IsTrue(PaceBaseline.TryExpectedScore(plan, 20f, 1f, out int atNext));

            Assert.AreEqual(0, atStart, "판 시작의 par 는 0 이어야 한다");
            Assert.AreEqual(5, atMid, "첫 웨이브(10점) 창의 절반이면 5점이어야 한다 — 계단이 아니라 경사");
            Assert.AreEqual(10, atNext, "첫 웨이브 창이 끝나면 그 웨이브 전량이 par 다");
        }

        [Test]
        public void 단조_비감소다()
        {
            var plan = Plan();
            int prev = -1;
            for (float t = 0f; t <= 70f; t += 0.5f)
            {
                Assert.IsTrue(PaceBaseline.TryExpectedScore(plan, t, 1f, out int now));
                Assert.GreaterOrEqual(now, prev, $"t={t} 에서 예상선이 내려갔다");
                prev = now;
            }
        }

        [Test]
        public void 범위_밖_시각에서도_값이_튀지_않는다()
        {
            var plan = Plan();
            // 총점 = 10×1 + 20×1 + 10×3 = 60
            Assert.IsTrue(PaceBaseline.TryExpectedScore(plan, -5f, 1f, out int before));
            Assert.AreEqual(0, before, "판 시작 전인데 par 가 0 이 아니다");

            Assert.IsTrue(PaceBaseline.TryExpectedScore(plan, 9999f, 1f, out int after));
            Assert.AreEqual(60, after, "마지막 웨이브를 지나면 전량이 par 여야 한다");
        }

        [Test]
        public void 킬_가중치를_존중한다()
        {
            var plan = Plan();
            // 마지막 웨이브는 killScore 3 짜리 10기 = 30점. 가중치를 무시하면 10 이 된다.
            Assert.IsTrue(PaceBaseline.TryExpectedScore(plan, 9999f, 1f, out int total));
            Assert.AreEqual(60, total,
                "가중치(탱커 3점)를 무시하고 마릿수로 셌다 — 계약 7 위반");
        }

        [Test]
        public void par_비율이_그대로_곱해진다()
        {
            var plan = Plan();
            Assert.IsTrue(PaceBaseline.TryExpectedScore(plan, 9999f, 0.5f, out int half));
            Assert.AreEqual(30, half, "par 비율 0.5 가 반영되지 않았다");
        }
    }
}
