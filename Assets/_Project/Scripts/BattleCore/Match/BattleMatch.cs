using System.Collections.Generic;
using Wassup.BattleCore.Goals;
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
    //
    // unit 4 — 담당자 8 이 전부 섰다. 그들 사이의 **순서 의존은 이 파일에 없다** —
    // 사건 구독 순서(`EventOrder`)와 아래 단계 목록이 그것을 대신한다. 한 함수가 담당자
    // 둘을 차례로 부르는 모양이 생기면 그것이 계약 12 가 깨지는 첫 장면이다.
    public sealed class BattleMatch
    {
        /// <summary>고정 틱. 코어에는 가변 dt 가 없다 — 슬로모·정지는 틱 발행률이다(계약 5).</summary>
        public const float Dt = 1f / 60f;

        private readonly MatchDefinition _def;
        private readonly EventBus _bus;
        private readonly BattleWorld _world;
        private readonly RngStreams _rng;
        private readonly TickContext _ctx;

        // ── 담당자 8 ──
        private readonly MatchClock _clock;
        private readonly CostLedger _cost;
        private readonly ScoreLedger _score;
        private readonly HeartMeter _heart;
        private readonly WaveScheduler _waves;
        private readonly PlacementService _placement;
        private readonly HandDeck _hand;
        private readonly GimmickHost _gimmick;

        private readonly CommandPhase _commands;
        private readonly AiMovePhase _aiMove;
        private readonly FieldPrepPhase _fieldPrep;
        private readonly MapRuntime _map;
        private readonly SeamHooks _seams;

        // ── unit 7a: 트리거 → 발동 ──
        // 등록부(누가 무엇을 들었나) · 디스패처(seam 마다 줄 세우고 드레인) · 문맥(질의) · 쓰기 표면(의도 적용).
        // 넷 다 **판정·상태의 담당자**다 — 규칙의 상태는 `Binding` 이 들고, 이 파일은 만들고 꽂기만 한다.
        private readonly Trigger.BindingRegistry _bindings;
        private readonly Trigger.IntentApplier _intents;
        private readonly Trigger.CoreSkillContext _skills;
        private readonly Trigger.TriggerDispatcher _triggers;

        private readonly IMatchGoal _goal;
        private readonly MatchGoalContext _goalCtx;

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
            // 장 준비 단계는 **판 경계에도** 할 일이 있다(저작 거점 세우기) — 그래서 배열
            // 리터럴 안에서 만들지 않고 붙들어 둔다.
            _fieldPrep = new FieldPrepPhase(_map, chasePool);

            _clock = new MatchClock();
            _seams = new SeamHooks();
            // unit 7a — 등록부는 **스폰보다 먼저** 꽂힌다(판 경계의 거점 스폰도 저작 규칙을 붙인다).
            _bindings = new Trigger.BindingRegistry(_bus, _def);
            _world.BindRegistry(_bindings);

            // ⚠ **만드는 순서가 구독 순서의 동률 tie-break 다**(`EventBus`: 같은 order 면 구독한
            // 차례). `HeartMeter` 를 `WaveScheduler` 보다 먼저 만드는 것은 처치 사건에서 둘이
            // 같은 order 를 쓰는데 보너스 제안이 **그 틱의 회복을 반영한 스트레스**를 봐야 하기
            // 때문이다(X2). 순서를 바꾸면 문턱 근처에서 판정이 한 틱 묵는다.
            _cost = new CostLedger(_bus, _clock);
            _score = new ScoreLedger(_bus);
            _heart = new HeartMeter(_bus, _world, _clock, _def);
            _waves = new WaveScheduler(_bus, _world, _clock, _def, _map, _heart);
            _placement = new PlacementService(_bus, _world, _clock, _def, _map, _cost);
            _hand = new HandDeck(_bus, _world, _def);
            _gimmick = new GimmickHost(_bus, _def);

            _goal = MatchGoals.Create(_def.Mode.Goal);
            _goalCtx = new MatchGoalContext(_clock, _score, _waves, _heart, in _def.Mode);

            _commands = new CommandPhase(_world, _clock, _def, _map, _placement, _cost, _waves, _hand, _gimmick);

            _intents = new Trigger.IntentApplier(_world, _map, _def, _bus, _cost, _hand);
            _skills = new Trigger.CoreSkillContext(_world, _map, _intents);
            _triggers = new Trigger.TriggerDispatcher(_world, _def, _bus, _bindings, _skills);

            // 이동 단계는 **붙들어 둔다**(unit 5b). 거점 선택의 후보 배열이 그 안에 있고,
            // 예고선이 같은 답을 받아야 하기 때문이다(M18) — 배열을 밖으로 복제하는 대신
            // 「고르는 자」에게 물으러 간다.
            _aiMove = new AiMovePhase(_map, chasePool);

            // 틱 순서. **순서를 바꾸는 것은 규칙을 바꾸는 것**이므로 그때 같은 커밋에서 근거를 남긴다.
            _pipeline = new TickPipeline(new ITickPhase[]
            {
                _commands,                              // phase 0 — Immediate seam
                _fieldPrep,                             // unit 2 — 장애물·어그로·사냥판·순찰
                _aiMove,                                // unit 2 — 상태·도발·거점·감지·이동·분리
                new TickProjectilePhase(_map, _gimmick), // unit 3 — 발사 요청·궤적·착탄 · 6b2 기믹 셈판
                new CombatPhase(_map),                  // unit 3 — 공격·피해·사망·도약
                // ── unit 4: 담당자 단계 ──
                // 배치 활성화가 **맨 앞**인 이유: 이번 틱에 활성화된 유닛이 다음 틱의 전투에
                // 들어가고, 그 한 틱의 차이가 배치 페이즈 길이의 정의다.
                // 마음이 **맨 앞**인 이유 둘: ⓐ 마음 타워의 인박스를 비우는 것이 이 담당자의
                // 일이고(체력이 여기 있다) 그 피해는 방금 끝난 전투 단계가 넣은 것이다.
                // ⓑ 붕괴가 판을 끝내므로, 뒤에 두면 이미 무너진 판에서 웨이브가 한 번 더 나온다.
                _heart,                                 // 마음 방패 관찰 · 타워 인박스 드레인
                _placement,                             // 재배치 대기 · 배치 활성화
                _cost,                                  // 코스트 재생
                _waves,                                 // 웨이브 예약 · 스폰
                _hand,                                  // 액티브 재사용 대기
                new GoalPhase(_goal, _goalCtx),         // 「끝났나」 — 담당자들이 다 돈 뒤
                // ⚠ UML §4 의 `DeathConvergePhase` 는 **따로 만들지 않았다.** 그것이 들고 있던
                // 두 일이 각자 주인을 찾았기 때문이다: 사망 표시 수렴은 `CombatPhase` 의 피해
                // 단계(표시)와 소멸 단계(한 틱 뒤 제거)로 나뉘었고, 배치 활성화는 `PlacementService`
                // 의 것이다. 빈 단계를 남기면 다음 사람이 「여기 뭘 넣어야 하나」를 묻는다.
                _clock,         // 시계·종료 통로
                new FlushPhase(),
            });

            // unit 7a — seam 순서표는 **파이프라인에서** 만든다(단계가 자기 seam 을 순서대로 말한다).
            _triggers.Install(_seams, SeamTickOrder.From(_pipeline));

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
                Triggers = _triggers,
            };
            // 커맨드는 틱 밖에서 들어오는데 스폰 조립이 문맥을 요구한다. 판당 한 벌이라
            // 한 번 묶으면 끝이다(매 틱 다시 묶으면 「언제 묶였나」가 규칙이 된다).
            _commands.Bind(_ctx);
            _placement.Bind(_ctx);
            _intents.Bind(_ctx);
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

        /// <summary>
        /// 이동 단계. 뷰가 읽는 것은 **거점 선택 하나**다(`TryPickStructure` — M18).
        /// 상태를 고치는 통로가 아니다 — 예고선이 「적이 어디로 갈까」를 이동과 같은 자로
        /// 묻기 위한 창구이고, 그 외의 용도로 늘리면 그것이 새 브리지의 첫 줄이다.
        /// </summary>
        public AiMovePhase AiMove => _aiMove;

        /// <summary>트리거 레이어(unit 7)가 여기 등록한다. 등록은 **틱 밖**에서만.</summary>
        public SeamHooks Seams => _seams;

        /// <summary>unit 7a — 규칙 등록부(읽기 · 7b 의 카드 부착이 여기 붙인다).</summary>
        public Trigger.BindingRegistry Bindings => _bindings;

        /// <summary>unit 7a — 트리거 디스패처(seam 순서표 · 줄 선 발동 수 — 진단).</summary>
        public Trigger.TriggerDispatcher Triggers => _triggers;

        /// <summary>unit 7a — 스킬 쓰기 표면(테스트·7b 액티브가 의도를 직접 넣는 창구 — 판정을 갖지 않는다).</summary>
        public Trigger.IntentApplier Intents => _intents;

        /// <summary>
        /// 진단 통로. **조용한 무동작 금지**(C4)의 수신처이고, 연결하지 않으면 버려진다 —
        /// 코어는 로거를 소유하지 않는다.
        /// </summary>
        public System.Action<string> Report
        {
            get => _ctx.Report;
            set
            {
                _ctx.Report = value;
                // 담당자도 같은 통로로 말한다. 각자 로거를 갖게 두면 「어디로 갔는지」가 갈린다.
                _placement.Report = value;
                _hand.Report = value;
                _bindings.Report = value;
                _triggers.Report = value;
                _skills.Report = value;
                _intents.Report = value;
            }
        }

        public MatchDefinition Definition => _def;
        public BattleWorld World => _world;
        public EventBus Bus => _bus;
        public RngStreams Rng => _rng;
        public TickPipeline Pipeline => _pipeline;

        // ── 담당자 읽기 모델 ──
        public MatchClock Clock => _clock;
        public CostLedger Cost => _cost;
        public ScoreLedger Score => _score;
        public HeartMeter Heart => _heart;
        public WaveScheduler Waves => _waves;
        public PlacementService Placement => _placement;
        public HandDeck Hand => _hand;
        public GimmickHost Gimmick => _gimmick;

        /// <summary>이 판의 목표. 「끝났나 / 몇 점인가」 두 판정만 갖는다.</summary>
        public IMatchGoal Goal => _goal;

        /// <summary>HUD·결과 화면이 읽는 진행값.</summary>
        public GoalReadModel GoalRead => _goal.Read(_goalCtx);

        /// <summary>
        /// 판이 끝난 시점의 성적. **조립 지점은 여기 하나**다 — 예전엔 종료 경로 다섯이
        /// 각자 조립해 한 곳만 빠뜨려도 조용히 어긋났다(Y10).
        /// </summary>
        public MatchOutcome Outcome => _goal.BuildOutcome(_goalCtx);

        /// <summary>배달까지 끝난 이벤트. Unity 층이 틱 뒤에 드레인하고 `ClearEvents()` 한다.</summary>
        public IReadOnlyList<CoreEvent> Events => _bus.Outbox;

        public void ClearEvents() => _bus.ClearOutbox();

        public void Begin()
        {
            _ctx.Tick = 0;

            // 시작 사건은 **첫 틱을 기다리지 않는다** — 뷰가 판을 세우는 신호라 틱 0 의
            // 스폰보다 먼저 배달돼야 한다. 그래서 담당자들의 판 경계보다도 앞에 발행한다.
            _bus.Publish(CoreEvent.MatchStartedAt(0));

            // 판 경계는 **담당자마다 자기 `Begin`** 이다. 「판 경계」를 부르는 한 함수를 만들지
            // 않는 것이 중복 7 의 처방이고, 여기 나열된 호출은 조립이지 규칙이 아니다.
            ref var mode = ref _def.Mode;
            _clock.Begin(in mode, _bus, Dt);
            _cost.Begin(in mode.Cost, _def.CostRateMultiplier, regenStartsNow: !mode.HasPlacementPhase);
            _score.Begin();
            // ⚠ **본능이 마음보다 먼저 선다.** 마음의 방패가 「본능이 살아 있나」라는 관찰이라,
            // 순서가 뒤집히면 판의 첫 틱 동안만 마음이 조준 가능한 창이 생긴다.
            // (id 발급 순서이기도 하다 — 결정론의 축이므로 바꾸면 모든 골든이 갈린다.)
            _fieldPrep.Begin(_world, _def, 0);
            _heart.Begin(in _def.Heart);
            _placement.Begin(_def.Roster, mode.PlacementInputEnabled, mode.RetireEnabled,
                             mode.BoardCap, _def.EffectTileCount,
                             Wassup.Core.MatchSeed.DeriveMapSeed(_def.Seed));
            _waves.Begin(in _def.WaveDeck, in _def.WavePlan,
                         mode.WaveSource == WaveSourceKind.AuthoredPlan,
                         _def.Enemies, _def.Seed,
                         System.Math.Max(1, _def.Map.Spawns.Length), _ctx.Report);
            _hand.Begin(null, _def.Seed, in mode.Awakening, mode.HandSize, mode.AttachCap);
            _gimmick.Begin(mode.GimmickEnabled, _def.Seed);
            _goal.OnBegin(_goalCtx);

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
