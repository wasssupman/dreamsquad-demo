using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.PlayMode
{
    // drag-cancel-affordance unit 0 — 트레이 복귀 취소 존의 두 계약을 지키는 가드.
    //
    // (1) 취소는 무차감이다 — 취소 존에서 손을 떼면 세션이 끝나고 코스트가 그대로다.
    // (2) 판정은 **가상 포인터**다 — 손가락이 트레이 안이어도 조준점이 트레이 밖이면 취소가 아니다.
    //     이 비대칭이 없으면 큰 맵의 최하단 행이 통째로 배치 불가가 된다(그 행을 노리는 손가락이
    //     트레이 y 대역 안에 있다). 도달성 쪽 가드는 DragPlacementReachTest 가 짝으로 잰다.
    public class DragCancelZoneTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReleaseOverTray_CancelsWithoutSpending_AndAimPointerOwnsTheJudgement()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var selector = Object.FindObjectOfType<DefenderSelector>();
            var unit = FindCatalog().ById("ranger");
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            for (int i = 0; i < 3; i++) yield return null;

            var ctrl = selector.DragController;
            Assert.IsNotNull(ctrl, "tray build created the drag controller");
            var trayRect = selector.PanelGO != null ? (RectTransform)selector.PanelGO.transform : null;
            Assert.IsNotNull(trayRect, "tray panel exists");

            var corners = new Vector3[4];
            trayRect.GetWorldCorners(corners);          // 오버레이 캔버스 = world corner 가 곧 스크린 px
            Vector2 trayCenter = (corners[0] + corners[2]) * 0.5f;
            float trayTop = corners[2].y;
            float offset = ctrl.PlacementPointerOffsetPx;

            // ── (2) 손가락이 트레이 안 + 조준점은 트레이 밖 = 취소가 아니다 ────────────
            var boardStart = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            ctrl.BeginDrag(unit, boardStart);
            yield return null;
            Assert.IsTrue(ctrl.IsDragging, "drag session active");

            // 조준점을 트레이 top 바로 **위**로 두는 손가락 = 트레이 안(top - 4px)이지만 취소 아님.
            var fingerInTrayAimAbove = new Vector2(trayCenter.x, trayTop - 4f);
            ctrl.UpdateDrag(fingerInTrayAimAbove);
            yield return null;
            Assert.IsTrue((bool)Field(ctrl, "_cancelZoneLeft"), "보드에서 시작해 존을 벗어난 상태");
            Assert.IsFalse((bool)Field(ctrl, "_cancelHover"),
                $"손가락({fingerInTrayAimAbove.y:F0}px)이 트레이 안이어도 조준점"
                + $"({fingerInTrayAimAbove.y + offset:F0}px)이 트레이 top({trayTop:F0}px) 위면 취소가 아니다. "
                + "raw 포인터로 판정이 되돌아가면 큰 맵 최하단 행이 배치 불가가 된다.");

            // ── (1) 조준점을 트레이 안으로 = 취소, 무차감 ────────────────────────────
            int costBefore = gm.CostRuntime.CurrentInt;
            var fingerCancel = new Vector2(trayCenter.x, trayCenter.y - offset);
            ctrl.UpdateDrag(fingerCancel);
            yield return null;
            Assert.IsTrue((bool)Field(ctrl, "_cancelHover"),
                $"조준점({fingerCancel.y + offset:F0}px)이 트레이 중심이면 취소 존 안이다");

            ctrl.EndDrag(fingerCancel);
            yield return null;
            Assert.IsFalse(ctrl.IsDragging, "취소로 드래그 세션이 끝난다");
            Assert.AreEqual(costBefore, gm.CostRuntime.CurrentInt,
                "취소는 코스트를 쓰지 않는다 — 취소는 커밋(TryBeginDefenderDeployment) 이전에 갈라진다");

            ctrl.Disarm();
            yield return null;
        }

        private static object Field(object o, string name)
            => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }
    }
}
