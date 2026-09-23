using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild 5b 수정 — **정의표 유닛 줄이 저작을 다 싣는가.**
    //
    // 사용자 플레이 1차의 문장: **「트레이 코스트가 전부 0 이다」**. 화면이 읽는 값은
    // `MatchDefinition.Units[i].Cost` 하나이므로 증상 단언은 「그 줄이 SO 와 같다」다.
    //
    // ⚠ 코스트 한 칸만 묻지 않는다. 결함의 실체는 **배치 저작 블록(unit 4 가 정의표에 더한
    // 일곱 칸)이 통째로 빌더를 안 지난 것**이고, 나머지 여섯은 0 이어도 화면에 안 보인다 —
    // 상한 0 은 1 로 접히고(고유 유닛처럼 보인다), 쿨타임 0 은 「항상 준비됨」이고,
    // 배치 모션 0 은 **배치 페이즈 자체를 없앤다**. 한 칸만 고치면 나머지는 다음 사람이
    // 「원래 그런 줄」로 배운다.
    public sealed class CoreUnitDefMappingTests
    {
        [UnityTest]
        public IEnumerator 정의표_유닛_줄은_저작_SO_의_배치_값을_그대로_싣는다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");

            var assets = driver.DefenderAssets;
            var units = driver.Definition.Units;
            Assert.Greater(assets.Count, 0, "이 씬에 저작 방어유닛이 없다 — 증언할 수 없다");
            Assert.GreaterOrEqual(units.Length, assets.Count,
                "정의표 줄 수가 저작 수보다 적다 — 줄 번호가 이미 갈렸다");

            bool anyAuthoredCost = false;
            for (int i = 0; i < assets.Count; i++)
            {
                var a = assets[i];
                if (a == null) continue;
                var d = units[i];              // 이터레이터라 ref 지역을 못 둔다 — 값 복사로 읽는다
                string who = $"[{i}] {a.id}";

                Assert.AreEqual(a.cost, d.Cost, who + " 의 코스트가 정의표에 안 실렸다");
                Assert.AreEqual(a.EffectiveMaxOnBoard, d.EffectiveMaxOnBoard,
                    who + " 의 판 위 상한이 안 실렸다");
                Assert.AreEqual(a.placementCooldown, d.PlacementCooldown, 1e-4f,
                    who + " 의 연사 게이트가 안 실렸다");
                Assert.AreEqual(a.EffectiveDeathCooldown, d.EffectiveDeathCooldown, 1e-4f,
                    who + " 의 사망 재배치 대기가 안 실렸다");
                Assert.AreEqual(a.EffectiveRetireCooldown, d.EffectiveRetireCooldown, 1e-4f,
                    who + " 의 퇴근 재배치 대기가 안 실렸다");
                Assert.AreEqual(a.DeployMotionSeconds, d.DeployMotionSeconds, 1e-4f,
                    who + " 의 배치 모션 길이가 안 실렸다 — 0 이면 배치 페이즈가 통째로 없다");
                Assert.AreEqual(a.awakeningReward, d.AwakeningReward,
                    who + " 의 각성 보상이 안 실렸다");

                if (a.cost > 0) anyAuthoredCost = true;
            }

            Assert.IsTrue(anyAuthoredCost,
                "저작 코스트가 전부 0 이다 — 이 판으로는 「코스트가 산다」를 증언할 수 없다");
        }

        [UnityTest]
        public IEnumerator 배치는_그_유닛의_코스트만큼_깎는다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver);
            driver.Apply(Command.FinishPlacement());

            var placement = driver.Match.Placement;
            var cost = driver.Match.Cost;

            for (int i = 0; i < driver.Definition.Units.Length; i++)
            {
                if (!placement.InRoster(i)) continue;
                int price = driver.Definition.Units[i].Cost;
                if (price <= 0) continue;                       // 공짜 유닛은 이 계약을 증언하지 않는다
                if (!TryFindAnchor(driver, i, out var anchor)) continue;

                float before = cost.Current;
                var receipt = driver.Apply(Command.PlaceDefender(i, anchor));
                Assert.IsTrue(receipt.Accepted, "판정이 통과한 자리인데 거절됐다: " + receipt.Reason);
                Assert.AreEqual(before - price, cost.Current, 1e-3f,
                    $"배치가 코스트를 안 깎았다 — 값 {price} 짜리를 놓았는데 잔액이 그대로다");
                yield break;
            }

            Assert.Fail("값이 붙은 유닛을 놓을 자리가 없다 — 이 판으로는 증언할 수 없다");
        }

        // row-major 첫 합격. 「어디가 되나」는 코어에 묻는다(규칙의 두 번째 사본을 만들지 않는다).
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
