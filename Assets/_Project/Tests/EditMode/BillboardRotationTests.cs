using NUnit.Framework;
using UnityEngine;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    // tilemap-world-surround 13 — Billboard/PropBillboard 공유 회전 수학 회귀.
    public class BillboardRotationTests
    {
        private const float Eps = 0.01f;

        [Test]
        public void Tilted_ReturnsEulerXOnly_NoCameraNeeded()
        {
            var rot = BillboardRotation.Compute(BillboardRotation.Facing.Tilted, 30f, null, Vector3.zero, flip180: false);
            Assert.IsTrue(rot.HasValue);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(30f, 0f, 0f), rot.Value), Eps);
        }

        [Test]
        public void None_ReturnsNull()
        {
            var rot = BillboardRotation.Compute(BillboardRotation.Facing.None, 30f, null, Vector3.zero, flip180: false);
            Assert.IsFalse(rot.HasValue);
        }

        [Test]
        public void CameraFacing_NullCamera_ReturnsNull()
        {
            var rot = BillboardRotation.Compute(BillboardRotation.Facing.Camera, 0f, null, Vector3.zero, flip180: false);
            Assert.IsFalse(rot.HasValue);
        }

        [Test]
        public void YAxis_NullCamera_ReturnsNull()
        {
            var rot = BillboardRotation.Compute(BillboardRotation.Facing.YAxis, 0f, null, Vector3.zero, flip180: false);
            Assert.IsFalse(rot.HasValue);
        }

        [Test]
        public void Flip180_AddsYaw180()
        {
            var noFlip = BillboardRotation.Compute(BillboardRotation.Facing.Tilted, 0f, null, Vector3.zero, flip180: false).Value;
            var flip = BillboardRotation.Compute(BillboardRotation.Facing.Tilted, 0f, null, Vector3.zero, flip180: true).Value;
            Assert.Less(Quaternion.Angle(noFlip * Quaternion.Euler(0f, 180f, 0f), flip), Eps);
        }

        [Test]
        public void Camera_MatchesCameraRotation()
        {
            var camGo = new GameObject("TestCam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                camGo.transform.rotation = Quaternion.Euler(12f, 34f, 0f);
                var rot = BillboardRotation.Compute(BillboardRotation.Facing.Camera, 0f, cam, Vector3.zero, flip180: false);
                Assert.IsTrue(rot.HasValue);
                Assert.Less(Quaternion.Angle(camGo.transform.rotation, rot.Value), Eps);
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void YAxis_FacesAwayFromCamera_StaysHorizontal()
        {
            var camGo = new GameObject("TestCam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                // 카메라가 -z/+y 에서 원점을 내려봄. 대상(원점) → 카메라 반대(+z) 를 수평으로 바라봐야 함.
                camGo.transform.position = new Vector3(0f, 5f, -10f);
                var rot = BillboardRotation.Compute(BillboardRotation.Facing.YAxis, 0f, cam, Vector3.zero, flip180: false);
                Assert.IsTrue(rot.HasValue);
                var fwd = rot.Value * Vector3.forward;
                Assert.AreEqual(0f, fwd.y, 1e-3f);  // Y축 빌보드 = 기울지 않음
                Assert.Greater(fwd.z, 0.99f);        // 카메라 반대 방향(+z)
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void YAxis_DegenerateDirection_ReturnsNull()
        {
            var camGo = new GameObject("TestCam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                // 카메라와 대상이 수평상 동일 위치(높이만 다름) → 평탄화 시 방향 0 → null.
                camGo.transform.position = new Vector3(0f, 5f, 0f);
                var rot = BillboardRotation.Compute(BillboardRotation.Facing.YAxis, 0f, cam, Vector3.zero, flip180: false);
                Assert.IsFalse(rot.HasValue);
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }

        // ----- tilted-billboard unit 6: ResolveDistanceTilt (refElev = 라이브 카메라 pitch) -----

        [Test]
        public void DistanceTilt_NullCamera_ReturnsBase()
        {
            float t = BillboardRotation.ResolveDistanceTilt(45f, 0.78f, 28f, 62f, null, Vector3.zero);
            Assert.AreEqual(45f, t, Eps);
        }

        [Test]
        public void DistanceTilt_ZeroFactor_ReturnsBase()
        {
            var camGo = new GameObject("TestCam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                camGo.transform.position = new Vector3(0f, 10f, -10f);
                camGo.transform.LookAt(Vector3.zero);
                float t = BillboardRotation.ResolveDistanceTilt(45f, 0f, 28f, 62f, cam, Vector3.zero);
                Assert.AreEqual(45f, t, Eps);
            }
            finally { Object.DestroyImmediate(camGo); }
        }

        [Test]
        public void DistanceTilt_PropAtCameraLookCenter_ReturnsBase()
        {
            var camGo = new GameObject("TestCam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                // cam at (0,10,-10) looking at origin → pitch 45. prop at origin → elev to cam = 45 = pitch → delta 0.
                camGo.transform.position = new Vector3(0f, 10f, -10f);
                camGo.transform.LookAt(Vector3.zero);
                float t = BillboardRotation.ResolveDistanceTilt(45f, 0.78f, 28f, 62f, cam, Vector3.zero);
                Assert.AreEqual(45f, t, 0.1f);
            }
            finally { Object.DestroyImmediate(camGo); }
        }

        [Test]
        public void DistanceTilt_NearerProp_HigherElev_IncreasesTilt()
        {
            var camGo = new GameObject("TestCam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                camGo.transform.position = new Vector3(0f, 10f, -10f);
                camGo.transform.LookAt(Vector3.zero); // pitch 45, look-center = origin
                // near prop (closer in XZ than center) → higher elev → tilt > base; far prop → tilt < base.
                float near = BillboardRotation.ResolveDistanceTilt(45f, 0.78f, 0f, 90f, cam, new Vector3(0f, 0f, -5f));
                float far = BillboardRotation.ResolveDistanceTilt(45f, 0.78f, 0f, 90f, cam, new Vector3(0f, 0f, 5f));
                Assert.Greater(near, 45f);
                Assert.Less(far, 45f);
            }
            finally { Object.DestroyImmediate(camGo); }
        }

        [Test]
        public void DistanceTilt_ClampsToRange()
        {
            var camGo = new GameObject("TestCam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                camGo.transform.position = new Vector3(0f, 10f, -10f);
                camGo.transform.LookAt(Vector3.zero); // pitch 45
                // prop nearly under camera → elev ~89 → base + (89-45)*2 huge → clamp to max.
                float t = BillboardRotation.ResolveDistanceTilt(45f, 2f, 28f, 62f, cam, new Vector3(0f, 0f, -9.8f));
                Assert.LessOrEqual(t, 62f + Eps);
                Assert.GreaterOrEqual(t, 28f - Eps);
                Assert.AreEqual(62f, t, 0.5f); // actually hits the max clamp
            }
            finally { Object.DestroyImmediate(camGo); }
        }
    }
}
