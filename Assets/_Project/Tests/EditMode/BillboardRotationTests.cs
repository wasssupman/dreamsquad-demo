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
    }
}
