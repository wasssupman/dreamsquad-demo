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
    // defender-relocation unit 1 — 홀드→이동모드 상태 머신 검증. 씬에 배선된 컨트롤러를
    // disable(실제 입력 Update 차단) 후 private Step 을 reflection 으로 구동한다(원격 검증 경로).
    // 검증: 짧은 탭 불소비 / 홀드 진입+슬로모 / 취소+해제 / 진입 쿨다운 / 타임아웃 자동 취소.
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
        public IEnumerator HoldEntersMoveMode_TapDoesNot_CooldownAndTimeoutApply()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var controller = Object.FindObjectOfType<DefenderRelocationController>();
            Assert.IsNotNull(controller, "DefenderRelocationController wired in scene");
            controller.enabled = false; // 실제 입력 Update 차단 — Step 을 테스트가 단독 구동

            // 빠른 테스트용 설정 주입
            var fast = ScriptableObject.CreateInstance<RelocationSettings>();
            fast.holdSeconds = 0.3f;
            fast.entryCooldownSeconds = 0.5f;
            fast.moveModeTimeoutSeconds = 1.0f;
            SetField(controller, "settings", fast);

            // 배치 준비 (PlacementAuraTest 패턴)
            var cat = FindCatalog();
            var unit = cat.ById("ranger");
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place defender");
            gm.SetPhase(GamePhase.Battle); // 홀드 진입 게이트 = Battle 페이즈
            yield return null;

            // 유닛 셀의 화면 좌표 (roundtrip 검증)
            var cell = SoleCell(bridge);
            var cam = Camera.main;
            Vector2 screen = cam.WorldToScreenPoint(bridge.GridCellToViewCenter(cell));
            Assert.IsTrue(bridge.TryScreenToCell(cam, screen, out var rt) && rt == cell,
                $"screen roundtrip hits the defender cell (cell={cell}, rt={rt})");

            // 1) 짧은 탭 — 홀드 임계 전 릴리즈는 아무것도 하지 않는다
            Step(controller, true, true, screen, 0.05f);
            Step(controller, false, false, screen, 0.05f);
            Assert.IsFalse(controller.InMoveMode, "short tap does not enter move mode");
            Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 0.001f, "no slowmo after tap");

            // 2) 홀드 → 이동모드 + 슬로모
            Step(controller, true, true, screen, 0.05f);
            for (int i = 0; i < 5; i++) Step(controller, false, true, screen, 0.1f); // 누적 0.5s > 0.3s
            Assert.IsTrue(controller.InMoveMode, "hold enters move mode");
            Assert.AreEqual(cell, controller.MoveSourceCell, "source cell captured");
            Assert.Less(TimeManager.Instance.ScaleOf(TimeDomain.Battle), 0.999f, "slowmo lease active");

            // 3) 취소 → 슬로모 해제
            controller.CancelMoveMode();
            Assert.IsFalse(controller.InMoveMode, "cancel exits move mode");
            Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 0.001f, "slowmo released on cancel");

            // 4) 진입 쿨다운 — 직후 재홀드는 진입 불가
            Step(controller, true, true, screen, 0.05f);
            for (int i = 0; i < 5; i++) Step(controller, false, true, screen, 0.1f);
            Assert.IsFalse(controller.InMoveMode, "entry cooldown blocks immediate re-entry");
            Step(controller, false, false, screen, 0.05f); // 릴리즈

            // 5) 쿨다운 경과 후 재진입 → 타임아웃 자동 취소
            for (int i = 0; i < 8; i++) Step(controller, false, false, screen, 0.1f); // 쿨다운 0.5s 소진
            Step(controller, true, true, screen, 0.05f);
            for (int i = 0; i < 5; i++) Step(controller, false, true, screen, 0.1f);
            Assert.IsTrue(controller.InMoveMode, "re-entry after cooldown");
            for (int i = 0; i < 12; i++) Step(controller, false, true, screen, 0.1f); // 타임아웃 1.0s 초과
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
