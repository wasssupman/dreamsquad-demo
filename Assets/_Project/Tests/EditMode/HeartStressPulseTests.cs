using NUnit.Framework;
using UnityEngine;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    // heart-stress-axis unit 1 rev 2 — 스트레스 = 심박수.
    //
    // 고정하는 계약 넷:
    //   (1) 심박은 스트레스에 **단조 증가**한다. 안 그러면 「나아지는 중」으로 오독된다.
    //   (2) 파형이 **비대칭**이다 — lub-dub 뒤에 쉼이 있어야 «심장» 으로 읽힌다.
    //       사인파면 «숨쉬기» 다(위아래 대칭 + 쉼 없음).
    //   (3) 위상은 **누적**이라 bpm 이 바뀌어도 튀지 않는다. `time × bpm` 으로 접으면
    //       심박이 빨라지는 순간 박이 끊긴다.
    //   (4) 세기 곡선은 후반 가중이다 — 낮은 스트레스에서 화면이 벌써 붉으면
    //       판이 항상 위급해 보여 진짜 위급한 구간이 안 읽힌다.
    public class HeartStressPulseTests
    {
        private const float Tol = 1e-4f;

        [Test]
        public void Bpm_RisesWithStress()
        {
            Assert.AreEqual(52f, HeartStressPulse.Bpm(0f, 52f, 168f), Tol);
            Assert.AreEqual(168f, HeartStressPulse.Bpm(1f, 52f, 168f), Tol);
            Assert.Less(HeartStressPulse.Bpm(0.3f, 52f, 168f),
                        HeartStressPulse.Bpm(0.7f, 52f, 168f));
        }

        [Test]
        public void Bpm_ClampsOutOfRangeStress()
        {
            Assert.AreEqual(52f, HeartStressPulse.Bpm(-1f, 52f, 168f), Tol);
            Assert.AreEqual(168f, HeartStressPulse.Bpm(9f, 52f, 168f), Tol);
        }

        [Test]
        public void Beat_StaysInUnitRange()
        {
            for (int i = 0; i <= 400; i++)
            {
                float v = HeartStressPulse.Beat(i / 400f);
                Assert.GreaterOrEqual(v, 0f, $"phase {i / 400f}");
                Assert.LessOrEqual(v, 1f, $"phase {i / 400f}");
            }
        }

        [Test]
        public void Beat_IsLubDub_TwoThumpsThenRest()
        {
            // 첫 박이 가장 강하다.
            Assert.AreEqual(1f, HeartStressPulse.Beat(0f), 0.01f, "첫 박은 최대");

            // 둘째 박이 있다 — 첫 박보다 약하지만 0 은 아니다.
            float second = HeartStressPulse.Beat(0.17f);
            Assert.Greater(second, 0.3f, "둘째 박이 없으면 lub-dub 이 아니라 단발이다");
            Assert.Less(second, 1f, "둘째 박은 첫 박보다 약하다");

            // 그리고 **쉼** — 후반부는 완전히 조용하다. 이 비대칭이 «심장» 의 신호다.
            for (int i = 45; i <= 95; i++)
                Assert.AreEqual(0f, HeartStressPulse.Beat(i / 100f), Tol,
                    $"phase {i}% 에서 쉬어야 한다 — 쉼이 없으면 숨쉬기로 읽힌다");
        }

        [Test]
        public void Beat_IsPeriodic()
        {
            for (int i = 0; i <= 20; i++)
            {
                float p = i / 20f;
                Assert.AreEqual(HeartStressPulse.Beat(p), HeartStressPulse.Beat(p + 3f), Tol);
                Assert.AreEqual(HeartStressPulse.Beat(p), HeartStressPulse.Beat(p - 2f), Tol);
            }
        }

        [Test]
        public void AdvancePhase_WrapsAndNeverJumpsOnBpmChange()
        {
            // 심박이 판 중에 계속 바뀌는데(스트레스가 오르내린다) 위상은 이어져야 한다.
            float phase = 0.9f;
            float a = HeartStressPulse.AdvancePhase(phase, 0.1f, 60f);   // +0.1
            Assert.AreEqual(0f, a, 0.001f, "1.0 을 넘으면 0 으로 감긴다");

            // bpm 을 크게 바꿔도 «직전 위상 + 증분» 이라 튀지 않는다.
            float slow = HeartStressPulse.AdvancePhase(0.5f, 1f / 60f, 52f);
            float fast = HeartStressPulse.AdvancePhase(0.5f, 1f / 60f, 168f);
            Assert.Greater(slow, 0.5f);
            Assert.Greater(fast, slow, "빠른 심박이 위상을 더 밀어야 한다");
            Assert.Less(fast, 0.6f, "한 프레임 증분이 박을 통째로 건너뛰면 안 된다");
        }

        [Test]
        public void AdvancePhase_AlwaysInUnitRange()
        {
            float phase = 0f;
            for (int i = 0; i < 600; i++)
            {
                phase = HeartStressPulse.AdvancePhase(phase, 1f / 60f, 168f);
                Assert.GreaterOrEqual(phase, 0f);
                Assert.Less(phase, 1f);
            }
        }

        [Test]
        public void BeatScale_IsBrightnessMultiplier()
        {
            Assert.AreEqual(1f, HeartStressPulse.BeatScale(1f, 0.5f), Tol, "박 정점 = 최대 밝기");
            Assert.AreEqual(0.5f, HeartStressPulse.BeatScale(0f, 0.5f), Tol, "쉼 = 1 − depth");
            Assert.AreEqual(1f, HeartStressPulse.BeatScale(0f, 0f), Tol, "depth 0 = 안 뛴다");
        }

        [Test]
        public void Intensity_IsBackLoaded()
        {
            // 곡선 지수 2 — 절반 스트레스에서 세기는 절반보다 훨씬 낮아야 한다.
            Assert.Less(HeartStressPulse.Intensity(0.5f, 2f), 0.3f,
                "낮은 스트레스에서 화면이 벌써 붉으면 진짜 위급한 구간이 안 읽힌다");
            Assert.AreEqual(0f, HeartStressPulse.Intensity(0f, 2f), Tol);
            Assert.AreEqual(1f, HeartStressPulse.Intensity(1f, 2f), Tol);
        }

        [Test]
        public void Intensity_IsMonotonic()
        {
            float prev = -1f;
            for (int i = 0; i <= 100; i++)
            {
                float v = HeartStressPulse.Intensity(i / 100f, 2f);
                Assert.GreaterOrEqual(v, prev, $"{i}% 에서 세기가 줄었다");
                prev = v;
            }
        }
    }
}
