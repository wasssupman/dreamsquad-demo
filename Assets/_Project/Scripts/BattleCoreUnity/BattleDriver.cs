using System;
using UnityEngine;
using Wassup.BattleCore;

namespace Wassup.BattleCoreUnity
{
    // battle-core-rebuild unit 1 — **시간만** 갖는 Unity 층(UML §6).
    //
    // 코어는 프레임을 모른다. 이 컴포넌트가 프레임 시간을 누산해 **정수 틱**으로 잘라
    // `BattleMatch.Tick()` 을 부른다. 그래서 슬로모·정지가 규칙을 건드리지 않는다 —
    // 느린 것은 판이 아니라 **틱 발행률**이고, `dt` 는 언제나 1/60 이다(계약 5).
    //
    // `Time.timeScale` 을 쓰지 않는다. 전역 시간 배율은 UI 애니메이션·연출까지 같이
    // 끌고 가고, 이 프로젝트는 그래서 도메인 시간 제어(`TimeManager`)를 따로 두었다.
    // 여기서는 «이 판의 틱만» 느려져야 한다.
    //
    // 이 컴포넌트는 **규칙을 하나도 소유하지 않는다.** 판정·상태·저장이 여기 들어오면
    // 그것이 새 브리지의 첫 줄이다(절대 제약 1).
    [DisallowMultipleComponent]
    public sealed class BattleDriver : MonoBehaviour
    {
        [Header("틱 발행")]
        [SerializeField, Min(0f), Tooltip("틱 발행률 배율. 1 = 실시간, 0 = 정지, 0.5 = 슬로모")]
        private float _timeScale = 1f;

        [SerializeField, Tooltip("한 프레임에 밀어 넣을 틱의 상한. 프레임이 튀었을 때 나선형 지연을 막는다")]
        [Min(1)] private int _maxTicksPerFrame = 8;

        private BattleMatch _match;
        private float _accumulator;
        private bool _paused;

        /// <summary>
        /// 틱이 돈 뒤 한 번, 그 틱에 배달된 사건 전부를 순서대로 방출한다.
        /// 뷰 풀·HUD 는 각자 여기 붙는다 — 통합 뷰는 두지 않는다(UML §6).
        /// </summary>
        public event Action<CoreEvent> CoreEventRaised;

        public BattleMatch Match => _match;
        public bool Paused => _paused;
        public float TimeScale => _timeScale;

        /// <summary>판을 건다. 이미 걸려 있으면 교체한다(이전 판의 구독은 호출자가 정리한다).</summary>
        public void Begin(MatchDefinition definition)
        {
            _match = new BattleMatch(definition);
            _accumulator = 0f;
            _match.Begin();
            DrainEvents();
        }

        public void Pause(bool paused) => _paused = paused;

        public void SetTimeScale(float scale) => _timeScale = Mathf.Max(0f, scale);

        /// <summary>
        /// 커맨드를 건다. 코어가 그 자리에서 판정하고, 그 커맨드가 만든 사건도
        /// 그 자리에서 배달된다 — 정지 중에도 배치가 화면에 보이는 이유다.
        /// </summary>
        public Receipt Apply(in Command command)
        {
            if (_match == null) return Receipt.Reject(RejectReason.NotRunningOrPlacementClosed);
            var receipt = _match.Apply(command);
            DrainEvents();
            return receipt;
        }

        private void Update()
        {
            if (_match == null) return;
            if (_paused || _timeScale <= 0f) { DrainEvents(); return; }

            _accumulator += Time.unscaledDeltaTime * _timeScale;

            int ticks = 0;
            while (_accumulator >= BattleMatch.Dt && ticks < _maxTicksPerFrame)
            {
                _accumulator -= BattleMatch.Dt;
                _match.Tick();
                ticks++;
            }

            // 상한에 걸렸으면 남은 누산을 **버린다**. 안 버리면 다음 프레임에 더 많은
            // 틱을 밀어야 하고, 그 빚이 계속 불어 프레임이 영영 못 따라잡는다.
            if (ticks >= _maxTicksPerFrame) _accumulator = 0f;

            DrainEvents();
        }

        private void DrainEvents()
        {
            var events = _match.Events;
            if (events.Count == 0) return;
            if (CoreEventRaised != null)
                for (int i = 0; i < events.Count; i++) CoreEventRaised(events[i]);
            _match.ClearEvents();
        }
    }
}
