using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Wassup.Core;
using Wassup.Bridge;

namespace Wassup.Tests.PlayMode
{
    // three-minute-survival unit 0 — 골 안정도가 패배를 소유한다.
    //
    // 이 판정은 EditMode 로 옮길 수 없다: 안정도 차감은 유출 이벤트 드레인(브리지) 위에
    // 있고, 유출은 적이 실제로 골까지 걸어가야 발생한다. 그래서 라이브 판을 돌린다.
    //
    // 하네스는 TallyFlowTest 와 같다 — 디펜더를 한 기도 놓지 않고 웨이브를 전부 당겨
    // 유출을 만든다. 승리 유도는 밸런스 의존이라 불안정하다.
    public class GoalStabilityTest
    {
        private const float TimeoutSec = 120f;

        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator Stability_DrainsOnLeak_AndZeroEndsMatch()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            Assert.IsNotNull(gm, "GameManager present");

            bridge.BeginPlacement();
            yield return null;

            int max = bridge.GoalStabilityMax;
            Assert.Greater(max, 0, "덱이 goalStabilityMax 를 저작해야 한다 — 0 이면 패배가 영원히 안 온다");
            Assert.AreEqual(max, bridge.GoalStabilityCurrent, "판 시작 시 안정도는 만피다");

            bridge.StartBattle();
            for (int i = 0; i < 20 && bridge.NextWaveHasNext; i++) bridge.ForceNextWave();

            // 안정도가 줄어드는 유일한 원인은 유출이어야 한다. RemainingLeakAllowance 는
            // 유출마다 1 줄어드는 공개 카운터라 유출 프록시로 쓴다(스트레스 누적은 private).
            int prevStability = bridge.GoalStabilityCurrent;
            int prevAllowance = bridge.RemainingLeakAllowance();
            bool sawDrain = false;
            float start = Time.unscaledTime;

            while (bridge.GoalStabilityCurrent > 0 && gm.CurrentPhase == GamePhase.Battle)
            {
                Assert.Less(Time.unscaledTime - start, TimeoutSec,
                    $"{TimeoutSec}초 안에 안정도가 0 에 닿지 않았다 (현재 {bridge.GoalStabilityCurrent}/{max}). " +
                    "유출이 안정도를 안 깎거나 적이 골에 도달하지 못하고 있다.");

                int stability = bridge.GoalStabilityCurrent;
                int allowance = bridge.RemainingLeakAllowance();
                if (stability < prevStability)
                {
                    sawDrain = true;
                    int drop = prevStability - stability;
                    Assert.Less(allowance, prevAllowance,
                        "안정도가 줄었는데 유출 카운터가 안 늘었다 — 유출 외의 경로가 안정도를 깎고 있다.");
                    Assert.LessOrEqual(drop, 5,
                        "한 프레임 낙폭이 보스 피해(5)를 넘었다 — 티어값이 아니라 다른 수가 들어가고 있다.");
                }
                prevStability = stability;
                prevAllowance = allowance;
                yield return null;
            }

            Assert.IsTrue(sawDrain, "유출이 안정도를 깎는 것을 한 번도 관측하지 못했다");
            Assert.AreEqual(0, bridge.GoalStabilityCurrent, "안정도는 0 에서 바닥친다(음수 금지)");

            // 안정도 0 = 패배. 스트레스 한계 패배는 제거됐으므로 종료 경로는 이것뿐이다.
            float tallyStart = Time.unscaledTime;
            while (gm.CurrentPhase == GamePhase.Battle)
            {
                Assert.Less(Time.unscaledTime - tallyStart, 10f,
                    "안정도 0 인데 Battle 페이즈를 벗어나지 않았다 — 패배 전이가 끊겼다.");
                yield return null;
            }
        }
    }
}
