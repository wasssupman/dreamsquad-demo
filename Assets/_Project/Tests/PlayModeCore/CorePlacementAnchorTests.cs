using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.Input;
using Wassup.Data;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild 5b 수정 — **손끝이 가리킨 칸이 곧 결과인가.**
    //
    // 사용자 플레이 2차의 문장: **「배치 불가 타일이 겹친 곳이나 포인터 미세 이동에 보정이
    // 너무 심해 위치가 크게 바뀐다. 보정만 제거해」**.
    //
    // 증상 단언: 못 놓는 칸을 가리켰을 때 **앵커가 움직이지 않는다.** 예전에는 둘레에서
    // 합격 자리를 찾아 거기로 옮겼고(자석), 그래서 화면이 「여기 놓인다」고 말한 적 없는
    // 칸에 유닛이 섰다.
    public sealed class CorePlacementAnchorTests
    {
        [UnityTest]
        public IEnumerator 못_놓는_칸을_가리켜도_앵커가_움직이지_않는다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");

            var input = Object.FindAnyObjectByType<DragPlacementInput>();
            Assert.IsNotNull(input, "배치 입력이 씬에 없다");
            var cam = Camera.main;
            Assert.IsNotNull(cam, "보드 카메라가 없다");

            driver.Apply(Command.FinishPlacement());

            // 유닛을 하나 세워 「겹친 불가 칸」을 만든다 — 사용자 문장의 그 상황이다.
            Assert.IsTrue(TryFindPlaceable(driver, out int placedIndex, out int2 anchor),
                "로스터의 어떤 유닛도 이 판 어디에도 놓을 수 없다");
            Assert.IsTrue(driver.Apply(Command.PlaceDefender(placedIndex, anchor)).Accepted);
            yield return null;

            // ⚠ 끄는 것은 **다른 유닛**이다. 방금 놓은 종류는 판 위 상한이 차서 모든 칸이
            // `LimitReached` 가 되고, 그러면 「둘레에 합격 칸이 있는 불가 칸」이 존재할 수 없다.
            Assert.IsTrue(TryFindPlaceable(driver, out int defIndex, out _, placedIndex),
                "끌어 볼 두 번째 유닛이 없다 — 이 판으로는 증언할 수 없다");

            // 그 유닛 때문에 못 놓게 된 손끝 칸을 찾는다. **둘레에 합격 칸이 있는** 자리라야
            // 자석이 걸릴 수 있고, 그래야 「안 움직인다」가 증언이 된다.
            var placement = driver.Match.Placement;
            var size = driver.GridSize;
            var fp = new Vector2Int(math.max(1, driver.Definition.Units[defIndex].FootprintWidth),
                                    math.max(1, driver.Definition.Units[defIndex].FootprintHeight));
            bool found = false;
            Vector2Int fingerCell = default;
            int2 wantAnchor = default;
            for (int y = 0; y < size.y && !found; y++)
            for (int x = 0; x < size.x && !found; x++)
            {
                var c = new Vector2Int(x, y);
                var a = FootprintMath.AnchorFromBottomCenter(c, fp);
                var ai = new int2(a.x, a.y);
                if (placement.Judge(defIndex, ai) == RejectReason.None) continue;
                if (!HasPlaceableNeighbour(placement, defIndex, ai)) continue;
                fingerCell = c;
                wantAnchor = ai;
                found = true;
            }
            Assert.IsTrue(found, "자석이 걸릴 수 있는 불가 칸이 없다 — 이 판으로는 증언할 수 없다");

            // 그 칸 한가운데를 손끝으로 가리킨다.
            float ts = driver.TileSize;
            Vector3 world = (Vector3)Wassup.Core.BoardSpace.ToView(
                new float3(fingerCell.x * ts, 0f, fingerCell.y * ts));
            Vector2 screen = cam.WorldToScreenPoint(world);

            Assert.IsTrue(input.TryResolveAnchor(screen, defIndex, sticky: false,
                                                 out var got, out bool valid),
                "손끝이 판 위인데 칸을 못 찾았다");
            Assert.IsFalse(valid, "못 놓는 칸인데 유효하다고 답했다");
            Assert.AreEqual(wantAnchor, got,
                "손끝이 가리킨 칸이 아니라 다른 칸으로 옮겨졌다 — 보정이 아직 살아 있다");
        }

        [UnityTest]
        public IEnumerator 놓을_수_있는_칸은_그대로_유효하다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver);
            var input = Object.FindAnyObjectByType<DragPlacementInput>();
            var cam = Camera.main;
            Assert.IsNotNull(input);
            Assert.IsNotNull(cam);
            driver.Apply(Command.FinishPlacement());

            Assert.IsTrue(TryFindPlaceable(driver, out int defIndex, out int2 anchor));
            var fp = new Vector2Int(math.max(1, driver.Definition.Units[defIndex].FootprintWidth),
                                    math.max(1, driver.Definition.Units[defIndex].FootprintHeight));

            // 그 앵커를 만들어 내는 손끝 칸을 역산한다(앵커 = 하단 행 가로 중앙).
            var size = driver.GridSize;
            bool found = false;
            Vector2Int fingerCell = default;
            for (int y = 0; y < size.y && !found; y++)
            for (int x = 0; x < size.x && !found; x++)
            {
                var a = FootprintMath.AnchorFromBottomCenter(new Vector2Int(x, y), fp);
                if (a.x != anchor.x || a.y != anchor.y) continue;
                fingerCell = new Vector2Int(x, y);
                found = true;
            }
            Assert.IsTrue(found, "그 앵커의 손끝 칸을 못 찾았다");

            float ts = driver.TileSize;
            Vector3 world = (Vector3)Wassup.Core.BoardSpace.ToView(
                new float3(fingerCell.x * ts, 0f, fingerCell.y * ts));
            Vector2 screen = cam.WorldToScreenPoint(world);

            Assert.IsTrue(input.TryResolveAnchor(screen, defIndex, sticky: false, out var got, out bool valid));
            Assert.AreEqual(anchor, got, "놓을 수 있는 칸인데 앵커가 달라졌다");
            Assert.IsTrue(valid);
        }

        private static bool HasPlaceableNeighbour(PlacementService placement, int defIndex, int2 at)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                if (placement.Judge(defIndex, new int2(at.x + dx, at.y + dy)) == RejectReason.None)
                    return true;
            }
            return false;
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
