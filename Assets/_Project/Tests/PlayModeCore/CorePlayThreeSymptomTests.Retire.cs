using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.Hud;
using Wassup.BattleCoreUnity.Input;
using Wassup.BattleCoreUnity.View;
using Wassup.Data;

namespace Wassup.Tests.PlayMode.Core
{
    public sealed partial class CorePlayThreeSymptomTests
    {
        [UnityTest]
        public IEnumerator 유닛을_선택해_손패가_열려도_퇴근_버튼을_누른_손가락은_버튼에_닿는다()
        {
            BattleDriver driver = null;
            yield return BootWithDeck(d => driver = d, TremorCard);
            var panel = Object.FindAnyObjectByType<CoreSelectionPanel>();
            var selection = Object.FindAnyObjectByType<SelectionInput>();
            Assert.IsNotNull(panel);
            Assert.IsNotNull(selection);
            Assert.IsNotNull(EventSystem.current, "EventSystem 이 없다");

            driver.Apply(Command.FinishPlacement());
            int defIndex = -1; int2 anchor = default;
            var placement = driver.Match.Placement;
            for (int i = 0; i < driver.Definition.Units.Length && defIndex < 0; i++)
            {
                if (!placement.InRoster(i)) continue;
                for (int y = 0; y < driver.GridSize.y && defIndex < 0; y++)
                for (int x = 0; x < driver.GridSize.x && defIndex < 0; x++)
                    if (placement.Judge(i, new int2(x, y)) == RejectReason.None) { defIndex = i; anchor = new int2(x, y); }
            }
            Assert.GreaterOrEqual(defIndex, 0, "놓을 자리가 없다");
            Assert.IsTrue(driver.Apply(Command.PlaceDefender(defIndex, anchor)).Accepted);
            SimEntityId id = SimEntityId.None;
            var units = driver.Match.World.Units;
            for (int i = units.Count - 1; i >= 0 && id.IsNone; i--) if (units[i].Kind == UnitKind.Defender) id = units[i].Id;
            for (float t = 0f; driver.Find(id) != null && driver.Find(id).Deploying && t < 4f; t += Time.unscaledDeltaTime)
                yield return null;

            selection.SelectAt(id);
            for (int i = 0; i < 60; i++) yield return null;   // 손패 딜·열림
            Assert.IsTrue(panel.IsVisible, "선택했는데 상세가 안 열렸다");
            Assert.IsTrue(panel.ActionEnabled, "퇴근 버튼이 잠겨 있다");

            Button action = null;
            foreach (var b in panel.GetComponentsInChildren<Button>(true)) if (b.name == "Action") action = b;
            Assert.IsNotNull(action, "액션 슬롯 버튼이 없다");
            var rt = (RectTransform)action.transform;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));

            var hitsUi = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hitsUi);
            Assert.Greater(hitsUi.Count, 0, "버튼 자리에 UI 가 하나도 없다");
            var top = hitsUi[0].gameObject;
            Assert.IsTrue(top.transform.IsChildOf(action.transform),
                $"퇴근 버튼을 누른 손가락이 '{top.name}'(캔버스 {top.GetComponentInParent<Canvas>().rootCanvas.name}) 에 먼저 닿는다");
        }
    }
}
