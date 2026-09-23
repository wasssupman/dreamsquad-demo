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

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 5b — **배치가 도는가.**
    //
    // 이 lane 이 증언하는 것은 「커맨드 → receipt → 뷰 스폰」 사슬이다. 포인터 제스처 자체는
    // 여기서 흉내 내지 않는다 — 그건 Input System 의 테스트 픽스처가 필요하고, 그렇게 얻는
    // 증언(「손가락이 움직였다」)은 이 unit 의 질문(「놓으면 서는가」)에 답하지 않는다.
    // 손끝→칸 변환은 순수 함수 셋(`PlacementCellSnap`·`PlacementPointerOffset`·
    // `PlacementSnapDebounce`)이 이미 각자 테스트를 갖고 있고, 그 셋을 **재사용**한 것이
    // 이 unit 의 선택이다.
    public sealed class CorePlacementFlowTests
    {
        [UnityTest]
        public IEnumerator 배치_커맨드는_receipt_와_뷰_스폰으로_이어진다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");

            // 배치 창을 닫는다 — 이 판의 모드가 배치 중 입력을 막을 수도 있고, 그 축은
            // 여기서 묻는 것이 아니다(닫는 함수는 하나라 이 호출이 곧 정상 경로다).
            driver.Apply(Command.FinishPlacement());

            var pool = Object.FindAnyObjectByType<CoreUnitViewPool>();
            Assert.IsNotNull(pool, "유닛 뷰 풀이 씬에 없다");

            int defIndex;
            int2 anchor;
            Assert.IsTrue(TryFindPlaceable(driver, out defIndex, out anchor),
                "로스터의 어떤 유닛도 이 판 어디에도 놓을 수 없다 — 정의표·코스트·맵을 볼 것");

            int before = pool.ViewCount;
            var receipt = driver.Apply(Command.PlaceDefender(defIndex, anchor));
            Assert.IsTrue(receipt.Accepted, "판정이 통과한 자리인데 거절됐다: " + receipt.Reason);

            yield return null;
            Assert.AreEqual(before + 1, pool.ViewCount,
                "배치가 성사됐는데 뷰가 안 섰다 — 스폰 사건이 풀에 닿지 않았다");

            CoreSceneFixture.EndErrorWatch();
            Assert.AreEqual(0, CoreSceneFixture.Errors.Count,
                "콘솔 에러: " + string.Join(" | ", CoreSceneFixture.Errors));
        }

        [UnityTest]
        public IEnumerator 거절은_receipt_와_사건이_같은_사유를_나른다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver);
            driver.Apply(Command.FinishPlacement());

            var seen = new List<RejectReason>();
            System.Action<CoreEvent> probe = e =>
            {
                if (e.Kind == CoreEventKind.PlacementRejected) seen.Add((RejectReason)e.Arg);
            };
            driver.Subscribe(ViewOrder.Trace, probe);
            try
            {
                // 판 밖 — 공간이 답한다. receipt 는 그 입력을 낸 쪽에게, 사건은 판 전체에.
                // 둘이 다르면 트레이가 화면에 쓰는 말과 드래그가 받은 답이 갈린다.
                var receipt = driver.Apply(Command.PlaceDefender(0, new int2(9999, 9999)));
                Assert.IsFalse(receipt.Accepted);
                Assert.AreEqual(1, seen.Count, "거절 사건이 정확히 한 번 나야 한다");
                Assert.AreEqual(receipt.Reason, seen[0]);
            }
            finally
            {
                driver.Unsubscribe(probe);
            }
        }

        [UnityTest]
        public IEnumerator 트레이_칸의_답은_드롭_거절과_같은_함수에서_나온다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver);
            driver.Apply(Command.FinishPlacement());

            var tray = Object.FindAnyObjectByType<CoreDefenderTray>();
            Assert.IsNotNull(tray, "트레이가 씬에 없다");
            yield return null;                                  // 트레이가 칸을 세우는 프레임
            Assert.Greater(tray.SlotCount, 0, "로스터가 비었거나 트레이가 안 섰다");

            // 「소진 > 쿨타임 > 코스트」의 순서는 **코어가 소유한다**(`SlotBlock`). 트레이가
            // 자기 순서를 들면 그것이 두 번째 자다 — 그 계약을 여기서 한 줄로 못박는다.
            var placement = driver.Match.Placement;
            for (int i = 0; i < driver.Definition.Units.Length; i++)
            {
                if (!placement.InRoster(i)) continue;
                var block = placement.SlotBlock(i);
                if (block != RejectReason.None) continue;

                int2 anchor;
                if (!TryFindAnchor(driver, i, out anchor)) continue;
                var receipt = driver.Apply(Command.PlaceDefender(i, anchor));
                Assert.IsTrue(receipt.Accepted,
                    "칸이 「쓸 수 있다」고 답했는데 드롭이 거절됐다: " + receipt.Reason);
                yield break;
            }
            Assert.Fail("쓸 수 있는 칸이 하나도 없다 — 이 판에서는 계약을 증언할 수 없다");
        }

        // ── 공용 ─────────────────────────────────────────────────────────────

        private static bool TryFindPlaceable(BattleDriver driver, out int defIndex, out int2 anchor)
        {
            var placement = driver.Match.Placement;
            for (int i = 0; i < driver.Definition.Units.Length; i++)
            {
                if (!placement.InRoster(i)) continue;
                if (TryFindAnchor(driver, i, out anchor)) { defIndex = i; return true; }
            }
            defIndex = -1;
            anchor = default;
            return false;
        }

        // row-major 첫 합격. 「어디가 되나」를 코어에 묻는다 — 지형 규칙을 여기서 다시
        // 세우면 이 테스트가 규칙의 두 번째 사본이 된다.
        private static bool TryFindAnchor(BattleDriver driver, int defIndex, out int2 anchor)
        {
            var placement = driver.Match.Placement;
            var size = driver.GridSize;
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                var c = new int2(x, y);
                if (placement.Judge(defIndex, c) != RejectReason.None) continue;
                anchor = c;
                return true;
            }
            anchor = default;
            return false;
        }
    }
}
