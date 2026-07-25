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

            // (2) 10초 고정간격 30웨이브 (StartBattle 이후 조회)
            var plan = GetField(bridge, "_wavePlan");
            var waves = (IList)GetPublic(plan, "waves");
            Assert.AreEqual(30, waves.Count, "30 웨이브");
            float interval = (float)GetPublic(plan, "waveIntervalSec");
            Assert.AreEqual(10f, interval, 0.001f, "10초 고정간격");

            // (5) 디펜더 0 + 전 웨이브 당김 → 적 유출. 누수가 쌓여도 패배 안 함.
            for (int i = 0; i < 40 && bridge.NextWaveHasNext; i++) bridge.ForceNextWave();

            int maxLeaksWhileAlive = 0;
            bool defeatSeen = false;
            bool ended = false;
            int endTimeScore = -1;
            float start = Time.unscaledTime;
            for (int f = 0; f < 30000; f++)
            {
                int leaks = (int)GetField(bridge, "_goalReachedCount");
                bool running = (bool)GetField(bridge, "_running");
                bool shown = (bool)GetField(bridge, "_resultShown");
                if (running) maxLeaksWhileAlive = Mathf.Max(maxLeaksWhileAlive, leaks);

                if (shown)
                {
                    ended = true;
                    ReadResult(out string outcome, out endTimeScore);
                    if (outcome == "defeat") defeatSeen = true;
                    break;
                }
                if (leaks >= 20) break;                    // 충분히 관측(빠른 종료)
                if (Time.unscaledTime - start > 80f) break; // 안전장치
                yield return null;
            }

            Assert.Greater(maxLeaksWhileAlive, 0, "적이 골에 유출돼 누수가 쌓였어야 한다(디펜더 0)");
            Assert.IsFalse(defeatSeen, "무한 모드는 누수로 패배하지 않는다(defeatEnabled=!IsEndless)");
            if (ended)
                Assert.AreEqual(0, endTimeScore, "무한 모드 종료 결과의 시간점수는 0");
        }

        // BattleLogger.currentEntry.result 에서 outcome/time_score 를 읽는다(TallyFlowTest 와 동형).
        private static void ReadResult(out string outcome, out int timeScore)
        {
            outcome = "unknown";
            timeScore = -1;
            var logger = GameManager.Instance != null ? GameManager.Instance.Logger : null;
            if (logger == null) return;
            var entry = logger.GetType().GetField("currentEntry", F)?.GetValue(logger);
            var result = entry?.GetType().GetField("result")?.GetValue(entry);
            if (result == null) return;
            var rt = result.GetType();
            outcome = (string)rt.GetField("outcome").GetValue(result);
            timeScore = (int)rt.GetField("time_score").GetValue(result);
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
