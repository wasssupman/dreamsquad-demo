using System.Collections;
using UnityEngine;
using Somnia.Battle.BattleCore;
using Somnia.Battle.Core.TimeControl;
using Somnia.Battle.Data.BattleView;

namespace Somnia.Battle.BattleCoreUnity
{
    // battle-core-rebuild unit 5c → demo-diet unit 0 — **판이 끝난 뒤의 박자.**
    //
    // 옛 `CoreMatchOutcomePresenter` 는 네 걸음(성적 받기 → 통보 → 박자 → 표시)을 다 했다. 그중 성적 받기는
    // `BattleDriver.MatchFinished` 사건이 됐고, 통보(서버)·표시(결과 화면)·기록(프로필)은 아웃게임의 사정이라 전투
    // 밖으로 나갔다(demo-diet Q3 — 결과 화면 통째 제거). 여기 남은 것은 **붕괴 박자** 하나다: 마음이 터지는 판에서
    // `HeartHudConfig.CoreBurstHoldSec` 동안 Battle 도메인을 `CoreBurstTimeScale` 로 늦춰 폭발 연출이 보이게 한다.
    //
    // ⚠ 박자는 **연출**이지 규칙이 아니다 — 판은 이미 끝났고(틱 0, 계약 5) 성적 사건도 이미 나갔다. 결과 화면이
    // 바깥(App)으로 가면 그쪽이 「박자가 끝났나」를 알고 싶을 수 있어 `HoldActive` 를 읽기 모델로 둔다.
    //
    // ⚠ **대기는 unscaled** 다. 스케일된 시간으로 기다리면 아래 슬로우가 대기 자체를 늘려 박자가 배로 길어진다.
    // `Time.timeScale` 은 쓰지 않는다 — 시간 제어는 도메인 리스뿐이다(`TimeManager`).
    [DisallowMultipleComponent]
    public sealed class CoreMatchEndBeat : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        [Tooltip("붕괴 박자(붙드는 시간·시간 배율). 비우면 박자 없음.")]
        [SerializeField] private HeartHudConfig _heartHud;

        /// <summary>박자가 돌고 있나(리스 보유 중).</summary>
        public bool HoldActive => _holdLeased;

        /// <summary>이 판에서 박자를 시작한 횟수(판당 0 또는 1).</summary>
        public int BeatCount { get; private set; }

        private bool _closed;                 // 종료 처리가 이미 돌았다(박자 중 포함)
        private Coroutine _holdRoutine;
        private TimeLease _holdLease;
        private bool _holdLeased;

        private void OnEnable()
        {
            _closed = false;
            BeatCount = 0;
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

            // 터지는 판에만 박자를 준다. 「종료 사유 표기」가 아니라 **사건이 있을 때만 그 사건의 연출**이다 —
            // 만료·제출은 터지는 것이 없어 박자가 없다.
            float hold = _heartHud != null ? _heartHud.CoreBurstHoldSec : 0f;
            if (_driver.Match.Clock.EndHasPresentationBeat && hold > 0f && isActiveAndEnabled)
            {
                BeatCount++;
                _holdRoutine = StartCoroutine(Hold(hold));
            }
        }

        private IEnumerator Hold(float hold)
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
    }
}
