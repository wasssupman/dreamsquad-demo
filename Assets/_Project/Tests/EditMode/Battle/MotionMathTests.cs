using NUnit.Framework;
using UnityEngine;
using Somnia.Battle.Presentation;

namespace Somnia.Battle.Tests.EditMode
{
    // keyring-unify 0 → demo-diet unit 0 — 옛 `KeyringSimTests` 의 후계. 전투가 쓰는 운동 수학만 남겼다
    // (낙하 `FallStep`·기울임 `LeanAngle` 은 로비 키링과 함께 사라졌다).
    // SpringStep 은 추출 전 인라인 수학의 전사(레퍼런스)와 bit-exact 비교로 동작 무변경을 고정한다.
    public class MotionMathTests
    {
        private const float Dt = 1f / 60f;

        // --- SpringStep: 추출 전 인라인 수학 전사 레퍼런스 ---
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
        public void SpringStep_BitExact_AgainstInlineMath()
        {
            // 인게임 상수(DragSwaySettings 기본): spring 100 / damping 2.5 / maxSpeed 12.
            Vector3 posA = new Vector3(1f, 0f, -2f), velA = Vector3.zero;
            Vector3 posB = posA, velB = velA;
            for (int i = 0; i < 120; i++)
            {
                var target = new Vector3(Mathf.Sin(i * 0.1f) * 3f, 0.35f, Mathf.Cos(i * 0.1f) * 3f);
                MotionMath.SpringStep(ref posA, ref velA, target, 100f, 2.5f, 12f, Dt);
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
        public void SpringStep_Vector2Path_BitExact_AndZStaysZero()
        {
            // Vector2 포워딩 — Vector3(z=0) 경유가 bit-exact 이고 z 가 0 에 머무는지 고정.
            Vector2 pos2 = new Vector2(40f, -120f), vel2 = Vector2.zero;
            Vector3 pos3 = pos2, vel3 = vel2;
            for (int i = 0; i < 120; i++)
            {
                Vector2 target = new Vector2(Mathf.Sin(i * 0.2f) * 300f, -160f + i * 2f);
                ReferenceSpringStep2(ref pos2, ref vel2, target, 100f, 2.5f, 2400f, Dt);
                MotionMath.SpringStep(ref pos3, ref vel3, target, 100f, 2.5f, 2400f, Dt);
                Assert.AreEqual(pos2.x, pos3.x, 0f, $"pos.x step {i}");
                Assert.AreEqual(pos2.y, pos3.y, 0f, $"pos.y step {i}");
                Assert.AreEqual(0f, pos3.z, 0f, $"z 잔류 step {i}");
                Assert.AreEqual(vel2.x, vel3.x, 0f, $"vel.x step {i}");
                Assert.AreEqual(vel2.y, vel3.y, 0f, $"vel.y step {i}");
            }
        }

        [Test]
        public void SpringStep_ScalarPath_MatchesVector3X()
        {
            float p = 0f, v = 0f;
            Vector3 p3 = Vector3.zero, v3 = Vector3.zero;
            for (int i = 0; i < 90; i++)
            {
                MotionMath.SpringStep(ref p, ref v, 1f, 60f, 15.5f, 0f, Dt);
                MotionMath.SpringStep(ref p3, ref v3, new Vector3(1f, 0f, 0f), 60f, 15.5f, 0f, Dt);
                Assert.AreEqual(p3.x, p, 0f, $"step {i}");
            }
            Assert.Greater(p, 0.9f, "스프링이 목표에 접근한다");
        }

        [Test]
        public void FlightTimeRemap_PowerOne_IsIdentity_AndEndpointsHold()
        {
            for (int i = 0; i <= 10; i++)
            {
                float u = i / 10f;
                Assert.AreEqual(u, MotionMath.FlightTimeRemap(u, 1f), 0f, "power 1 = 항등(byte-identical)");
            }
            Assert.AreEqual(0f, MotionMath.FlightTimeRemap(0f, 0.7f), 1e-6f);
            Assert.AreEqual(1f, MotionMath.FlightTimeRemap(1f, 0.7f), 1e-6f);
            Assert.AreEqual(0.5f, MotionMath.FlightTimeRemap(0.5f, 0.7f), 1e-6f, "중점은 고정");
            // 단조 증가 — 시간만 재분배하지 되감지 않는다.
            float prev = 0f;
            for (int i = 1; i <= 20; i++)
            {
                float cur = MotionMath.FlightTimeRemap(i / 20f, 0.7f);
                Assert.GreaterOrEqual(cur, prev, $"step {i}");
                prev = cur;
            }
        }

        [Test]
        public void DismountPoint_EndpointsExact()
        {
            var start = new Vector3(2f, 1f, -3f);
            var end = new Vector3(-1f, 0f, 4f);
            var up = Vector3.up;
            var p0 = MotionMath.DismountPoint(start, Vector3.zero, end, up, 0.25f, 0.3f, 0.5f, 0.6f, new Vector2(0.3f, 1f), 0.4f, 0f);
            var p1 = MotionMath.DismountPoint(start, Vector3.zero, end, up, 0.25f, 0.3f, 0.5f, 0.6f, new Vector2(0.3f, 1f), 0.4f, 1f);
            Assert.AreEqual(0f, Vector3.Distance(start, p0), 1e-5f, "t=0 은 출발점");
            Assert.AreEqual(0f, Vector3.Distance(end, p1), 1e-5f, "t=1 은 착지점(끝점 정확 — 착지 오차 0)");
        }
    }
}
