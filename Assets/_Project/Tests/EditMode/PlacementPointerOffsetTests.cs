using NUnit.Framework;
using UnityEngine;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // placement-thumb-occlusion unit 1 — 배치 판정 포인터 오프셋의 순수 정책 함수.
    // 지키는 계약: 램프는 **승격 임계에서 0 에서 시작**하고 이동량에 비례해 1 로 차오른다.
    // 이 성질이 깨지면 16px 승격이 곧바로 65px 오프셋 점프를 만든다(램프 도입의 원 이유).
    public class PlacementPointerOffsetTests
    {
        const float Threshold = 16f;
        const float RampPx = 60f;

        [Test]
        public void Ramp_AtPromotionThreshold_IsZero()
        {
            Assert.AreEqual(0f, PlacementPointerOffset.Ramp(Threshold, Threshold, RampPx), 1e-5f,
                "승격 순간엔 오프셋이 0 이어야 한다 — 아니면 하이라이트가 한 칸 순간이동한다");
        }

        [Test]
        public void Ramp_BelowThreshold_ClampsToZero()
        {
            Assert.AreEqual(0f, PlacementPointerOffset.Ramp(0f, Threshold, RampPx), 1e-5f);
            Assert.AreEqual(0f, PlacementPointerOffset.Ramp(Threshold - 5f, Threshold, RampPx), 1e-5f);
        }

        [Test]
        public void Ramp_ReachesFull_AtThresholdPlusRampDistance()
        {
            Assert.AreEqual(1f, PlacementPointerOffset.Ramp(Threshold + RampPx, Threshold, RampPx), 1e-5f);
        }

        [Test]
        public void Ramp_ClampsAboveFull()
        {
            Assert.AreEqual(1f, PlacementPointerOffset.Ramp(Threshold + RampPx * 10f, Threshold, RampPx), 1e-5f);
        }

        [Test]
        public void Ramp_IsProportionalToTravel_NotTime()
        {
            // 절반 지점 = 정확히 0.5. 이동량 비례라는 계약의 핵심 — 시간 램프면 이 값이 손가락
            // 이동과 무관해진다.
            Assert.AreEqual(0.5f, PlacementPointerOffset.Ramp(Threshold + RampPx * 0.5f, Threshold, RampPx), 1e-5f);
        }

        [Test]
        public void Ramp_ZeroRampDistance_MeansInstantFullOffset()
        {
            Assert.AreEqual(1f, PlacementPointerOffset.Ramp(Threshold, Threshold, 0f), 1e-5f);
            Assert.AreEqual(1f, PlacementPointerOffset.Ramp(0f, Threshold, 0f), 1e-5f);
        }

        [Test]
        public void Apply_RaisesOnlyScreenY_ScaledByRamp()
        {
            var raw = new Vector2(100f, 200f);
            var full = PlacementPointerOffset.Apply(raw, 65f, 1f);
            Assert.AreEqual(100f, full.x, 1e-5f, "수평 성분은 건드리지 않는다");
            Assert.AreEqual(265f, full.y, 1e-5f);

            var half = PlacementPointerOffset.Apply(raw, 65f, 0.5f);
            Assert.AreEqual(232.5f, half.y, 1e-5f);
        }

        [Test]
        public void Apply_ZeroRamp_IsIdentity()
        {
            // 탭 경로가 이 항등에 의존한다(승격 전 램프 0 → 스카우트가 누른 칸을 그대로 비춘다).
            var raw = new Vector2(42f, 84f);
            Assert.AreEqual(raw, PlacementPointerOffset.Apply(raw, 65f, 0f));
        }
    }
}
