using Wassup.Skills;
using Wassup.BattleCore.Effects;

namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7d — **시즌 기믹이 판에 얹는 규칙.** 6b2 가 셈판(픽업·사직서·열기·피로의 한 걸음)을
    // 세우고 「무엇이 언제 그것을 놓는가」를 남겼다. 그 답이 여기 넷이고, 넷 다 `Binding` 하나다(어휘 6개념):
    //
    //   | 기믹   | 호스트           | 사건                  | 대상 필터(옛 쿼리 그대로)                              |
    //   |--------|------------------|-----------------------|--------------------------------------------------------|
    //   | 레드불 | **판**(Match)    | 주기(`SpawnInterval`) | — (판 위 픽업 수 = 동시 상한)                          |
    //   | 온천   | **유닛** 마다    | 주기(`HeatInterval`)  | 전 유닛(방어유닛·순찰·적) · 사망·**배치 중** 제외        |
    //   | 번아웃 | **유닛** 마다    | 주기(`FatigueInterval`)| 방어유닛·순찰(옛 `DefenderUnitTag`) · **배치 중 포함**   |
    //   | 퇴근   | **판**(Match)    | 사망(`OnDeath` · Any) | 배치된 방어유닛(점유가 있는 것 — 순찰·거점 제외)        |
    //
    // ⚠ **주기는 판이 아니라 유닛이 소유한다**(rev 3 정정 2 · C4). 온천·번아웃은 유닛마다 **부착 시점이 위상**인
    // 타이머다 — 판 호스트 하나로 접으면 전원이 같은 틱에 같이 쌓인다. 레드불만 판의 주기다.
    // ⚠ **필터는 기믹마다 다르고 통일하지 않는다**(critic 열린 질문 1). 온천은 배치 모션이 끝나야(활성화) 붙고,
    // 번아웃은 배치 순간부터 쌓인다 — 옛 두 시스템의 쿼리가 그랬다(`HeatAccrualSystem` `WithNone<PendingDeployment>` ·
    // `FatigueAccrualSystem` 무필터). 필터는 **저작 노출이 없는 코어 축**이다(rev 3 §1).
    // ⚠ 피로 누적은 **스탯 적용 뒤**다(1틱 지연 박제 — 6b2 구현 5). 여기서는 요청(`GimmickStacks.RequestFatigue`)만 넣는다.
    //
    // 매니저가 아니다: 셈판은 6b2 의 것(`GimmickStacks` · `PickupSpawn` · `ResignationDrop`)이고, 발동 판정·순서·수명은
    // 등록부와 디스패처의 것이다. 이 파일은 **어느 규칙을 누구에게 붙이나**와 그 규칙의 실행 한 줄씩만 든다.
    //
    // 활성 게이트는 하나다 — 「그 기믹이 뽑혔나」(`GimmickHost.TryActive`, 6b2 구현 6).
    public sealed class GimmickBindings
    {
        private readonly BattleWorld _world;
        private readonly Map.MapRuntime _map;
        private readonly GimmickHost _gimmick;
        private readonly BindingRegistry _registry;
        private readonly EventBus _bus;

        private readonly HeatStep _heat;
        private readonly FatigueStep _fatigue;
        private readonly RedBullCadence _redBull;
        private readonly ResignationOnDeath _resignation;

        public System.Action<string> Report;

        public GimmickBindings(EventBus bus, BattleWorld world, Map.MapRuntime map, GimmickHost gimmick,
                               BindingRegistry registry)
        {
            _bus = bus;
            _world = world;
            _map = map;
            _gimmick = gimmick;
            _registry = registry;
            _heat = new HeatStep(this);
            _fatigue = new FatigueStep(this);
            _redBull = new RedBullCadence(this);
            _resignation = new ResignationOnDeath(this);
            // 유닛 호스트 규칙은 **스폰·활성화 사건**에서 붙는다(스폰 경로 무수정 — 옛 lazy-attach 의 뜻).
            bus.Subscribe(CoreEventKind.UnitSpawned, EventOrder.GimmickAttach, OnSpawned);
            bus.Subscribe(CoreEventKind.DefenderActivated, EventOrder.GimmickAttach, OnActivated);
        }

        /// <summary>판 호스트 규칙 — `GimmickHost.Begin` **뒤** 판 시작 1회(구현 6).</summary>
        public void Begin(int tick)
        {
            if (_gimmick.TryActive(GimmickKind.RedBull, out var rb) && rb.RedBull.SpawnInterval > 0f)
            {
                var d = Row("기믹 레드불 · 픽업 주기", TriggerKind.PeriodicTimer, _redBull);
                d.PeriodSeconds = rb.RedBull.SpawnInterval;
                d.Lifetime = BindingLifetime.Match;
                _registry.Attach(null, in d, -1, tick);
            }
            if (_gimmick.TryActive(GimmickKind.ClockOut, out _))
            {
                var d = Row("기믹 퇴근 · 사직서 드랍", TriggerKind.OnDeath, _resignation);
                d.Subject = BindingSubject.Any;
                d.SubjectFilter = BindingSubjectFilter.PlacedDefender;
                d.Lifetime = BindingLifetime.Match;
                _registry.Attach(null, in d, -1, tick);
            }
        }

        // ── 유닛 호스트 부착 ─────────────────────────────────────────────────

        private void OnSpawned(CoreEvent e)
        {
            var u = _world.Find(e.A);
            if (u == null || u.Dead) return;
            // 온천 — 전 유닛, **배치 중이면 활성화까지 기다린다**(옛 쿼리 `WithNone<PendingDeployment>`).
            if (u.Kind == UnitKind.Enemy || u.Kind == UnitKind.Patrol
                || (u.Kind == UnitKind.Defender && !u.Deploying))
                AttachHeat(u, e.Tick);
            // 번아웃 — 방어유닛·순찰(옛 `DefenderUnitTag` — 순찰 소환물도 그 태그를 가졌다), **배치 중 포함**.
            if (u.Kind == UnitKind.Defender || u.Kind == UnitKind.Patrol)
                AttachUnitRow(u, GimmickKind.Burnout, _fatigue, e.Tick);
        }

        private void OnActivated(CoreEvent e)
        {
            var u = _world.Find(e.A);
            if (u == null || u.Dead) return;
            AttachHeat(u, e.Tick);
        }

        private void AttachHeat(Unit u, int tick) => AttachUnitRow(u, GimmickKind.Onsen, _heat, tick);

        private void AttachUnitRow(Unit u, GimmickKind kind, ICoreEffect fx, int tick)
        {
            if (!_gimmick.TryActive(kind, out var g)) return;
            float period = kind == GimmickKind.Onsen ? g.Onsen.HeatInterval : g.Burnout.FatigueInterval;
            if (period <= 0f) return;   // 잘못 저작된 SO — 옛 self-gate(`interval <= 0 → return`)
            var list = u.Bindings;
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i].Def.CoreSkill, fx)) return;   // 활성화 경로가 둘이다(즉시 · 모션 뒤)
            var d = Row(kind == GimmickKind.Onsen ? "기믹 온천 · 열기" : "기믹 번아웃 · 피로",
                        TriggerKind.PeriodicTimer, fx);
            d.PeriodSeconds = period;
            var b = _registry.Attach(u, in d, -1, tick);
            // **부착 틱도 센다.** 옛 lazy-attach 는 붙인 그 프레임에 `elapsed += dt` 를 했다(ECB 재생 뒤 같은 프레임 패스).
            // 여기서 붙는 자리는 그 틱의 주기 seam **뒤**(사건 배달)라, 그 한 틱을 미리 쳐 줘야 위상이 옛과 같다.
            if (b != null) b.Elapsed = BattleMatch.Dt;
        }

        // skill-data-table 1b — 코어 효과 줄은 **효과 표 밖**이다(`EffectIndex = -1` → 효과 값 없음 · `Binding.Effect` 는 기본값).
        // 수치는 이 기믹의 `GimmickDef` 가 들고 실행자(`ICoreEffect`)가 거기서 읽는다 — 효과 줄로 옮길 값이 없다.
        private static BindingDef Row(string label, TriggerKind trigger, ICoreEffect fx)
        {
            var d = BindingDef.Default();
            d.Label = label;
            d.Trigger = trigger;
            d.CoreSkill = fx;
            d.Origin = BindingOrigin.Gimmick;
            return d;
        }

        private void Publish(int tick, SimEntityId owner, SimEntityId made, GimmickKind kind, float amount,
                             Site at, Faction faction)
            => _bus.Publish(CoreEvent.GimmickTriggered(tick, owner, made, kind, amount, at, faction));

        // ── 실행(한 줄씩 — 셈판은 6b2 의 것) ────────────────────────────────

        // 온천 열기 +1 → `HeatMath` → 부호만 보고 회복/피해 인박스. 피해 단계 **앞**(장 준비 끝)이라 같은 틱에 정산된다
        // (옛 `HeatAccrualSystem` 이 `[UpdateBefore(DamageApplicationSystem)]` 였다).
        private sealed class HeatStep : ICoreEffect
        {
            private readonly GimmickBindings _o;
            public HeatStep(GimmickBindings o) => _o = o;

            public void Fire(Binding b, in TriggerEvent e, TickContext ctx)
            {
                if (!_o._gimmick.TryActive(GimmickKind.Onsen, out var g)) return;
                var u = _o._world.Find(b.Owner);
                if (u == null || u.Dead || u.Deploying) return;
                float delta = GimmickStacks.AccrueHeat(u, in g.Onsen);
                _o.Publish(ctx.Tick, u.Id, SimEntityId.None, GimmickKind.Onsen, delta,
                           new Site(u.Position, u.HitRadius), u.Faction);
            }
        }

        // 번아웃 피로 — **요청만** 넣는다. 쌓는 것은 스탯 적용 뒤 단계다(1틱 지연 박제 · 6b2 구현 5).
        private sealed class FatigueStep : ICoreEffect
        {
            private readonly GimmickBindings _o;
            public FatigueStep(GimmickBindings o) => _o = o;

            public void Fire(Binding b, in TriggerEvent e, TickContext ctx)
            {
                if (!_o._gimmick.TryActive(GimmickKind.Burnout, out var g)) return;
                var u = _o._world.Find(b.Owner);
                if (u == null) return;
                GimmickStacks.RequestFatigue(_o._world, u, in g.Burnout);
                _o.Publish(ctx.Tick, u.Id, SimEntityId.None, GimmickKind.Burnout, g.Burnout.FatigueAmount,
                           new Site(u.Position, u.HitRadius), u.Faction);
            }
        }

        // 레드불 주기 — 판 위 픽업이 동시 상한에 닿아 있으면 **밀린 주기를 접어 둔다**(옛 `elapsed = min(elapsed,
        // interval)` 클램프 · `PickupSpawnSystem.cs:75`): 슬롯이 비는 틱에 곧바로 하나가 선다. 빈 칸을 못 찾으면(보드 포화)
        // 이번 주기만 건너뛴다(옛 `continue`). 둘을 가르는 이유 — 앞쪽은 주기를 잃지 않고 뒤쪽은 잃는다.
        private sealed class RedBullCadence : ICoreEffect
        {
            private readonly GimmickBindings _o;
            public RedBullCadence(GimmickBindings o) => _o = o;

            public void Fire(Binding b, in TriggerEvent e, TickContext ctx)
            {
                if (!_o._gimmick.TryActive(GimmickKind.RedBull, out var g)) return;
                ref var spec = ref g.RedBull;
                if (_o._world.Pickups.Count >= spec.MaxActive)
                {
                    b.Elapsed = spec.SpawnInterval;   // 다음 틱에 다시 넘는다 = 상한이 풀리는 즉시
                    return;
                }
                var p = PickupSpawn.TrySpawnRandom(ctx, PickupKind.RedBull, in spec, ctx.Tick);
                if (p == null) return;
                _o.Publish(ctx.Tick, SimEntityId.Match, p.Id, GimmickKind.RedBull, 0f, Site.AtCell(p.Center), Faction.None);
            }
        }

        // 사직서 — 배치된 방어유닛이 **죽어서** 사라질 때 그 배치 칸(점유 앵커)에 한 장. 퇴근은 사망 사건이 아니라
        // 이 규칙이 듣지 않는다(불변식 11 — 배제 코드 0줄). 사건은 모든 사망 경로가 합류하는 자리(소멸 직전)에서 오고,
        // 자리는 값 스냅샷이다 — 드레인 시점엔 주인이 이미 없다.
        private sealed class ResignationOnDeath : ICoreEffect
        {
            private readonly GimmickBindings _o;
            public ResignationOnDeath(GimmickBindings o) => _o = o;

            public void Fire(Binding b, in TriggerEvent e, TickContext ctx)
            {
                if (!_o._gimmick.TryActive(GimmickKind.ClockOut, out _)) return;
                if (!e.HasSubjectAnchor)
                {
                    _o.Report?.Invoke($"[Gimmick] 사직서 — 죽은 방어유닛 {e.Subject} 의 배치 칸이 사건에 없다. 떨어뜨리지 않는다.");
                    return;
                }
                var r = ResignationDrop.At(_o._world, _o._map, e.SubjectAnchor, e.Subject, e.SubjectFaction, ctx.Tick);
                if (r == null) return;
                _o.Publish(ctx.Tick, e.Subject, r.Id, GimmickKind.ClockOut, 0f, Site.AtCell(r.Center), e.SubjectFaction);
            }
        }
    }
}
