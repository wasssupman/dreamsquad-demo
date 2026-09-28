using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.Skills;

namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7a — seam 훅에 붙는 **유일한 핸들러**. 「무슨 일이 일어나면 무엇이 터지나」의
    // 레일이다.
    //
    // 일은 셋이다:
    //   ① **들은다** — 감지자(단계)가 사실을 값 스냅샷으로 올리면(`Raise*`), 그 사건을 듣는 규칙을 고르고
    //      카운터·게이트를 그 자리에서 판정해 발동을 줄 세운다. 주기·경계는 상태 폴링이라 자기 seam 에서
    //      직접 센다(`DetectPeriodic`·`DetectThresholds`).
    //   ② **줄 세운다** — seam 마다 한 줄. 잔여 규칙 = 「후속 seam 이면 같은 틱, 지난 seam 이면 다음 틱」이고
    //      그 판정은 `SeamTickOrder` 로만 한다(enum 값 비교 0).
    //   ③ **드레인한다** — 전순서 (세대, 생산 순번) → 한 사건 안에서는 (소유자 id, `InstanceId`) 오름차순.
    //      실행은 concrete 가 하고 쓰기는 `IntentApplier` 하나를 지난다.
    //
    // 연쇄(정정 6 · H5): 세대 BFS 는 **드레인 안에서의 바인딩→바인딩 직접 재진입**에만 적용한다(오늘 0건 —
    // concrete 는 `Emit` 만 하고 사건 생산은 감지자뿐이다). 시체 폭발 → 처치 → 잿불 같은 intent 경유 연쇄는
    // 투사체·피해를 지나 **각자의 phase 에서** 돈다 — 한 틱으로 접으면 반경 멤버십과 연출 리플이 바뀐다.
    // 깊이 예산 4, 초과는 `Report`(조용한 폐기 금지).
    //
    // 매니저가 아니다: 상태는 규칙의 것(`Binding`)이고, 여기는 순서만 안다.
    public sealed class TriggerDispatcher
    {
        /// <summary>직접 재진입 깊이 예산(rev 3 §3 — 오늘 재진입 0 이라 현행 무영향. 라이브 근거가 생기면 조정).</summary>
        public const int MaxDepth = 4;
        /// <summary>한 틱의 발동 상한(rev 3 §6 할당 상한 — 사건 큐 ≤ 1024/틱).</summary>
        public const int MaxPerTick = 1024;

        private readonly BattleWorld _world;
        private readonly MatchDefinition _def;
        private readonly EventBus _bus;
        private readonly BindingRegistry _registry;
        private readonly CoreSkillContext _skills;
        private SeamTickOrder _order;

        private readonly List<PendingFire>[] _queues;
        private readonly List<Binding> _listeners = new List<Binding>(8);

        private int _currentTick;
        private int _position = -1;   // 이번 틱에 이미 돈 마지막 seam 의 순번(-1 = 아직 없음)
        private int _seq;
        private int _raisedThisTick;
        private bool _overflowWarned;

        // unit 7d — 이 판에 **감지자가 올린 사실**의 수(종류별). 「왜 안 터졌나」의 첫 원인(감지자 없음)을
        // 추측이 아니라 관측으로 답하려고 센다. 규칙 상태가 아니다 — 판정에 쓰지 않는다.
        private readonly int[] _sensed = new int[256];

        private bool _draining;
        private Seam _drainingSeam;
        private int _drainingGeneration;

        public System.Action<string> Report;

        public TriggerDispatcher(BattleWorld world, MatchDefinition def, EventBus bus,
                                 BindingRegistry registry, CoreSkillContext skills)
        {
            _world = world;
            _def = def;
            _bus = bus;
            _registry = registry;
            _skills = skills;
            _queues = new List<PendingFire>[(int)Seam._Count];
            for (int i = 0; i < _queues.Length; i++) _queues[i] = new List<PendingFire>(8);
            // 배치 엣지(S1)는 **담당자의 사건**에서 나온다 — 표식(`JustDeployed`)은 안 옮겼다(6b2 E6).
            // 사건은 틱 끝 배달이라 이 규칙은 **다음 틱의 주기 seam** 에서 돈다(옛 `DeploymentActivationSystem`
            // 이 `[UpdateAfter(BossPeriodicTriggerSystem)]` 이라 옛 전투도 다음 프레임이었다).
            _bus.Subscribe(CoreEventKind.DefenderActivated, EventOrder.Trace, OnActivated);
        }

        /// <summary>조립 시점 — seam 마다 핸들러 하나. 순서표는 파이프라인에서 온다.</summary>
        public void Install(SeamHooks seams, SeamTickOrder order)
        {
            _order = order;
            for (int s = 0; s < (int)Seam._Count; s++)
            {
                var seam = (Seam)s;
                seams.Register(seam, ctx => Drain(seam, ctx));
            }
        }

        public SeamTickOrder Order => _order;

        /// <summary>unit 7d — 등록부(도구·디버그 커맨드가 규칙을 id 로 찾는다 — 읽기 전용 목록만 쓴다).</summary>
        public BindingRegistry Registry => _registry;

        /// <summary>
        /// unit 7d — 이 판에 그 종류의 사실이 감지자에게서 몇 번 올라왔나(진단 전용 · 판정에 안 쓴다).
        /// 폴링 감지(`IsPolled`)는 여기 안 든다 — 그쪽은 감지자가 늘 돈다.
        /// </summary>
        public int SensedCount(TriggerKind kind) => _sensed[(byte)kind];

        /// <summary>
        /// unit 7d — 감지자가 **사건이 아니라 폴링**인 종류(주기 타이머 · 체력 경계 — `DetectPeriodic`·`DetectThresholds`).
        /// 이 둘은 「감지자 없음」이 될 수 없다 — 안 터졌다면 조건(주기·경계)이 아직이다.
        /// </summary>
        public static bool IsPolled(TriggerKind kind)
            => kind == TriggerKind.PeriodicTimer || kind == TriggerKind.HealthThreshold;

        /// <summary>그 seam 에 줄 선 발동 수(테스트·진단).</summary>
        public int PendingAt(Seam seam) => _queues[(int)seam].Count;

        /// <summary>틱 시작 — `CommandPhase.Run` 이 부른다.</summary>
        public void BeginTick(int tick)
        {
            _currentTick = tick;
            _position = _order != null ? _order.IndexOf(Seam.Immediate) : 0;
            _raisedThisTick = 0;
            _overflowWarned = false;
        }

        // ── ① 감지자가 올리는 사실 ───────────────────────────────────────────

        /// <summary>공격 성사(RESOLVE · 폭탄 투척). 대표 대상은 **지금 손에 든 값**이다.</summary>
        public void RaiseAttack(Unit attacker, Unit target, float3 targetPos)
        {
            var e = SubjectOf(attacker, Seam.Attack, TriggerKind.AttackN);
            e.Target = target != null ? target.Id : SimEntityId.None;
            e.TargetHp = target != null ? target.Health : 0f;
            e.TargetMaxHp = target != null ? target.MaxHealth : 0f;
            e.HasSite = true;
            e.Site = targetPos;
            e.SiteBody = target != null ? target.HitRadius : 0f;
            e.Direction = math.normalizesafe((targetPos - attacker.Position).xz);
            e.TargetLayers = attacker.Attack != null ? attacker.Attack.TargetLayers : (byte)0;
            Raise(in e);
        }

        /// <summary>관통 피해를 입고 **살아남았다**(피격 N회). 체력은 이 피격 적용 뒤.</summary>
        public void RaiseDamaged(Unit victim)
        {
            var e = SubjectOf(victim, Seam.Death, TriggerKind.OnDamagedN);
            e.HasSite = true; e.Site = victim.Position; e.SiteBody = victim.HitRadius;   // 자리 = 나(짝)
            Raise(in e);
        }

        /// <summary>실드 합이 양수 → 0(파열). 사망과 독립 — 관통 킬 틱에도 난다.</summary>
        public void RaiseShieldBreak(Unit victim)
        {
            var e = SubjectOf(victim, Seam.Death, TriggerKind.OnShieldBreak);
            e.HasSite = true; e.Site = victim.Position; e.SiteBody = victim.HitRadius;
            Raise(in e);
        }

        /// <summary>
        /// 피해로 적을 죽였다. 자리 = **죽은 적**, 몸 = **죽은 적의 몸**(킬러의 몸이 아니다 — 시체 폭발의 폭심은
        /// 시체다). ⚠ 피해자가 적일 때만 — 옛 시체 폭발 블록이 `AttackUnitTag` 안에 있었다(방어유닛이 죽어도
        /// 킬러 폭발이 터지면 사양 변경).
        /// </summary>
        public void RaiseKill(Unit killer, Unit victim)
        {
            if (killer == null || victim == null || victim.Kind != UnitKind.Enemy) return;
            var e = SubjectOf(killer, Seam.Death, TriggerKind.OnKill);
            e.HasSite = true; e.Site = victim.Position; e.SiteBody = victim.HitRadius;
            e.TargetLayers = killer.Attack != null ? killer.Attack.TargetLayers : (byte)0;
            Raise(in e);
        }

        /// <summary>
        /// 판에서 **죽어서** 사라진다(소멸 직전 — 모든 사망 경로가 합류하는 자리). 진영·몸·자리는 지금 값으로
        /// 싣는다 — 드레인 시점엔 이미 파괴됐다.
        /// </summary>
        public void RaiseDeath(Unit dying)
        {
            var e = SubjectOf(dying, Seam.Lifecycle, TriggerKind.OnDeath);
            e.SubjectGone = true;
            e.SubjectAttack = EffectMagnitude.BasisOf(dying, BasisStat.Attack);   // 비율형 기준 스냅샷(계약 9)
            e.HasSite = true; e.Site = dying.Position; e.SiteBody = dying.HitRadius;   // 자리의 주인 = 죽은 나
            Collect(in e, dying, includeDetached: false);
        }

        /// <summary>
        /// 퇴근(커맨드) — `Immediate` seam(7b 8-1: 옛 `Lifecycle` 자리는 퇴근이 지나가지 않는다).
        /// 자리 = **비워진 칸 중심**, 몸 = **0 = 자리형**(의도 — 채우면 배스티온이 퇴근할 때만 운석이 넓어진다).
        /// 주인은 곧 파괴되므로 진영은 값이다(방어유닛 전용 사건이라 참).
        /// </summary>
        public void RaiseRetire(Unit u, float3 vacatedCenter)
        {
            var e = SubjectOf(u, Seam.Immediate, TriggerKind.OnRetire);
            e.SubjectGone = true;
            e.SubjectAttack = EffectMagnitude.BasisOf(u, BasisStat.Attack);   // 비율형 기준 스냅샷(계약 9)
            e.SubjectBody = 0f;
            e.HasSite = true; e.Site = vacatedCenter; e.SiteBody = 0f;
            Collect(in e, u, includeDetached: false);
        }

        private void OnActivated(CoreEvent ev)
        {
            var u = _world.Find(ev.A);
            if (u == null) return;
            var e = SubjectOf(u, Seam.Periodic, TriggerKind.OnPlace);
            e.TargetLayers = u.Attack != null ? u.Attack.TargetLayers : (byte)0;
            Raise(in e);
        }

        internal static TriggerEvent SubjectOf(Unit u, Seam seam, TriggerKind kind) => new TriggerEvent
        {
            Seam = seam,
            Kind = kind,
            Subject = u.Id,
            SubjectFaction = u.Faction,
            SubjectPos = u.Position,
            SubjectBody = u.HitRadius,
            SubjectHp = u.Health,
            SubjectMaxHp = u.MaxHealth,
            SubjectAnchor = u.Footprint != null ? u.Footprint.Anchor : default,
            HasSubjectAnchor = u.Footprint != null,
            Target = SimEntityId.None,
        };

        /// <summary>사실 하나 → 그것을 듣는 규칙들. 주인이 살아 있는 사건의 공용 입구.</summary>
        public void Raise(in TriggerEvent e) => Collect(in e, _world.Find(e.Subject), includeDetached: false);

        /// <summary>
        /// 규칙 하나에 직접 사건을 건다 — 부착 즉시(`trigger == None`) · 강제 발화 도구(7d)의 입구다.
        /// 카운터·게이트를 거치지 않는다(그 사건은 「그 규칙이 지금 발동한다」 자체다).
        /// </summary>
        public void RaiseFor(Binding b, in TriggerEvent e) => Enqueue(b, in e);

        private void Collect(in TriggerEvent e, Unit subject, bool includeDetached)
        {
            _sensed[(byte)e.Kind]++;
            _listeners.Clear();
            if (subject != null)
            {
                var own = subject.Bindings;
                for (int i = 0; i < own.Count; i++)
                    if (own[i].Def.Subject == BindingSubject.Self && own[i].Def.Trigger == e.Kind)
                        _listeners.Add(own[i]);
            }
            CollectAny(in e, subject);
            // 한 사건 안의 순서 = (소유자 id, InstanceId) 오름차순. 자기 규칙과 `Any` 규칙이 섞이므로 정렬한다
            // (n 은 한 자리 수라 삽입 정렬).
            for (int a = 1; a < _listeners.Count; a++)
            {
                var key = _listeners[a];
                int b = a - 1;
                while (b >= 0 && Before(key, _listeners[b])) { _listeners[b + 1] = _listeners[b]; b--; }
                _listeners[b + 1] = key;
            }
            for (int i = 0; i < _listeners.Count; i++)
            {
                var b = _listeners[i];
                if (!Admit(b, in e)) continue;
                Enqueue(b, in e);
            }
        }

        private static bool Before(Binding x, Binding y)
            => x.Owner.Value != y.Owner.Value ? x.Owner.Value < y.Owner.Value : x.InstanceId < y.InstanceId;

        // `Any` 규칙 — 판 위 누구의 사건이든 듣는다(배치 오라 · Squad 상속 — 7b 가 붙인다).
        private void CollectAny(in TriggerEvent e, Unit subject)
        {
            var units = _world.Units;
            for (int u = 0; u < units.Count; u++)
            {
                var list = units[u].Bindings;
                for (int i = 0; i < list.Count; i++)
                    if (list[i].Def.Subject == BindingSubject.Any && list[i].Def.Trigger == e.Kind
                        && SubjectPasses(list[i], subject))
                        _listeners.Add(list[i]);
            }
            var match = _registry.MatchBindings;
            for (int i = 0; i < match.Count; i++)
                if (match[i].Def.Subject == BindingSubject.Any && match[i].Def.Trigger == e.Kind
                    && SubjectPasses(match[i], subject))
                    _listeners.Add(match[i]);
        }

        private bool SubjectPasses(Binding b, Unit subject) => SubjectPasses(in b.Def, subject, _def);

        /// <summary>
        /// `Any` 바인딩의 주어 필터 — 직업 비트 ∧ 배치 코스트(옛 카드 축 `MatchesDcAxis`). 둘 다 0 = 전원.
        /// **한 함수**다: 상속(배치 사건)과 Squad 카드의 부착 즉시 전개(`CardBindings`)가 같은 답을 받아야 한다.
        /// </summary>
        public static bool SubjectPasses(in BindingDef d, Unit subject, MatchDefinition def)
        {
            // unit 7d — 코어 내부 주어 필터(저작 노출 없음). 직업·코스트와 곱(∧).
            if (d.SubjectFilter == BindingSubjectFilter.PlacedDefender
                && (subject == null || subject.Kind != UnitKind.Defender || subject.Footprint == null))
                return false;
            int mask = d.SubjectClassMask;
            if (mask == 0 && d.SubjectCost == 0) return true;
            if (subject == null || subject.Kind != UnitKind.Defender) return false;
            if (subject.DefIndex < 0 || subject.DefIndex >= def.Units.Length) return false;
            ref var u = ref def.Units[subject.DefIndex];
            if (mask != 0 && (u.Role < 0 || (mask & (1 << u.Role)) == 0)) return false;
            if (d.SubjectCost != 0 && u.Cost != d.SubjectCost) return false;
            return true;
        }

        // 카운터·게이트 — **통과한 사건만 센다**(카운트 게이트: `if (GatePass) Tick`).
        private static bool Admit(Binding b, in TriggerEvent e)
        {
            ref var d = ref b.Def;
            switch (e.Kind)
            {
                case TriggerKind.AttackN:
                    if (d.Gate != GateKind.None)
                    {
                        // 처형타 — 대표 대상의 **피해 전** 체력. 대상이 없으면(방향탄 lapse) 게이트 실패.
                        if (e.Target.IsNone) return false;
                        if (!SkillRouting.GatePass(d.Gate, d.GateValue, e.TargetHp, e.TargetMaxHp)) return false;
                    }
                    return TriggerCounters.Tick(ref b.Counter, d.Period);
                case TriggerKind.OnDamagedN:
                    if (!SkillRouting.GatePass(d.Gate, d.GateValue, e.SubjectHp, e.SubjectMaxHp)) return false;
                    return TriggerCounters.Tick(ref b.Counter, d.Period);
                default:
                    return true;
            }
        }

        // ── ② 줄 세우기 ──────────────────────────────────────────────────────

        private void Enqueue(Binding b, in TriggerEvent e)
        {
            if (++_raisedThisTick > MaxPerTick)
            {
                if (!_overflowWarned)
                {
                    _overflowWarned = true;
                    Warn($"[Trigger] 한 틱 발동이 상한 {MaxPerTick} 을 넘었다 — 넘친 발동을 버린다(틱 {_currentTick}).");
                }
                return;
            }

            int gen = 0;
            int due;
            if (_draining && e.Seam == _drainingSeam)
            {
                // 바인딩→바인딩 **직접 재진입** — 같은 드레인이 다음 세대로 받는다(BFS).
                gen = _drainingGeneration + 1;
                if (gen > MaxDepth)
                {
                    Warn($"[Trigger] 연쇄 깊이 {MaxDepth} 초과 — '{b.Def.Label}' 발동을 버린다(틱 {_currentTick}, {e.Seam}).");
                    return;
                }
                due = _currentTick;
            }
            else if (e.Seam == Seam.Immediate)
            {
                due = _currentTick;   // 커맨드 콜스택이 곧 드레인한다
            }
            else
            {
                // 잔여 규칙 — 후속 seam 이면 같은 틱, 지난 seam 이면 다음 틱. **순서표로만** 판정한다.
                // 「지금 이 seam 에 서 있다」(자기 seam 안의 감지 — 주기·경계)도 같은 틱이다.
                due = _order != null && (_order.IsLater(e.Seam, _position) || _order.IndexOf(e.Seam) == _position)
                    ? _currentTick : _currentTick + 1;
            }

            _queues[(int)e.Seam].Add(new PendingFire
            {
                Evt = e,
                Binding = b,
                Generation = gen,
                Seq = _seq++,
                DueTick = due,
            });
        }

        // ── ③ 드레인 ─────────────────────────────────────────────────────────

        private void Drain(Seam seam, TickContext ctx)
        {
            if (seam != Seam.Immediate && _order != null) _position = _order.IndexOf(seam);

            if (seam == Seam.Periodic)
            {
                _registry.StepLifetimes(ctx.Dt, ctx.Tick);
                DetectPeriodic(ctx);
            }
            else if (seam == Seam.Threshold)
            {
                DetectThresholds();
            }

            var q = _queues[(int)seam];
            if (q.Count == 0) return;

            bool outer = !_draining;
            var prevSeam = _drainingSeam;
            var prevGen = _drainingGeneration;
            _draining = true;
            _drainingSeam = seam;
            try
            {
                // 세대 순. 같은 세대 안은 생산 순번 순(= 줄 선 순서). 새 세대는 뒤에 붙으므로 한 번의
                // 전진 순회가 BFS 다. 아직 때가 안 된 것(다음 틱)은 남긴다.
                int i = 0;
                while (i < q.Count)
                {
                    var p = q[i];
                    if (seam != Seam.Immediate && p.DueTick > ctx.Tick) { i++; continue; }
                    q.RemoveAt(i);
                    _drainingGeneration = p.Generation;
                    Execute(in p, ctx);
                }
            }
            finally
            {
                _draining = !outer;
                _drainingSeam = prevSeam;
                _drainingGeneration = prevGen;
            }
        }

        private void DetectPeriodic(TickContext ctx)
        {
            var units = _world.Units;
            for (int u = 0; u < units.Count; u++)
            {
                var unit = units[u];
                // 죽은 유닛은 새 발동을 시작하지 않는다(옛 `WithNone<DeadTag>`). ⚠ **잠든·기절한 유닛은 쏜다** —
                // 옛 주기 감지자는 행동 잠금을 안 읽었다(사용자 결정 ③ 기본값 = 현행 박제, README).
                if (unit.Dead) continue;
                var list = unit.Bindings;
                for (int i = 0; i < list.Count; i++)
                {
                    var b = list[i];
                    if (b.Def.Trigger != TriggerKind.PeriodicTimer) continue;
                    if (!TriggerCounters.Periodic(ref b.Elapsed, ctx.Dt, b.Def.PeriodSeconds)) continue;
                    var e = SubjectOf(unit, Seam.Periodic, TriggerKind.PeriodicTimer);
                    e.TargetLayers = unit.Attack != null ? unit.Attack.TargetLayers : (byte)0;
                    Enqueue(b, in e);
                }
            }
            var match = _registry.MatchBindings;
            for (int i = 0; i < match.Count; i++)
            {
                var b = match[i];
                if (b.Def.Trigger != TriggerKind.PeriodicTimer) continue;
                if (!TriggerCounters.Periodic(ref b.Elapsed, ctx.Dt, b.Def.PeriodSeconds)) continue;
                Enqueue(b, new TriggerEvent
                {
                    Seam = Seam.Periodic, Kind = TriggerKind.PeriodicTimer,
                    Subject = SimEntityId.Match, SubjectFaction = b.CastFaction, Target = SimEntityId.None,
                });
            }
        }

        private void DetectThresholds()
        {
            var units = _world.Units;
            for (int u = 0; u < units.Count; u++)
            {
                var unit = units[u];
                // 시체는 경계에서 폭발·도약하지 않는다 — 오버킬로 여러 경계를 한 번에 뚫는 틱이 그 자리다.
                if (unit.Dead) continue;
                var list = unit.Bindings;
                for (int i = 0; i < list.Count; i++)
                {
                    var b = list[i];
                    if (b.Def.Trigger != TriggerKind.HealthThreshold) continue;
                    if (!TriggerCounters.HealthThreshold(unit.Health, b.MaxHpRef, b.Def.Fraction, ref b.NextBoundary)) continue;
                    // ⚠ 경계 발동은 층을 안 싣는다(= 무제한) — 경계 자폭이 비행 적을 못 때리면 사양 변경이다.
                    Enqueue(b, SubjectOf(unit, Seam.Threshold, TriggerKind.HealthThreshold));
                }
            }
        }

        private void Execute(in PendingFire p, TickContext ctx)
        {
            var b = p.Binding;
            ref var d = ref b.Def;
            var e = p.Evt;

            // 떨어진 규칙은 버린다 — 단 **주인이 사라지는 사건**(자기 죽음 · 퇴근)은 떨어진 뒤가 정상이다.
            if (b.Detached && !e.SubjectGone) return;
            if (d.FireCap > 0 && b.FireCount >= d.FireCap) return;
            if (d.CoreSkill != null)
            {
                ExecuteCore(b, in e, ctx);
                return;
            }
            if (d.Skill == null)
            {
                Warn($"[Trigger] '{d.Label}' 에 실행자가 없다 — bake 가 거절했어야 한다. 발동을 버린다.");
                return;
            }

            var owner = _world.Find(e.Subject);
            if (owner == null && !e.SubjectGone && e.Subject.IsEntity)
            {
                Warn($"[Trigger] '{d.Label}' 의 시전자가 드레인 전에 사라졌다 — 발동을 버린다.");
                return;
            }

            // unified-effect-layer unit 2 — **원점 두 값을 여기서 한 번 채운다**(README 계약 1). concrete 는 `target.Origin`
            // 만 읽는다 — 새 산출기가 아니라 감지자 스냅샷과 이 드레인이 이미 만들던 값을 한 묶음으로 넘길 뿐이다.
            // ① 발사 자리 = 발동 주체: 판 위에 있으면 **지금** 자리(concrete 가 `ctx.Position(caster)` 로 읽던 그 값),
            //    없으면(자기 죽음 · 퇴근 · 판 시전) 발화 시점 스냅샷.
            CasterRef caster;
            float3 launchSite;
            float launchBody;
            if (owner != null && !e.SubjectGone)
            {
                launchSite = owner.Position;
                launchBody = owner.HitRadius;
                caster = CasterRef.OfUnit(CoreSkillContext.ToSkill(owner.Id), owner.Faction, launchBody);
            }
            else
            {
                launchSite = e.SubjectPos;
                launchBody = e.SubjectBody;
                caster = new CasterRef(SkillEntityId.None,
                                       e.SubjectFaction != Faction.None ? e.SubjectFaction : b.CastFaction,
                                       launchBody);
            }

            // ② 효과 좌표 = 사건이 실은 자리(몸은 감지자 스냅샷 — 시체 폭발이면 죽은 적) · 지정 칸(자리형 0) ·
            //    둘 다 없으면 주인의 스냅샷(옛 `TargetPosition == 0` 폴백).
            float3 site;
            float siteBody;
            if (e.HasCellAim)
            {
                site = _skills.CellCenter(e.CellA);
                siteBody = 0f;
            }
            else
            {
                site = e.HasSite ? e.Site : e.SubjectPos;
                siteBody = e.HasSite ? e.SiteBody : e.SubjectBody;
            }
            var origin = new SkillOrigin(launchSite, launchBody, site, siteBody, e.CellA);

            var targetUnit = _world.Find(e.Target);
            var target = new SkillTarget(targetUnit != null ? CoreSkillContext.ToSkill(targetUnit.Id) : SkillEntityId.None,
                                         in origin, e.CellB, e.HasCellB, e.Direction);

            // skill-data-table unit 3 — **시전 순간**(README 계약 9) = 이 드레인. 비율형 수치를 여기서 **한 번** 고정값으로 풀어
            // 실행에 넘긴다 — 탄 · 발사 명세(버스트 전 발) · 장판 · 도약 슬램은 이 값을 실어 나르고 착탄 때 주인을 되묻지 않는다.
            var fx = b.Effect;
            if (fx.MagnitudeMode == MagnitudeMode.OwnerStatRatio)
                fx = EffectMagnitude.Resolve(in fx, CastBasis(fx.BasisStat, b, owner, in e));
            var prm = d.ToParams(in fx, e.TargetLayers);

            b.FireCount++;
            PublishFired(b, in e, ctx.Tick, targetUnit);

            _skills.Begin(b, in e, in fx, caster.Faction, ctx);
            try
            {
                d.Skill.Execute(caster, in target, in prm, _skills);
            }
            catch (System.Exception ex)
            {
                // 한 규칙의 배선 실수가 그 틱 전체를 죽이지 않게 — 이 발동만 버리고 말한다.
                Warn($"[Trigger] '{d.Label}' 실행이 던졌다 — 이 발동만 버린다. {ex.Message}");
            }
            finally
            {
                _skills.End();
            }

            if (d.Lifetime == BindingLifetime.UntilFireCap && d.FireCap > 0 && b.FireCount >= d.FireCap)
                _registry.Detach(b, BindingDetachReason.FireCapReached, ctx.Tick);
        }

        // skill-data-table unit 3 — 비율형의 기준값. 주인이 판에 있으면 **지금**(최종 스탯) · 떠났으면(죽음 · 퇴근) 감지 순간
        // 스냅샷. 주인 없는 시전(판 · 액티브 · 드림스톤 — 사건 주체 `Match`)은 기준이 없다 — 검증(`EffectComboRule`)이 거절했어야
        // 한다. 조용히 넘기지 않고 말한 뒤 0 으로 푼다(효과는 헛발).
        //
        // U17 — 「남의 사건」(`Any` — 남의 배치)의 기준은 **규칙 소유자(숙주)** 의 지금 최종 스탯이다(사건 주체 = 놓인 유닛은
        // 발사 자리 · 킬 귀속만 — U3). 소유자가 사라진 경우의 스냅샷은 없다 — 필요가 없다: `Any` 는 배치(주체가 살아 있는
        // 사건)만 듣고, 소유자 소멸은 규칙을 떼며(`BindingRegistry.OnOwnerRemoved`), 떨어진 규칙의 주체가 안 떠난 발동은
        // `Execute` 첫 줄이 버린다. 그래서 여기까지 온 `Any` 발동의 소유자는 판 위에 있다(없으면 판 호스트 소유 = 주인 없는 시전).
        private float CastBasis(BasisStat stat, Binding b, Unit owner, in TriggerEvent e)
        {
            if (b.Def.Subject == BindingSubject.Any)
            {
                var holder = _world.Find(b.Owner);
                if (holder != null) return EffectMagnitude.BasisOf(holder, stat);
            }
            else
            {
                if (owner != null && !e.SubjectGone) return EffectMagnitude.BasisOf(owner, stat);
                if (e.SubjectGone) return stat == BasisStat.MaxHealth ? e.SubjectMaxHp : e.SubjectAttack;
            }
            Warn($"[Trigger] '{b.Def.Label}' 비율형 수치인데 주인이 없다(주인 없는 시전) — 검증이 거절했어야 한다. 값 0 으로 푼다.");
            return 0f;
        }

        // unit 7d — 코어 효과(시즌 기믹). 레일(감지·카운터·줄·수명·발동 상한)은 스킬과 같고 실행자만 다르다.
        // ⚠ `TriggerFired` 를 내지 않는다 — 효과가 실제로 일을 했을 때 자기 사건(`GimmickTriggered`)을 낸다.
        // 유닛마다 주기로 도는 규칙이라, 헛발(상한에 막힌 픽업 주기)까지 사건을 내면 트레이스가 그 소음으로 찬다.
        private void ExecuteCore(Binding b, in TriggerEvent e, TickContext ctx)
        {
            ref var d = ref b.Def;
            if (!e.SubjectGone && e.Subject.IsEntity && _world.Find(e.Subject) == null)
            {
                Warn($"[Trigger] '{d.Label}' 의 주인이 드레인 전에 사라졌다 — 발동을 버린다.");
                return;
            }
            b.FireCount++;
            try
            {
                d.CoreSkill.Fire(b, in e, ctx);
            }
            catch (System.Exception ex)
            {
                Warn($"[Trigger] '{d.Label}' 코어 효과가 던졌다 — 이 발동만 버린다. {ex.Message}");
            }
            if (d.Lifetime == BindingLifetime.UntilFireCap && d.FireCap > 0 && b.FireCount >= d.FireCap)
                _registry.Detach(b, BindingDetachReason.FireCapReached, ctx.Tick);
        }

        private void PublishFired(Binding b, in TriggerEvent e, int tick, Unit targetUnit)
        {
            var casterSite = new Site(e.SubjectPos, e.SubjectBody);
            var targetSite = e.HasSite ? new Site(e.Site, e.SiteBody) : casterSite;
            ref var fx = ref b.Effect;
            if (fx.Kind == EffectKind.AreaBreath)
            {
                // 브레스의 그림은 **이 스킬의 콘**이다(6c 후속 3) — 축 = 시전자→대상, 반각·사거리 = 저작.
                var cone = new Combat.AttackShapeBaked
                {
                    kind = Combat.AttackShapeBaked.SectorKind,
                    cosHalf = fx.ConeCosHalf,
                    sinHalf = fx.ConeSinHalf,
                };
                _bus.Publish(CoreEvent.TriggerFired(tick, b, e.Target, casterSite, targetSite, e.SubjectFaction,
                                                    e.Direction, cone, fx.TileRange));
                return;
            }
            _bus.Publish(CoreEvent.TriggerFired(tick, b, e.Target, casterSite, targetSite, e.SubjectFaction));
        }

        private void Warn(string msg) => Report?.Invoke(msg);
    }
}
