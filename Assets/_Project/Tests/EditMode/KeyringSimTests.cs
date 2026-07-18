using NUnit.Framework;
using UnityEngine;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // keyring-unify 0 — KeyringSim 순수 계산 회귀 테스트.
    // FallStep 케이스는 lobby-keyring-drag 2 의 LobbyKeyringFallStepTests 재조준.
    // SpringStep/LeanAngle 은 추출 전 인라인 수학의 전사(레퍼런스)와 bit-exact 비교로 동작 무변경을 고정.
    public class KeyringSimTests
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
                landed = KeyringSim.FallStep(ref y, ref vy, Floor, Dt,
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
                landed = KeyringSim.FallStep(ref y, ref vy, Floor, Dt,
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
                landed = KeyringSim.FallStep(ref y, ref vy, Floor, Dt,
                    Gravity, BounceDamping, BounceMinSpeed);
            }
            Assert.IsTrue(landed);
            Assert.AreEqual(Floor, y, 1e-3f);
            Assert.AreEqual(0f, vy);
        }

        // --- SpringStep: 추출 전 인라인 수학 전사 레퍼런스 ---
        // DefenderDragPlacementController(d197bc79, Update) / LobbyKeyringDrag(2643383b, TickDrag)
        // 의 연산 순서를 그대로 옮긴 것. KeyringSim 이 이와 bit-exact 이어야 "동작 무변경".

        private static void ReferenceSpringStep(ref Vector3 pos, ref Vector3 vel, Vector3 target,
            float spring, float damping, float maxSpeed, float dt)
        {
            Vector3 accel = (target - pos) * spring - vel * damping;
            vel += accel * dt;
            if (maxSpeed > 0f)
            {
                float sp = vel.magnitude;
                if (sp > maxSpeed) vel *= maxSpeed / sp;
            }
            pos += vel * dt;
        }

        private static void ReferenceSpringStep2(ref Vector2 pos, ref Vector2 vel, Vector2 target,
            float spring, float damping, float maxSpeed, float dt)
        {
            Vector2 accel = (target - pos) * spring - vel * damping;
            vel += accel * dt;
            if (maxSpeed > 0f)
            {
                float sp = vel.magnitude;
                if (sp > maxSpeed) vel *= maxSpeed / sp;
            }
            pos += vel * dt;
        }

        [Test]
        public void SpringStep_BitExact_AgainstIngameInlineMath()
        {
            // 인게임 상수(DragSwaySettings 기본): spring 100 / damping 2.5 / maxSpeed 12.
            Vector3 posA = new Vector3(1f, 0f, -2f), velA = Vector3.zero;
            Vector3 posB = posA, velB = velA;
            for (int i = 0; i < 120; i++)
            {
                var target = new Vector3(Mathf.Sin(i * 0.1f) * 3f, 0.35f, Mathf.Cos(i * 0.1f) * 3f);
                KeyringSim.SpringStep(ref posA, ref velA, target, 100f, 2.5f, 12f, Dt);
                ReferenceSpringStep(ref posB, ref velB, target, 100f, 2.5f, 12f, Dt);
                Assert.AreEqual(posB.x, posA.x, 0f, $"pos.x step {i}");
                Assert.AreEqual(posB.y, posA.y, 0f, $"pos.y step {i}");
                Assert.AreEqual(posB.z, posA.z, 0f, $"pos.z step {i}");
                Assert.AreEqual(velB.x, velA.x, 0f, $"vel.x step {i}");
                Assert.AreEqual(velB.y, velA.y, 0f, $"vel.y step {i}");
                Assert.AreEqual(velB.z, velA.z, 0f, $"vel.z step {i}");
            }
        }

        [Test]
        public void SpringStep_Vector2Path_BitExact_AgainstOutgameInlineMath()
        {
            // 아웃게임 상수(LobbyKeyringSettings 기본): spring 100 / damping 2.5 / maxSpeed 2400.
            // 아웃게임은 Vector2 — KeyringSim(Vector3, z=0) 경유가 bit-exact 이고 z 가 0 에 머무는지 고정.
            Vector2 pos2 = new Vector2(40f, -120f), vel2 = Vector2.zero;
            Vector3 pos3 = pos2, vel3 = vel2;
            for (int i = 0; i < 120; i++)
            {
                Vector2 target = new Vector2(Mathf.Sin(i * 0.2f) * 300f, -160f + i * 2f);
                ReferenceSpringStep2(ref pos2, ref vel2, target, 100f, 2.5f, 2400f, Dt);
                KeyringSim.SpringStep(ref pos3, ref vel3, target, 100f, 2.5f, 2400f, Dt);
                Assert.AreEqual(pos2.x, pos3.x, 0f, $"pos.x step {i}");
                Assert.AreEqual(pos2.y, pos3.y, 0f, $"pos.y step {i}");
                Assert.AreEqual(0f, pos3.z, 0f, $"z 잔류 step {i}");
                Assert.AreEqual(vel2.x, vel3.x, 0f, $"vel.x step {i}");
                Assert.AreEqual(vel2.y, vel3.y, 0f, $"vel.y step {i}");
            }
        }

        [Test]
        public void SpringStep_Vector2Overload_BitExact_AgainstOutgameInlineMath()
        {
            // 리뷰 반영 — 아웃게임 호출측 마샬링을 흡수한 Vector2 오버로드도 동일하게 고정.
            Vector2 posRef = new Vector2(40f, -120f), velRef = Vector2.zero;
            Vector2 posOv = posRef, velOv = velRef;
            for (int i = 0; i < 120; i++)
            {
                Vector2 target = new Vector2(Mathf.Sin(i * 0.2f) * 300f, -160f + i * 2f);
                ReferenceSpringStep2(ref posRef, ref velRef, target, 100f, 2.5f, 2400f, Dt);
                KeyringSim.SpringStep(ref posOv, ref velOv, target, 100f, 2.5f, 2400f, Dt);
                Assert.AreEqual(posRef.x, posOv.x, 0f, $"pos.x step {i}");
                Assert.AreEqual(posRef.y, posOv.y, 0f, $"pos.y step {i}");
                Assert.AreEqual(velRef.x, velOv.x, 0f, $"vel.x step {i}");
                Assert.AreEqual(velRef.y, velOv.y, 0f, $"vel.y step {i}");
            }
        }

        [Test]
        public void CubicBezier_Endpoints_Exact_And_Midpoint()
        {
            // defender-tap-to-place unit 6 — 3차 던지기 곡선. endpoints 정확 + 중점 가중치 회귀.
            var a = new Vector3(1f, 0f, -2f);
            var c1 = new Vector3(0.5f, 3f, -1f);
            var c2 = new Vector3(-0.5f, 1f, 1f);
            var b = new Vector3(-1f, 0f, 2f);

            var at0 = KeyringSim.CubicBezier(a, c1, c2, b, 0f);
            Assert.AreEqual(a.x, at0.x, 0f, "t=0 x"); Assert.AreEqual(a.y, at0.y, 0f, "t=0 y"); Assert.AreEqual(a.z, at0.z, 0f, "t=0 z");

            var at1 = KeyringSim.CubicBezier(a, c1, c2, b, 1f);
            Assert.AreEqual(b.x, at1.x, 0f, "t=1 x"); Assert.AreEqual(b.y, at1.y, 0f, "t=1 y"); Assert.AreEqual(b.z, at1.z, 0f, "t=1 z");

            var mid = 0.125f * a + 0.375f * c1 + 0.375f * c2 + 0.125f * b;
            var atH = KeyringSim.CubicBezier(a, c1, c2, b, 0.5f);
            Assert.AreEqual(mid.x, atH.x, 1e-5f, "t=0.5 x");
            Assert.AreEqual(mid.y, atH.y, 1e-5f, "t=0.5 y");
            Assert.AreEqual(mid.z, atH.z, 1e-5f, "t=0.5 z");
        }

        [Test]
        public void LeanAngle_BitExact_AgainstInlineFormula()
        {
            // 추출 전 공식: clamp(-atan2(x, max(y, 1e-3)) * Rad2Deg, ±maxAngle).
            // 단위벡터(아웃게임)·비단위 투영값(인게임)·eps 퇴화·클램프 영역을 모두 커버.
            (float x, float y)[] cases =
            {
                (0f, 1f),          // 수직 — 기울임 0
                (0.05f, 0.99f),    // 미세 스윙
                (-0.05f, 0.99f),
                (0.3f, 0.2f),      // 비단위 투영값(인게임 카메라 기울임)
                (1f, 0f),          // 수평 — eps floor + 클램프
                (-1f, -0.5f),      // 역방향(y<0) — eps floor
                (0.0004f, 0.0003f) // 퇴화 소벡터
            };
            foreach (var c in cases)
            {
                float expected = Mathf.Clamp(
                    -Mathf.Atan2(c.x, Mathf.Max(c.y, 1e-3f)) * Mathf.Rad2Deg, -8f, 8f);
                Assert.AreEqual(expected, KeyringSim.LeanAngle(c.x, c.y, 8f), 0f, $"({c.x},{c.y})");
            }
        }
    }
}
