using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.View;
using Wassup.Core.TimeControl;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 5a — 새 씬 부팅 스모크.
    //
    // 재는 것 넷:
    //   ① 콘솔 에러 0
    //   ② 판이 **끝까지** 돈다(3분 판이 종료 사유를 달고 끝난다)
    //   ③ **매 초** 뷰 수 = 코어 유닛 수 — 「소멸 사건을 안 낸 경로」가 있으면 여기서 갈린다
    //   ④ 스테이지 저작 거점 수 = 월드의 거점 유닛 수
    //
    // 3분을 실시간으로 기다리지 않는다. 발행률을 올려 **틱을 몰아 준다** — 코어의 `dt` 는
    // 1/60 그대로이므로 판이 겪는 시간은 똑같다(계약 5). 이것이 「슬로모는 규칙이 아니라
    // 발행률」의 뒷면이다.
    public sealed class CoreSceneBootTests
    {
        private const float FastForward = 8f;

        [UnityTest]
        public IEnumerator Boot_NoErrors_ViewsTrackUnits_AndMatchRunsToEnd()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);

            Assert.IsNotNull(driver, "씬에 BattleDriver 가 있어야 한다");
            Assert.IsTrue(driver.Running, "부팅 직후 판이 걸려 있어야 한다 — 스테이지·모드 배선 확인");

            var pool = Object.FindAnyObjectByType<CoreUnitViewPool>();
            Assert.IsNotNull(pool, "씬에 CoreUnitViewPool 이 있어야 한다");

            // ④ 저작 거점 = 월드 거점. 방어 마음은 골(`Goals`)이 정본이라 이 축에서 빠진다 —
            //    세우는 자가 다르므로(마음은 `HeartMeter`) 같은 수로 세면 항상 어긋난다.
            int authoredStructures = 0;
            var stage = driver.StageStructures;
            for (int i = 0; i < stage.Count; i++)
            {
                var s = stage[i];
                if (s.data == null) continue;
                if (Wassup.Data.StructurePlacements.DeriveFaction(s.side, s.data.kind)
                    == Wassup.Battle.Units.Faction.DefenderCore) continue;
                authoredStructures++;
            }
            int worldStructures = CountStructures(driver, excludeDefenderCore: true);
            Assert.AreEqual(authoredStructures, worldStructures,
                "스테이지 저작 거점 수 = 월드의 거점 유닛 수(방어 마음 제외)");

            using (TimeManager.Instance.Request(TimeDomain.Battle, FastForward))
            {
                float nextCheck = Time.unscaledTime + 1f;
                // 3분 판 + 여유. 발행률 8 이면 프레임당 8틱이라 대략 1,400 프레임이면 닿는다.
                const int FrameBudget = 6000;
                int frames = 0;
                while (!driver.Match.Clock.Ended && frames < FrameBudget)
                {
                    yield return null;
                    frames++;
                    if (Time.unscaledTime < nextCheck) continue;
                    nextCheck = Time.unscaledTime + 1f;
                    AssertViewCount(driver, pool);
                }

                Assert.IsTrue(driver.Match.Clock.Ended,
                    $"3분 판이 {frames} 프레임 안에 끝나야 한다 (틱 {driver.Match.Clock.Tick})");
                Assert.AreNotEqual(MatchEndReason.None, driver.Match.Clock.EndReason,
                    "종료에는 사유가 있어야 한다");
            }

            // 종료 후에는 틱이 0 이다(계약 5).
            int tickAtEnd = driver.Match.Clock.Tick;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(tickAtEnd, driver.Match.Clock.Tick, "판이 끝난 뒤에는 틱이 돌지 않는다");

            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors,
                "부팅~완주 동안 콘솔 에러 0:\n" + string.Join("\n", CoreSceneFixture.Errors));
        }

        private static void AssertViewCount(BattleDriver driver, CoreUnitViewPool pool)
        {
            int live = 0;
            var units = driver.Units;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Kind != UnitKind.Structure) live++;

            Assert.AreEqual(live, pool.ViewCount,
                $"틱 {driver.Match.Clock.Tick}: 뷰 수 = 코어 유닛 수(거점 제외). "
                + "어긋나면 소멸 사건을 안 낸 경로가 있다(계약 7)");
        }

        private static int CountStructures(BattleDriver driver, bool excludeDefenderCore)
        {
            int n = 0;
            var units = driver.Units;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].Kind != UnitKind.Structure) continue;
                if (excludeDefenderCore
                    && units[i].Faction == Wassup.Battle.Units.Faction.DefenderCore) continue;
                n++;
            }
            return n;
        }
    }
}
