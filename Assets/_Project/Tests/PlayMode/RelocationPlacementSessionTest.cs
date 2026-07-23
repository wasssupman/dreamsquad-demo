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
using Wassup.Battle.Units;
using Wassup.UI;

namespace Wassup.Tests.PlayMode
{
    // defender-relocation unit 2 — 이동모드 배치 세션: 홀드 릴리즈(커밋 아님) → 탭 커밋 /
    // 본인 탭 취소 / 무효(점유) 탭 reject+유지 / 드래그 릴리즈 커밋. 코스트 불변(계약 1).
    // 컨트롤러 Step 을 reflection 으로 구동(원격 검증 경로), 커밋 꼬리는 unit 3 전 임시 즉시형.
    public class RelocationPlacementSessionTest
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
        public IEnumerator TapAndDragCommit_SelfCancel_InvalidReject()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var controller = Object.FindObjectOfType<DefenderRelocationController>();
            controller.enabled = false; // 실제 입력 Update 차단

            var fast = ScriptableObject.CreateInstance<RelocationSettings>();
            fast.holdSeconds = 0.2f;
            fast.entryCooldownSeconds = 0.1f;
            fast.moveModeTimeoutSeconds = 30f;
            fast.redeploySeconds = 0.2f; // unit 3 — 비행+재전개 총 대기 짧게
            SetField(controller, "settings", fast);

            var cat = FindCatalog();
            var unit = cat.ById("ranger");
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place mover");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var source = SoleCell(bridge);
            gm.SetPhase(GamePhase.Battle);
            yield return null;

            var cam = Camera.main;
            Vector2 srcScreen = ScreenOf(bridge, cam, source);

            // 유효 목적지 / 점유(무효) 목적지 준비
            Vector2Int target = FindRelocTarget(bridge, source);
            Assert.IsTrue(bridge.PlaceDefenderAs(target.x, target.y, unit), "place blocker on future-invalid cell");
            Vector2Int occupiedCell = target;                     // 이제 점유 → 무효 목적지
            Vector2Int target2 = FindRelocTarget(bridge, source); // 새 유효 목적지
            Assert.AreNotEqual(occupiedCell, target2, "distinct valid target exists");

            // ── 1) 홀드 진입 → 손 떼기(커밋 아님) → 무효(점유) 탭 = reject + 유지 → 본인 탭 = 취소
            EnterMoveMode(controller, srcScreen, fast);
            Assert.IsTrue(controller.InMoveMode, "in move mode");
            Step(controller, false, false, srcScreen, 0.02f); // 홀드 승계 press 릴리즈(임계 전) — 탭 대기
            Assert.IsTrue(controller.InMoveMode, "carried-press release keeps move mode (no commit)");

            Vector2 occScreen = ScreenOf(bridge, cam, occupiedCell);
            Step(controller, true, true, occScreen, 0.02f);
            Step(controller, false, false, occScreen, 0.02f);
            Assert.IsTrue(controller.InMoveMode, "invalid (occupied) tap keeps move mode");
            Assert.AreEqual(source, CellOf(bridge, em, controller.MoveEntity), "binding unchanged after reject");

            Step(controller, true, true, srcScreen, 0.02f);
            Step(controller, false, false, srcScreen, 0.02f);
            Assert.IsFalse(controller.InMoveMode, "self-cell tap cancels move mode");
            Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 0.001f, "slowmo released on self-cancel");
            Assert.AreEqual(source, CellOf(bridge, em, controller.MoveEntity), "no move on cancel");

            // ── 2) 탭 커밋 (쿨다운 소진 후 재진입)
            for (int i = 0; i < 4; i++) Step(controller, false, false, srcScreen, 0.05f);
            EnterMoveMode(controller, srcScreen, fast);
            Step(controller, false, false, srcScreen, 0.02f); // 탭 대기 전환
            var mover = controller.MoveEntity;
            float costBefore = gm.CostRuntime.Current;
            Vector2 tgtScreen = ScreenOf(bridge, cam, target2);
            var posBefore = em.GetComponentData<Unity.Transforms.LocalTransform>(mover).Position;
            Step(controller, true, true, tgtScreen, 0.02f);
            Step(controller, false, false, tgtScreen, 0.02f); // 릴리즈 = 커밋
            Assert.IsFalse(controller.InMoveMode, "commit exits move mode");
            Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 0.001f, "slowmo released on commit");
            Assert.AreEqual(costBefore, gm.CostRuntime.Current, 0.001f, "relocation costs no cost (계약 1)");
            Assert.AreEqual(target2, CellOf(bridge, em, mover), "binding at tap target (확정 프레임)");
            // unit 3 — 커밋 직후는 비행/재전개 중(비타겟·비무장), 활성화는 비동기.
            Assert.IsTrue(em.HasComponent<PendingDeployment>(mover), "pending during flight/redeploy (unit 3)");
            yield return WaitUntilActivated(em, mover, 5f);
            Assert.IsFalse(em.HasComponent<PendingDeployment>(mover), "activated after flight+redeploy");
            var posAfter = em.GetComponentData<Unity.Transforms.LocalTransform>(mover).Position;
            Assert.Greater(Unity.Mathematics.math.distance(posBefore.xz, posAfter.xz), 0.5f,
                "sim position moved at landing (Finish)");

            // ── 3) 드래그 커밋: 홀드 승계 press 를 임계 초과 이동 → 릴리즈 = 커밋 (원 타일로 복귀)
            for (int i = 0; i < 4; i++) Step(controller, false, false, tgtScreen, 0.05f); // 쿨다운 소진
            Vector2 nowScreen = ScreenOf(bridge, cam, target2);
            EnterMoveMode(controller, nowScreen, fast);
            Vector2 backScreen = ScreenOf(bridge, cam, source); // 원 타일(현재 비어 있음)
            Step(controller, false, true, backScreen, 0.02f);   // 손 안 떼고 임계 초과 이동
            Step(controller, false, false, backScreen, 0.02f);  // 릴리즈 = 드래그 커밋
            Assert.IsFalse(controller.InMoveMode, "drag commit exits move mode");
            Assert.AreEqual(source, CellOf(bridge, em, mover), "binding back at source via drag commit");
            yield return WaitUntilActivated(em, mover, 5f); // 비행 완결 후 종료(다음 테스트 오염 방지)
            Assert.IsFalse(em.HasComponent<PendingDeployment>(mover), "second flight also activates");

            Object.Destroy(fast);
        }

        private static IEnumerator WaitUntilActivated(EntityManager em, Entity e, float timeoutSec)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSec;
            while (Time.realtimeSinceStartup < deadline
                   && em.Exists(e) && em.HasComponent<PendingDeployment>(e))
                yield return null;
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static void EnterMoveMode(DefenderRelocationController c, Vector2 screen, RelocationSettings s)
        {
            Step(c, true, true, screen, 0.02f);
            int ticks = Mathf.CeilToInt(s.holdSeconds / 0.05f) + 2;
            for (int i = 0; i < ticks; i++) Step(c, false, true, screen, 0.05f);
            Assert.IsTrue(c.InMoveMode, "hold entered move mode");
        }

        private static Vector2 ScreenOf(BattleBridge bridge, Camera cam, Vector2Int cell)
        {
            Vector2 screen = cam.WorldToScreenPoint(bridge.GridCellToViewCenter(cell));
            Assert.IsTrue(bridge.TryScreenToCell(cam, screen, out var rt) && rt == cell,
                $"screen roundtrip for {cell} (got {rt})");
            return screen;
        }

        private static Vector2Int FindRelocTarget(BattleBridge bridge, Vector2Int from)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanRelocateDefender(from, new Vector2Int(x, y), out _))
                        return new Vector2Int(x, y);
            Assert.Fail("no valid relocation target");
            return default;
        }

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

        private static Vector2Int CellOf(BattleBridge bridge, EntityManager em, Entity entity)
        {
            if (entity != Entity.Null && em.Exists(entity) && em.HasComponent<DefenderTile>(entity))
            {
                var c = em.GetComponentData<DefenderTile>(entity).cell;
                return new Vector2Int(c.x, c.y);
            }
            return new Vector2Int(int.MinValue, int.MinValue);
        }
    }
}
