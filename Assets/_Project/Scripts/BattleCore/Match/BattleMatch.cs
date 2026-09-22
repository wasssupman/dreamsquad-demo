using System.Collections.Generic;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Move;

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
        private readonly MapRuntime _map;
        private readonly SeamHooks _seams;

        private readonly TickPipeline _pipeline;

        public BattleMatch(MatchDefinition definition, int worldCapacity = 256)
        {
            _def = definition;
            _bus = new EventBus();
            _world = new BattleWorld(_bus, worldCapacity);
            _rng = new RngStreams(definition.Seed);

            // unit 2 — 맵 런타임. 통행 마스크 목록은 **정의표에서** 온다(그 판에 나오는 유닛들이
            // 여는 층). 슬롯 수가 여기서 정해지므로 정의표 밖에서 층을 만들 수 없다.
            _map = new MapRuntime(definition.Map, CollectTraversalMasks(definition));
            var chasePool = new ChaseFieldPool(definition.Map.CellCount);

            _clock = new MatchClock();
            _commands = new CommandPhase(_world, _clock, _def, _map);
            _seams = new SeamHooks();

            // 틱 순서. 남은 빈 자리(사망 수렴 · 효과/투사체 · 전투 · 담당자 단계)는 unit 3~4 가
            // **이 배열에 끼운다**. 순서를 바꾸는 것은 규칙을 바꾸는 것이므로 그때 같은 커밋에서
            // 근거를 남긴다.
            _pipeline = new TickPipeline(new ITickPhase[]
            {
                _commands,                              // phase 0 — Immediate seam
                new FieldPrepPhase(_map, chasePool),    // unit 2 — 장애물·어그로·사냥판·순찰
                new AiMovePhase(_map, chasePool),       // unit 2 — 상태·도발·거점·감지·이동·분리
                new TickProjectilePhase(_map),          // unit 3 — 발사 요청·궤적·착탄
                new CombatPhase(_map),                  // unit 3 — 공격·피해·사망·도약
                // unit 4: OwnerSteps (WaveScheduler · CostLedger · PlacementService ·
                //         HeartMeter · GimmickHost · IMatchGoal)
                // ⚠ UML §4 의 `DeathConvergePhase` 는 **따로 만들지 않았다.** 그것이 들고 있던
                // 두 일이 각자 주인을 찾았기 때문이다: 사망 표시 수렴은 `CombatPhase` 의 피해
                // 단계(표시)와 소멸 단계(한 틱 뒤 제거)로 나뉘었고, 배치 활성화는 `PlacementService`
                // (unit 4)의 것이다. 빈 단계를 남기면 다음 사람이 「여기 뭘 넣어야 하나」를 묻는다.
                _clock,         // 시계·종료 통로
                new FlushPhase(),
            });

            _ctx = new TickContext
            {
                World = _world,
                Def = _def,
                Bus = _bus,
                Rng = _rng,
                Map = _map,
                Dt = Dt,
                Tick = 0,
                Seams = _seams,
            };
        }

        // 그 판에 나올 수 있는 유닛들의 통행 층. 0(미저작)은 기본 마스크로 접힌다.
        private static byte[] CollectTraversalMasks(MatchDefinition def)
        {
            var masks = new List<byte>(4);
            for (int i = 0; i < def.Enemies.Length; i++) Add(masks, (byte)def.Enemies[i].TraversalLayers);
            for (int i = 0; i < def.Units.Length; i++) Add(masks, (byte)def.Units[i].TraversalLayers);
            return masks.ToArray();

            void Add(List<byte> into, byte m)
            {
                if (m == 0) return;
                if (!into.Contains(m)) into.Add(m);
            }
        }

        public MapRuntime Map => _map;

        /// <summary>트리거 레이어(unit 7)가 여기 등록한다. 등록은 **틱 밖**에서만.</summary>
        public SeamHooks Seams => _seams;

        /// <summary>
        /// 진단 통로. **조용한 무동작 금지**(C4)의 수신처이고, 연결하지 않으면 버려진다 —
        /// 코어는 로거를 소유하지 않는다.
        /// </summary>
        public System.Action<string> Report
        {
            get => _ctx.Report;
            set => _ctx.Report = value;
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
