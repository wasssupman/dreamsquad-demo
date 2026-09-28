using System.Collections.Generic;
using Wassup.Skills;

namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7a — **누가 어떤 규칙을 들고 있나**의 등록부.
    //
    // 소유자별 목록(`Unit.Bindings`) + Match 목록 하나. 순회는 언제나 `InstanceId` 오름차순이다 —
    // 부착 순으로 append 하고 id 가 단조 증가하므로 목록 자체가 정렬돼 있다(결정론, 계약 5).
    //
    // 매니저가 아니다: 판정은 하지 않는다(발동 여부는 `TriggerDispatcher`, 실행은 concrete). 여기가
    // 하는 일은 붙이기·떼기·수명·「떨어졌다」 사건 셋뿐이다.
    //
    // 상한(rev 3 §6): 바인딩/유닛 ≤ 8 · Match 바인딩 ≤ 16. **할당 상한이지 밸런스 값이 아니다** —
    // 넘으면 조용히 버리지 않고 `Report` 로 말한다.
    public sealed class BindingRegistry
    {
        /// <summary>한 유닛이 동시에 드는 규칙 상한(rev 3 §6 할당 상한 — 라이브 최다 4 = 짱쎈).</summary>
        public const int MaxPerUnit = 8;
        /// <summary>판 호스트 규칙 상한(rev 3 §6).</summary>
        public const int MaxMatch = 16;

        private readonly EventBus _bus;
        private readonly MatchDefinition _def;
        private BattleWorld _world;

        private readonly List<Binding> _match = new List<Binding>(4);
        private readonly List<Effects.ModifierSlot> _revoked = new List<Effects.ModifierSlot>(4);
        private int _nextInstanceId = 1;
        private int _seq;

        public System.Action<string> Report;

        public BindingRegistry(EventBus bus, MatchDefinition def)
        {
            _bus = bus;
            _def = def;
        }

        internal void Bind(BattleWorld world) => _world = world;

        public IReadOnlyList<Binding> MatchBindings => _match;

        /// <summary>다음에 발급할 id. 진단·테스트용(단조 증가 증언).</summary>
        public int PeekNextInstanceId => _nextInstanceId;

        // ── 붙이기 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 저작 규칙을 붙인다 — `BattleWorld.Spawn` 이 부른다(스폰 경로가 몇이든 **한 자리**).
        /// 줄은 종류가 고른다: 방어유닛·순찰 = `Units` · 적 = `Enemies`.
        /// </summary>
        internal void AttachAuthored(Unit u, int tick)
        {
            int[] rows = AuthoredRowsOf(u);
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                int r = rows[i];
                if (r < 0 || r >= _def.Bindings.Length) { Warn($"[Binding] 유닛 {u.Id} 의 규칙 줄 {r} 이 표 밖이다 — 건너뛴다."); continue; }
                Attach(u, in _def.Bindings[r], r, tick);
            }
        }

        private int[] AuthoredRowsOf(Unit u)
        {
            if (u.DefIndex < 0) return null;
            switch (u.Kind)
            {
                case UnitKind.Defender:
                case UnitKind.Patrol:
                    return u.DefIndex < _def.Units.Length ? _def.Units[u.DefIndex].Bindings : null;
                case UnitKind.Enemy:
                    return u.DefIndex < _def.Enemies.Length ? _def.Enemies[u.DefIndex].Bindings : null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 규칙 하나를 붙인다. `owner == null` 이면 판 호스트(`SimEntityId.Match`)에 붙는다.
        /// 상한을 넘으면 null + `Report`(조용한 폐기 금지).
        /// </summary>
        public Binding Attach(Unit owner, in BindingDef def, int defIndex, int tick)
        {
            var list = owner != null ? owner.Bindings : _match;
            int cap = owner != null ? MaxPerUnit : MaxMatch;
            if (list.Count >= cap)
            {
                Warn($"[Binding] {(owner != null ? owner.Id.ToString() : "판")} 의 규칙이 상한 {cap} 에 찼다 — '{def.Label}' 를 붙이지 않는다.");
                return null;
            }
            var b = new Binding
            {
                Def = def,
                Effect = _def != null ? _def.EffectOf(in def) : EffectDef.Default(),
                DefIndex = defIndex,
                Owner = owner != null ? owner.Id : SimEntityId.Match,
                InstanceId = _nextInstanceId++,
                Seq = _seq++,
                MaxHpRef = owner != null ? owner.MaxHealth : 0f,
                Remaining = def.Lifetime == BindingLifetime.Timed ? def.LifetimeSeconds : 0f,
            };
            list.Add(b);
            _bus.Publish(CoreEvent.BindingAttached(tick, b, owner));
            return b;
        }

        // ── 떼기 ─────────────────────────────────────────────────────────────

        /// <summary>그 소유자가 판에서 사라진다 — 규칙 전부가 떨어진다. `BattleWorld.Destroy` 가 **리셋 전에** 부른다.</summary>
        internal void OnOwnerRemoved(Unit u, int tick)
        {
            var list = u.Bindings;
            for (int i = 0; i < list.Count; i++) DetachCore(list[i], BindingDetachReason.OwnerRemoved, tick, u);
            list.Clear();
        }

        /// <summary>규칙 하나를 뗀다. 반환 = 떼었나.</summary>
        public bool Detach(Binding b, BindingDetachReason reason, int tick)
        {
            if (b == null || b.Detached) return false;
            var owner = _world != null ? _world.Find(b.Owner) : null;
            var list = owner != null ? owner.Bindings : _match;
            if (!list.Remove(b)) return false;
            DetachCore(b, reason, tick, owner);
            return true;
        }

        private void DetachCore(Binding b, BindingDetachReason reason, int tick, Unit owner)
        {
            if (b.Detached) return;
            b.Detached = true;
            // 소급 회수(H6) — 이 규칙이 건 스탯 칸을 **슬롯 삭제**로 지운다(6a 의 회수 축). 칸 판별자는
            // `InstanceId` 라 다른 규칙의 칸을 건드리지 않는다.
            if (b.Def.RevokeOnExpire && _world != null)
            {
                var units = _world.Units;
                for (int i = 0; i < units.Count; i++)
                {
                    _revoked.Clear();
                    if (units[i].Modifiers.RevokeTag(Effects.SlotKind.BindingInstance, b.InstanceId, _revoked) == 0) continue;
                    for (int k = 0; k < _revoked.Count; k++)
                        _bus.Publish(CoreEvent.ModifierRevoked(tick, units[i], _revoked[k].Key.Source, _revoked[k].Key.Stat));
                }
            }
            _bus.Publish(CoreEvent.BindingDetached(tick, b, owner, reason));
        }

        // ── 수명 ─────────────────────────────────────────────────────────────

        /// <summary>`Timed` 수명을 깎는다. 판의 시계(틱) 기준 — `TriggerDispatcher` 가 주기 seam 에서 부른다.</summary>
        internal void StepLifetimes(float dt, int tick)
        {
            if (_world == null) return;
            var units = _world.Units;
            for (int i = 0; i < units.Count; i++) StepList(units[i].Bindings, dt, tick, units[i]);
            StepList(_match, dt, tick, null);
        }

        private void StepList(List<Binding> list, float dt, int tick, Unit owner)
        {
            for (int i = 0; i < list.Count;)
            {
                var b = list[i];
                if (b.Def.Lifetime != BindingLifetime.Timed) { i++; continue; }
                b.Remaining -= dt;
                if (b.Remaining > 0f) { i++; continue; }
                list.RemoveAt(i);
                DetachCore(b, BindingDetachReason.Expired, tick, owner);
            }
        }

        // ── 카드 묶음(7b) ─────────────────────────────────────────────────────
        //
        // 카드 한 장 = 규칙 여러 줄 + 공격 수식자 몇 개. **떼는 단위가 카드**라서 묶음(`CardAttachment`)으로 든다 —
        // 손패 담당자는 이 핸들 하나만 쥐고, 효과가 무엇인지 모른다(`HandDeck` 에 효과가 0 줄인 이유).
        // 핸들은 `InstanceId` 와 같은 번호판에서 뗀다: 판 수명 단조 · 재사용 없음(F1).

        /// <summary>
        /// 카드 한 장을 숙주에 붙인다(행과 수식자는 `CardBindings.Plan` 이 이미 골랐다). 사건 `CardAttached` 1건 +
        /// 규칙마다 `BindingAttached`. 반환 = 묶음(떼기의 핸들).
        /// </summary>
        public CardAttachment AttachCard(Unit host, int entryId, int cardIndex,
                                         List<int> rows, List<int> squadRows, List<Combat.AttackModDef> mods, int tick)
        {
            var att = new CardAttachment
            {
                Handle = _nextInstanceId++,
                EntryId = entryId,
                CardIndex = cardIndex,
                Host = host.Id,
            };
            AttachRows(host, rows, att.Bindings, tick);
            AttachRows(host, squadRows, att.SquadBindings, tick);
            if (mods != null && host.Attack != null)
                for (int i = 0; i < mods.Count; i++)
                {
                    var m = new Combat.AttackModState { Def = mods[i], OwnerInstanceId = att.Handle };
                    host.Attack.Mods.Add(m);
                    att.Mods.Add(m);
                }
            _bus.Publish(CoreEvent.CardAttached(tick, host.Id, host, entryId, cardIndex, att.Handle));
            return att;
        }

        private void AttachRows(Unit host, List<int> rows, List<Binding> into, int tick)
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Count; i++)
            {
                int r = rows[i];
                if (r < 0 || r >= _def.Bindings.Length) { Warn($"[Binding] 카드 규칙 줄 {r} 이 표 밖이다 — 건너뛴다."); continue; }
                var b = Attach(host, in _def.Bindings[r], r, tick);
                if (b == null) continue;
                // **카드의 주기 규칙은 붙는 순간 첫 발동한다**(사용자 결정 2026-08-16 — 옛 `elapsed = periodSeconds`).
                // 카드는 전투 중에 붙는다 — 붙이자마자 주기만큼 아무 일도 없으면 「안 붙었다」로 읽힌다. 유닛 저작
                // 스킬(스폰과 함께 시작)은 이 줄을 안 탄다.
                if (b.Def.Trigger == TriggerKind.PeriodicTimer) b.Elapsed = b.Def.PeriodSeconds;
                // 호접몽 완주 버프의 칸 판별자 = 그 규칙의 `InstanceId`(옛 `_dcStackCounter++` — 붙일 때마다 새 칸).
                if (b.Effect.Kind == EffectKind.DreamCocoon) b.Def.StackId = b.InstanceId;
                into.Add(b);
            }
        }

        /// <summary>
        /// 카드 한 장을 뗀다(숙주가 떠났다 · 표식 대상이 사라졌다). 남은 규칙을 떼고(이미 소유자 소멸로 떨어졌으면 무동작),
        /// 수식자를 걷고, `CardDetached` 1건. 두 번 불러도 한 번만 일어난다.
        /// </summary>
        public void DetachCard(CardAttachment att, int tick)
        {
            if (att == null || att.Detached) return;
            att.Detached = true;
            for (int i = 0; i < att.Bindings.Count; i++) Detach(att.Bindings[i], BindingDetachReason.Manual, tick);
            for (int i = 0; i < att.SquadBindings.Count; i++) Detach(att.SquadBindings[i], BindingDetachReason.Manual, tick);
            var host = _world != null ? _world.Find(att.Host) : null;
            if (host?.Attack != null)
                for (int i = 0; i < att.Mods.Count; i++) host.Attack.Mods.Remove(att.Mods[i]);
            _bus.Publish(CoreEvent.CardDetached(tick, att.Host, host, att.EntryId, att.CardIndex, att.Handle));
        }

        /// <summary>
        /// 액티브 시전 — 판 호스트에 규칙 하나를 **발동 1회 수명**으로 붙인다(시전자가 없다 — 진영은 플레이어로 접힌다).
        /// 드레인이 발동하면 `FireCapReached` 로 떨어진다. 사건 `CardCast` 1건.
        /// </summary>
        public Binding AttachCast(int row, int entryId, int cardIndex, Unity.Mathematics.float3 cellA,
                                  Unity.Mathematics.float3 cellB, int tick)
        {
            if (row < 0 || row >= _def.Bindings.Length) { Warn($"[Binding] 액티브 규칙 줄 {row} 이 표 밖이다."); return null; }
            var b = Attach(null, in _def.Bindings[row], row, tick);
            if (b == null) return null;
            b.Def.FireCap = 1;
            b.Def.Lifetime = BindingLifetime.UntilFireCap;
            _bus.Publish(CoreEvent.CardCast(tick, entryId, cardIndex, cellA, cellB, b.InstanceId));
            return b;
        }

        /// <summary>판 호스트의 판 수명 규칙(드림스톤 — 판 진입 장비). `BattleMatch.Begin` 이 한 번 부른다.</summary>
        internal void AttachMatchRows(int[] rows, int tick)
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                int r = rows[i];
                if (r < 0 || r >= _def.Bindings.Length) { Warn($"[Binding] 판 규칙 줄 {r} 이 표 밖이다 — 건너뛴다."); continue; }
                Attach(null, in _def.Bindings[r], r, tick);
            }
        }

        private void Warn(string msg) => Report?.Invoke(msg);
    }

    /// <summary>
    /// 붙어 있는 카드 한 장 = 규칙 몇 줄 + 공격 수식자 몇 개. 개체가 아니다 — 숙주의 등록부 항목 묶음이다.
    /// 손패 담당자는 이것을 **핸들로만** 쥔다(안을 들여다보면 그것이 효과를 아는 첫 줄이 된다).
    /// </summary>
    public sealed class CardAttachment
    {
        public int Handle;
        public int EntryId;
        public int CardIndex;
        public SimEntityId Host;
        public bool Detached;
        public readonly List<Binding> Bindings = new List<Binding>(2);
        /// <summary>Squad 스탯 줄 — 붙는 순간 판 위 해당 유닛 전원에게 한 번씩, 이후 배치분은 상속.</summary>
        public readonly List<Binding> SquadBindings = new List<Binding>(1);
        public readonly List<Combat.AttackModState> Mods = new List<Combat.AttackModState>(1);
    }
}
