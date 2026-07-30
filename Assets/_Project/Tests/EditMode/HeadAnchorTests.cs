using NUnit.Framework;
using UnityEngine;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    public class HeadAnchorTests
    {
        [Test]
        public void Lift_CameraPlaneHeightPreservesDepthAndScreenXAcrossBoardEdges()
        {
            var cameraGo = new GameObject("HeadAnchorTestCamera");
            try
            {
                var cam = cameraGo.AddComponent<Camera>();
                cam.fieldOfView = 40f;
                cam.aspect = 16f / 9f;
                cam.transform.position = new Vector3(0f, 8f, -12f);
                cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

                float? expectedScreenLift = null;
                foreach (float cameraX in new[] { -5f, 0f, 5f })
                {
                    Vector3 basePos = cam.transform.TransformPoint(new Vector3(cameraX, 0f, 23f));
                    Vector3 lifted = HeadAnchor.Lift(basePos, Vector3.up * 0.7f, cam);
                    Vector3 baseCamera = cam.transform.InverseTransformPoint(basePos);
                    Vector3 liftedCamera = cam.transform.InverseTransformPoint(lifted);
                    Vector3 baseScreen = cam.WorldToScreenPoint(basePos);
                    Vector3 liftedScreen = cam.WorldToScreenPoint(lifted);

                    Assert.AreEqual(baseCamera.x, liftedCamera.x, 0.0001f);
                    Assert.AreEqual(baseCamera.z, liftedCamera.z, 0.0001f);
                    Assert.AreEqual(baseScreen.x, liftedScreen.x, 0.001f);

                    float screenLift = liftedScreen.y - baseScreen.y;
                    if (expectedScreenLift.HasValue)
                        Assert.AreEqual(expectedScreenLift.Value, screenLift, 0.001f);
                    else
                        expectedScreenLift = screenLift;
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
            }
        }

        [Test]
        public void Lift_WithoutCameraUsesWorldOffsetFallback()
        {
            var basePos = new Vector3(2f, 3f, 4f);
            var offset = new Vector3(0.25f, 0.7f, -0.5f);

            Assert.AreEqual(basePos + offset, HeadAnchor.Lift(basePos, offset, null));
        }
    }
}
