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
    //
    // goal-tower-siege — 골 타워는 Faction.Defender 진영의 건물 엔티티이고, 적은 자기
    // 공격으로 그것을 때린다. 즉 이 테스트는 **공성 지속 피해**를 통과 경로로 돈다.
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

            // goal-tower-siege — 안정도가 줄어드는 원인은 이제 **둘**이다: 골에 남은 적의
            // 지속 공격(주 경로)과 공격 수단 없는 돌격형의 자폭(Runner·Swift). 그래서 예전의
            // "안정도가 줄면 유출 카운터도 반드시 늘었다" 단언은 성립하지 않는다 —
            // 새 적이 도착하지 않은 프레임에도 공성 DPS 로 줄어든다.
            //
            // 낙폭 상한도 티어값(보스 5)이 아니라 **적 공격력** 축이다(예: Rootcaster 14).
            // 그래서 상한을 걸지 않고, 대신 "유출이 한 번이라도 있어야 줄기 시작한다" 는
            // 인과만 고정한다 — 아무도 골에 못 갔는데 안정도가 줄면 그건 진짜 결함이다.
            int prevStability = bridge.GoalStabilityCurrent;
            bool sawDrain = false;
            int initialAllowance = bridge.RemainingLeakAllowance();
            float start = Time.unscaledTime;

            while (bridge.GoalStabilityCurrent > 0 && gm.CurrentPhase == GamePhase.Battle)
            {
                Assert.Less(Time.unscaledTime - start, TimeoutSec,
                    $"{TimeoutSec}초 안에 안정도가 0 에 닿지 않았다 (현재 {bridge.GoalStabilityCurrent}/{max}). " +
                    "유출이 안정도를 안 깎거나 적이 골에 도달하지 못하고 있다.");

                int stability = bridge.GoalStabilityCurrent;
                if (stability < prevStability)
                {
                    sawDrain = true;
                    Assert.Less(bridge.RemainingLeakAllowance(), initialAllowance,
                        "골에 도달한 적이 한 기도 없는데 안정도가 줄었다 — 유출 외의 경로가 깎고 있다.");
                }
                prevStability = stability;
                yield return null;
            }

            Assert.IsTrue(sawDrain, "골이 뚫렸는데 안정도가 한 번도 줄지 않았다");
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
