using System.Collections.Generic;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — **조립 지점.** 규칙도 상태도 여기 없다(계약 12).
    //
    // 이 클래스가 하는 일은 셋뿐이다:
    //   ① 담당자를 만든다   ② 틱 단계의 순서를 나열한다   ③ 위임한다(Apply/Tick/Events)
    //
    // 판정·저장·판단이 이 파일에 들어오는 순간 그것은 매니저이고, 새 전투 코어의 절대
    // 제약 1 위반이다. 「여기 두면 편한데」가 곧 그 신호다 — 그 일의 담당자를 찾는다.
    public sealed class BattleMatch
    {
        /// <summary>고정 틱. 코어에는 가변 dt 가 없다 — 슬로모·정지는 틱 발행률이다(계약 5).</summary>
        public const float Dt = 1f / 60f;

        private readonly MatchDefinition _def;
        private readonly EventBus _bus;
        private readonly BattleWorld _world;
        private readonly RngStreams _rng;
        private readonly TickContext _ctx;

        // ── 담당자 ──
        private readonly MatchClock _clock;
        private readonly CommandPhase _commands;

        private readonly TickPipeline _pipeline;

        public BattleMatch(MatchDefinition definition, int worldCapacity = 256)
        {
            _def = definition;
            _bus = new EventBus();
            _world = new BattleWorld(_bus, worldCapacity);
            _rng = new RngStreams(definition.Seed);

            _clock = new MatchClock();
            _commands = new CommandPhase(_world, _clock, _def);

            // 틱 순서. 빈 자리 6개(장 준비 · 사망 수렴 · AI/이동 · 효과/투사체 · 전투 ·
            // 담당자 단계)는 unit 2~4 가 **이 배열에 끼운다**. 순서를 바꾸는 것은 규칙을
            // 바꾸는 것이므로 그때 같은 커밋에서 근거를 남긴다.
            _pipeline = new TickPipeline(new ITickPhase[]
            {
                _commands,      // phase 0 — Immediate seam
                // unit 2: FieldPrepPhase
                // unit 3: DeathConvergePhase
                // unit 2: AiMovePhase
                // unit 3: TickProjectilePhase
                // unit 3: CombatPhase
                // unit 4: OwnerSteps (WaveScheduler · CostLedger · PlacementService ·
                //         HeartMeter · GimmickHost · IMatchGoal)
                _clock,         // 시계·종료 통로
                new FlushPhase(),
            });

            _ctx = new TickContext
            {
                World = _world,
                Def = _def,
                Bus = _bus,
                Rng = _rng,
                Dt = Dt,
                Tick = 0,
            };
        }

        public MatchDefinition Definition => _def;
        public BattleWorld World => _world;
        public EventBus Bus => _bus;
        public RngStreams Rng => _rng;
        public MatchClock Clock => _clock;
        public TickPipeline Pipeline => _pipeline;

        /// <summary>배달까지 끝난 이벤트. Unity 층이 틱 뒤에 드레인하고 `ClearEvents()` 한다.</summary>
        public IReadOnlyList<CoreEvent> Events => _bus.Outbox;

        public void ClearEvents() => _bus.ClearOutbox();

        public void Begin()
        {
            _clock.Begin(_def.Mode, _bus, Dt);
            _ctx.Tick = 0;
            _bus.Publish(CoreEvent.MatchStartedAt(0));
            // 시작 사건은 **첫 틱을 기다리지 않는다** — 뷰가 판을 세우는 신호라 틱 0 의
            // 스폰보다 먼저 배달돼야 한다.
            _bus.Flush();
        }

        /// <summary>
        /// 커맨드. 동기 적용 + receipt(계약 7).
        ///
        /// 적용 **직후 배달까지 한다.** 그것이 「Immediate seam」의 뜻이다 —
        /// 커맨드가 만든 사건을 다음 틱으로 미루면, 판을 끝내는 커맨드(제출)의
        /// 종료 사건이 **영영 배달되지 않는다**(종료 뒤 틱은 no-op 이라 플러시가 안 돈다).
        /// 실제로 그 구멍이 한 번 났다.
        /// </summary>
        public Receipt Apply(in Command cmd)
        {
            var receipt = _commands.Execute(cmd, _clock.Tick);
            _bus.Flush();
            return receipt;
        }

        /// <summary>한 틱. 판이 끝났으면 아무것도 하지 않는다(계약 5).</summary>
        public void Tick()
        {
            if (_clock.Ended) return;
            _ctx.Tick = _clock.Tick;
            _pipeline.Run(_ctx);
        }
    }
}
