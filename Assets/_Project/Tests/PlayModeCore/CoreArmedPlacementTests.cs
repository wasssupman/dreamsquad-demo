using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.Hud;
using Wassup.BattleCoreUnity.Input;
using Wassup.BattleCoreUnity.View;
using Wassup.Data;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild 5b 수정 — **집어 들고 판을 탭해서 놓는 길이 있는가.**
    //
    // 사용자 플레이 2차의 문장: **「유닛 셀 선택 → 배치 상태 → 타일 터치 또는 드래그」 기능 누락**.
    //
    // 5b 는 이 길을 「옛 게임에서 은퇴한 클릭 배치」로 읽고 제외했는데 **오판이었다** —
    // 은퇴한 것은 옛 `PlacementInput` 의 클릭 경로이고, 그 뒤 `defender-tap-to-place` ·
    // `placement-armed-board-drag` 가 트레이 탭 → armed → 판 탭/드래그를 새로 세웠다.
    //
    // ⚠ 포인터 제스처 자체는 여기서도 흉내 내지 않는다. 대신 **제스처가 부르는 그 함수들**
    // (`ToggleArm` = 트레이 탭 · `ReleaseArmedAt` = 판에서 손 뗌)을 직접 부른다.
    public sealed class CoreArmedPlacementTests
    {
        [UnityTest]
        public IEnumerator 트레이_탭은_집어_들고_판_탭은_그_칸에_놓는다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");

            var input = Object.FindAnyObjectByType<DragPlacementInput>();
            var flight = Object.FindAnyObjectByType<CoreDeployFlightPresenter>();
            var tray = Object.FindAnyObjectByType<CoreDefenderTray>();
            var cam = Camera.main;
            Assert.IsNotNull(input, "배치 입력이 씬에 없다");
            Assert.IsNotNull(flight);
            Assert.IsNotNull(tray);
            Assert.IsNotNull(cam);

            driver.Apply(Command.FinishPlacement());
            yield return null;

            Assert.IsTrue(TryFindPlaceable(driver, out int defIndex, out int2 anchor),
                "로스터의 어떤 유닛도 이 판 어디에도 놓을 수 없다");

            var placed = new List<int>();
            System.Action<CoreEvent> probe = e =>
            {
                // ⚠ 배치 사건의 정의표 줄은 `Arg` 다(`DefIndex` 는 주체가 사라지는 사건 둘만 쓴다).
                if (e.Kind == CoreEventKind.Placed) placed.Add(e.Arg);
            };
            driver.Subscribe(ViewOrder.Trace, probe);

            try
            {
                // ① 트레이 칸 탭 = 집어 든다.
                var trayScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.1f);
                input.ToggleArm(defIndex, trayScreen);
                Assert.AreEqual(defIndex, input.ArmedDefIndex,
                    "트레이 탭으로 집어 들지 못했다 — 배치 상태가 없다");
                Assert.AreEqual(defIndex, tray.DraggingDefIndex,
                    "집어 든 칸이 트레이에서 강조되지 않는다");

                // ② 판 탭 = 그 칸에 배치. 커맨드는 **한 번**이고 비행도 **한 번**이다.
                Vector2 boardScreen = ScreenOfCell(driver, cam, FingerCellFor(driver, defIndex, anchor));
                Assert.IsTrue(input.ReleaseArmedAt(boardScreen, sticky: false),
                    "판 탭으로 배치되지 않았다");

                Assert.AreEqual(1, placed.Count, "배치 커맨드가 정확히 한 번 나야 한다");
                Assert.AreEqual(defIndex, placed[0], "집어 든 것과 다른 유닛이 놓였다");
                Assert.AreEqual(1, flight.FlightCount,
                    "탭 배치도 트레이 칸에서 날아와야 한다 — 드래그 배치와 같은 착지다");

                // ③ 릴리즈는 **언제나** 선택을 끝낸다 — 손을 뗐는데 선택이 남지 않는다.
                Assert.AreEqual(-1, input.ArmedDefIndex, "배치 뒤에도 선택이 남아 있다");
                Assert.AreEqual(-1, tray.DraggingDefIndex, "배치 뒤에도 트레이 강조가 남아 있다");
            }
            finally
            {
                driver.Unsubscribe(probe);
            }

            CoreSceneFixture.EndErrorWatch();
            Assert.AreEqual(0, CoreSceneFixture.Errors.Count,
                "콘솔 에러: " + string.Join(" | ", CoreSceneFixture.Errors));
        }

        [UnityTest]
        public IEnumerator 같은_칸_재탭은_해제하고_다른_칸_탭은_갈아탄다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver);
            var input = Object.FindAnyObjectByType<DragPlacementInput>();
            Assert.IsNotNull(input);
            driver.Apply(Command.FinishPlacement());

            Assert.IsTrue(TryFindPlaceable(driver, out int a, out _));
            Assert.IsTrue(TryFindPlaceable(driver, out int b, out _, a),
                "두 번째 유닛이 없다 — 갈아타기를 증언할 수 없다");

            var screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.1f);
            input.ToggleArm(a, screen);
            Assert.AreEqual(a, input.ArmedDefIndex);

            input.ToggleArm(a, screen);
            Assert.AreEqual(-1, input.ArmedDefIndex, "같은 칸을 다시 탭했는데 안 풀렸다");

            input.ToggleArm(a, screen);
            input.ToggleArm(b, screen);
            Assert.AreEqual(b, input.ArmedDefIndex, "다른 칸을 탭했는데 안 갈아탔다");

            input.Disarm();
            Assert.AreEqual(-1, input.ArmedDefIndex);
        }

        [UnityTest]
        public IEnumerator 판_밖_탭은_커맨드_없이_선택만_푼다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver);
            var input = Object.FindAnyObjectByType<DragPlacementInput>();
            var flight = Object.FindAnyObjectByType<CoreDeployFlightPresenter>();
            Assert.IsNotNull(input);
            Assert.IsNotNull(flight);
            driver.Apply(Command.FinishPlacement());

            Assert.IsTrue(TryFindPlaceable(driver, out int defIndex, out _));

            var rejected = new List<RejectReason>();
            System.Action<CoreEvent> probe = e =>
            {
                if (e.Kind == CoreEventKind.PlacementRejected) rejected.Add((RejectReason)e.Arg);
            };
            driver.Subscribe(ViewOrder.Trace, probe);
            try
            {
                input.ToggleArm(defIndex, new Vector2(Screen.width * 0.5f, Screen.height * 0.1f));
                // 화면 구석 — 판이 없는 자리다. 「칸 없음」은 거부가 아니라 **취소**라
                // 커맨드를 아예 안 보낸다(드래그의 보드 밖 드롭과 같은 규칙).
                Assert.IsFalse(input.ReleaseArmedAt(new Vector2(2f, Screen.height - 2f), sticky: false));
                Assert.AreEqual(-1, input.ArmedDefIndex, "판 밖에서 뗐는데 선택이 남았다");
                Assert.AreEqual(0, rejected.Count, "취소인데 거절 사건이 났다");
                Assert.AreEqual(0, flight.FlightCount, "취소인데 비행이 떴다");
            }
            finally
            {
                driver.Unsubscribe(probe);
            }
        }

        // ── 공용 ─────────────────────────────────────────────────────────────

        // 그 앵커를 만들어 내는 손끝 칸(앵커 = footprint 하단 행 가로 중앙).
        private static Vector2Int FingerCellFor(BattleDriver driver, int defIndex, int2 anchor)
        {
            var fp = new Vector2Int(math.max(1, driver.Definition.Units[defIndex].FootprintWidth),
                                    math.max(1, driver.Definition.Units[defIndex].FootprintHeight));
            var size = driver.GridSize;
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                var a = FootprintMath.AnchorFromBottomCenter(new Vector2Int(x, y), fp);
                if (a.x == anchor.x && a.y == anchor.y) return new Vector2Int(x, y);
            }
            Assert.Fail("그 앵커의 손끝 칸을 못 찾았다");
            return default;
        }

        private static Vector2 ScreenOfCell(BattleDriver driver, Camera cam, Vector2Int cell)
        {
            float ts = driver.TileSize;
            Vector3 world = (Vector3)Wassup.Core.BoardSpace.ToView(
                new float3(cell.x * ts, 0f, cell.y * ts));
            return cam.WorldToScreenPoint(world);
        }

        private static bool TryFindPlaceable(BattleDriver driver, out int defIndex, out int2 anchor,
                                             int skipIndex = -1)
        {
            var placement = driver.Match.Placement;
            var size = driver.GridSize;
            for (int i = 0; i < driver.Definition.Units.Length; i++)
            {
                if (i == skipIndex || !placement.InRoster(i)) continue;
                for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    var c = new int2(x, y);
                    if (placement.Judge(i, c) != RejectReason.None) continue;
                    defIndex = i;
                    anchor = c;
                    return true;
                }
            }
            defIndex = -1;
            anchor = default;
            return false;
        }
    }
}
