using System.Collections.Generic;
using Unity.Mathematics;
using Somnia.Battle.Skills;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.BattleCore
{
    // battle-core-rebuild unit 4 — **배치 판정.**
    //
    // 판정 순서가 규칙이다(「구조 &gt; 자원」):
    //
    //     페이즈 → [정의표 참조] → 공간 → 로스터 → 보드 상한 → 재배치 대기 → 코스트
    //
    // 대괄호는 규칙이 아니라 **계산의 전제**다 — 정의표 줄을 못 읽으면 footprint 도 층도
    // 모르므로 공간을 물을 수조차 없다. spec 의 나열(페이즈 → 공간 → 유닛 유효 → …)에서
    // 이 한 칸만 앞으로 당겼고 그 이유가 이것이다.
    //
    // unit 5b — **보드 상한이 재배치 대기보다 앞으로 왔다.** 둘 다 「구조」라 그 사이의
    // 순서는 규칙이 아니라 «둘 다 걸렸을 때 무엇을 말해 주나» 이고, 옛 트레이는 그 답을
    // 「소진 &gt; 쿨타임」으로 이미 정해 두었다(도색 우선순위). 뒤쪽 뒤에 두면 상한 1 짜리
    // 유닛이 「재배치 대기 중」이라고 답해 **플레이어가 기다리면 된다고 배운다** — 거짓이다.
    // 자세한 근거는 `SlotBlock` 헤더에 있다.
    //
    // 코스트가 **마지막**인 것이 「구조 &gt; 자원」의 이행이다: 못 놓을 자리에 놓으려 했을 때
    // 「돈이 없다」고 답하면 플레이어가 배우는 것이 틀린다(P6 — 성공 판정 → 차감).
    //
    // ⚠ **점유와 주인은 항상 쌍으로 바뀐다**(`PlacementOccupancy`). 쌍이 깨지면 죽은 유닛이
    // 칸을 영영 물고, 증상은 「가끔 못 놓는 칸」으로만 나온다.
    //
    // ⚠ **퇴근은 `Dead` 를 켜지 않는다.** 그냥 소멸한다 — 그래서 사직서·작별 선물·각성이
    // **배제 코드 0 줄로** 안 일어난다(`defender-clock-out` 의 발견). 죽음과 퇴근을 한
    // 플래그로 접으면 그 셋을 하나씩 빼는 분기가 다시 생긴다.
    public sealed class PlacementService : ITickPhase
    {
        public string Name => "PlacementService";

        /// <summary>재배치 대기를 건 **출처**. 옛 구현은 「건 순간의 길이」로 근사했다(L5).</summary>
        public enum CooldownSource : byte { None = 0, Place = 1, Death = 2, Retire = 3 }

        /// <summary>
        /// 한 **칸**의 상태. 유닛과 무관하다 — 화면의 배치 하이라이트가 이것을 읽는다.
        /// 「막혔다」의 이유가 둘인 것이 요점이다: 지형·프랍은 내가 어떻게 할 수 없고,
        /// 유닛 점유는 치우거나 기다리면 열린다. 플레이어가 배우는 것이 다르다.
        /// </summary>
        public enum CellState : byte { Free = 0, Blocked = 1, Occupied = 2 }

        // ⚠ 남은 시간은 **틱으로 센다**(초가 아니라). 초를 매 틱 빼면 float 누적 오차가 쌓여
        // 「1초짜리 배치 모션이 61틱 걸리는」 드리프트가 난다 — 실측이다. `MatchClock` 이
        // 「시간이 아니라 틱이 정본이다」로 같은 함정을 이미 닫아 두었고, 여기도 같은 자를 쓴다.
        private struct Cooldown
        {
            public int Remaining;
            public int Total;
            public CooldownSource Source;
        }

        private struct Pending
        {
            public SimEntityId Id;
            public int DefIndex;
            public int Remaining;
        }

        private readonly EventBus _bus;
        private readonly BattleWorld _world;
        private readonly MatchClock _clock;
        private readonly MatchDefinition _def;
        private readonly MapRuntime _map;
        private readonly CostLedger _cost;

        private readonly Dictionary<int, Cooldown> _cooldowns = new Dictionary<int, Cooldown>(16);
        private readonly List<Pending> _pending = new List<Pending>(8);
        private readonly HashSet<int> _retiring = new HashSet<int>();
        private readonly List<int2> _effectTiles = new List<int2>(8);
        // unit 6b — 칸마다 어느 종류냐(`MatchDefinition.EffectTiles` 줄). `_effectTiles` 와 **쌍으로** 바뀐다.
        private readonly List<int> _effectTileKinds = new List<int>(8);
        // unit 6b — 타일 칸에 놓였지만 아직 활성화 전인 유닛의 타일 종류. 효과는 **활성화 엣지**에 건다
        // (옛 `ApplyEffectTileOnce` 가 배치 스킬 seam 안에 있었다). 표식을 배치 스킬과 공유하지
        // 않는 것이 F19 다 — 이 표는 이 담당자 혼자 쓴다.
        private readonly List<ArmedTile> _tileOnActivate = new List<ArmedTile>(4);
        private readonly List<Effects.ModifierSlot> _tileRevoked = new List<Effects.ModifierSlot>(4);
        private TickContext _ctx;

        private struct ArmedTile
        {
            public SimEntityId Id;
            public int Kind;
        }
        private readonly List<int> _roster = new List<int>(8);

        private bool _inputEnabledDuringPlacement;
        private bool _retireEnabled = true;
        private int _boardCap;

        public PlacementService(EventBus bus, BattleWorld world, MatchClock clock,
                                MatchDefinition def, MapRuntime map, CostLedger cost)
        {
            _bus = bus;
            _world = world;
            _clock = clock;
            _def = def;
            _map = map;
            _cost = cost;
            _bus.Subscribe(CoreEventKind.UnitDestroyed, EventOrder.PlacementCooldown, OnDestroyed);
        }

        /// <summary>남은 재배치 대기(초). 없으면 0 — 「기록이 없으면 준비된 것」(L3).</summary>
        public float CooldownRemaining(int defIndex)
            => _cooldowns.TryGetValue(defIndex, out var c) ? c.Remaining * BattleMatch.Dt : 0f;

        /// <summary>남은 비율 1→0(아이콘 레이디얼용). 없거나 길이가 0 이면 0(L7).</summary>
        public float CooldownFraction(int defIndex)
            => _cooldowns.TryGetValue(defIndex, out var c) && c.Total > 0
                ? math.saturate(c.Remaining / (float)c.Total)
                : 0f;

        public bool IsReady(int defIndex)
            => !_cooldowns.TryGetValue(defIndex, out var c) || c.Remaining <= 0;

        /// <summary>판 위의 그 종류 수(배치 중 포함 — 자리를 이미 먹었기 때문이다).</summary>
        public int OnBoard(int defIndex)
        {
            var units = _world.Units;
            int n = 0;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind == UnitKind.Defender && !u.Dead && u.DefIndex == defIndex) n++;
            }
            return n;
        }

        public int OnBoardTotal()
        {
            var units = _world.Units;
            int n = 0;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Kind == UnitKind.Defender && !units[i].Dead) n++;
            return n;
        }

        /// <summary>아직 활성화를 기다리는 유닛 수(배치 모션 중).</summary>
        public int PendingActivations => _pending.Count;

        /// <summary>
        /// 효과 타일 칸들. **판 시작에 한 번 뽑고 판 내내 안 바뀐다**(옛 규칙 — 칸 소비 없음).
        /// 그 칸이 개체에게 **준 효과**는 퇴근 때 회수된다(unit 6b — F33).
        /// </summary>
        public IReadOnlyList<int2> ArmedEffectTiles => _effectTiles;

        /// <summary>그 칸의 효과 타일 종류(`MatchDefinition.EffectTiles` 줄). 없으면 -1.</summary>
        public int EffectTileKindAt(int2 cell)
        {
            for (int i = 0; i < _effectTiles.Count; i++)
                if (_effectTiles[i].Equals(cell)) return _effectTileKinds[i];
            return -1;
        }

        /// <summary>틱 문맥. 효과 타일이 효과 관문(`EffectApply`)을 지나려면 필요하다.</summary>
        public void Bind(TickContext ctx) => _ctx = ctx;

        public void Begin(int[] roster, bool inputEnabledDuringPlacement, bool retireEnabled,
                          int boardCap, int effectTileCount, int mapSeed)
        {
            _cooldowns.Clear();
            _pending.Clear();
            _retiring.Clear();
            _effectTiles.Clear();
            _effectTileKinds.Clear();
            _tileOnActivate.Clear();
            _map.Occupancy.Clear();

            _inputEnabledDuringPlacement = inputEnabledDuringPlacement;
            _retireEnabled = retireEnabled;
            _boardCap = boardCap;

            _roster.Clear();
            // 로스터가 비면 **정의표 전체**다. 라이브는 스쿼드만 실려 오지만, 전투 빌더가
            // 카탈로그 밖 에셋(순찰 소환물)을 표에 편입하므로 「표에 있다 = 놓을 수 있다」가
            // 언제나 참인 것은 아니다 — 그래서 명시 로스터가 이긴다.
            if (roster != null) _roster.AddRange(roster);

            if (effectTileCount > 0 && _map.Snapshot.CellCount > 0)
            {
                var cells = new int2[effectTileCount];
                int n = _map.SelectEffectTiles(mapSeed, effectTileCount, cells);
                var kinds = new int[n];
                EffectTileSelect.AssignKinds(mapSeed, _def.EffectTiles.Length, kinds, n);
                for (int i = 0; i < n; i++)
                {
                    _effectTiles.Add(cells[i]);
                    _effectTileKinds.Add(kinds[i]);
                }
            }
        }

        public bool InRoster(int defIndex)
            => _roster.Count == 0 || _roster.Contains(defIndex);

        // ── 판정 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 배치. **여기 한 곳**이 판정의 전부다 — 입력도 뷰도 미리 거르지 않는다(중복 3 의 처방:
        /// 「모자라면 거부」가 세 곳에 살던 것을 receipt 의 거절 사유 하나로 접는다).
        /// </summary>
        public Receipt TryPlace(int defIndex, int2 anchor, float2 facing, int tick)
        {
            var reason = Judge(defIndex, anchor, out int w, out int h);
            if (reason != RejectReason.None)
            {
                _bus.Publish(CoreEvent.PlacementRejected(tick, reason, defIndex));
                return Receipt.Reject(reason);
            }

            ref var d = ref _def.Units[defIndex];

            // 코스트는 **판정을 다 통과한 뒤**에 깎는다(P6). 여기서 실패할 수 있는 이유는
            // 위 `Judge` 가 이미 `CanAfford` 를 보았기 때문에 사실상 없지만, 「본 값으로
            // 판정하고 다른 값으로 지불」이 되지 않게 지불도 같은 담당자에게 맡긴다.
            if (!_cost.TryPay(d.Cost, tick))
            {
                _bus.Publish(CoreEvent.PlacementRejected(tick, RejectReason.InsufficientCost, defIndex));
                return Receipt.Reject(RejectReason.InsufficientCost);
            }

            // 배치 페이즈 — **한 단계**다. 비행은 프레젠테이션 시간이라 뷰가 `LandDefender` 로
            // 착지를 알리고, 그때 모션 길이만큼 **다시** 잰다. 뷰가 없는 판(헤드리스)에서는
            // 배치 시점부터 재므로 **활성화가 영영 안 오는 갭이 생기지 않는다** — 옛 전투에서
            // 코스트 재생 스위치를 UI 가 들고 있어 생겼던 harness≠live 갭과 같은 종류다.
            //
            // ⚠ **모션이 0 이면 페이즈 자체가 없다.** 「0 = 착지 즉시 활성화」이고, 그것을
            // 한 틱짜리 대기로 흉내 내면 그 틱 동안 이 유닛이 사냥판의 소스도 표적도 아니게
            // 되어 「놓았는데 한 프레임 유령」이 된다.
            int motionTicks = MatchClock.TicksOf(d.DeployMotionSeconds, BattleMatch.Dt);
            bool hasDeployPhase = motionTicks > 0;
            var u = SpawnDefender(defIndex, anchor, w, h, facing, tick, deploying: hasDeployPhase);
            if (hasDeployPhase)
                _pending.Add(new Pending { Id = u.Id, DefIndex = defIndex, Remaining = motionTicks });

            StartCooldown(defIndex, d.PlacementCooldown, CooldownSource.Place);
            ArmTileFor(u, anchor, tick);

            _bus.Publish(CoreEvent.Placed(tick, u, defIndex, d.Cost));
            // 배치 페이즈가 없는 유닛은 **그 자리에서** 활성화된다. 배치 스킬의 엣지가
            // 이 사건이므로 순서가 「배치 → 활성화」인 것이 계약이다.
            if (!hasDeployPhase) Activate(u, defIndex, tick);
            return Receipt.Ok;
        }

        /// <summary>
        /// 「이 **칸**이 지금 어떤 상태인가」 — **유닛과 무관한 질문**이다(사용자 결정 2026-09-23:
        /// 하이라이트가 말하는 것은 「칸의 상태」다).
        ///
        /// ⚠ 이것과 `SpaceBlock` 은 **다른 질문**이고, 섞으면 화면이 거짓말한다. 저쪽은
        /// 「이 유닛의 footprint 를 여기 두면 겹치나」라서 끌고 있는 유닛의 크기만큼 답이
        /// **부푼다**(민코프스키 합) — 2×2 가 선 자리에 2×2 를 끌면 점유가 3×3 으로 보인다.
        /// 「그 유닛을 놓을 수 있나」는 여전히 `Judge` 가 답한다(고스트).
        /// </summary>
        public CellState CellStateAt(int2 cell)
        {
            var map = _map.Snapshot;
            bool hasGrid = map.CellCount > 0;
            if (hasGrid && !map.InBounds(cell)) return CellState.Blocked;
            // 점유는 **배치 유닛만** 넣는다(거점은 안 넣는다 — 그쪽은 지형처럼 마스크가 닫는다).
            if (_map.Occupancy.IsOccupied(cell)) return CellState.Occupied;
            // 배치 마스크가 통째로 0 = 어떤 층도 못 서는 칸 = 지형·프랍이 막았다.
            if (hasGrid && map.PlaceMask[map.Index(cell)] == 0) return CellState.Blocked;
            return CellState.Free;
        }

        /// <summary>판정만. 프리뷰(「여기 놓을 수 있나」)가 같은 자를 쓰게 하는 진입점이다.</summary>
        public RejectReason Judge(int defIndex, int2 anchor) => Judge(defIndex, anchor, out _, out _);

        private RejectReason Judge(int defIndex, int2 anchor, out int w, out int h)
        {
            var space = SpaceBlock(defIndex, anchor, out w, out h);
            if (space != RejectReason.None) return space;

            // ③~⑥ 은 자리를 묻지 않는다 — 트레이 도색이 같은 답을 받아야 해서
            // **한 함수**로 뽑아 뒀다(`SlotBlock`).
            return SlotBlock(defIndex);
        }

        /// <summary>
        /// 「이 자리에 이 유닛이 **설 수 있나**」 — 자원 없이 묻는 판정(①②). `SlotBlock` 의 짝이다:
        /// 저쪽이 자리 없이 슬롯을 묻는다면 이쪽은 슬롯 없이 자리를 묻는다.
        ///
        /// **배치 가능 칸 하이라이트가 이것을 읽는다.** `Judge`(전부)를 읽으면 안 되는 이유는
        /// 하이라이트가 «공간 조건»을 말하는 표시이기 때문이다 — 코스트를 섞으면 **코스트 재생
        /// 경계마다 보드 전체가 깜빡이고**, 못 사는 유닛을 끌 때 「놓을 곳이 한 칸도 없다」고
        /// 거짓말한다. 「밝은 칸인데 비용이 모자라 고스트는 빨강」이 정상이다
        /// (`placement-eligible-tile-highlight` 의 「의미 계약」).
        /// </summary>
        private RejectReason SpaceBlock(int defIndex, int2 anchor) => SpaceBlock(defIndex, anchor, out _, out _);

        private RejectReason SpaceBlock(int defIndex, int2 anchor, out int w, out int h)
        {
            w = 1;
            h = 1;

            // ① 페이즈. 종료 뒤의 입력이 상태를 움직이면 결과 화면이 판 뒤에 바뀐다(계약 5).
            if (_clock.Ended) return RejectReason.MatchEnded;
            if (_clock.Phase == MatchPhase.Placement && !_inputEnabledDuringPlacement)
                return RejectReason.NotRunningOrPlacementClosed;

            // [전제] 정의표 줄. 이것 없이는 footprint 도 층도 모른다.
            if (defIndex < 0 || defIndex >= _def.Units.Length) return RejectReason.InvalidUnit;
            ref var d = ref _def.Units[defIndex];
            w = math.max(1, d.FootprintWidth);
            h = math.max(1, d.FootprintHeight);

            // ② 공간. **앵커는 min 코너**이고 다칸은 **전 칸**이 통과해야 한다 —
            // 한 칸만 보면 건물이 벽을 파고든다.
            var map = _map.Snapshot;
            if (map.CellCount > 0)
            {
                for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    var c = new int2(anchor.x + dx, anchor.y + dy);
                    if (!map.InBounds(c)) return RejectReason.OutOfBounds;
                    if (!map.PlaceableAt(c, (byte)d.PlacementLayers)) return RejectReason.NotBuildable;
                }
            }
            if (!_map.Occupancy.IsFree(anchor, w, h)) return RejectReason.Occupied;

            return RejectReason.None;
        }

        /// <summary>
        /// 「이 슬롯을 지금 못 쓰는 이유」 — **자리 없이** 묻는 판정. `RejectReason.None` = 쓸 수 있다.
        ///
        /// 트레이가 이것을 읽는다. 트레이가 자기 셈(「판 위에 몇이지 / 쿨이 남았나 / 살 수 있나」)을
        /// 가지면 드롭 거절과 **다른 답**을 낼 수 있고, 그 순간 화면이 규칙을 틀리게 가르친다.
        ///
        /// 순서는 「**소진 &gt; 쿨타임 &gt; 코스트**」다 — 옛 트레이의 도색 우선순위 그대로이고,
        /// 그것이 옳은 이유는 **소진이 더 오래 가는 답**이기 때문이다. 상한 1 짜리 유닛은
        /// 쿨이 끝나도 여전히 못 놓는다(판 위의 그 유닛이 사라져야 열린다). 둘 다 걸렸을 때
        /// 「재배치 대기 중」이라고 답하면 플레이어는 기다리면 된다고 배운다 — 거짓이다.
        ///
        /// 자원이 **마지막**인 것은 그대로다(「구조 &gt; 자원」 — P6).
        /// </summary>
        public RejectReason SlotBlock(int defIndex)
        {
            if (_clock.Ended) return RejectReason.MatchEnded;
            if (_clock.Phase == MatchPhase.Placement && !_inputEnabledDuringPlacement)
                return RejectReason.NotRunningOrPlacementClosed;

            if (defIndex < 0 || defIndex >= _def.Units.Length) return RejectReason.InvalidUnit;
            ref var d = ref _def.Units[defIndex];

            // ③ 로스터.
            if (!InRoster(defIndex)) return RejectReason.NotInPickedPool;

            // ④ 보드 상한(소진). 유닛 저작과 모드 상한이 **둘 다** 걸린다.
            if (OnBoard(defIndex) >= d.EffectiveMaxOnBoard) return RejectReason.LimitReached;
            if (_boardCap > 0 && OnBoardTotal() >= _boardCap) return RejectReason.LimitReached;

            // ⑤ 재배치 대기(쿨타임).
            if (!IsReady(defIndex)) return RejectReason.OnCooldown;

            // ⑥ 자원. 마지막이다.
            if (!_cost.CanAfford(d.Cost)) return RejectReason.InsufficientCost;

            return RejectReason.None;
        }

        // ── 착지·활성화 ──────────────────────────────────────────────────────

        /// <summary>
        /// 뷰가 「착지했다」고 알린다. 모션 길이를 **그 순간부터** 다시 잰다.
        /// 배치 중이 아닌 개체는 조용히 무시하지 않고 거절한다(조용한 무동작 금지).
        /// </summary>
        public Receipt Land(SimEntityId id)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].Id != id) continue;
                var p = _pending[i];
                p.Remaining = DeployMotionTicksOf(p.DefIndex);
                _pending[i] = p;
                return Receipt.Ok;
            }
            return Receipt.Reject(RejectReason.NoSuchEntity);
        }

        public void Run(TickContext ctx)
        {
            StepCooldowns();
            StepActivation(ctx);
        }

        private void StepCooldowns()
        {
            if (_cooldowns.Count == 0) return;   // 「0 = 없는 것과 같다」(L2)
            // 사전을 순회하며 고치지 않는다 — 키를 모아 두 번 돈다. 결정론에 영향이 없는
            // 이유는 각 항목이 서로를 안 읽기 때문이다(감소는 독립).
            _cooldownKeys.Clear();
            foreach (var k in _cooldowns.Keys) _cooldownKeys.Add(k);
            for (int i = 0; i < _cooldownKeys.Count; i++)
            {
                int k = _cooldownKeys[i];
                var c = _cooldowns[k];
                c.Remaining--;
                if (c.Remaining <= 0) _cooldowns.Remove(k);
                else _cooldowns[k] = c;
            }
        }

        private readonly List<int> _cooldownKeys = new List<int>(16);

        private void StepActivation(TickContext ctx)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                var u = _world.Find(p.Id);
                if (u == null) { _pending.RemoveAt(i); continue; }

                // **시체는 배치되지 않는다**(E5) — 배치 중에 죽으면 활성화도 배치 스킬도 없다.
                if (u.Dead) { _pending.RemoveAt(i); continue; }

                p.Remaining--;
                if (p.Remaining > 0) { _pending[i] = p; continue; }

                _pending.RemoveAt(i);
                u.Deploying = false;
                // 배치 스킬(unit 7)의 엣지가 이 사건이다. 표식 컴포넌트를 남기지 않는 이유:
                // 남으면 다음 배치 사건과 섞인다(E6).
                Activate(u, p.DefIndex, ctx.Tick);
            }
        }

        // 활성화 = 효과 타일 적용 → 활성화 사건. **활성화 경로가 둘**(즉시 · 모션 뒤)이라 한 함수로
        // 접는다 — 한쪽에만 타일을 걸면 모션 없는 유닛만 타일을 먹는다.
        private void Activate(Unit u, int defIndex, int tick)
        {
            ApplyArmedTile(u, tick);
            _bus.Publish(CoreEvent.DefenderActivated(tick, u, defIndex));
        }

        // ⚠ **저작한 연산자를 그대로 쓴다** — 값으로 버킷을 고르는 중앙 헬퍼를 지나지 않는다
        // (재생은 기본 0 이라 가산이어야 하고 그 선택은 타일이 한다 — 옛 규칙).
        // 지속 = 무한(+∞ 는 만료 경로를 자연 통과한다). 끝은 **회수**다(퇴근).
        private void ApplyArmedTile(Unit u, int tick)
        {
            for (int i = 0; i < _tileOnActivate.Count; i++)
            {
                if (_tileOnActivate[i].Id != u.Id) continue;
                int kind = _tileOnActivate[i].Kind;
                _tileOnActivate.RemoveAt(i);
                if (kind < 0 || kind >= _def.EffectTiles.Length || _ctx == null) return;
                ref var row = ref _def.EffectTiles[kind];
                var tag = new Effects.SlotTag(Effects.SlotKind.Tile);
                for (int e = 0; e < row.EntryCount; e++)
                {
                    var en = row.Entries[e];
                    Effects.EffectApply.Stat(_ctx, u.Id, u, u, (Effects.StatKind)en.Stat,
                                             (Effects.CombineOp)en.Op, en.Magnitude,
                                             float.PositiveInfinity, tag, 0f, Effects.ModifierOrigin.Tile);
                }
                return;
            }
        }

        // 퇴근 회수(F33). 퇴근은 개체를 지우므로 슬롯은 어차피 사라지지만, **회수 사건**을
        // 내는 자리가 여기다 — 뷰·로그가 「타일 효과가 풀렸다」를 개체 소멸에서 추론하지 않게.
        private void RevokeTile(Unit u, int tick)
        {
            for (int i = _tileOnActivate.Count - 1; i >= 0; i--)
                if (_tileOnActivate[i].Id == u.Id) _tileOnActivate.RemoveAt(i);

            _tileRevoked.Clear();
            if (u.Modifiers.RevokeTag(Effects.SlotKind.Tile, 0, _tileRevoked) == 0) return;
            for (int k = 0; k < _tileRevoked.Count; k++)
                _bus.Publish(CoreEvent.ModifierRevoked(tick, u, _tileRevoked[k].Key.Source,
                                                       _tileRevoked[k].Key.Stat));
        }

        // ── 퇴근 ─────────────────────────────────────────────────────────────

        public Receipt Retire(SimEntityId id, int tick)
        {
            if (_clock.Ended) return Receipt.Reject(RejectReason.MatchEnded);
            if (!_retireEnabled) return Receipt.Reject(RejectReason.NotRunningOrPlacementClosed);

            var u = _world.Find(id);
            if (u == null) return Receipt.Reject(RejectReason.NoSuchEntity);
            if (u.Kind != UnitKind.Defender) return Receipt.Reject(RejectReason.InvalidUnit);

            int defIndex = u.DefIndex;
            float cd = defIndex >= 0 && defIndex < _def.Units.Length
                ? _def.Units[defIndex].EffectiveRetireCooldown
                : 0f;

            // 「퇴근이다」를 소멸 구독에 알려 두는 한 칸. 출처를 키에 넣을 수 있게 된 것이
            // 옛 근사(「건 순간의 길이로 어느 쿨인지 추정」)를 없앤 자리다(L5).
            _retiring.Add(id.Value);

            // ⚠ **소멸보다 먼저 알린다.** 「인수인계」(퇴근 회수 앞당김)를 소멸 사건으로
            // 판정하면 사망과 구분이 안 된다 — 버스는 발행 순서대로 배달하므로 이 한 줄의
            // 위치가 그 구분을 만든다. 개체가 아직 살아 있어야 자리·몸을 실을 수 있기도 하다.
            _bus.Publish(CoreEvent.Retired(tick, u, defIndex, cd));
            RevokeTile(u, tick);

            // unit 7a — 감지자 사실: 퇴근(퇴직 위로금 · 퇴근 운석). **파괴 직전에** 스냅샷을 싣는다 — 자리 = 비워진
            // 칸 중심(대표 칸), 몸 = 0(자리형). 드레인은 이 커맨드의 콜스택(`Immediate`)이다(7b 8-1).
            if (_ctx?.Triggers != null)
            {
                var cell = u.Footprint != null ? u.Footprint.Anchor : _map.CellOf(u.Position);
                _ctx.Triggers.RaiseRetire(u, _map.CenterOf(cell));
            }

            _map.Occupancy.Release(id);
            // 중단 정책의 「퇴근」 열. 제거 **앞**에 불러야 닫힘 사건의 사유가 「제거」가 아니라 「퇴근」이다.
            _world.InterruptProgress(u, ProgressInterrupt.Retire, tick);
            // ⚠ `Dead` 를 켜지 않는다. 퇴근은 죽음이 아니다.
            _world.Destroy(id, tick);
            return Receipt.Ok;
        }

        private void OnDestroyed(CoreEvent e)
        {
            if (e.Faction != Faction.DefenderUnit) return;

            for (int i = _pending.Count - 1; i >= 0; i--)
                if (_pending[i].Id == e.A) _pending.RemoveAt(i);
            for (int i = _tileOnActivate.Count - 1; i >= 0; i--)
                if (_tileOnActivate[i].Id == e.A) _tileOnActivate.RemoveAt(i);

            // 소멸 사건의 `Arg` 는 **종류**(UnitKind)라 정의표 인덱스가 아니다 — 그래서
            // 스폰 때 적어 둔 자리를 본다.
            int defIndex = DefIndexOfDestroyed(e);
            if (defIndex < 0) return;

            bool retired = _retiring.Remove(e.A.Value);
            float seconds = retired
                ? _def.Units[defIndex].EffectiveRetireCooldown
                : _def.Units[defIndex].EffectiveDeathCooldown;
            StartCooldown(defIndex, seconds, retired ? CooldownSource.Retire : CooldownSource.Death);
        }

        // 소멸 사건은 정의표 인덱스를 나르지 않는다(`Arg` 는 종류다). 퇴근 경로는 우리가
        // 부른 것이라 인덱스를 알지만 사망 경로는 그렇지 않으므로, 소멸 **직전**에 기록해 둔
        // 자리를 본다. 그래서 이 사전은 「배치된 유닛의 종류」를 판 내내 들고 있다.
        private readonly Dictionary<int, int> _defIndexById = new Dictionary<int, int>(32);

        private int DefIndexOfDestroyed(CoreEvent e)
        {
            if (!_defIndexById.TryGetValue(e.A.Value, out int defIndex)) return -1;
            _defIndexById.Remove(e.A.Value);
            return defIndex >= 0 && defIndex < _def.Units.Length ? defIndex : -1;
        }

        // ── 스폰 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 방어유닛 하나를 세운다. **스폰하는 쪽이 전부 이 함수를 지난다**(배치·디버그) —
        /// 두 벌이면 「어떤 경로로 태어났나」가 공격·점유 규칙을 바꾼다.
        /// </summary>
        public Unit SpawnDefender(int defIndex, int2 anchor, int w, int h, float2 facing,
                                  int tick, bool deploying)
        {
            ref var d = ref _def.Units[defIndex];
            var u = _world.Spawn(UnitKind.Defender, Faction.DefenderUnit, defIndex,
                                 FootCenter(anchor, w), d.BodyRadiusTiles, d.Health,
                                 deploying, tick);
            u.Footprint = _world.Parts.RentFootprint();
            u.Footprint.Anchor = anchor;
            u.Footprint.Width = w;
            u.Footprint.Height = h;
            if (d.AggroCapacity > 0)
            {
                u.Aggro = _world.Parts.RentAggro();
                u.Aggro.Capacity = d.AggroCapacity;
            }
            u.Attack = CombatPhase.BuildAttackState(in d, _def, _world.Parts);
            _map.Occupancy.Occupy(u.Id, anchor, w, h);
            _defIndexById[u.Id.Value] = defIndex;
            return u;
        }

        /// <summary>발밑 = 하단 행 가로 중앙. 사거리 원점·몸 원이 전부 이 점이다.</summary>
        private float3 FootCenter(int2 anchor, int width)
        {
            float ts = _map.TileSize;
            return new float3((anchor.x + (width - 1) * 0.5f) * ts, 0f, anchor.y * ts);
        }

        private int DeployMotionTicksOf(int defIndex)
            => defIndex >= 0 && defIndex < _def.Units.Length
                ? MatchClock.TicksOf(_def.Units[defIndex].DeployMotionSeconds, BattleMatch.Dt)
                : 0;

        // ── 재배치 대기 ──────────────────────────────────────────────────────

        /// <summary>
        /// 대기를 건다. 0 이하면 **등록조차 안 한다**(L2 「0 = 없는 것과 같다」).
        ///
        /// ⚠ 같은 키가 이미 있으면 **덮어쓰지 않고 `max`** 를 쓴다. 덮어쓰면 판 상한이 2 이상인
        /// 종류에서 「#2 를 놓자마자 #1 이 죽는」 순간에 짧은 배치 쿨이 긴 사망 쿨을 지운다.
        /// (라이브는 전원 상한 1 이라 오늘의 거동은 같다 — 그래서 지금 고치는 것이 싸다.)
        /// </summary>
        public void StartCooldown(int defIndex, float seconds, CooldownSource source)
        {
            if (defIndex < 0 || seconds <= 0f) return;
            int ticks = MatchClock.TicksOf(seconds, BattleMatch.Dt);
            if (ticks <= 0) return;
            _cooldowns.TryGetValue(defIndex, out var existing);
            if (existing.Remaining >= ticks) return;
            _cooldowns[defIndex] = new Cooldown
            {
                Remaining = ticks,
                Total = math.max(ticks, existing.Total),
                Source = source,
            };
        }

        /// <summary>
        /// 배치를 **취소**했을 때 그 배치가 건 대기만 지운다. 출처를 키에 넣어 두었으므로
        /// 옛 근사(「건 순간의 길이로 어느 쿨인지 추정」)가 필요 없다(L5).
        /// </summary>
        public bool ClearPlaceCooldown(int defIndex)
        {
            if (!_cooldowns.TryGetValue(defIndex, out var c)) return false;
            if (c.Source != CooldownSource.Place) return false;
            _cooldowns.Remove(defIndex);
            return true;
        }

        // ── 효과 타일 ────────────────────────────────────────────────────────

        // **칸은 판 내내 남는다**(옛 `_effectTilesByCell` — 맵 빌드 때만 채우고 지우지 않았다,
        // `BattleBridge.cs:297·1492·9078`). 그 칸에 놓이는 **개체마다** 한 번 건다 — 옛
        // `ApplyEffectTileOnce` 는 「개체당 1회」 가드였지 칸 소비가 아니었다(`:9126-9131`).
        // 판정 칸은 **대표 칸(앵커) 하나**다 — 「효과 타일·배치 스킬은 대표 셀에서 발동」
        // (`:7867`, defender-footprint unit 1). footprint 의 다른 칸이 타일 위여도 안 받는다.
        // 효과는 활성화 엣지에 건다(`ApplyArmedTile`), 끝은 퇴근 회수(`RevokeTile`) — 그래서
        // 다음 유닛이 같은 칸에서 다시 받는다(옛 전투가 못 하던 것, 6b 계약 11).
        private void ArmTileFor(Unit u, int2 anchor, int tick)
        {
            int kind = EffectTileKindAt(anchor);
            if (kind < 0) return;
            _tileOnActivate.Add(new ArmedTile { Id = u.Id, Kind = kind });
            Report?.Invoke($"[PlacementService] 효과 타일 ({anchor.x},{anchor.y}) 종류 {kind} — 활성화 때 건다(tick {tick}).");
        }

        /// <summary>진단 통로. 연결하지 않으면 버려진다 — 코어는 로거를 소유하지 않는다.</summary>
        public System.Action<string> Report;
    }
}
