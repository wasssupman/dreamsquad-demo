using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.PlayMode
{
    // endless-mode unit 4 — 무한 모드 통합 스모크. DevMapOverride.Endless 로 부팅해:
    //   (1) Deck_Endless 로드, (2) 10초 고정간격 30웨이브, (3) 공용 mapPool 미오염,
    //   (4) 디펜더 0 → 적 유출 누적해도 패배하지 않음(무한=defeatEnabled 꺼짐).
    // 점수 산식(시간0·누수 saturation)의 결정론 검증은 EditMode EndlessScoreTests 가 맡는다.
    public class EndlessModeSmokeTest
    {
        private const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 필수 — 다음 테스트/에디터 세션에 무한 모드가 새면 안 된다.
            DevMapOverride.Clear();
            if (TimeManager.Instance != null) TimeManager.Instance.ResetAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Endless_Boots_FixedInterval_NoPoolPollution_NoDeathOnLeak()
        {
            DevMapOverride.Clear();
            DevMapOverride.Endless = true;               // 맵 빌드 이전에 세팅해야 진입 분기가 잡는다

            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");

            bridge.BeginPlacement();                      // 드래프트 맵 빌드(무한 인카운터 resolve)
            yield return null;

            // (1) 무한 덱 로드
            Assert.IsNotNull(bridge.ActiveDeck, "ActiveDeck resolved");
            Assert.AreEqual(BattleMode.Endless, bridge.ActiveDeck.battleMode, "endless 모드 덱");
            Assert.AreEqual("Deck_Endless", bridge.ActiveDeck.deckId, "Deck_Endless 로드");

            // (3) 공용 풀 미오염 — 무한은 mapPool 밖 전용 인카운터로 진입
            var pool = GetField(bridge, "mapPool");
            int poolCount = (int)pool.GetType().GetProperty("Count").GetValue(pool);
            Assert.AreEqual(6, poolCount, "mapPool.Count 불변(랜덤/토너먼트 선택 회귀 0)");

            // (4) 배틀 시작 — _wavePlan 은 StartBattle 에서 빌드된다.
            bridge.StartBattle();

            // (2) three-minute-survival unit 2 — 무한 모드도 같은 케이던스를 상속한다:
            // 웨이브 상한 100(명목) + 상한 간격 20초(구 10초 고정간격은 스폰 창 13.5초와
            // 충돌해 은퇴). 명목 그리드는 브리핑·로그 표기 전용이고 런타임은 전멸/상한으로 굴린다.
            var plan = GetField(bridge, "_wavePlan");
            var waves = (IList)GetPublic(plan, "waves");
            Assert.AreEqual(100, waves.Count, "웨이브 상한 100(명목)");
            float interval = (float)GetPublic(plan, "waveIntervalSec");
            Assert.AreEqual(20f, interval, 0.001f, "상한 간격 20초가 명목 그리드를 정한다");

            // (5) 디펜더 0 + 전 웨이브 당김 → 적이 골에 도달. stress-after-breach(2026-08-08)
            // 이후로는 **도달 = 유출이 아니다**: 골 타워가 살아 있으면 적은 공성으로 안정도를
            // 깎고, 유출(스트레스)은 타워가 부서진 뒤에만 생긴다. 그래서 이 스모크의 전제는
            // "누수 > 0" 이 아니라 "공성이 실제로 일어났다(안정도 감소)" 로 바뀐다.
            for (int i = 0; i < 40 && bridge.NextWaveHasNext; i++) bridge.ForceNextWave();

            int maxLeaksWhileAlive = 0;
            bool sawStabilityDrain = false;
            int prevStability = bridge.GoalStabilityCurrent;
            bool defeatSeen = false;
            bool ended = false;
            int endStability = -1;
            float start = Time.unscaledTime;
            for (int f = 0; f < 30000; f++)
            {
                int leaks = (int)GetField(bridge, "_goalReachedCount");
                bool running = (bool)GetField(bridge, "_running");
                bool shown = (bool)GetField(bridge, "_resultShown");
                if (running) maxLeaksWhileAlive = Mathf.Max(maxLeaksWhileAlive, leaks);
                int stability = bridge.GoalStabilityCurrent;
                if (stability < prevStability) sawStabilityDrain = true;
                prevStability = stability;

                if (shown)
                {
                    ended = true;
                    ReadResult(out string outcome);
                    endStability = bridge.GoalStabilityCurrent;
                    if (outcome == "defeat") defeatSeen = true;
                    break;
                }
                if (leaks >= 20) break;                    // 충분히 관측(빠른 종료)
                if (Time.unscaledTime - start > 80f) break; // 안전장치
                yield return null;
            }

            Assert.IsTrue(sawStabilityDrain,
                "디펜더 0 인데 안정도가 한 번도 줄지 않았다 — 적이 골에 도달하지 못하고 있다");
            // three-minute-survival unit 0 — 무한 모드도 **골 안정도 0 으로 패배한다**
            // (endless-mode 계약 4 "누수로 죽지 않음" 은 이 spec 이 갱신했다). 유출이 쌓이면
            // 패배가 정상이므로 defeat 부재를 요구하지 않는다.
            if (defeatSeen)
                Assert.AreEqual(0, endStability, "패배로 끝났다면 안정도가 0 이어야 한다");

            // 무한 모드 누수 HUD 는 죽는 한계가 없으니 "/한계" 를 숨기고 개수만 표시한다.
            // (TMP_Text 타입 참조를 피해 .text 프로퍼티를 reflection 으로 읽는다 — asmdef 무변경.)
            var hud = Object.FindObjectOfType<ScoreHudView>();
            var leakValueObj = hud != null ? GetField(hud, "_leakValue") : null;
            string leakText = leakValueObj?.GetType().GetProperty("text")?.GetValue(leakValueObj) as string;
            if (leakText != null)
                Assert.IsFalse(leakText.Contains("/"),
                    $"무한 모드 누수 HUD 는 '/한계' 를 숨겨야 한다 (실제: '{leakText}')");
        }

        // BattleLogger.currentEntry.result 에서 outcome 을 읽는다(TallyFlowTest 와 동형).
        // three-minute-survival unit 3 — time_score/stress_score 는 은퇴했다(점수 축 = 처치 하나).
        private static void ReadResult(out string outcome)
        {
            outcome = "unknown";
            var logger = GameManager.Instance != null ? GameManager.Instance.Logger : null;
            if (logger == null) return;
            var entry = logger.GetType().GetField("currentEntry", F)?.GetValue(logger);
            var result = entry?.GetType().GetField("result")?.GetValue(entry);
            if (result == null) return;
            var rt = result.GetType();
            outcome = (string)rt.GetField("outcome").GetValue(result);
        }

        private static object GetField(object o, string name)
        {
            var f = o.GetType().GetField(name, F);
            Assert.IsNotNull(f, $"field '{name}' on {o.GetType().Name}");
            return f.GetValue(o);
        }

        private static object GetPublic(object o, string name)
        {
            var f = o.GetType().GetField(name);
            Assert.IsNotNull(f, $"public field '{name}' on {o.GetType().Name}");
            return f.GetValue(o);
        }
    }
}
