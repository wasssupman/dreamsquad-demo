using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.Skills;
using Wassup.BattleCore.Trigger;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 4 — **드림캐쳐의 자원.**
    //
    // 옛 `DreamcatcherHandController` 19행 + `DreamcatcherCycleDeck` 이 여기로 온다.
    // 이 담당자가 갖는 것은 **자원뿐**이다: 큐 · 손패 창 · 각성 게이지 · 부착 등록부 ·
    // 액티브 재사용 대기. 「그 카드가 무엇을 하는가」는 규칙 레이어의 것이고, 그래서 여기에
    // 효과가 한 줄도 없다 — 있으면 다음 사람이 그것을 읽어 실행하려 든다.
    // unit 7b — 효과는 **규칙 등록부 호출로만** 나간다: 판정 `CardBindings.Plan` → 부착
    // `BindingRegistry.AttachCard` → 부착 즉시 `CardBindings.FireOnAttach`(Immediate seam 에 줄 세움 —
    // 드레인은 이 커맨드의 콜스택 `CommandPhase`). 이 담당자는 **핸들**(`CardAttachment`)만 쥔다.
    //
    // 구조:
    //   · 큐 = 12장(저장 부착 10 + 공용 액티브 2). **섞는 것은 한 곳**이고 시드는 판 시드 하나.
    //   · 손패 = 큐 **앞 N**(파괴적이지 않은 창). 창 멤버십은 플레이어가 쓸 때까지 유지된다.
    //   · 액티브를 쓰면 **뒤로 재활용**, 부착 카드를 쓰면 **풀에서 이탈**(돌아올 때만 복귀).
    //
    // ⚠ **앞 삽입은 창을 미는 유일한 연산**이다(인수인계). 그래서 «플레이어가 누른 퇴근» 에만
    // 붙인다 — 사망 경로에 얹으면 조준 중 비동기 재정렬이 손패 멤버십 가드를 나중에 깨뜨린다.
    public sealed class HandDeck : ITickPhase
    {
        public string Name => "HandDeck";

        public struct Entry
        {
            public int EntryId;
            public int CardIndex;
        }

        private struct Attachment
        {
            public SimEntityId Host;
            public int Seq;
            /// <summary>unit 7b — 규칙 등록부의 묶음 핸들. 떼기는 이것으로만 한다.</summary>
            public CardAttachment Group;
        }

        private readonly EventBus _bus;
        private readonly BattleWorld _world;
        private readonly MatchDefinition _def;
        private readonly MatchClock _clock;

        // unit 7b — 규칙 레이어(조립 뒤에 꽂힌다 — 디스패처가 이 담당자보다 늦게 선다).
        private BindingRegistry _registry;
        private TriggerDispatcher _triggers;
        private Map.MapRuntime _map;
        private readonly List<int> _planRows = new List<int>(4);
        private readonly List<int> _planSquad = new List<int>(2);
        private readonly List<Combat.AttackModDef> _planMods = new List<Combat.AttackModDef>(2);
        private int _debugSeq;

        private readonly List<Entry> _queue = new List<Entry>(16);
        private readonly Dictionary<int, Entry> _outOfPool = new Dictionary<int, Entry>(16);
        private readonly Dictionary<int, Attachment> _attachedTo = new Dictionary<int, Attachment>(16);
        // ⚠ 남은 대기는 **틱으로 센다**. 초를 매 틱 빼면 float 누적 오차가 쌓여 「1초가
        // 61틱」이 된다(`PlacementService` 에서 실측). 「시간이 아니라 틱이 정본」이 같은 자다.
        private readonly Dictionary<int, int> _cooldowns = new Dictionary<int, int>(8);
        private readonly List<int> _recoverScratch = new List<int>(8);
        private readonly List<Entry> _frontScratch = new List<Entry>(8);
        private readonly List<int> _cooldownKeys = new List<int>(8);

        private float _gauge;
        private float _gaugeMax = 100f;
        private int _handSize = 5;
        private int _attachCap = 3;
        private int _attachSeq;
        private float _overflowLost;

        public HandDeck(EventBus bus, BattleWorld world, MatchDefinition def, MatchClock clock = null)
        {
            _bus = bus;
            _world = world;
            _def = def;
            _clock = clock;

            // 처치·사망이 각성을 준다. **퇴근은 안 준다** — 주면 배치→퇴근 반복이 게이지 파밍이
            // 된다(D7). 그래서 `Retired` 는 회수만 구독하고 게이지는 건드리지 않는다.
            _bus.Subscribe(CoreEventKind.UnitSlain, EventOrder.Hand, OnSlain);
            // 퇴근은 **소멸보다 먼저** 배달된다(`PlacementService` 가 그 순서로 발행한다) —
            // 그래야 「인수인계」가 소멸 경로에 먹히지 않는다.
            _bus.Subscribe(CoreEventKind.Retired, EventOrder.Hand, e => Recover(e.A, retired: true, e.Tick));
            // 표식 붙은 적의 소멸(처치 · 유출)도 여기로 온다 — 숙주가 적일 뿐 같은 회수다(보상 차이는 처치 사건의 배율).
            _bus.Subscribe(CoreEventKind.UnitDestroyed, EventOrder.Hand, e => Recover(e.A, retired: false, e.Tick));
        }

        /// <summary>unit 7b — 조립 시점(`BattleMatch`). 규칙 레이어가 이 담당자보다 늦게 선다.</summary>
        internal void Bind(BindingRegistry registry, TriggerDispatcher triggers, Map.MapRuntime map)
        {
            _registry = registry;
            _triggers = triggers;
            _map = map;
        }

        public float Gauge => _gauge;
        public float GaugeMax => _gaugeMax;

        /// <summary>상한에 막혀 **소멸한** 각성의 누계. 화면이 그 손실을 알리는 근거다(D6).</summary>
        public float OverflowLost => _overflowLost;

        public int QueueCount => _queue.Count;
        public int OutOfPoolCount => _outOfPool.Count;
        public int AttachedCount => _attachedTo.Count;
        public int HandSize => _handSize;
        public int AttachCap => _attachCap;

        /// <summary>진단 통로. 연결하지 않으면 버려진다 — 코어는 로거를 소유하지 않는다.</summary>
        public System.Action<string> Report;

        /// <summary>
        /// 판 경계. 덱을 **매번 새로 구성한다**(캐시 없음 — D1). 섞기는 여기 한 번뿐이고
        /// 시드는 판 시드 그대로다.
        ///
        /// ⚠ 옛 구현은 `System.Random` 으로 섞었다. 계약 5 가 그것을 금지하므로(구현이
        /// 플랫폼·런타임 버전에 매여 결정론 보장이 없다) xorshift 로 바꿨고, 그래서 **같은
        /// 시드의 순열이 라이브와 다르다.** 규칙(「한 곳에서 한 번, 판 시드로」)은 보존됐고
        /// 밸런스도 골든도 특정 순열에 기대지 않는다.
        /// </summary>
        public void Begin(int[] cardIndices, int seed, in AwakeningDef awakening,
                          int handSize, int attachCap)
        {
            _queue.Clear();
            _outOfPool.Clear();
            _attachedTo.Clear();
            _cooldowns.Clear();
            _attachSeq = 0;
            _overflowLost = 0f;

            _gaugeMax = math.max(0f, awakening.Max);
            _gauge = math.clamp(awakening.Start, 0f, _gaugeMax);
            _handSize = math.max(0, handSize);
            _attachCap = math.max(0, attachCap);

            // `cardIndices` 가 null 이면 **정의표의 카드 배열 그대로**가 덱이다 — 그 배열의
            // 순서가 곧 구성 순서(저장 부착 10 + 공용 액티브 2)라서 별도 목록을 두면 둘이 갈린다.
            if (cardIndices == null)
            {
                for (int i = 0; i < _def.Cards.Length; i++)
                    _queue.Add(new Entry { EntryId = _queue.Count, CardIndex = i });
            }
            else
            {
                for (int i = 0; i < cardIndices.Length; i++)
                {
                    int c = cardIndices[i];
                    if (c < 0 || c >= _def.Cards.Length) continue;
                    _queue.Add(new Entry { EntryId = _queue.Count, CardIndex = c });
                }
            }

            // Fisher-Yates(앞으로).
            var rng = new Unity.Mathematics.Random(seed != 0 ? (uint)math.abs(seed) : 1u);
            for (int i = 0; i < _queue.Count - 1; i++)
            {
                int j = i + rng.NextInt(0, _queue.Count - i);
                var tmp = _queue[i];
                _queue[i] = _queue[j];
                _queue[j] = tmp;
            }
        }

        /// <summary>손패 = 큐 **앞 N**. 남은 것이 N 보다 적으면 그만큼만(빈 칸은 뷰의 몫).</summary>
        public void Hand(List<Entry> into)
        {
            into.Clear();
            int take = math.min(_handSize, _queue.Count);
            for (int i = 0; i < take; i++) into.Add(_queue[i]);
        }

        public int CountAttachedTo(SimEntityId host)
        {
            int n = 0;
            foreach (var kv in _attachedTo)
                if (kv.Value.Host == host) n++;
            return n;
        }

        public bool CanAttachMore(SimEntityId host) => CountAttachedTo(host) < _attachCap;

        /// <summary>액티브가 준비됐나. **기록 자체가 없으면 준비된 것**이다(K1).</summary>
        public bool IsReady(int cardIndex)
            => !_cooldowns.TryGetValue(cardIndex, out int left) || left <= 0;

        /// <summary>남은 비율 1→0. 쿨다운 0 짜리는 항상 0(K4).</summary>
        public float CooldownNormalized(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= _def.Cards.Length) return 0f;
            int total = MatchClock.TicksOf(_def.Cards[cardIndex].CooldownSeconds, BattleMatch.Dt);
            if (total <= 0) return 0f;
            return _cooldowns.TryGetValue(cardIndex, out int left)
                ? math.saturate(left / (float)total)
                : 0f;
        }

        // ── 틱 ───────────────────────────────────────────────────────────────

        /// <summary>
        /// 액티브 재사용 대기를 **판의 시계**로 잰다(사용자 답 대기 중의 기본값 — spec 구현 10).
        /// 옛 구현은 벽시계였고 그것이 「감속 중에도 쿨다운이 돈다」였다. 고정 틱에서는
        /// 가만두면 판의 시계로 옮겨가므로, **바꾼 쪽이 기본값**임을 여기 적어 둔다.
        /// 답이 다르면 이 함수 하나가 바뀐다.
        /// </summary>
        public void Run(TickContext ctx)
        {
            if (_cooldowns.Count == 0) return;
            _cooldownKeys.Clear();
            foreach (var k in _cooldowns.Keys) _cooldownKeys.Add(k);
            for (int i = 0; i < _cooldownKeys.Count; i++)
            {
                int k = _cooldownKeys[i];
                int left = _cooldowns[k] - 1;
                if (left <= 0) _cooldowns.Remove(k);
                else _cooldowns[k] = left;
            }
        }

        // ── 쓰기 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 부착. 순서가 규칙이다(D12): **① 적용 → ② 차감 → ③ 순환.** 실패한 부착은 차감도 순환도 하지 않는다(D11).
        /// ① 은 판정(`CardBindings.Plan`) → 규칙 부착 → 부착 즉시 발동을 `Immediate` seam 에 줄 세우는 데까지이고,
        /// 그 드레인은 이 커맨드의 콜스택(`CommandPhase.Execute`)이 **이 함수가 돌아간 직후** 한다 — 틱을 기다리지
        /// 않는다(큐에 넣고 틱을 기다리면 소모 뒤에 실행이 도착한다). 적용 성사 여부는 판정이 이미 정했다
        /// (옛 `attached == 0` 도 실행 전 판정이었다).
        /// </summary>
        public Receipt TryAttach(int entryId, SimEntityId host, int tick)
        {
            int index = IndexInHand(entryId);
            if (index < 0) return Receipt.Reject(RejectReason.CardNotInHand);

            var entry = _queue[index];
            ref var card = ref _def.Cards[entry.CardIndex];
            if (card.Kind != CardKind.Attach) return Receipt.Reject(RejectReason.WrongCardKind);

            var u = _world.Find(host);
            if (u == null) return Receipt.Reject(RejectReason.NoSuchEntity);
            // 상한은 방어유닛 손패 규칙이다 — 적을 겨누는 표식은 그 밖이다(D14). Unit·Squad 는 **같이** 센다(9-1).
            if (!card.TargetsEnemies && !CanAttachMore(host)) return Receipt.Reject(RejectReason.AttachCapReached);
            if (_gauge < card.Cost) return Receipt.Reject(RejectReason.InsufficientAwakening);

            // ① 적용.
            var reason = Apply(entry.CardIndex, u, entryId, tick, out var group);
            if (reason != RejectReason.None) return Receipt.Reject(reason);

            // ② 지불.
            Spend(card.Cost);

            // ③ 풀에서 이탈 + 등록. **부착한 순서 자체가 기능이다**(D24) — 인수인계가 그 순서를 그대로 손패에 싣는다.
            _queue.RemoveAt(index);
            _outOfPool[entryId] = entry;
            _attachedTo[entryId] = new Attachment { Host = host, Seq = _attachSeq++, Group = group };
            return Receipt.Ok;
        }

        /// <summary>
        /// 시전. 준비 확인 · 성사 · 차감 · 재사용 대기 · **덱 뒤로 재활용**이 **한 함수**다(K2 · D17) — 옛 구현은
        /// 「준비 확인은 호출부 책임」이라 호출부 둘이 짝을 맞춰야 했다.
        /// ⚠ **대기는 판의 시계**다(사용자 확정 2026-09-23 — 틱으로 센다). 옛 `SkillRuntime` 은 벽시계로 빼서
        /// 슬로모에 안 느려졌다 — 새 코어에서는 느려진다(규칙 변경 · 7b 이식 제외 표).
        /// </summary>
        public Receipt TryCast(int entryId, int2 cellA, int2 cellB, bool hasCellB, int tick)
        {
            int index = IndexInHand(entryId);
            if (index < 0) return Receipt.Reject(RejectReason.CardNotInHand);

            var entry = _queue[index];
            ref var card = ref _def.Cards[entry.CardIndex];
            if (card.Kind != CardKind.Active) return Receipt.Reject(RejectReason.WrongCardKind);
            if (!IsReady(entry.CardIndex)) return Receipt.Reject(RejectReason.CardOnCooldown);
            if (_gauge < card.Cost) return Receipt.Reject(RejectReason.InsufficientAwakening);

            var reason = Cast(entry.CardIndex, entryId, cellA, cellB, hasCellB, tick);
            if (reason != RejectReason.None) return Receipt.Reject(reason);

            Spend(card.Cost);
            int cdTicks = MatchClock.TicksOf(card.CooldownSeconds, BattleMatch.Dt);
            if (cdTicks > 0) _cooldowns[entry.CardIndex] = cdTicks;
            _queue.RemoveAt(index);
            _queue.Add(entry);                       // 뒤로 재활용
            return Receipt.Ok;
        }

        /// <summary>하위 호환 — 칸 없는 시전(칸을 안 쓰는 액티브 · unit 4 테스트).</summary>
        public Receipt TryCast(int entryId, int tick) => TryCast(entryId, int2.zero, int2.zero, false, tick);

        // ── unit 7b — 규칙 레이어로 넘기는 두 문(효과는 여기 없다) ────────────────────

        private RejectReason Apply(int cardIndex, Unit host, int entryId, int tick, out CardAttachment group)
        {
            group = null;
            var reason = CardBindings.Plan(_def, cardIndex, host, _planRows, _planSquad, _planMods);
            if (reason != RejectReason.None) return reason;
            if (_registry == null)
            {
                // 조립 밖(단위 테스트가 담당자만 세운 경우) — 조용히 지나가지 않는다(C4).
                Report?.Invoke($"[HandDeck] 규칙 등록부가 없다 — '{_def.Cards[cardIndex].Id}' 의 규칙을 붙이지 않는다.");
                return RejectReason.None;
            }
            group = _registry.AttachCard(host, entryId, cardIndex, _planRows, _planSquad, _planMods, BattleMatch.PlayerFaction, tick);
            CardBindings.FireOnAttach(_triggers, _world, _def, group, host);
            return RejectReason.None;
        }

        private RejectReason Cast(int cardIndex, int entryId, int2 cellA, int2 cellB, bool hasCellB, int tick)
        {
            ref var card = ref _def.Cards[cardIndex];
            // 전투 중에만(옛 `CastSkillAtTile` 의 `!_running` 거절 — 배치 창에서는 시전이 안 된다).
            if (_clock != null && _clock.Phase != MatchPhase.Battle) return RejectReason.NotRunningOrPlacementClosed;
            int row = card.ActiveBinding;
            // ⚠ struct 기본값 0 은 유효 줄이라(S4) 「그 줄이 카드 규칙인가」까지 본다 — 아니면 bake 가 거절한 카드다.
            if (row < 0 || row >= _def.Bindings.Length || _def.Bindings[row].Origin != BindingOrigin.Card)
                return RejectReason.Unclassified;
            if (card.NeedsTwoCells)
            {
                if (!hasCellB) return RejectReason.NeedsSecondCell;
                // 두 칸 조준의 규칙 — **같은 칸이면 거절**(같은 칸을 잇는 포탈은 반경 안 적을 매 틱 되돌리는 정지장이 된다).
                if (cellA.Equals(cellB)) return RejectReason.SameCell;
            }
            else { cellB = cellA; hasCellB = false; }
            if (_registry == null || _triggers == null)
            {
                Report?.Invoke($"[HandDeck] 규칙 레이어가 없다 — '{card.Id}' 시전을 실행하지 않는다.");
                return RejectReason.None;
            }
            var b = _registry.AttachCast(row, entryId, cardIndex, CenterOf(cellA), CenterOf(cellB), BattleMatch.PlayerFaction, tick);
            if (b == null) return RejectReason.Unclassified;
            var e = CardBindings.CastEvent(b, cellA, cellB, hasCellB);
            _triggers.RaiseFor(b, in e);
            return RejectReason.None;
        }

        private float3 CenterOf(int2 c) => _map != null ? _map.CenterOf(c) : new float3(c.x, 0f, c.y);

        /// <summary>
        /// 조준 preflight(7c — 리티클·링 색). **커밋과 같은 판정 함수**를 부른다(`CardBindings.Plan`) — 상한·각성은
        /// 따로 본다(옛 `WouldDreamcatcherCardApply` 도 부착 가능성만 답했다). 쓰기가 없다.
        /// </summary>
        public RejectReason WouldAttach(int cardIndex, SimEntityId host)
        {
            var u = _world.Find(host);
            var r = CardBindings.Plan(_def, cardIndex, u, _planRows, _planSquad, _planMods);
            if (r != RejectReason.None) return r;
            if (!_def.Cards[cardIndex].TargetsEnemies && !CanAttachMore(host)) return RejectReason.AttachCapReached;
            return RejectReason.None;
        }

        /// <summary>
        /// 7c — 손패 카드의 **쓸 수 있나** preflight(딤·드래그 게이트·거절 문구). 부착·시전 **공통 앞단**만 답한다 —
        /// 손패에 있나 · (액티브) 대기 · 각성 값. 숙주에 달린 것(상한·적용성)은 `WouldAttach` 가, 국면·칸은 시전 커맨드가
        /// 답한다. 순서는 `TryAttach`/`TryCast` 의 앞단과 같다(대기 &gt; 각성) — 뷰가 자기 셈(`gauge &gt;= cost`)을 들면
        /// 딤과 거절이 갈린다. 쓰기가 없다.
        /// </summary>
        public RejectReason UsableReason(int entryId)
        {
            int index = IndexInHand(entryId);
            if (index < 0) return RejectReason.CardNotInHand;
            int cardIndex = _queue[index].CardIndex;
            ref var card = ref _def.Cards[cardIndex];
            if (card.Kind == CardKind.Active && !IsReady(cardIndex)) return RejectReason.CardOnCooldown;
            if (_gauge < card.Cost) return RejectReason.InsufficientAwakening;
            return RejectReason.None;
        }

        /// <summary>그 카드가 적을 겨누나(7c 조준 라우팅 — 뷰가 메커닉을 뒤져 추측하지 않게).</summary>
        public bool TargetsEnemies(int cardIndex)
            => cardIndex >= 0 && cardIndex < _def.Cards.Length && _def.Cards[cardIndex].TargetsEnemies;

        /// <summary>
        /// 디버그 부착(`CommandKind.DebugAttachCard`) — 손패·각성·상한을 건너뛰고 **판정은 지난다**. 숙주가 떠나면 떨어지고
        /// 큐로는 안 돌아온다(덱에 없던 카드다).
        /// </summary>
        public Receipt DebugAttach(int cardIndex, SimEntityId host, int tick)
        {
            if (cardIndex < 0 || cardIndex >= _def.Cards.Length) return Receipt.Reject(RejectReason.CardNotInHand);
            var u = _world.Find(host);
            if (u == null) return Receipt.Reject(RejectReason.NoSuchEntity);
            int entryId = -(++_debugSeq);
            var reason = Apply(cardIndex, u, entryId, tick, out var group);
            if (reason != RejectReason.None) return Receipt.Reject(reason);
            _attachedTo[entryId] = new Attachment { Host = host, Seq = _attachSeq++, Group = group };
            return Receipt.Ok;
        }

        /// <summary>디버그 시전(`CommandKind.DebugCastCard`) — 손패·각성·재사용 대기를 건너뛴다.</summary>
        public Receipt DebugCast(int cardIndex, int2 cellA, int2 cellB, bool hasCellB, int tick)
        {
            if (cardIndex < 0 || cardIndex >= _def.Cards.Length) return Receipt.Reject(RejectReason.CardNotInHand);
            if (_def.Cards[cardIndex].Kind != CardKind.Active) return Receipt.Reject(RejectReason.WrongCardKind);
            var reason = Cast(cardIndex, -(++_debugSeq), cellA, cellB, hasCellB, tick);
            return reason == RejectReason.None ? Receipt.Ok : Receipt.Reject(reason);
        }

        /// <summary>「쿨다운 감소」 효과는 **모든 스킬에 일괄** 적용되고 0 이하는 지워진다(K3).</summary>
        public void ReduceAllCooldowns(float seconds)
        {
            if (seconds <= 0f || _cooldowns.Count == 0) return;
            int ticks = MatchClock.TicksOf(seconds, BattleMatch.Dt);
            if (ticks <= 0) return;
            _cooldownKeys.Clear();
            foreach (var k in _cooldowns.Keys) _cooldownKeys.Add(k);
            for (int i = 0; i < _cooldownKeys.Count; i++)
            {
                int k = _cooldownKeys[i];
                int left = _cooldowns[k] - ticks;
                if (left <= 0) _cooldowns.Remove(k);
                else _cooldowns[k] = left;
            }
        }

        // ── 회수 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 숙주가 판을 떠났다. 붙어 있던 카드가 **전부** 큐로 돌아온다. 기본은 **맨 뒤**
        /// (떠난 순서 = 돌아오는 순서). 예외 하나: **퇴근**이고 그 숙주에 「인수인계」 카드가
        /// 있었으면 나머지가 부착 순서 그대로 큐 **맨 앞**으로 온다(선언한 카드 자신은 맨 뒤).
        /// </summary>
        private void Recover(SimEntityId host, bool retired, int tick)
        {
            if (_attachedTo.Count == 0) return;

            _recoverScratch.Clear();
            foreach (var kv in _attachedTo)
                if (kv.Value.Host == host) _recoverScratch.Add(kv.Key);
            if (_recoverScratch.Count == 0) return;

            // 부착 순서 오름차순. 앞으로 보낼 때 이 순서가 곧 손패 순서다.
            _recoverScratch.Sort(CompareByAttachSeq);

            bool recall = false;
            if (retired)
                for (int i = 0; i < _recoverScratch.Count; i++)
                    if (DeclaresRecall(_recoverScratch[i])) { recall = true; break; }

            _frontScratch.Clear();
            for (int i = 0; i < _recoverScratch.Count; i++)
            {
                int entryId = _recoverScratch[i];
                bool declares = DeclaresRecall(entryId);
                // unit 7b — 카드의 규칙을 뗀다(숙주 소멸로 이미 떨어졌으면 사건만 — `CardDetached`). 회수 = 슬롯 삭제는
                // 규칙 쪽(`RevokeOnExpire`)이 이미 했다.
                if (_registry != null) _registry.DetachCard(_attachedTo[entryId].Group, tick);
                _attachedTo.Remove(entryId);
                if (!_outOfPool.TryGetValue(entryId, out var entry)) continue;
                _outOfPool.Remove(entryId);

                // 앞으로 가는 것: 앞당김이 켜져 있고 **선언 카드 자신이 아닐** 때.
                // 선언 카드는 맨 뒤로 간다 — 자기가 연 문으로 자기가 먼저 들어가지 않는다.
                if (recall && !declares) _frontScratch.Add(entry);
                else _queue.Add(entry);
            }

            // 준 순서 그대로 0,1,2… 에 꽂는다. **큐에서 남의 인덱스를 미는 유일한 연산**이라
            // 플레이어가 누른 퇴근에만 도달한다.
            for (int i = 0; i < _frontScratch.Count; i++) _queue.Insert(i, _frontScratch[i]);
            _frontScratch.Clear();
        }

        private int CompareByAttachSeq(int a, int b)
            => _attachedTo[a].Seq.CompareTo(_attachedTo[b].Seq);

        // 「인수인계」인가. 옛 전투는 이 판정이 **두 곳**(손패 컨트롤러 ↔ 브리지 부착
        // 화이트리스트)에 있어 한쪽만 넓히면 「붙는데 무효」나 「검증 없이 발동」이 됐다(D10).
        // 여기서는 저작 한 칸(`CardDef.DeclaresRetireRecall`)이 곧 판정이다.
        private bool DeclaresRecall(int entryId)
        {
            if (!_outOfPool.TryGetValue(entryId, out var e)) return false;
            return e.CardIndex >= 0 && e.CardIndex < _def.Cards.Length
                   && _def.Cards[e.CardIndex].DeclaresRetireRecall;
        }

        // ── 각성 ─────────────────────────────────────────────────────────────

        // ⚠ **개체를 되묻지 않는다.** 정의표 줄은 사건이 값으로 실어 온다(`CoreEvent.DefIndex`) —
        // 처치 사건의 대상은 곧 사라지는 개체라, 드레인 시점에 되물으면 소멸이 한 틱 당겨지는
        // 날 각성 보상이 조용히 0 이 된다. 어느 표를 보는지는 **진영**이 정한다.
        private void OnSlain(CoreEvent e)
        {
            float reward = 0f;
            int i = e.DefIndex;
            if (e.Faction == Faction.EnemyUnit)
            {
                if (i >= 0 && i < _def.Enemies.Length) reward = _def.Enemies[i].AwakeningReward;
            }
            else if (e.Faction == Faction.DefenderUnit)
            {
                if (i >= 0 && i < _def.Units.Length) reward = _def.Units[i].AwakeningReward;
            }
            // unit 7b — 살찌운 제물: 표식된 적은 **보상 배율**을 값으로 싣고 온다(S16 — 양이 아니라 배율).
            // 마음 회복(`HeartMeter`)은 이 배율을 안 본다 — SO 원값이 정본이다(표식 두 축 겸직 금지).
            float mul = e.RewardMul > 0f ? e.RewardMul : 1f;
            Gain(reward * mul);
        }

        /// <summary>
        /// 각성 획득. 상한을 넘은 만큼은 **소멸한다**(D6) — 그리고 그 손실을 세어 둔다.
        /// 화면이 「넘쳤다」를 알릴 근거가 없으면 플레이어는 그냥 안 받은 줄 안다.
        /// </summary>
        public float Gain(float reward)
        {
            if (reward <= 0f) return 0f;
            float next = math.min(_gauge + reward, _gaugeMax);
            float applied = next - _gauge;
            if (applied < reward) _overflowLost += reward - applied;
            _gauge = next;
            return applied;
        }

        /// <summary>값 지불. 각성에서 깎고 **0 밑으로 내려가지 않는다**(D18).</summary>
        private void Spend(int cost) => _gauge = math.max(0f, _gauge - cost);

        private int IndexInHand(int entryId)
        {
            int take = math.min(_handSize, _queue.Count);
            for (int i = 0; i < take; i++)
                if (_queue[i].EntryId == entryId) return i;
            return -1;
        }
    }
}
