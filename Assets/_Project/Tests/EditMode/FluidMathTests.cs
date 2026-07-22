using NUnit.Framework;
using UnityEngine;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    // fluid-paint-mixing unit 0 — 유체 솔버의 아키텍처-blind 순수 계산.
    // CalcResolution 은 원본 WebGL getResolution 이식: 짧은 변=target, 긴 변=round(target×정규화aspect),
    // 화면비에 따라 가로/세로가 정해진다. 잘못되면 유체가 늘어나거나 RT 가 잘못 할당된다(sim-critical).
    public class FluidMathTests
    {
        [Test]
        public void CalcResolution_Square_ReturnsEqualSides()
        {
            var r = FluidMath.CalcResolution(128, 1f);
            Assert.AreEqual(new Vector2Int(128, 128), r);
        }

        [Test]
        public void CalcResolution_Landscape_LongSideIsWidth()
        {
            // 16:9 (aspect ≈ 1.7778): min=128, max=round(128×1.7778)=228 → (width, height)=(228,128).
            var r = FluidMath.CalcResolution(128, 16f / 9f);
            Assert.AreEqual(new Vector2Int(228, 128), r);
        }

        [Test]
        public void CalcResolution_Portrait_LongSideIsHeight()
        {
            // 9:16 (aspect = 0.5625): 정규화 ratio=1.7778, min=128, max=228 → 세로가 긴 (128,228).
            var r = FluidMath.CalcResolution(128, 9f / 16f);
            Assert.AreEqual(new Vector2Int(128, 228), r);
        }

        [Test]
        public void CalcResolution_RoundsLongSide()
        {
            // target=100, aspect=1.5 → max=round(150)=150.
            var r = FluidMath.CalcResolution(100, 1.5f);
            Assert.AreEqual(new Vector2Int(150, 100), r);
        }

        [Test]
        public void CalcResolution_NonFiniteAspect_FallsBackToSquare()
        {
            // NaN / +Inf / ≤0 aspect 는 정사각으로 폴백 (ARM64 float→int 캐스트 함정 방어).
            Assert.AreEqual(new Vector2Int(128, 128), FluidMath.CalcResolution(128, float.NaN));
            Assert.AreEqual(new Vector2Int(128, 128), FluidMath.CalcResolution(128, float.PositiveInfinity));
            Assert.AreEqual(new Vector2Int(128, 128), FluidMath.CalcResolution(128, 0f));
            Assert.AreEqual(new Vector2Int(128, 128), FluidMath.CalcResolution(128, -2f));
        }

        [Test]
        public void CalcResolution_TargetBelowOne_ClampedToOne()
        {
            var r = FluidMath.CalcResolution(0, 1f);
            Assert.AreEqual(new Vector2Int(1, 1), r);
        }

        [Test]
        public void TexelSize_IsReciprocalOfDimensions()
        {
            var t = FluidMath.TexelSize(new Vector2Int(100, 200));
            Assert.AreEqual(0.01f, t.x, 1e-6f);
            Assert.AreEqual(0.005f, t.y, 1e-6f);
        }

        [Test]
        public void TexelSize_ZeroDimensions_GuardedAgainstDivideByZero()
        {
            // 0 크기 RT 는 없어야 하지만, 방어적으로 1 로 클램프해 Inf 텍셀을 막는다.
            var t = FluidMath.TexelSize(new Vector2Int(0, 0));
            Assert.AreEqual(1f, t.x, 1e-6f);
            Assert.AreEqual(1f, t.y, 1e-6f);
        }
    }
}
