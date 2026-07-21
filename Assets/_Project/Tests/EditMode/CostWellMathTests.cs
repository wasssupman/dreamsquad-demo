using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // tray-cost-well unit 0 — 물통 채움 산식.
    // 핵심 계약 둘: (1) max 도달은 소수부와 무관하게 가득으로 읽는다,
    // (2) 소수부 되감김 비교는 epsilon 가드가 있어야 정수 획득을 자연 충전으로
    // 오분류하지 않는다.
    public class CostWellMathTests
    {
        private const float Tol = 1e-5f;
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            _host = null;
        }

        private CostRuntime MakeRuntime(float current, float max)
        {
            _host = new GameObject("CostRuntimeUnderTest");
            var runtime = _host.AddComponent<CostRuntime>();
            runtime.Configure(current, max, 0f);
            runtime.ResetToStart();
            return runtime;
        }

        // ---- WellFill ----

        [Test]
        public void EmptyPool_WellIsEmpty()
        {
            Assert.AreEqual(0f, CostWellMath.WellFill(0f, 10f), Tol);
        }

        [Test]
        public void MidFraction_WellShowsFraction()
        {
            Assert.AreEqual(0.5f, CostWellMath.WellFill(3.5f, 10f), Tol);
        }

        [Test]
        public void NearNextInteger_WellNearlyFull()
        {
            Assert.AreEqual(0.9f, CostWellMath.WellFill(9.9f, 10f), 1e-4f);
        }

        // 이 unit 의 핵심. max 에서는 리젠이 멈춰 소수부가 0 이지만, 그대로
        // 그리면 만땅이 빈 통으로 보인다. startingCost == maxCost == 10 이라
        // 모든 판의 배치 페이즈가 이 상태로 시작한다.
        [Test]
        public void AtMax_WellIsFull_NotEmpty()
        {
            Assert.AreEqual(1f, CostWellMath.WellFill(10f, 10f), Tol);
        }

        [Test]
        public void AboveMax_WellIsFull()
        {
            Assert.AreEqual(1f, CostWellMath.WellFill(11f, 10f), Tol);
        }

        [Test]
        public void ZeroMax_WellIsEmpty_NoDivideByZero()
        {
            Assert.AreEqual(0f, CostWellMath.WellFill(5f, 0f), Tol);
        }

        // ---- DisplayInt ----

        [Test]
        public void DisplayInt_FloorsFraction()
        {
            Assert.AreEqual(3, CostWellMath.DisplayInt(3.9f));
            Assert.AreEqual(10, CostWellMath.DisplayInt(10f));
            Assert.AreEqual(0, CostWellMath.DisplayInt(0f));
        }

        // 뷰가 런타임 인스턴스 없이 계산해도 같은 값이어야 한다.
        [TestCase(0f)]
        [TestCase(3.9f)]
        [TestCase(7f)]
        [TestCase(9.99f)]
        [TestCase(10f)]
        public void DisplayInt_MatchesCostRuntimeCurrentInt(float current)
        {
            var runtime = MakeRuntime(current, 10f);
            Assert.AreEqual(runtime.CurrentInt, CostWellMath.DisplayInt(runtime.Current));
        }

        // ---- FillEpsilon (unit 2 되감김 판정 회귀 가드) ----

        // AddCost 는 정수만 더하므로 소수부가 보존돼야 한다. 하지만 float32
        // 누적 오차로 1 ULP 하향 드리프트가 생기고, epsilon 없이 비교하면
        // 외부 획득이 "자연 충전으로 물통이 넘침"으로 오분류된다.
        [Test]
        public void IntegerGain_NeverReadsAsFractionRewind()
        {
            const float max = 100f;
            for (int i = 0; i < 200; i++)
            {
                float before = i * 0.05f;
                float after = Mathf.Min(max, before + 1f); // CostRuntime.AddCost 와 동일 연산
                float fillBefore = CostWellMath.WellFill(before, max);
                float fillAfter = CostWellMath.WellFill(after, max);

                Assert.IsFalse(fillAfter < fillBefore - CostWellMath.FillEpsilon,
                    $"AddCost(1) 가 되감김으로 오분류됨: before={before:R} ({fillBefore:R}) → after={after:R} ({fillAfter:R})");
            }
        }

        // 반대 방향: 진짜 자연 충전 wrap 은 확실히 잡혀야 한다.
        [Test]
        public void NaturalRegenWrap_ReadsAsFractionRewind()
        {
            const float max = 10f;
            float fillBefore = CostWellMath.WellFill(6.98f, max);
            float fillAfter = CostWellMath.WellFill(7.01f, max);

            Assert.IsTrue(fillAfter < fillBefore - CostWellMath.FillEpsilon,
                $"wrap 이 감지되지 않음: {fillBefore:R} → {fillAfter:R}");
        }
    }
}
