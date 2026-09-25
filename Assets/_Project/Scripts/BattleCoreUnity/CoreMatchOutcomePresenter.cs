using System.Collections;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Goals;
using Wassup.Core;
using Wassup.Core.Api;
using Wassup.Core.TimeControl;
using Wassup.Data.BattleView;
using Wassup.UI;

namespace Wassup.BattleCoreUnity
{
    // battle-core-rebuild unit 5c — **판이 끝난 뒤의 네 걸음.**
    //
    //   성적 받기(`MatchOutcome`) → 통보(서버) → 박자(붕괴만) → 표시(결과 화면)
    //
    // 옛 전투에서 이 네 걸음은 브리지의 `EndMatch` 안에 있었고, 그래서 브리지가 결과 화면
    // 참조(`resultScreen`)와 제출 게이트를 둘 다 들고 있었다. 여기로 옮기면서 **판정은 하나도
    // 안 따라왔다** — 「무엇을 제출하나」는 `MatchOutcome` 이, 「언제 끝났나」는 `MatchClock` 이,
    // 「박자가 있나」는 `MatchClock.EndHasPresentationBeat`(X15)이 이미 답한다.
    //
    // ⚠ **성적은 사건이 온 그 자리에서 받는다.** 판이 끝나면 틱이 0 이라(계약 5) 나중에 물어도
    // 같은 값이 나오지만, 「나중에 물어도 된다」는 습관이 붙으면 다음 사람이 소멸하는 것에도
    // 같은 모양을 쓴다 — 계약 7 이 막는 것이 정확히 그 습관이다.
    //
    // ⚠ **제출이 표시보다 앞이라는 순서는 계약이다**(score-tally-sequence 계약 3). 화면을
    // 기다리다 앱이 죽으면 기록이 통째로 사라진다. 둘은 독립이고, 그래서 붕괴 박자(1.25초)는
    // **표시만** 늦춘다.
    [DisallowMultipleComponent]
    public sealed class CoreMatchOutcomePresenter : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        [Tooltip("결과 화면. 옛 브리지의 `resultScreen`(장부 25행)의 새 주인이 이 슬롯이다.")]
        [SerializeField] private ResultScreen _resultScreen;

        [Tooltip("붕괴 박자(붙드는 시간·시간 배율). 비우면 박자 없이 즉시 표시한다.")]
        [SerializeField] private HeartHudConfig _heartHud;

        [Tooltip("「한 판 해봤다」(G15)를 적을 프로필. 이번 세션에 읽은 프로필일 때만 저장한다.")]
        [SerializeField] private PlayerProfileSO _profile;

        /// <summary>
        /// 프로필 저장 seam(옛 `GameManager.ProfileSaver`). 테스트가 개발자의 실제 `profile.json` 을 재작성하지 않게 갈아 끼운다.
        /// </summary>
        public System.Action<PlayerProfile> ProfileSaver { get; set; } = ProfileStore.Save;

        /// <summary>이 판을 「플레이한 판」으로 적었나(판당 1회 래치 — G15).</summary>
        public bool MatchRecorded => _recorded;

        /// <summary>나가기로 떠났나(0점 마감 1회).</summary>
        public bool Abandoned { get; private set; }

        /// <summary>결과 화면을 띄운 횟수. **판당 한 번**이고 테스트가 그 한 번을 증언한다.</summary>
        public int ShownCount { get; private set; }

        public bool ResultShown => ShownCount > 0;

        /// <summary>서버에 통보를 걸었나. 제출 게이트가 닫힌 모드에서는 끝까지 거짓이다.</summary>
        public bool Submitted { get; private set; }

        private bool _closed;                 // 종료 처리가 이미 돌았다(박자 중 포함)
        private Coroutine _holdRoutine;
        private TimeLease _holdLease;
        private bool _holdLeased;
        private bool _recorded;               // G15 래치 — 결과·나가기 두 통로가 함께 불러도 +1

        private void OnEnable()
        {
            _closed = false;
            ShownCount = 0;
            Submitted = false;
            if (_driver != null) _driver.Subscribe(ViewOrder.Outcome, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            ReleaseHold(stopRoutine: true);
        }

        private void OnCoreEvent(CoreEvent e)
        {
            if (e.Kind != CoreEventKind.MatchEnded) return;
            if (_closed) return;   // 판은 두 번 끝나지 않는다(`MatchClock.EndMatch`) — 여기도 한 번만
            _closed = true;

            // 조립 지점은 코어에 하나뿐이다(`BattleMatch.Outcome` → `IMatchGoal.BuildOutcome`).
            // 값 타입이라 여기 담는 순간 복사본이고, 이 뒤로 코어를 다시 묻지 않는다.
            MatchOutcome outcome = _driver.Match.Outcome;

            Submit(in outcome);
            // G15 — 결과로 끝난 판도 「한 판」이다(옛 `SetPhase(Result)` → `RecordMatchPlayed`).
            RecordMatchPlayed();

            // 터지는 판에만 박자를 준다. 「종료 사유 표기」가 아니라 **사건이 있을 때만 그
            // 사건의 연출**이다 — 만료·제출은 터지는 것이 없어 즉시 결과 화면이다.
            float hold = _heartHud != null ? _heartHud.CoreBurstHoldSec : 0f;
            if (_driver.Match.Clock.EndHasPresentationBeat && hold > 0f && isActiveAndEnabled)
            {
                _holdRoutine = StartCoroutine(HoldThenShow(outcome, hold));
                return;
            }
            Show(in outcome);
        }

        // ── 통보 ─────────────────────────────────────────────────────────────
        //
        // **게이트 둘을 다 넘어야 나간다**: 그 모드가 토너먼트에 올라가는 모드인가
        // (`SubmitsReport` — v1 은 `KillScoreTimed` 만, 계약 13) + 그 모드가 제출 어휘를
        // 여는가(`AllowSubmit`). 둘은 다른 축이라 하나로 접지 않는다 — 앞은 서버에 올릴
        // 판인지이고 뒤는 플레이어가 판을 끊을 수 있는지다.
        //
        // ⚠ **`ReportResult` 시그니처는 건드리지 않는다**(v1 서버 무변, 계약 13). 모드·정렬
        // 방향을 payload 에 싣는 것은 서버 확장이고 그건 이 저장소 밖이다 — `MatchOutcome` 이
        // 그 값을 이미 들고 있는 것은 그날을 위한 자리이지 지금의 계약이 아니다.
        private void Submit(in MatchOutcome outcome)
        {
            if (!outcome.SubmitsReport) return;
            if (!_driver.Definition.Mode.AllowSubmit) return;

            Submitted = true;
            var screen = _resultScreen;
            // unit 8b — 덱 스냅샷은 드라이버가 반입·덱 확정 두 시점에 지은 문자열이다(`TournamentDeckInfo.Serialize` 직접 —
            // 로거를 새 씬에 들이지 않는다, 결정 ⑷). 나가기(`AbandonMatch`)도 같은 문자열을 싣는다.
            TournamentMatchReporter.ReportResult(outcome.Score, _driver.DeckInfoJson,
                ranking =>
                {
                    if (screen != null)
                        screen.UpdateLeaderboard(ranking, UserSession.Current?.userId);
                },
                onError: _ => NoticePopup.ShowAlert("점수 전송 실패",
                    "이번 판 점수가 서버에 전송되지 않았습니다.\n네트워크 상태를 확인해 주세요."));
        }

        // ── 나가기 · 기록 ─────────────────────────────────────────────────────
        //
        // 옛 `MenuPopup.OnExit`(`:136~161`) 의 후계. 판이 끝나기 전에 떠나는 통로는 **참가 포기 0점 마감**이다 —
        // 앱이 살아 있는 동안 지금 보낸다(덱 포함). 그 판도 히스토리에 자기 엔트리로 남으므로 「한 판」으로 센다.
        // ⚠ **씬 전환 앞이어야 한다** — 전환 뒤엔 이 컴포넌트가 이미 파괴돼 기록이 유실된다.
        /// <summary>나가기: 0점 마감(덱 포함) → 「한 판」 기록 → 로비. 제출 전의 판에서만 부른다(제출 뒤엔 「성적 확정」).</summary>
        public void AbandonAndLeave(bool loadLobby = true)
        {
            if (!Abandoned)
            {
                Abandoned = true;
                TournamentMatchReporter.AbandonMatch(_driver != null ? _driver.DeckInfoJson : null);
            }
            RecordMatchPlayed();
            if (loadLobby) SceneTransition.Go(SceneNames.Outgame);
        }

        /// <summary>
        /// G15 — 이 판을 「플레이한 판」으로 적는다. **판당 한 번**(래치)이고, 이번 세션에 읽은 프로필일 때만 저장한다 —
        /// 에디터 직접 진입은 프로필이 없어, 그때 저장하면 빈 메모리 상태가 디스크의 편성·덱을 덮는다(옛 `RecordMatchPlayed`).
        /// </summary>
        public void RecordMatchPlayed()
        {
            if (_recorded) return;
            if (_profile == null || !_profile.IsLoadedThisSession || _profile.profile == null)
            {
                Debug.Log("[CoreMatchOutcomePresenter] matchesPlayed 기록 생략 — 이번 세션에 로드된 프로필이 아니다.", this);
                return;
            }
            _recorded = true;
            _profile.profile.matchesPlayed++;
            try { (ProfileSaver ?? ProfileStore.Save)(_profile.profile); }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CoreMatchOutcomePresenter] matchesPlayed 저장 실패 — 무시하고 진행한다: {ex.Message}", this);
            }
        }

        // ── 박자 ─────────────────────────────────────────────────────────────
        //
        // ⚠ **대기는 unscaled** 다. 스케일된 시간으로 기다리면 아래 슬로우가 대기 자체를 늘려
        // 박자가 배로 길어진다. `Time.timeScale` 은 쓰지 않는다 — 시간 제어는 도메인 리스뿐이고
        // (`TimeManager`), 전역 배율은 결과 화면의 트윈까지 같이 끌고 간다.
        private IEnumerator HoldThenShow(MatchOutcome outcome, float hold)
        {
            float scale = _heartHud != null ? _heartHud.CoreBurstTimeScale : 1f;
            if (TimeManager.Instance != null && scale < 1f)
            {
                _holdLease = TimeManager.Instance.Request(TimeDomain.Battle, scale, priority: 100);
                _holdLeased = true;
            }
            yield return new WaitForSecondsRealtime(hold);
            _holdRoutine = null;
            ReleaseHold(stopRoutine: false);
            Show(in outcome);
        }

        private void ReleaseHold(bool stopRoutine)
        {
            if (stopRoutine && _holdRoutine != null)
            {
                StopCoroutine(_holdRoutine);
                _holdRoutine = null;
            }
            if (!_holdLeased) return;
            _holdLeased = false;
            // 리스는 자기 해제를 안다(`TimeLease.Dispose`). 매니저를 다시 찾아 id 로 풀지
            // 않는 이유가 그것이다 — 두 해제 경로가 생기면 한쪽만 멱등해진다.
            _holdLease.Dispose();
        }

        // ── 표시 ─────────────────────────────────────────────────────────────
        private void Show(in MatchOutcome outcome)
        {
            ShownCount++;
            if (_resultScreen == null)
            {
                Debug.LogWarning("[CoreMatchOutcomePresenter] 결과 화면이 배선되지 않았다 — 판은 끝났는데 화면이 없다.", this);
                return;
            }
            // unit 8a — 결과 화면이 후계(`MatchOutcome`)를 **직접** 받는다. 5c 의 `MatchTally` 어댑터는 걷었다.
            _resultScreen.Show(in outcome);
        }
    }
}
