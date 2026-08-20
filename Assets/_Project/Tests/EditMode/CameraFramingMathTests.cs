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

        // --- unit 9: 보드 깊이에 정규화한 DoF 거리 -------------------------------------

        private const float Fov = 36f;
        private const float Margin = 1.12f;
        private const float Pullback = 4f;
        private static readonly Bounds Board = new Bounds(Vector3.zero, new Vector3(20f, 0.2f, 12f));

        // 주어진 화면비에서 실제로 적용될 카메라 깊이(fit + pullback)와 코너 버퍼.
        private static Vector3[] FramedCorners(float aspect, out float camDistance)
        {
            var rot = Quaternion.Euler(60f, 0f, 0f);
            var corners = CameraFramingMath.LocalCorners(Board, rot);
            Tangents(Fov, aspect, out float tanH, out float tanV);
            camDistance = CameraFramingMath.FitDistance(corners, tanH, tanV, Margin) + Pullback;
            return corners;
        }

        // 이 결함의 회귀 테스트: 절대 거리로 저작하면 화면비가 넓어질 때 카메라가 보드에 붙어
        // 임계값이 화면 밖으로 밀려난다. 보드 기준으로 저작하면 임계값이 카메라를 따라온다.
        [Test]
        public void DofRange_FollowsCameraWhenAspectWidens()
        {
            var narrow = FramedCorners(16f / 9f, out float dNarrow);
            var wide = FramedCorners(2340f / 1080f, out float dWide);

            Assert.Less(dWide, dNarrow, "가로가 넓어지면 보드가 화면에 더 쉽게 들어와 카메라가 가까워진다");

            Assert.IsTrue(CameraFramingMath.DofRange(narrow, dNarrow, 0.6f, 0.88f, out float s0, out float e0));
            Assert.IsTrue(CameraFramingMath.DofRange(wide, dWide, 0.6f, 0.88f, out float s1, out float e1));

            // 임계값이 카메라 이동량만큼 정확히 같이 당겨진다 = 화면에서 같은 자리에서 흐려진다.
            float shift = dNarrow - dWide;
            Assert.AreEqual(shift, s0 - s1, 1e-3f, "블러 시작이 카메라와 같이 움직여야 한다");
            Assert.AreEqual(shift, e0 - e1, 1e-3f, "블러 최대 지점도 같이 움직여야 한다");
        }

        [Test]
        public void DofRange_IsRelativeToBoardDepthSpan()
        {
            var corners = FramedCorners(16f / 9f, out float dist);
            float nearZ = float.MaxValue, farZ = float.MinValue;
            for (int i = 0; i < corners.Length; i++)
            {
                float z = corners[i].z + dist;
                if (z < nearZ) nearZ = z;
                if (z > farZ) farZ = z;
            }

            Assert.IsTrue(CameraFramingMath.DofRange(corners, dist, 0f, 1f, out float s, out float e));
            Assert.AreEqual(nearZ, s, 1e-3f, "t=0 은 보드 앞단");
            Assert.AreEqual(farZ, e, 1e-3f, "t=1 은 보드 뒷단");
        }

        [Test]
        public void DofRange_EndStaysAheadOfStart_EvenWhenKnobsInverted()
        {
            var corners = FramedCorners(16f / 9f, out float dist);
            Assert.IsTrue(CameraFramingMath.DofRange(corners, dist, 0.9f, 0.1f, out float s, out float e));
            Assert.Greater(e, s, "램프 폭이 음수가 되면 URP 에서 블러가 뒤집힌다");
        }

        [Test]
        public void DofRange_BoardBehindCamera_ReturnsFalse()
        {
            var rot = Quaternion.Euler(60f, 0f, 0f);
            var corners = CameraFramingMath.LocalCorners(Board, rot);
            Assert.IsFalse(CameraFramingMath.DofRange(corners, 0f, 0.6f, 0.88f, out _, out _));
            Assert.IsFalse(CameraFramingMath.DofRange(null, 20f, 0.6f, 0.88f, out _, out _));
            Assert.IsFalse(CameraFramingMath.DofRange(new Vector3[0], 20f, 0.6f, 0.88f, out _, out _));
        }
    }
}
