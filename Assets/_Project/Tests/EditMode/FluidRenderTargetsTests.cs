using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    public class FluidRenderTargetsTests
    {
        [Test]
        public void Release_WhenOwnedTargetIsActive_ClearsActiveTarget()
        {
            var config = ScriptableObject.CreateInstance<FluidSimConfig>();
            var targets = new FluidRenderTargets();
            bool activeReleaseError = false;
            Application.LogCallback onLog = (condition, _, type) =>
            {
                if (type == LogType.Error
                    && condition.Contains("Releasing render texture that is set to be RenderTexture.active"))
                    activeReleaseError = true;
            };

            try
            {
                targets.Allocate(config, 64, 64);
                RenderTexture.active = targets.Display;
                Application.logMessageReceived += onLog;

                targets.Release();

                Assert.IsFalse(activeReleaseError,
                    "활성 유체 RT를 그대로 Release하면 Unity가 콘솔 에러를 기록한다");
                Assert.IsNull(RenderTexture.active,
                    "해제한 유체 RT가 현재 렌더 타깃으로 남으면 안 된다");
            }
            finally
            {
                Application.logMessageReceived -= onLog;
                RenderTexture.active = null;
                targets.Release();
                Object.DestroyImmediate(config);
            }
        }
    }
}
