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
    // goal-tower-siege — 골 타워는 Faction.DefenderCore 진영의 건물 엔티티이고, 적은 자기
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
            // rev 2 에서 "유출이 한 번이라도 있어야 줄기 시작한다" 인과 단언도 폐기했다:
            // 원거리 적은 사거리에서 멈춰 타워를 쏘므로 **골에 도달한 적이 0 이어도** 안정도가
            // 준다(스펙이 '수용된 대가'로 명시). 그 단언을 남기면 원거리 편성에서 거짓 실패한다.
            int prevStability = bridge.GoalStabilityCurrent;
            bool sawDrain = false;
            float start = Time.unscaledTime;

            // 하네스 주의(2026-08-08 검증에서 적발): 이 테스트는 GameManager 를 거치지 않고
            // 브리지를 직접 몬다(BeginPlacement/StartBattle 직접 호출). 그래서 `CurrentPhase` 는
            // **Battle 로 바뀌지 않는다** — Battle 로 게이팅하면 관측 루프 본문이 한 번도 돌지 않아
            // "안정도가 한 번도 줄지 않았다" 는 거짓 실패가 난다. 자매 하네스(TallyFlowTest)가
            // Result 도달만 보는 이유가 이것이다. 종료 조건은 안정도 0 또는 Result 뿐이다.
            // 관측은 **본문에서** 하고 종료는 본문 끝에서 판단한다. 조건절에 `stability > 0` 를
            // 두면 안정도가 만피에서 0 으로 **한 번에** 떨어지는 판(타워 20 은 적 2~3대면 녹는다)에서
            // 루프가 그 값을 못 보고 빠져나가 "한 번도 줄지 않았다" 는 거짓 실패가 난다
            // (2026-08-08 검증에서 실제로 이 형태로 실패했다).
            while (gm.CurrentPhase != GamePhase.Result)
            {
                Assert.Less(Time.unscaledTime - start, TimeoutSec,
                    $"{TimeoutSec}초 안에 안정도가 0 에 닿지 않았다 (현재 {bridge.GoalStabilityCurrent}/{max}). " +
                    "공성 피해가 안정도를 안 깎거나 적이 골에 도달하지 못하고 있다.");

                int stability = bridge.GoalStabilityCurrent;
                if (stability < prevStability) sawDrain = true;
                prevStability = stability;
                if (stability <= 0) break;
                yield return null;
            }

            Assert.IsTrue(sawDrain, "골이 뚫렸는데 안정도가 한 번도 줄지 않았다");
            // battle-structures unit 4(ⓐ) — 붕괴 프레임의 미러는 0(방금 죽은 골)이고, 다음
            // 프레임부터 **살아남은 골 중 최저**를 보여준다(계약 7 — 골 단위 붕괴). 그래서
            // «항상 0» 단정은 멀티골 맵에서 계약과 어긋난다. 남는 불변식 = 음수 금지.
            Assert.GreaterOrEqual(bridge.GoalStabilityCurrent, 0, "안정도는 0 에서 바닥친다(음수 금지)");

            // heart-stress-axis unit 0 — **단언이 뒤집혔다.**
            //
            // three-minute-kill-race 시절 이 자리는 정반대를 고정했다: 「마음을 다 내줘도 판은
            // 계속된다 — 5초를 감시해 Result 로 가지 않는지 본다」. 그 spec 의 계약(마음은 판정
            // 권한이 0)을 heart-stress-axis 가 **의도적으로 뒤집었으므로** 테스트도 뒤집는다.
            // 이제 마음이 무너지는 프레임이 곧 판의 끝이다(스트레스 100 = 마음 HP 0).
            //
            // 이 하네스는 브리지를 직접 몰아 `CurrentPhase` 가 Battle 로 가지 않으므로(위 주의),
            // 이 테스트가 보는 유일한 페이즈 전이가 `EndMatch → Result` 다. 그래서
            // 「Result 에 닿는다」가 곧 「마감이 불렸다」의 정확한 관측이다.
            //
            // 사실 위 루프가 Result 로 이미 빠져나왔을 가능성이 높다(EndMatch 는 안정도가 0 이
            // 되는 **그 프레임에** 동기로 불린다). 여유 창을 두는 것은 관측 순서가 한 프레임
            // 어긋나는 경우를 흡수하기 위해서다.
            float watchStart = Time.unscaledTime;
            while (gm.CurrentPhase != GamePhase.Result)
            {
                Assert.Less(Time.unscaledTime - watchStart, 5f,
                    "마음이 무너졌는데 판이 안 끝났다 — 스트레스 100 종료 경로가 끊겼다"
                    + "(계약: 게임 종료 = 3분 만료 · 스트레스 100)");
                yield return null;
            }

            // rev 3 — 「유출 0」 단언은 뺐다. `_goalReachedCount` 가 이제 **돌격형 도달**을 세므로
            // 라이브 판에서 0 이 아닐 수 있다(그게 정상이다). 「붕괴 후 유출 전환이 실행되지
            // 않는다」는 정밀 단언은 EditMode 쪽이 갖는다 —
            // StructureSpawnAndBreachTests.FirstCoreDestroyed_EndsMatchImmediately_AndNeverOpensLeakDrain.
        }
    }
}
