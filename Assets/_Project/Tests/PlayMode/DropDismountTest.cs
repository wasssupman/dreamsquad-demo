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
    // defender-drop-dismount unit 5 — 하마 비행 계약 회귀 가드.
    //   계약 5: 핸드오프 팝 0(고스트 마지막 발점 ≈ 첫 오버라이드)
    //   계약 4: 활성화 시계 commit 기준(deploymentDuration), 착지 ≤ 활성화
    //   계약 1: 탭(시뮬) 경로는 dismount 미발동
    //   계약 7: 비행은 세션 독립(새 드래그가 이전 비행을 죽이지 않음)
    public class DropDismountTest
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
        public IEnumerator RealDragDrop_HandoffContinuity_ActivationClock_TapGate_SessionIndependence()
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
            Assert.IsNotNull(ctrl, "drag controller");
            var cam = Camera.main;

            // ── 1) 실드래그 릴리스: 핸드오프 연속성 + 점유/코스트 커밋 프레임 확정 ──
            var cellA = FindValidCellWithScreen(bridge, cam, unit, out var screenA);
            var driveA = DriveAiming(ctrl, screenA);
            ctrl.BeginDrag(unit, new Vector2(Screen.width * 0.5f, Screen.height * 0.08f));
            // 스프링 추종(_unitPosWorld)이 자리 잡도록 몇 프레임 목표를 유지
            for (int i = 0; i < 12; i++) { ctrl.UpdateDrag(driveA); yield return null; }
            Assert.IsTrue(ctrl.IsDragging, "drag session active");

            Vector3 ghostFeet = (Vector3)Field(ctrl, "_unitPosWorld");
            float costBefore = gm.CostRuntime.Current;
            float commitTime = Time.realtimeSinceStartup;
            ctrl.EndDrag(driveA); // 커밋(동기) — 같은 프레임에 점유·오버라이드 등록

            Assert.IsTrue(bridge.TryGetDefenderAt(cellA, out var entityA, out _, out bool busyA),
                "commit frame: cell occupied");
            Assert.IsTrue(busyA, "commit frame: pending(비전투)");
            Assert.Less(gm.CostRuntime.Current, costBefore, "commit frame: cost spent");

            var firstOverride = GetOverride(bridge, entityA);
            Assert.IsTrue(firstOverride.HasValue, "commit frame: view override registered (dismount began)");
            Assert.Less(Vector3.Distance(firstOverride.Value, ghostFeet), 0.05f,
                $"핸드오프 팝: 고스트 발점 {ghostFeet} vs 첫 오버라이드 {firstOverride.Value}");

            // ── 2) 세션 독립: 비행 중 새 드래그 시작 → 이전 비행 계속 진행 ──
            ctrl.BeginDrag(unit, new Vector2(Screen.width * 0.5f, Screen.height * 0.08f));
            Vector3 sampleA = GetOverride(bridge, entityA) ?? default;
            bool progressed = false;
            for (int i = 0; i < 8; i++)
            {
                ctrl.UpdateDrag(new Vector2(Screen.width * 0.4f, Screen.height * 0.5f));
                yield return null;
                var cur = GetOverride(bridge, entityA);
                if (!cur.HasValue) { progressed = true; break; } // 이미 착지(정리)도 진행의 증거
                if (Vector3.Distance(cur.Value, sampleA) > 1e-4f) { progressed = true; break; }
            }
            Assert.IsTrue(progressed, "새 드래그 시작 후에도 이전 dismount 비행이 계속 갱신돼야 한다(계약 7)");
            // 보드 밖 릴리스 = 취소. **가상** 포인터가 보드 밖이어야 한다 — 오프셋을 안 빼면 판정점이
            // 화면 위로 올라가 보드 안에 들어오고, 취소가 아니라 배치가 되어 이 섹션의 의도가 조용히 사라진다.
            ctrl.EndDrag(DriveAiming(ctrl, new Vector2(Screen.width * 0.5f, Screen.height * 0.01f)));

            // ── 3) 활성화 시계: commit + deploymentDuration(±0.15s 창), 착지 ≤ 활성화 ──
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline
                   && em.Exists(entityA) && em.HasComponent<Wassup.Battle.Units.PendingDeployment>(entityA))
                yield return null;
            float activationElapsed = Time.realtimeSinceStartup - commitTime;
            Assert.IsFalse(em.HasComponent<Wassup.Battle.Units.PendingDeployment>(entityA), "activated");
            float expected = Mathf.Max(0f, unit.deploymentDuration);
            // 창 상한 +0.25 는 에디터 프레임 히치 여유 — 진짜 회귀(착지 후 시계 시작 = +0.45 시프트)와는
            // 명확히 구분된다. 하한은 조기 활성화(클램프 위반) 검출.
            Assert.That(activationElapsed, Is.InRange(expected - 0.05f, expected + 0.25f),
                $"활성화 = commit + deploymentDuration({expected}s) 유지(계약 4). 실측 {activationElapsed:F3}s");
            for (int i = 0; i < 2; i++) yield return null;
            Assert.IsFalse(GetOverride(bridge, entityA).HasValue,
                "착지(오버라이드 해제)가 활성화보다 늦지 않다(계약 3 클램프)");

            // 착지 앵커 = 실제 렌더 좌표 — sim/view 혼동 회귀 가드(2026-07-28 육안 재현:
            // 앵커가 sim 좌표면 비행이 화면 밖으로 이탈하다 해제 순간 타일로 텔레포트).
            Assert.IsTrue(bridge.TryGetDefenderRestViewPos(cellA, out var restPos), "rest view pos resolvable");
            var viewT = ResolveViewTransform(bridge, entityA);
            Assert.IsNotNull(viewT, "unit view transform exists");
            Assert.Less(Vector3.Distance(viewT.position, restPos), 0.05f,
                $"착지 앵커({restPos})는 실제 렌더 좌표({viewT.position})와 일치(ToView 출력 미러)");

            // ── 4) 탭(시뮬) 경로 게이트: dismount 미발동 = 오버라이드 미등록 ──
            var cellB = FindValidCellWithScreen(bridge, cam, unit, out _, exclude: cellA);
            int overrideSightings = 0;
            ctrl.SimulateDragTo(unit, new Vector2(Screen.width * 0.5f, Screen.height * 0.08f), cellB);
            float tapDeadline = Time.realtimeSinceStartup + 8f;
            Entity entityB = Entity.Null;
            while (Time.realtimeSinceStartup < tapDeadline)
            {
                if (OverrideCount(bridge) > 0) overrideSightings++;
                if (bridge.TryGetDefenderAt(cellB, out entityB, out _, out _)) break;
                yield return null;
            }
            Assert.AreNotEqual(Entity.Null, entityB, "tap placement landed");
            for (int i = 0; i < 3; i++)
            {
                if (OverrideCount(bridge) > 0) overrideSightings++;
                yield return null;
            }
            Assert.AreEqual(0, overrideSightings, "탭 경로는 dismount(뷰 오버라이드) 미발동(계약 1)");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        // placement-thumb-occlusion unit 0 — 배치 판정은 **가상 포인터**(실제 포인터 + 화면 up × offset)를
        // 쓴다. 목표 지점을 가상 포인터가 가리키게 하는 **구동 좌표**를 준다 = Apply 의 역. 산식을 손으로
        // 재구현하지 않는 이유: 축이 바뀌면 이 가드가 옛 산식으로 조용히 초록이 된다.
        // UpdateDrag/EndDrag 를 직접 부르므로 화면 밖 y 도 무해하다(ScreenPointToRay 가 유효한 ray 를 준다).
        // 셀 탐색/roundtrip 은 보정 전 값 그대로 쓴다.
        private static Vector2 DriveAiming(DefenderDragPlacementController ctrl, Vector2 aimTarget)
            => PlacementPointerOffset.Apply(aimTarget, -ctrl.PlacementPointerOffsetPx, 1f);

        private static object Field(object o, string name)
            => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

        private static System.Collections.IDictionary OverrideDict(BattleBridge bridge)
            => (System.Collections.IDictionary)typeof(BattleBridge)
                .GetField("_defenderViewOverride", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(bridge);

        private static int OverrideCount(BattleBridge bridge) => OverrideDict(bridge).Count;

        private static Vector3? GetOverride(BattleBridge bridge, Entity entity)
        {
            var dict = OverrideDict(bridge);
            if (!dict.Contains(entity)) return null;
            var f3 = (Unity.Mathematics.float3)dict[entity];
            return (Vector3)f3;
        }

        // 유효 배치 셀 중 화면 좌표가 정확히 그 셀로 되돌아오는(roundtrip) 첫 셀.
        private static Vector2Int FindValidCellWithScreen(BattleBridge bridge, Camera cam,
            DefenderUnitData unit, out Vector2 screen, Vector2Int? exclude = null)
        {
            var grid = bridge.DebugGridSize;
            for (int y = 0; y < grid.y; y++)
                for (int x = 0; x < grid.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (exclude.HasValue && cell == exclude.Value) continue;
                    if (!bridge.CanPlaceDefenderAt(x, y, unit, out _)) continue;
                    Vector2 s = cam.WorldToScreenPoint(bridge.GridCellToViewCenter(cell));
                    if (s.x < 0 || s.x >= Screen.width || s.y < 0 || s.y >= Screen.height) continue;
                    if (bridge.TryScreenToCell(cam, s, out var rt) && rt == cell) { screen = s; return cell; }
                }
            Assert.Fail("no valid cell with screen roundtrip");
            screen = default;
            return default;
        }

        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        // Spine → Quad 폴백 순으로 실제 렌더 Transform (bridge 내부 앵커 해석 재사용).
        private static Transform ResolveViewTransform(BattleBridge bridge, Entity entity)
            => (Transform)typeof(BattleBridge)
                .GetMethod("ResolveUnitViewTransform", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(bridge, new object[] { entity });
    }
}
