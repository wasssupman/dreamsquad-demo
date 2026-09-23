using Unity.Mathematics;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 4 — **배치 자원.**
    //
    // 옛 `CostRuntime`(MonoBehaviour) 10행이 통째로 여기로 온다. 가장 큰 변화는 소유권이다:
    //
    // ⚠ **재생 스위치를 담당자가 갖는다**(X24). 오늘 그 스위치는 UI(`PlacementPhaseView`)에
    // 있고, 스크립트 진입은 그 뷰를 안 지나 자원이 0 에 멎어 배치가 전부 거부된다 —
    // 하네스가 UI 역할을 대행 중인 **유일하게 알려진 harness≠live 갭**이다. 여기서는
    // 배치 창이 닫히는 사건을 구독해 스스로 켜므로 그 갭이 구조적으로 닫힌다.
    //
    // ⚠ **판의 시계를 따른다**(C8·C9). 옛 구현은 `Update` 에서 렌더 프레임을 따라 자랐고,
    // 그래서 하네스인지 아닌지를 스스로 물어야 했다(중복 10). 여기서는 틱 파이프라인의
    // 한 단계라 그 게이트 자체가 사라진다 — 메뉴로 멈추면 안 차고 감속하면 비례해 느려진다.
    //
    // ⚠ **재생 배율은 초기화가 절대 건드리지 않는다**(C7). 그 값은 모드가 아니라 «그 판에
    // 들고 들어온 드림스톤» 이고, 판 안에서 다시 세팅되는 일이 없어야 플레이어의 버프가
    // 조용히 지워지지 않는다. 그래서 `Begin` 의 인자이고 setter 가 없다.
    public sealed class CostLedger : ITickPhase
    {
        public string Name => "CostLedger";

        private readonly EventBus _bus;
        private readonly MatchClock _clock;

        private float _current;
        private float _max = 15f;
        private float _start = 10f;
        private float _regenPerSec = 1f;
        private float _regenRateMultiplier = 1f;
        private bool _regenActive;

        public CostLedger(EventBus bus, MatchClock clock)
        {
            _bus = bus;
            _clock = clock;
            // 배치 창이 **닫히는** 순간이 재생의 시작이다(X24). 여는 순간이 아니다 —
            // 배치 중에 자원이 차면 「고민할수록 이득」이 되어 창의 길이가 밸런스가 된다.
            _bus.Subscribe(CoreEventKind.PlacementPhaseChanged, EventOrder.CostRegen, OnPhase);
        }

        public float Current => _current;
        public float Max => _max;

        /// <summary>화면에 보이는 수는 **내림**이다 — 판정은 실수로 한다(「9.9인데 10을 못 놓는다」).</summary>
        public int CurrentInt => (int)math.floor(_current);

        public bool RegenActive => _regenActive;
        public float RegenRateMultiplier => _regenRateMultiplier;

        /// <summary>
        /// 판 경계. 시작값은 0~최대치로 자르고, 최대치는 최소 1, 재생 속도는 음수 불가(C3).
        /// `regenStartsNow` = 이 판에 배치 창이 없다 → 전투로 시작하므로 재생도 지금 시작한다.
        /// </summary>
        public void Begin(in CostDef config, float regenRateMultiplier, bool regenStartsNow)
        {
            _max = math.max(1f, config.Max);
            _start = config.Start;
            _regenPerSec = math.max(0f, config.RegenPerSec);
            _regenRateMultiplier = math.max(0f, regenRateMultiplier);
            _current = math.clamp(_start, 0f, _max);
            _regenActive = regenStartsNow;
        }

        private void OnPhase(CoreEvent e)
        {
            if (e.Arg == 0) _regenActive = true;   // 닫혔다 = 전투 시작
        }

        public void Run(TickContext ctx)
        {
            if (!_regenActive || _current >= _max) return;
            _current += _regenPerSec * _regenRateMultiplier * ctx.Dt;
            if (_current > _max) _current = _max;
            // ⚠ 재생은 **사건을 내지 않는다.** 연속값이라 뷰가 읽는 것이 맞고, 매 틱 쏘면
            // 판 하나에 만 건이 쌓여 트레이스가 무의미해진다(`CostChanged` 주석).
        }

        /// <summary>모자라면 **지불이 거부되고 행동 자체가 일어나지 않는다**. 0 이하는 항상 성공(C4).</summary>
        public bool CanAfford(int amount) => amount <= 0 || _current >= amount;

        /// <summary>
        /// 지불. 배치 판정의 **마지막** 단계다(「구조 &gt; 자원」) — 성공 판정 뒤에 깎아야
        /// 실패한 배치가 차감 없이 거절된다(P6).
        /// </summary>
        public bool TryPay(int amount, int tick)
        {
            if (amount <= 0) return true;
            if (_current < amount) return false;
            _current -= amount;
            _bus.Publish(CoreEvent.CostChanged(tick, -amount, _current));
            return true;
        }

        /// <summary>되돌려 주는 코스트는 최대치를 넘지 않는다(C6). 메타 의도 `GainCost` 도 여기로.</summary>
        public int Gain(int amount, int tick)
        {
            if (amount <= 0) return 0;
            float before = _current;
            _current = math.min(_max, _current + amount);
            int gained = (int)math.floor(_current - before);
            if (_current != before) _bus.Publish(CoreEvent.CostChanged(tick, gained, _current));
            return gained;
        }

        /// <summary>메타 의도용 편의 — 「지금 몇 틱인가」를 호출부가 되묻지 않게 한다.</summary>
        public int Gain(int amount) => Gain(amount, _clock.Tick);
    }
}
