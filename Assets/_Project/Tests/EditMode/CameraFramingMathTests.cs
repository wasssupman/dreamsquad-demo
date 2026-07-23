using NUnit.Framework;
using UnityEngine;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    // camera-direction unit 8 — 보드 fit 거리 산식.
    public class CameraFramingMathTests
    {
        private static void Tangents(float fovDeg, float aspect, out float tanH, out float tanV)
            => CameraFramingMath.FrustumTangents(fovDeg, aspect, out tanH, out tanV);

        // fit 거리에 실제로 카메라를 놓았을 때 모든 코너가 프러스텀 안인지 검사.
        private static bool AllCornersInside(Bounds b, Quaternion rot, float fov, float aspect, float margin)
        {
            Tangents(fov, aspect, out float tanH, out float tanV);
            var corners = CameraFramingMath.LocalCorners(b, rot);
            float t = CameraFramingMath.FitDistance(corners, tanH, tanV, margin);
            for (int i = 0; i < corners.Length; i++)
            {
                float z = corners[i].z + t;
                if (z <= 0f) return false;                                   // 카메라 뒤
                if (Mathf.Abs(corners[i].x) > z * tanH + 1e-3f) return false;
                if (Mathf.Abs(corners[i].y) > z * tanV + 1e-3f) return false;
            }
            return true;
        }

        [Test]
        public void FitDistance_AllCornersInsideFrustum_ForTiltedBoard()
        {
            var board = new Bounds(Vector3.zero, new Vector3(20f, 0.2f, 12f));
            Assert.IsTrue(AllCornersInside(board, Quaternion.Euler(60f, 0f, 0f), 36f, 16f / 9f, 1f));
        }

        [Test]
        public void FitDistance_BiggerBoard_NeedsGreaterDistance()
        {
            var rot = Quaternion.Euler(60f, 0f, 0f);
            Tangents(36f, 16f / 9f, out float tanH, out float tanV);

            float small = CameraFramingMath.FitDistance(
                CameraFramingMath.LocalCorners(new Bounds(Vector3.zero, new Vector3(12f, 0.2f, 10f)), rot),
                tanH, tanV, 1f);
            float big = CameraFramingMath.FitDistance(
                CameraFramingMath.LocalCorners(new Bounds(Vector3.zero, new Vector3(20f, 0.2f, 12f)), rot),
                tanH, tanV, 1f);

            Assert.Greater(big, small, "큰 맵일수록 더 멀리서 봐야 한다");
        }

        [Test]
        public void FitDistance_NarrowerFov_NeedsGreaterDistance()
        {
            var rot = Quaternion.Euler(60f, 0f, 0f);
            var corners = CameraFramingMath.LocalCorners(new Bounds(Vector3.zero, new Vector3(20f, 0.2f, 12f)), rot);

            Tangents(30f, 16f / 9f, out float nTanH, out float nTanV);
            Tangents(50f, 16f / 9f, out float wTanH, out float wTanV);
            float narrow = CameraFramingMath.FitDistance(corners, nTanH, nTanV, 1f);
            float wide = CameraFramingMath.FitDistance(corners, wTanH, wTanV, 1f);

            Assert.Greater(narrow, wide, "FOV 가 좁을수록 같은 보드를 담으려면 멀어져야 한다");
        }

        [Test]
        public void FitDistance_MarginScalesResult()
        {
            var rot = Quaternion.Euler(60f, 0f, 0f);
            var corners = CameraFramingMath.LocalCorners(new Bounds(Vector3.zero, new Vector3(20f, 0.2f, 12f)), rot);
            Tangents(36f, 16f / 9f, out float tanH, out float tanV);

            float baseline = CameraFramingMath.FitDistance(corners, tanH, tanV, 1f);
            float margined = CameraFramingMath.FitDistance(corners, tanH, tanV, 1.1f);

            Assert.AreEqual(baseline * 1.1f, margined, 1e-3f);
        }

        [Test]
        public void FitDistance_WiderAspect_DoesNotShrinkBelowVerticalNeed()
        {
            // 가로가 넓어지면 가로 제약은 완화되지만 세로 제약이 남아 거리가 0 으로 붕괴하지 않는다.
            var rot = Quaternion.Euler(60f, 0f, 0f);
            var corners = CameraFramingMath.LocalCorners(new Bounds(Vector3.zero, new Vector3(20f, 0.2f, 12f)), rot);
            Tangents(36f, 21f / 9f, out float tanH, out float tanV);

            float d = CameraFramingMath.FitDistance(corners, tanH, tanV, 1f);
            Assert.Greater(d, 0f);
            Assert.IsTrue(AllCornersInside(new Bounds(Vector3.zero, new Vector3(20f, 0.2f, 12f)), rot, 36f, 21f / 9f, 1f));
        }

        [Test]
        public void FrustumTangents_InvalidAspect_FallsBackTo16By9()
        {
            Tangents(36f, 0f, out float tanH, out float tanV);
            Assert.AreEqual(tanV * (16f / 9f), tanH, 1e-4f);
        }

        [Test]
        public void FitDistance_EmptyCorners_ReturnsZero()
        {
            Assert.AreEqual(0f, CameraFramingMath.FitDistance(new Vector3[0], 1f, 1f, 1f));
            Assert.AreEqual(0f, CameraFramingMath.FitDistance(null, 1f, 1f, 1f));
        }
    }
}
