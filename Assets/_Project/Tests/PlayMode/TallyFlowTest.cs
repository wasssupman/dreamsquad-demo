using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.UI;

namespace Wassup.Tests.PlayMode
{
    // score-tally-sequence unit 4 — 전투 종료 → Tally 연출 → 결과 화면의 **흐름 계약**.
    //
    // 연출의 모양(타이밍·색·폰트)은 사람이 볼 문제라 건드리지 않는다. 여기서 지키는 건
    // 자동으로만 잡히는 것들이다 — 특히 **onDone 유실 = 결과 화면이 영영 안 뜨는 하드락**.
    // 그건 컴파일도 되고 EditMode 도 통과하므로 이 테스트가 유일한 방어선이다.
    public class TallyFlowTest
    {
        private const float ResultTimeoutSec = 90f;

        private BattleBridge _bridge;
        private GameManager _gm;
        private ScoreHudView _hud;
        private ScoreTallyView _tally;
        private readonly List<GamePhase> _phases = new();

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_gm != null) _gm.PhaseChanged -= OnPhase;
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        // 정상 흐름: Battle → Tally → Result. Tally 동안 점수 HUD 가 살아 있고,
        // 최종 HUD 숫자가 총점과 일치한다.
        [UnityTest]
        public IEnumerator BattleEnd_GoesThroughTally_AndReachesResult()
        {
            yield return Setup();
            yield return RunToEnd(skipAfterTallySec: -1f);

            AssertPhaseOrder();
            Assert.IsTrue(_sawHudDuringTally,
                "Tally 동안 점수 HUD 패널이 꺼져 있었다 — 연출의 주인공이 안 보인다");
            AssertTotalPreserved();
        }

        // 스킵 경로에서도 총점이 보존되는지. 남은 축의 AddScore 가 누락되기 쉬운 지점이다.
        [UnityTest]
        public IEnumerator SkippingTally_StillReachesResult_AndPreservesTotal()
        {
            yield return Setup();
            yield return RunToEnd(skipAfterTallySec: 0.2f);

            AssertPhaseOrder();
            AssertTotalPreserved();
        }

        // ── 흐름 ──────────────────────────────────────────────────────────────

        private bool _sawHudDuringTally;

        private void OnPhase(GamePhase p)
        {
            if (_phases.Count == 0 || _phases[^1] != p) _phases.Add(p);
        }

        private IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            _bridge = Object.FindObjectOfType<BattleBridge>();
            _gm = Object.FindObjectOfType<GameManager>();
            _hud = Object.FindObjectOfType<ScoreHudView>();
            var tallies = Object.FindObjectsByType<ScoreTallyView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            _tally = tallies.Length > 0 ? tallies[0] : null;

            Assert.IsNotNull(_bridge, "BattleBridge present");
            Assert.IsNotNull(_gm, "GameManager present");
            Assert.IsNotNull(_hud, "ScoreHudView present");
            Assert.IsNotNull(_tally, "ScoreTallyView present — 미배선이면 연출이 통째로 건너뛰어진다");

            _phases.Clear();
            _sawHudDuringTally = false;
            _gm.PhaseChanged += OnPhase;

            // 디펜더를 한 기도 놓지 않는다 — 전 웨이브를 당기면 유출이 빠르게 쌓여 패배로
            // 끝난다. 승리 유도는 밸런스 의존이라 불안정하다. 검증 대상인 흐름 계약은
            // 승패와 무관하게 동일하게 지난다.
            _bridge.BeginPlacement();
            yield return null;
        }

        private IEnumerator RunToEnd(float skipAfterTallySec)
        {
            _bridge.StartBattle();
            for (int i = 0; i < 20 && _bridge.NextWaveHasNext; i++) _bridge.ForceNextWave();

            var panelField = typeof(ScoreHudView)
                .GetField("_panel", BindingFlags.NonPublic | BindingFlags.Instance);

            float start = Time.unscaledTime;
            float tallyEnteredAt = -1f;
            bool skipSent = false;

            while (_gm.CurrentPhase != GamePhase.Result)
            {
                if (Time.unscaledTime - start > ResultTimeoutSec)
                {
                    Assert.Fail(
                        $"{ResultTimeoutSec}초 안에 Result 에 도달하지 못했다 (현재 {_gm.CurrentPhase}). " +
                        "onDone 유실 의심 — 결과 화면이 영영 안 뜨는 하드락이다. " +
                        $"페이즈 이력: {string.Join(" → ", _phases)}");
                }

                if (_gm.CurrentPhase == GamePhase.Tally)
                {
                    if (tallyEnteredAt < 0f) tallyEnteredAt = Time.unscaledTime;
                    var panel = panelField.GetValue(_hud) as GameObject;
                    if (panel != null && panel.activeSelf) _sawHudDuringTally = true;

                    if (skipAfterTallySec >= 0f && !skipSent &&
                        Time.unscaledTime - tallyEnteredAt >= skipAfterTallySec)
                    {
                        _tally.Skip();
                        skipSent = true;
                    }
                }
                yield return null;
            }
            yield return null;
        }

        // ── 단언 ──────────────────────────────────────────────────────────────

        private void AssertPhaseOrder()
        {
            int battle = _phases.IndexOf(GamePhase.Battle);
            int tally = _phases.IndexOf(GamePhase.Tally);
            int result = _phases.IndexOf(GamePhase.Result);

            Assert.Greater(tally, -1,
                $"Tally 를 거치지 않았다 — 연출이 통째로 건너뛰어졌다. 이력: {string.Join(" → ", _phases)}");
            Assert.Greater(result, -1, $"Result 에 도달하지 못했다. 이력: {string.Join(" → ", _phases)}");
            Assert.Less(battle, tally, $"Battle → Tally 순서가 아니다. 이력: {string.Join(" → ", _phases)}");
            Assert.Less(tally, result, $"Tally → Result 순서가 아니다. 이력: {string.Join(" → ", _phases)}");
        }

        // 계약 2 — 연출이 끝난 뒤 HUD 숫자가 총점과 정확히 같아야 한다.
        // ScoreMath 를 여기서 다시 부르지 않는다. 산식을 두 번 구현하면 회귀 검출력이 없다 —
        // Bridge 가 기록한 3축을 읽어 대조한다.
        private void AssertTotalPreserved()
        {
            var logger = _gm.Logger;
            Assert.IsNotNull(logger, "BattleLogger present");
            var entryField = logger.GetType()
                .GetField("currentEntry", BindingFlags.NonPublic | BindingFlags.Instance);
            var entry = entryField.GetValue(logger);
            Assert.IsNotNull(entry, "배틀로그 entry — 결과가 기록되지 않았다");

            var result = entry.GetType().GetField("result").GetValue(entry);
            var rt = result.GetType();
            int total = (int)rt.GetField("score").GetValue(result);
            int time = (int)rt.GetField("time_score").GetValue(result);
            int stress = (int)rt.GetField("stress_score").GetValue(result);
            int kill = (int)rt.GetField("kill_score").GetValue(result);

            Assert.AreEqual(total, time + stress + kill,
                $"3축 합이 총점과 다르다 (time {time} + stress {stress} + kill {kill} != {total})");

            int hud = (int)typeof(ScoreHudView)
                .GetField("_targetScore", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_hud);
            Assert.AreEqual(total, hud,
                $"연출 후 HUD 숫자({hud})가 총점({total})과 다르다 — 축이 누락되거나 중복 가산됐다");
        }
    }
}
