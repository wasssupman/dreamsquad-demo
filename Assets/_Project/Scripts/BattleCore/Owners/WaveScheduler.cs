using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Wave;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 4 — **웨이브.**
    //
    // 케이던스는 시각 그리드가 아니라 **사건**이다: 「전멸했다」 또는 「상한 시간이 지났다」.
    // 생성기가 내놓는 `TriggerTimeSec` 은 명목값(최악 케이스)이고 런타임은 그것을 읽지 않는다 —
    // 읽으면 잘 막은 플레이어가 보상을 못 받는다.
    //
    // ⚠ **전멸 판정은 자기 술어다.** 보너스 적은 세지 않는다(그쪽은 자기 큐·타임라인·포탈을
    // 쓰는 별개 축이다). 공용 「살아 있는 공격자」 목록에 필터를 거는 것이 옛 전투에서
    // 금지였던 이유는 소비처 11곳이 그 목록을 공유하기 때문이고(X12), 그래서 여기서만
    // 자기 술어를 쓴다.
    //
    // ⚠ **당김은 2층**이다: 규칙층(`TryPull` — 상한이 걸린다)과 기제층(`ForceNext` — 상한을
    // 무시한다). 상한은 **전멸로만 회복**된다 — 상한 경과는 회복이 아니다. 「기본 스케줄 대비
    // 선행 N웨이브」로 재지 않는 이유는 그 계산식이 **잘 막은 플레이어의 당김을 막기** 때문이다.
    public sealed class WaveScheduler : ITickPhase
    {
        public string Name => "WaveScheduler";

        private struct Scheduled
        {
            public float AtSec;
            public int EnemyIndex;
            public int Lane;
            public int PathIndex;
            public int2 Cell;
            public int WaveNumber;
            public bool Bonus;
        }

        private const float IntervalHardFallback = 20f;

        private readonly EventBus _bus;
        private readonly BattleWorld _world;
        private readonly MatchClock _clock;
        private readonly MatchDefinition _def;
        private readonly MapRuntime _map;
        private readonly HeartMeter _heart;

        private WavePlan _plan = WavePlan.Empty();
        private bool _authored;
        private float _deckInterval;
        private int _pullCap;

        private readonly List<Scheduled> _pending = new List<Scheduled>(64);
        private readonly List<PlannedSpawn> _expandScratch = new List<PlannedSpawn>(64);
        private readonly HashSet<int> _bonusIds = new HashSet<int>();

        private int _nextWave;            // 다음에 보낼 웨이브 인덱스
        private int _lastQueuedWave;      // 도달 웨이브(1부터). 성적이 읽는다.
        private float _lastDispatchAt;
        private bool _waveStartedAnnounced;
        private int _pullsSinceClear;

        private int _bonusKills;
        private int _bonusConsumed;
        private bool _bonusOfferLatched;
        private bool _bonusInFlight;

        public WaveScheduler(EventBus bus, BattleWorld world, MatchClock clock,
                             MatchDefinition def, MapRuntime map, HeartMeter heart)
        {
            _bus = bus;
            _world = world;
            _clock = clock;
            _def = def;
            _map = map;
            _heart = heart;

            // 처치·소멸을 **웨이브 예약보다 먼저** 받는다(X2 ①). 분열 자식은 소멸 seam 에서
            // 태어나므로 전멸 판정이 그 뒤에 와야 「엘리트를 죽이면 판이 빨라지는」
            // 역인센티브가 생기지 않는다.
            _bus.Subscribe(CoreEventKind.UnitSlain, EventOrder.WaveBookkeeping, OnSlain);
            _bus.Subscribe(CoreEventKind.UnitDestroyed, EventOrder.WaveBookkeeping, OnDestroyed);
            // 안정도가 움직인 **뒤** 보너스를 다시 판정한다(X2 ②③). 묵은 스트레스로 판정하면
            // 문턱에서 버튼이 떨린다 — 래치가 그 떨림을 구조적으로 불가능하게 만든다.
            _bus.Subscribe(CoreEventKind.HeartChanged, EventOrder.BonusOffer, _ => EvaluateBonusOffer());
        }

        /// <summary>도달 웨이브 = 마지막으로 큐에 올린 웨이브 번호(Y7).</summary>
        public int WaveReached => _lastQueuedWave;

        public int WaveCount => _plan.WaveCount;

        /// <summary>아직 안 나온 스폰 수. 0 이어야 「그 웨이브가 다 나왔다」이다.</summary>
        public int PendingSpawns => _pending.Count;

        public int PullsSinceClear => _pullsSinceClear;
        public int PullsLeft => math.max(0, _pullCap - _pullsSinceClear);

        public bool BonusOffered => _bonusOfferLatched;

        /// <summary>
        /// 보너스 당김 억제. **판 경계 리셋에서 지우지 않는다**(X6) — 판 시작 **전** 외부
        /// 주입이라, 리셋에 넣으면 켜 둔 억제가 판 시작에 지워진다. 다른 모든 보너스 상태와
        /// 규칙이 다르고, 그 차이가 이 프로퍼티가 `Begin` 에 없는 이유다.
        /// </summary>
        public bool BonusPullSuppressed { get; set; }

        /// <summary>
        /// **마지막 웨이브가 나갔고 필드가 비었다.** `WaveClear`·`TimeAttack` 이 읽는 한 줄이고
        /// 옛 `NoQueuedAttackersRemain` 의 후계다. 목표가 이 술어를 **다시 쓰지 않는다** —
        /// 두 벌이면 「목표는 끝났다는데 웨이브는 안 끝났다」가 난다.
        /// </summary>
        public bool LastWaveDispatchedAndFieldClear
            => _plan.WaveCount > 0 && _nextWave >= _plan.WaveCount && FieldClear;

        /// <summary>
        /// 전멸. **자기 술어**다 — 보너스 적은 세지 않는다. 공용 목록에 필터를 걸면
        /// 소비처 11곳이 조용히 같이 좁아진다(X12).
        /// </summary>
        public bool FieldClear
        {
            get
            {
                if (_pending.Count > 0)
                {
                    for (int i = 0; i < _pending.Count; i++)
                        if (!_pending[i].Bonus) return false;
                }
                var units = _world.Units;
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u.Faction != Faction.EnemyUnit || u.Dead) continue;
                    if (_bonusIds.Contains(u.Id.Value)) continue;
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// 판 경계 + **플랜 우선순위**. 저작 플랜이 있으면 그것이 이기고, 없으면 덱에서 굽는다.
        /// 「테스트 플랜 &gt; 저작 인카운터」의 구분은 **어느 에셋을 실어 보냈나**의 문제라
        /// 판 밖(빌더)에서 이미 끝난다 — 코어에 도착한 플랜은 하나다.
        ///
        /// 고정 시드(`WaveSeed` 비0)가 판 시드를 이긴다: 「같은 맵 같은 웨이브」의 손잡이다.
        /// </summary>
        public void Begin(in Wave.WaveDeckDef deck, in Wave.WavePlanDef authoredPlan,
                          bool preferAuthored, EnemyDef[] enemies, int matchSeed, int laneCount,
                          System.Action<string> report)
        {
            bool authored = preferAuthored && authoredPlan.HasWaves;
            WavePlan plan;
            if (authored)
            {
                plan = WaveGenerator.FromAuthored(in authoredPlan);
            }
            else if (deck.HasPool)
            {
                int waveSeed = deck.WaveSeed != 0
                    ? deck.WaveSeed
                    : Wassup.Core.MatchSeed.DeriveWaveSeed(matchSeed);
                plan = WaveGenerator.Generate(in deck, enemies, waveSeed, laneCount, report);
            }
            else
            {
                // 풀이 없으면 **웨이브가 없는 판**이다. 조용히 빈 플랜을 쓰되 한 번 말한다 —
                // 「적이 안 나온다」의 원인을 판 안에서 찾게 두지 않는다.
                plan = WavePlan.Empty();
                report?.Invoke("[WaveScheduler] 덱 풀이 2종 미만이고 저작 플랜도 없다 — 이 판에는 웨이브가 없다.");
            }

            _plan = plan;
            _authored = authored;
            _deckInterval = deck.MaxWaveIntervalSec;
            _pullCap = math.max(0, deck.EffectiveMaxPullsPerClear);

            _pending.Clear();
            _bonusIds.Clear();
            _nextWave = 0;
            _lastQueuedWave = 0;
            _lastDispatchAt = 0f;
            _waveStartedAnnounced = false;
            _pullsSinceClear = 0;
            _bonusKills = 0;
            _bonusConsumed = 0;
            _bonusOfferLatched = false;
            _bonusInFlight = false;
            // ⚠ `BonusPullSuppressed` 는 **여기서 지우지 않는다**(X6).
        }

        /// <summary>
        /// 웨이브 간 **상한** 간격. 폴백 사슬 전체가 fail-closed 장치다 —
        /// 0 이면 전 웨이브가 한 프레임에 쏟아진다(X11).
        /// </summary>
        public float Interval
            => _deckInterval > 0f ? _deckInterval
             : _plan.WaveIntervalSec > 0f ? _plan.WaveIntervalSec
             : IntervalHardFallback;

        public void Run(TickContext ctx)
        {
            // 웨이브는 **전투가 시작한 뒤에만** 돈다. 배치 창 동안 적이 나오면 「배치하는 동안
            // 안전하다」는 약속이 깨진다.
            if (_clock.Phase != MatchPhase.Battle || _clock.Ended) return;

            StepDispatch();
            StepSpawns(ctx);
        }

        // ── 케이던스 ─────────────────────────────────────────────────────────

        private void StepDispatch()
        {
            if (_nextWave >= _plan.WaveCount) return;

            if (_authored)
            {
                // 저작 플랜은 **타임라인이 정본**이다. 전멸도 상한도 보지 않는다 —
                // 저작자가 시각을 직접 적었다는 것이 그 면제의 근거다.
                if (_clock.BattleTime + 1e-4f < _plan.Waves[_nextWave].TriggerTimeSec) return;
                Dispatch(_plan.Waves[_nextWave].TriggerTimeSec);
                return;
            }

            // 웨이브 1 은 **즉시** 나간다(기다릴 앞 웨이브가 없다).
            if (_nextWave == 0) { Dispatch(_clock.BattleTime); return; }

            bool cleared = FieldClear;
            bool elapsed = _clock.BattleTime - _lastDispatchAt >= Interval;
            if (!cleared && !elapsed) return;

            // **전멸로만** 당김 상한이 회복된다. 상한 경과는 회복이 아니다.
            if (cleared) _pullsSinceClear = 0;
            Dispatch(_clock.BattleTime);
        }

        private void Dispatch(float baseSec)
        {
            ref var wave = ref _plan.Waves[_nextWave];
            // 리드인은 **스폰 기준시각에만** 더한다(X10). 예약 시각과 섞으면 당김 연타마다
            // 선행 시간이 누적 왜곡된다.
            float spawnBase = baseSec + (_authored ? 0f : _plan.SpawnLeadInSec);

            int lanes = math.max(1, _map.Snapshot.Spawns.Length);
            WaveGenerator.Expand(in wave, spawnBase, lanes, _plan.IntraWaveSpacingSec, _expandScratch);

            int waveNumber = _nextWave + 1;
            for (int i = 0; i < _expandScratch.Count; i++)
            {
                var s = _expandScratch[i];
                _pending.Add(new Scheduled
                {
                    AtSec = s.TriggerTimeSec,
                    EnemyIndex = s.EnemyIndex,
                    Lane = s.LaneIndex,
                    PathIndex = s.PathIndex,
                    Cell = int2.zero,
                    WaveNumber = waveNumber,
                    Bonus = false,
                });
            }

            _lastQueuedWave = waveNumber;
            _lastDispatchAt = baseSec;
            _waveStartedAnnounced = false;
            _nextWave++;
            _bus.Publish(CoreEvent.WaveQueued(_clock.Tick, waveNumber, wave.TotalCount));
        }

        // ── 스폰 ─────────────────────────────────────────────────────────────

        private void StepSpawns(TickContext ctx)
        {
            if (_pending.Count == 0) return;
            float now = _clock.BattleTime;

            // 목록은 **삽입 순서**로 돈다(생성기의 펼침 순서 = 결정론 키). 시간으로 정렬하면
            // 같은 시각의 동률을 정렬 안정성이 정하게 된다.
            //
            // ⚠ **제거는 «앞으로 접기»다.** 스왑 팝(끝 원소를 구멍에 넣기)은 남은 항목의
            // 순서를 흔들고, 그 순서가 곧 다음 틱의 스폰 차례라 **같은 시드가 다른 판**이 된다.
            // 역순 순회도 안 된다 — 한 틱에 여러 마리가 나올 때 발행 차례가 뒤집혀 웨이브
            // 시작 신호가 그 웨이브의 **마지막** 적에게 붙는다. 그래서 읽기 커서와 쓰기
            // 커서를 따로 두고 한 번만 지난다(O(n) · 순서 보존 · 인덱스 되감기 없음).
            int write = 0;
            for (int i = 0; i < _pending.Count; i++)
            {
                var s = _pending[i];
                if (s.AtSec > now)
                {
                    _pending[write++] = s;   // 아직 아니다 — 자리를 당겨 보존한다
                    continue;
                }

                var u = EnemySpawn.At(ctx, _map, s.EnemyIndex, s.Lane, s.Cell, s.PathIndex, ctx.Tick);
                if (u != null && s.Bonus) _bonusIds.Add(u.Id.Value);

                if (!s.Bonus && !_waveStartedAnnounced && s.WaveNumber == _lastQueuedWave)
                {
                    _waveStartedAnnounced = true;
                    // **보스 판별과 경보는 한 곳**이다(X13). 여기서 재판정하지 않고
                    // 생성기가 구운 플래그를 그대로 읽는다 — 재판정하면 이중 발화한다.
                    bool boss = s.WaveNumber - 1 < _plan.WaveCount
                                && _plan.Waves[s.WaveNumber - 1].IsBoss;
                    _bus.Publish(CoreEvent.WaveStarted(ctx.Tick, s.WaveNumber, boss));
                }
            }
            if (write < _pending.Count) _pending.RemoveRange(write, _pending.Count - write);
        }

        // ── 당김 2층 ─────────────────────────────────────────────────────────

        /// <summary>규칙층. 상한이 걸리고 **전멸로만 회복**된다. 저작 플랜은 면제다.</summary>
        public Receipt TryPull(int tick)
        {
            if (_clock.Ended) return Receipt.Reject(RejectReason.MatchEnded);
            if (_nextWave >= _plan.WaveCount) return Receipt.Reject(RejectReason.NoMoreWaves);
            // 저작 플랜의 타임라인은 저작자의 것이다 — 당김 상한을 적용하지 않는다.
            if (!_authored && _pullsSinceClear >= _pullCap)
                return Receipt.Reject(RejectReason.PullCapReached);

            if (!_authored) _pullsSinceClear++;
            ForceNext();
            return Receipt.Ok;
        }

        /// <summary>
        /// 기제층. 상한도 케이던스도 무시하고 다음 웨이브를 민다.
        /// **하네스가 판을 굴리는 동력**이라 no-op 으로 만들면 통합 스모크가 타임아웃한다.
        /// </summary>
        public bool ForceNext()
        {
            if (_nextWave >= _plan.WaveCount) return false;
            Dispatch(_clock.BattleTime);
            return true;
        }

        // ── 보너스 ───────────────────────────────────────────────────────────

        private void OnSlain(CoreEvent e)
        {
            if (e.Faction != Faction.EnemyUnit) return;
            // 크레딧은 **일반 적** 처치로만 쌓인다(계약 12). 보너스 적을 세면 실효 임계가
            // 내려가고, 임계 ≤ 보너스 마리수에서는 보너스 웨이브가 자기 자신을 무한 재발화한다.
            if (_bonusIds.Contains(e.B.Value)) return;
            _bonusKills++;
            EvaluateBonusOffer();
        }

        private void OnDestroyed(CoreEvent e)
        {
            if (_bonusIds.Remove(e.A.Value) && _bonusIds.Count == 0) _bonusInFlight = false;
        }

        private void EvaluateBonusOffer()
        {
            if (_bonusOfferLatched || _bonusInFlight) return;
            if (BonusPullSuppressed) return;
            ref var bonus = ref _def.Bonus;
            if (!bonus.Enabled) return;
            if (_map.Snapshot.BonusSpawns.Length == 0) return;
            if (_bonusKills - _bonusConsumed < bonus.KillThreshold) return;
            // 등장 조건이지 **유지 조건이 아니다** — 한 번 뜨면 소비할 때까지 유지된다.
            // 매 틱 재평가하면 스트레스가 문턱 근처에서 진동할 때 버튼이 떨린다.
            if (_heart.Stress > bonus.MaxStressToOffer) return;

            _bonusOfferLatched = true;
            _bus.Publish(CoreEvent.BonusOffered(_clock.Tick));
        }

        /// <summary>보너스 당김. 크레딧은 **한 회분씩** 깎는다(X14).</summary>
        public Receipt TryPullBonus(int tick)
        {
            if (_clock.Ended) return Receipt.Reject(RejectReason.MatchEnded);
            if (!_bonusOfferLatched) return Receipt.Reject(RejectReason.PullCapReached);

            ref var bonus = ref _def.Bonus;
            var portals = _map.Snapshot.BonusSpawns;
            if (portals.Length == 0) return Receipt.Reject(RejectReason.MissingMap);

            var entries = BonusWaveSchedule.Build(
                portals.Length, bonus.EnemyCount, bonus.FirstSpawnAtSec, bonus.SpawnIntervalSec);
            float now = _clock.BattleTime;
            for (int i = 0; i < entries.Length; i++)
            {
                _pending.Add(new Scheduled
                {
                    AtSec = now + entries[i].SpawnAtSec,
                    EnemyIndex = bonus.EnemyIndex,
                    Lane = -1,
                    PathIndex = -1,
                    Cell = portals[entries[i].PortalIndex],
                    WaveNumber = 0,
                    Bonus = true,
                });
            }

            // ⚠ **한 회분만** 깎는다. 현재 처치 수로 덮어쓰면 스트레스에 막혀 쌓인 초과
            // 크레딧이 통째로 증발한다(X14).
            _bonusConsumed += bonus.KillThreshold;
            _bonusOfferLatched = false;
            _bonusInFlight = true;
            _bus.Publish(CoreEvent.BonusPulled(tick, bonus.EnemyCount));
            return Receipt.Ok;
        }
    }
}
