using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.PlayMode
{
    // defender-relocation unit 5 — 이동모드 진입/취소/쿨다운/타임아웃 검증. 진입은 홀드가 아니라
    // 외부 BeginMoveModeFor(플립북 이동모드 버튼 대체). 컨트롤러를 disable 후 Step 을 reflection 으로
    // 구동해 쿨다운/타임아웃 틱을 진행한다(원격 검증 경로).
    public class RelocationMoveModeTest
    {
        static MethodInfo _stepMethod;

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
        public IEnumerator BeginMoveMode_Slomo_Cancel_Cooldown_Timeout()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var controller = Object.FindObjectOfType<DefenderRelocationController>();
            Assert.IsNotNull(controller, "DefenderRelocationController wired in scene");
            controller.enabled = false; // 실제 입력 Update 차단 — Step 을 테스트가 단독 구동(쿨다운/타임아웃 틱)

            var fast = ScriptableObject.CreateInstance<RelocationSettings>();
            fast.entryCooldownSeconds = 0.5f;
            fast.moveModeTimeoutSeconds = 1.0f;
            SetField(controller, "settings", fast);

            var cat = FindCatalog();
            var unit = cat.ById("ranger");
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place defender");
            gm.SetPhase(GamePhase.Battle); // 진입 게이트 = Battle 페이즈
            yield return null;

            var cell = SoleCell(bridge);
            Assert.IsTrue(bridge.TryGetDefenderAt(cell, out var entity, out _, out _), "resolve entity");
            Vector2 z = Vector2.zero;

            // 1) BeginMoveModeFor → 이동모드 + 슬로모
            Assert.IsTrue(controller.BeginMoveModeFor(entity, cell), "BeginMoveModeFor enters move mode");
            Assert.IsTrue(controller.InMoveMode, "in move mode");
            Assert.AreEqual(cell, controller.MoveSourceCell, "source cell captured");
            Assert.Less(TimeManager.Instance.ScaleOf(TimeDomain.Battle), 0.999f, "slowmo lease active");

            // 2) 취소 → 슬로모 해제
            controller.CancelMoveMode();
            Assert.IsFalse(controller.InMoveMode, "cancel exits move mode");
            Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 0.001f, "slowmo released on cancel");

            // 3) 진입 쿨다운 — 직후 재진입 거부
            Assert.IsFalse(controller.BeginMoveModeFor(entity, cell), "entry cooldown blocks immediate re-entry");
            Assert.IsFalse(controller.InMoveMode, "still not in move mode during cooldown");

            // 4) 쿨다운 소진(Step 틱) 후 재진입
            for (int i = 0; i < 8; i++) Step(controller, false, false, z, 0.1f); // 0.5s 쿨다운 소진
            Assert.IsTrue(controller.BeginMoveModeFor(entity, cell), "re-entry after cooldown");
            Assert.IsTrue(controller.InMoveMode, "re-entered");

            // 5) 타임아웃 자동 취소
            for (int i = 0; i < 12; i++) Step(controller, false, false, z, 0.1f); // >1.0s 누적
            Assert.IsFalse(controller.InMoveMode, "timeout auto-cancels move mode");
            Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 0.001f, "slowmo released on timeout");

            Object.Destroy(fast);
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static void Step(DefenderRelocationController c, bool pressStarted, bool pressed, Vector2 screen, float dt)
        {
            _stepMethod ??= typeof(DefenderRelocationController)
                .GetMethod("Step", BindingFlags.NonPublic | BindingFlags.Instance);
            _stepMethod.Invoke(c, new object[] { pressStarted, pressed, screen, dt });
        }

        private static void SetField(object obj, string name, object value)
            => obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);

        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
            return false;
        }

        private static Vector2Int SoleCell(BattleBridge bridge)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (System.Collections.DictionaryEntry de in (System.Collections.IDictionary)f.GetValue(bridge))
                return (Vector2Int)de.Key;
            return new Vector2Int(int.MinValue, int.MinValue);
        }
    }
}
