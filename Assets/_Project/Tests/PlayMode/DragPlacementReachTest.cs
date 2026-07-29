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
    // 트레이 드래그의 셀 판정이 **가상 포인터**(= 손가락 + 고정 오프셋)를 따르는지 지키는 회귀 가드.
    //
    // 회귀 이력: 판정 기준이 프리뷰용 발점(_unitTargetWorld)이었다. 발점은 손가락보다
    // totalDrop(= 유닛 키 + ropeLength×visualScale) 만큼 화면 아래라, 손가락을 화면 최상단까지
    // 올려도 발점이 상단 행에 못 미쳤다 → 15×11 맵에서 **상단 3행이 영구 배치 불가**, 동시에
    // 화면 하단 절반이 전부 row 0 에 뭉쳤다. 판정을 손가락 보드 히트(_fingerBoardWorld)로
    // 옮겨 해결. 스펙 계약은 원래 "배치 칸 = 마우스"(docs/spec/keyring-cord-preview/README.md).
    //
    // placement-thumb-occlusion unit 0 — 그 계약이 "배치 칸 = **가상 포인터**"로 개정됐다(손가락이
    // 하이라이트를 덮는 문제). 오프셋은 **화면 높이 비율의 상수 평행이동**이라 발점 회귀와는 종류가
    // 다르다 — 발점은 totalDrop 만큼 밀리며 히스테리시스 밴드 자체를 판 밖으로 끌고 나갔다.
    //
    // 이 테스트가 깨지면 ropeLength/오프셋 튜닝이 아니라 **판정 기준이 발점으로 되돌아갔는지** 먼저 본다.
    public class DragPlacementReachTest
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
        public IEnumerator TopPlaceableRow_IsReachable_AndCommitFollowsPlacementPointer()
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
            var cam = Camera.main;
            var grid = bridge.DebugGridSize;

            int topPlaceableRow = TopPlaceableRow(bridge, unit, grid);
            Assert.GreaterOrEqual(topPlaceableRow, 0, "map has at least one placeable cell");
            int bottomPlaceableRow = BottomPlaceableRow(bridge, unit, grid);

            ctrl.BeginDrag(unit, new Vector2(Screen.width * 0.5f, Screen.height * 0.08f));
            yield return null;
            Assert.IsTrue(ctrl.IsDragging, "drag session active");

            // 화면 중앙 열을 아래→위로 촘촘히(1%) 스윕. 1% 스텝은 의도적이다: placementStickMargin
            // 이 크면 이웃 도달 창이 (1 - margin) 타일로 좁아져, 거친 스텝은 실제 연속 드래그와 달리
            // 행을 건너뛴다(그건 판정 버그가 아니라 샘플링 아티팩트).
            // placement-thumb-occlusion unit 2 — 하한을 1px 까지 내린다. 오프셋이 붙으면 최하단 행을
            // 노리는 손가락이 화면 5% 아래로 내려간다. 오프셋은 **상수 평행이동**이라 stickMargin 과
            // 곱셈적으로 얽히지 않는다(Resolve 는 frac 만 본다) — 스텝 근거는 그대로 유효하다.
            int maxRow = int.MinValue, minRow = int.MaxValue;
            float maxYTargetingBottom = float.MinValue;
            int aimMismatch = 0, samples = 0;
            for (int p = 0; p <= 100; p++)
            {
                float y = Mathf.Clamp(Screen.height * (p / 100f), 1f, Screen.height - 1f);
                var screen = new Vector2(Screen.width * 0.5f, y);
                ctrl.UpdateDrag(screen);
                ResolveForceCommit(ctrl);

                if (!(bool)Field(ctrl, "_onBoard")) continue;
                var hover = SessionHover(ctrl);
                if (!hover.HasValue) continue;
                if (hover.Value.y > maxRow) maxRow = hover.Value.y;
                if (hover.Value.y < minRow) minRow = hover.Value.y;
                // unit 2 — 최하단 배치가능 행을 노릴 수 있는 **가장 높은** 손가락 위치. 오프셋이 커지면
                // 이 값이 내려간다 = 그 행을 놓으려면 손가락을 화면 밑(트레이 영역)까지 내려야 한다.
                if (hover.Value.y == bottomPlaceableRow && y > maxYTargetingBottom) maxYTargetingBottom = y;

                // 확정 셀은 **가상 포인터**가 가리키는 셀과 같아야 한다(히스테리시스 폭 이내).
                // unit 0 — 비교 기준이 손가락에서 가상 포인터로 옮겨졌다. 스윕 좌표는 raw 로 두고
                // 기대값만 올린다(컨트롤러가 UpdateDrag 진입부에서 같은 변환을 한다).
                // 산식은 프로덕션과 같은 함수를 쓴다 — 손으로 재구현하면 축이 바뀌어도(후속 후보의
                // "물리 단위화"가 그 방향) 이 가드가 옛 산식으로 조용히 초록이 된다.
                var aimScreen = PlacementPointerOffset.Apply(screen, ctrl.PlacementPointerOffsetPx, 1f);
                if (bridge.TryScreenToCell(cam, aimScreen, out var aimCell))
                {
                    samples++;
                    if (Mathf.Abs(hover.Value.y - aimCell.y) > 1) aimMismatch++;
                }
            }

            // unit 2 — **부등식**이다. 지키려는 계약은 "도달 가능"이고, 등식은 그보다 넓게 못박아
            // 결과 셀의 그리드 clamp(PlacementCellSnap:39-41)와 맵 placeability 분포에 결합된다.
            // 오프셋은 상단 도달을 늘리므로(손가락이 화면 끝까지 갈 필요가 없어진다) 등식은 깨질 수 있다.
            Assert.GreaterOrEqual(maxRow, topPlaceableRow,
                $"최상단 배치 가능 행({topPlaceableRow})에 도달해야 한다. 도달한 최대 행={maxRow}. " +
                "판정 기준이 프리뷰 발점으로 되돌아갔는지 확인하라.");
            Assert.Greater(samples, 20, "sweep produced samples");
            Assert.AreEqual(0, aimMismatch,
                $"확정 셀이 **가상 포인터** 셀에서 1행 이상 벗어난 샘플 {aimMismatch}/{samples}건. "
                + $"오프셋={ctrl.PlacementPointerOffsetPx:F1}px");

            // ── unit 2: 하단 ──────────────────────────────────────────────────
            // (A) 도달 tripwire — **약한 단언**이다. 결과 셀이 그리드로 clamp 되므로 손가락을 보드
            // 아래로 내리면 거의 항상 row 0 으로 접힌다. 이건 안전망이 아니라 판정이 통째로 망가졌을
            // 때만 울리는 회귀선이다. 하단 보증은 (B) 가 한다.
            Assert.LessOrEqual(minRow, bottomPlaceableRow,
                $"최하단 배치 가능 행({bottomPlaceableRow})에 도달해야 한다. 도달한 최소 행={minRow}");

            // (B) 조작성 계측 — 진짜 가드. 오프셋이 커질수록 최하단 행을 노릴 수 있는 가장 높은 손가락
            // 위치가 내려간다. 그 위치가 화면 하단 안전선 밑이면, 오프셋이 가림을 없앤 게 아니라
            // 오클루더를 손가락 → 트레이 UI 로 옮긴 것이다. BottomSafeRatio 는 트레이 높이의 **근사**다
            // (테스트는 UI 를 모른다) — 실측 대조는 실기기 확인 항목이 담당한다.
            Assert.Greater(maxYTargetingBottom, Screen.height * BottomSafeRatio,
                $"최하단 배치가능 행(row {bottomPlaceableRow})을 노릴 수 있는 가장 높은 손가락 위치가 "
                + $"{maxYTargetingBottom:F0}px = 화면의 {maxYTargetingBottom / Screen.height:P0} 로, 안전선"
                + $"({BottomSafeRatio:P0}) 아래다. 그 행을 놓으려면 손가락이 트레이 영역까지 내려가야 한다 — "
                + $"placementPointerOffsetHeightRatio 를 낮춰라. 현재 오프셋={ctrl.PlacementPointerOffsetPx:F1}px");

            ctrl.Disarm();
            yield return null;
        }

        // unit 2 — 트레이 UI 가 점유하는 화면 하단 비율의 근사. 테스트는 UI 를 모르므로 상수로 둔다.
        const float BottomSafeRatio = 0.12f;

        // 이름은 둘 유지(단언 메시지가 "최상단/최하단"으로 읽힌다), 스캔은 하나.
        // unit 2 — 하단은 row 0 이 전부 경로/Deco 면 0 이 아닌 값이 나온다.
        private static int TopPlaceableRow(BattleBridge bridge, DefenderUnitData unit, Vector2Int grid)
            => FirstPlaceableRow(bridge, unit, grid, fromTop: true);

        private static int BottomPlaceableRow(BattleBridge bridge, DefenderUnitData unit, Vector2Int grid)
            => FirstPlaceableRow(bridge, unit, grid, fromTop: false);

        private static int FirstPlaceableRow(BattleBridge bridge, DefenderUnitData unit, Vector2Int grid,
            bool fromTop)
        {
            for (int i = 0; i < grid.y; i++)
            {
                int y = fromTop ? grid.y - 1 - i : i;
                for (int x = 0; x < grid.x; x++)
                    if (bridge.CanPlaceDefenderAt(x, y, unit, out _))
                        return y;
            }
            return -1;
        }

        private static object Field(object o, string name)
            => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

        private static Vector2Int? SessionHover(object ctrl)
        {
            var session = Field(ctrl, "_session");
            return (Vector2Int?)session.GetType()
                .GetField("hoverTile", BindingFlags.Public | BindingFlags.Instance).GetValue(session);
        }

        // EndDrag 의 확정 경로와 동일 — throttle 을 기다리지 않고 손가락 최종 위치로 즉시 재해석.
        private static void ResolveForceCommit(object ctrl)
            => ctrl.GetType().GetMethod("ResolveFocusAndTarget", BindingFlags.NonPublic | BindingFlags.Instance)
                   .Invoke(ctrl, new object[] { 0f, true, null });

        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }
    }
}
