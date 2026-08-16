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
    // score-tally-sequence unit 4 — 전투 종료 → 결과 화면의 **흐름 계약**.
    //
    // three-minute-survival unit 3 — 합산 연출(ScoreTallyView)은 제거됐다: 시간·스트레스 축이
    // 사라져 더할 것이 없고, 전투 중 HUD 숫자가 이미 최종 점수다. 그래서 이 테스트에서 연출
    // 관련 단언(HUD 가시성·Skip 경로)은 빠지고 **페이즈 흐름과 총점 보존**만 남는다.
    // Battle → Tally → Result 전이 자체는 유지된다(전투 HUD 게이팅이 그 페이즈를 읽는다).
    //
    // 지키는 것: 결과 화면이 영영 안 뜨는 하드락. 컴파일도 되고 EditMode 도 통과하므로
    // 이 테스트가 유일한 방어선이다.
    public class TallyFlowTest
    {
        private const float ResultTimeoutSec = 90f;

        private BattleBridge _bridge;
        private GameManager _gm;
        private ScoreHudView _hud;
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
            yield return RunToEnd();

            AssertPhaseOrder();
            AssertTotalPreserved();
        }

        // ── 흐름 ──────────────────────────────────────────────────────────────

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
            Assert.IsNotNull(_bridge, "BattleBridge present");
            Assert.IsNotNull(_gm, "GameManager present");
            Assert.IsNotNull(_hud, "ScoreHudView present");

            _phases.Clear();
            _gm.PhaseChanged += OnPhase;

            // three-minute-kill-race unit 0 — 예전엔 «디펜더를 안 놓으면 유출이 쌓여 패배로
            // 끝난다» 로 판을 마감시켰다. 패배가 사라져 그 길이 없다. 이제 판을 끝내는 것은
            // **시계 하나**이므로 아래 RunToEnd 가 제한시간을 줄여 만료로 몬다.
            _bridge.BeginPlacement();
            yield return null;
        }

        private IEnumerator RunToEnd()
        {
            _bridge.StartBattle();
            // three-minute-survival unit 2 — 플레이어 경로는 사라졌지만 ForceNextWave 는
            // 테스트 진행 동력으로 남아 있다(킬이 실제로 나야 총점 보존을 검산할 수 있다).
            for (int i = 0; i < 20 && _bridge.NextWaveHasNext; i++) _bridge.ForceNextWave();

            // three-minute-kill-race unit 0 — **제한시간을 줄여 만료로 몬다.** StartBattle 이
            // 덱에서 `_timerDuration`(180초)을 읽은 **뒤**에 덮어써야 한다. 판을 끝내는 경로가
            // 시계 하나뿐이므로, 이걸 안 하면 3분을 통째로 기다려야 하고 그건 이 테스트의
            // 타임아웃(90초)을 넘는다. 줄이는 것은 **대기 시간**일 뿐 검증 대상인 흐름
            // (Battle → Tally → Result + 총점 보존)은 그대로다.
            SetPrivate(_bridge, "_timerDuration", 3f);

            float start = Time.unscaledTime;
            while (_gm.CurrentPhase != GamePhase.Result)
            {
                if (Time.unscaledTime - start > ResultTimeoutSec)
                {
                    Assert.Fail(
                        $"{ResultTimeoutSec}초 안에 Result 에 도달하지 못했다 (현재 {_gm.CurrentPhase}). " +
                        "결과 화면이 영영 안 뜨는 하드락이다. " +
                        $"페이즈 이력: {string.Join(" → ", _phases)}");
                }
                yield return null;
            }
            yield return null;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"BattleBridge.{field} 가 없다 — 하네스가 낡았다");
            f.SetValue(target, value);
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

        // 계약 2 — 종료 후 HUD 숫자가 총점과 정확히 같아야 한다.
        // 산식을 여기서 다시 계산하지 않는다. 두 번 구현하면 회귀 검출력이 없다 —
        // Bridge 가 기록한 값을 읽어 대조한다. 점수 축이 처치 하나라 총점 == kill_score 다.
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
            int kill = (int)rt.GetField("kill_score").GetValue(result);

            Assert.AreEqual(total, kill,
                $"총점({total})과 처치 점수({kill})가 다르다 — 점수 축은 처치 하나뿐이다");

            int hud = (int)typeof(ScoreHudView)
                .GetField("_targetScore", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_hud);
            Assert.AreEqual(total, hud,
                $"종료 후 HUD 숫자({hud})가 총점({total})과 다르다 — 처치 누적이 어긋났다");
        }
    }
}
