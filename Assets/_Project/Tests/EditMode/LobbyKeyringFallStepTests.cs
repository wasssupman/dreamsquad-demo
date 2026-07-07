using NUnit.Framework;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // lobby-keyring-drag 2 — 낙하 스텝(중력 적분 + 착지/반동 판정) 순수 계산 회귀 테스트.
    public class LobbyKeyringFallStepTests
    {
        private const float Gravity = 4000f;
        private const float BounceDamping = 0.35f;
        private const float BounceMinSpeed = 300f;
        private const float Floor = -400f;
        private const float Dt = 1f / 60f;

        [Test]
        public void HighDrop_BouncesAtLeastOnce_ThenSettlesOnFloor()
        {
            float y = Floor + 600f;
            float vy = 0f;
            bool bounced = false;
            bool landed = false;
            for (int i = 0; i < 10000 && !landed; i++)
            {
                landed = LobbyKeyringDrag.FallStep(ref y, ref vy, Floor, Dt,
                    Gravity, BounceDamping, BounceMinSpeed);
                if (!landed && vy > 0f) bounced = true; // 상승 속도 = 반동 발생
            }
            Assert.IsTrue(bounced, "높은 낙하는 최소 1회 반동해야 한다");
            Assert.IsTrue(landed, "반동 후 결국 정착해야 한다");
            Assert.AreEqual(Floor, y, 1e-3f);
            Assert.AreEqual(0f, vy);
        }

        [Test]
        public void SlowImpact_SettlesImmediately_WithoutBounce()
        {
            float y = Floor + 1f; // 1px 위 — 착지 속도가 bounceMinSpeed 미만
            float vy = 0f;
            bool landed = false;
            for (int i = 0; i < 100 && !landed; i++)
            {
                landed = LobbyKeyringDrag.FallStep(ref y, ref vy, Floor, Dt,
                    Gravity, BounceDamping, BounceMinSpeed);
                Assert.IsFalse(vy > 0f, "느린 착지는 반동이 없어야 한다");
            }
            Assert.IsTrue(landed);
            Assert.AreEqual(Floor, y, 1e-3f);
            Assert.AreEqual(0f, vy);
        }

        [Test]
        public void ReleaseBelowFloor_SettlesOnFloor_WithoutFlyingOff()
        {
            // 손가락을 화면 하단에 두면 캐릭터가 바닥선 아래에서 놓일 수 있다 —
            // 위로 튀지 않고 바닥선으로 정착해야 한다(상승 착지는 impact 음수 → 반동 없음).
            float y = Floor - 20f;
            float vy = 500f;
            bool landed = false;
            for (int i = 0; i < 100 && !landed; i++)
            {
                landed = LobbyKeyringDrag.FallStep(ref y, ref vy, Floor, Dt,
                    Gravity, BounceDamping, BounceMinSpeed);
            }
            Assert.IsTrue(landed);
            Assert.AreEqual(Floor, y, 1e-3f);
            Assert.AreEqual(0f, vy);
        }
    }
}
