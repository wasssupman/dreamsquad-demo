using NUnit.Framework;
using UnityEngine;

namespace Wassup.DepthParallax.Tests
{
    // depth-parallax unit 1 — 순수 패럴랙스 수학의 회귀 잠금.
    // rest no-op·중심 피벗·극성 플립(셰이더 곱셈 순서)·스프링 수렴 불변식을 고정한다.
    public class DepthParallaxMathTests
    {
        private const float Center = 0.5f;
        private const float Amplitude = 0.02f;
        private const float Eps = 1e-6f;

        [Test]
        public void UvOffset_ZeroTilt_ReturnsZero()
        {
            // rest = no-op: 틸트가 0 이면 뎁스에 무관하게 오프셋 0(원본 픽셀 동일 보장).
            Vector2 off = DepthParallaxMath.UvOffset(Vector2.zero, depth: 0.9f, Center, Amplitude, depthSign: 1f);
            Assert.AreEqual(0f, off.x, Eps);
            Assert.AreEqual(0f, off.y, Eps);
        }

        [Test]
        public void UvOffset_DepthAtCenter_ReturnsZero()
        {
            // 힌지 평면(depth==depthCenter)은 틸트가 있어도 정지.
            Vector2 off = DepthParallaxMath.UvOffset(new Vector2(1f, -0.5f), depth: Center, Center, Amplitude, depthSign: 1f);
            Assert.AreEqual(0f, off.x, Eps);
            Assert.AreEqual(0f, off.y, Eps);
        }

        [Test]
        public void UvOffset_AcrossCenter_OppositeSigns()
        {
            // 중심 피벗: center 위/아래 뎁스는 반대 방향으로 밀린다.
            Vector2 tilt = new Vector2(1f, 0f);
            Vector2 near = DepthParallaxMath.UvOffset(tilt, depth: 0.9f, Center, Amplitude, depthSign: 1f);
            Vector2 far = DepthParallaxMath.UvOffset(tilt, depth: 0.1f, Center, Amplitude, depthSign: 1f);
            Assert.Greater(near.x, 0f);
            Assert.Less(far.x, 0f);
            Assert.AreEqual(-far.x, near.x, Eps); // ±0.4 대칭
        }

        [Test]
        public void UvOffset_DepthSignFlips_ExactNegation()
        {
            // 극성 플립: depthSign=-1 은 +1 결과의 정확한 부호 반전.
            // 부호를 raw 뎁스에 먼저 곱하면 힌지가 밀려 이 불변식이 깨진다(셰이더 순서 회귀 잠금).
            Vector2 tilt = new Vector2(0.7f, -0.3f);
            Vector2 pos = DepthParallaxMath.UvOffset(tilt, depth: 0.8f, Center, Amplitude, depthSign: 1f);
            Vector2 neg = DepthParallaxMath.UvOffset(tilt, depth: 0.8f, Center, Amplitude, depthSign: -1f);
            Assert.AreEqual(-pos.x, neg.x, Eps);
            Assert.AreEqual(-pos.y, neg.y, Eps);
        }

        [Test]
        public void SpringStep_ConvergesToTarget()
        {
            // 임계감쇠 스프링이 유한 스텝 안에 target 으로 수렴.
            Vector2 pos = Vector2.zero;
            Vector2 vel = Vector2.zero;
            Vector2 target = new Vector2(1f, -0.5f);
            const float dt = 1f / 60f;
            for (int i = 0; i < 600; i++)
                DepthParallaxMath.SpringStep(ref pos, ref vel, target, spring: 90f, damping: 20f, maxSpeed: 0f, dt);
            Assert.Less((pos - target).magnitude, 1e-3f);
        }

        [Test]
        public void SpringStep_MaxSpeedClampsStepMagnitude()
        {
            // maxSpeed 는 속도 크기를 상한한다 → 스텝 이동량 <= maxSpeed*dt.
            Vector2 pos = Vector2.zero;
            Vector2 vel = Vector2.zero;
            Vector2 target = new Vector2(1000f, 0f); // 큰 오차로 클램프 강제
            const float dt = 1f / 60f;
            const float maxSpeed = 8f;
            Vector2 before = pos;
            DepthParallaxMath.SpringStep(ref pos, ref vel, target, spring: 500f, damping: 1f, maxSpeed, dt);
            Assert.LessOrEqual(vel.magnitude, maxSpeed + 1e-4f);
            Assert.LessOrEqual((pos - before).magnitude, maxSpeed * dt + 1e-4f);
        }
    }
}
