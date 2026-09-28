using Unity.Mathematics;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Effects;
using Wassup.Skills;

namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7a — 스킬이 세상을 바꾸는 **단 하나의 표면**(S20 을 「보류 → 결정」으로 닫는다).
    //
    // 옛 전투는 asmdef 가 「쓰기는 발행으로만」을 컴파일러로 강제했고 예외 4건이 폐쇄 목록이었다(ECB 스테이징
    // vs 직접 쓰기). 새 코어에서 concrete 는 여전히 엔진 무참조 `Wassup.Skills` 에 살지만 **코어 안에서는
    // 아무것도 막지 않으므로**, 규율을 표면 하나로 옮긴다: 스킬 경로의 상태 변경은 전부 `Apply` 를 지나고,
    // 그 사실을 `CoreArchitectureTests` 가 소스로 못박는다(막으려는 것이 값이 아니라 **형태**라서).
    //
    // 여기가 하는 일은 **번역과 배달**뿐이다 — 판정은 이미 있는 관문에 맡긴다:
    //   스탯·스택·지속 피해 = `EffectApply`(자격 가드 한 곳) · 군중 제어 = `BattleWorld.RequestCc`(면역 한 곳) ·
    //   실드 = `BattleWorld.GrantShield`(헛발동 금지 F20) · 탄 = 요청 줄(착탄 관문이 부여를 접는다, 6a2) ·
    //   장판 = `HazardSpawn` · 장 = `BattleWorld.SpawnField` · 도발 = 어그로 요청(수용량 게이트는 받는 쪽).
    // 새 규칙을 여기 두면 그것이 새 브리지의 첫 줄이다.
    //
    // **원자 개시는 한 함수다**(S19): 궁극기 = 잠금 + 무적(`BeginUltimateLeap`), 호접몽 = 잠 + 감시(`BeginDreamCocoon`).
    // 어느 하나만 붙는 틱이 있으면 안 된다.
    public sealed class IntentApplier
    {
        private readonly BattleWorld _world;
        private readonly Map.MapRuntime _map;
        private readonly MatchDefinition _def;
        private readonly EventBus _bus;
        private readonly CostLedger _cost;
        private readonly HandDeck _hand;

        // 한 발동의 문맥 — 칸 판별자(카드 = InstanceId)·발사 명세 슬롯·시전자 진영(주인이 없어도).
        private Binding _binding;
        private Faction _casterFaction = Faction.DefenderUnit;
        private TickContext _ctx;

        public System.Action<string> Report;

        public IntentApplier(BattleWorld world, Map.MapRuntime map, MatchDefinition def, EventBus bus,
                             CostLedger cost, HandDeck hand)
        {
            _world = world;
            _map = map;
            _def = def;
            _bus = bus;
            _cost = cost;
            _hand = hand;
        }

        public MatchDefinition Definition => _def;

        /// <summary>판당 한 벌의 틱 문맥(조립 시점). 발동 밖의 직접 적용(테스트·액티브)도 이 문맥을 쓴다.</summary>
        internal void Bind(TickContext ctx) { _home = ctx; if (_ctx == null) _ctx = ctx; }
        private TickContext _home;

        /// <summary>한 발동의 문맥을 연다(디스패처 · 7b 액티브). `ctx` 가 null 이면 판의 문맥을 쓴다.</summary>
        public void Begin(Binding b, Faction casterFaction, TickContext ctx)
        {
            _binding = b;
            _casterFaction = casterFaction;
            _ctx = ctx ?? _home;
        }

        public void End() { _binding = null; _casterFaction = Faction.DefenderUnit; _ctx = _home ?? _ctx; }

        private int Tick => _ctx != null ? _ctx.Tick : 0;
        private float TileSize => _map != null ? _map.TileSize : 1f;

        private Unit U(SkillEntityId id) => id.IsValid ? _world.Find(new SimEntityId(id.Value)) : null;
        private static SimEntityId Id(SkillEntityId id) => CoreSkillContext.FromSkill(id);

        // ── SimIntent 24 ─────────────────────────────────────────────────────

        public void Apply(in SimIntent i)
        {
            switch (i.Kind)
            {
                case SimIntentKind.DealDamage: DealDamage(in i); return;
                case SimIntentKind.Heal: Heal(in i); return;
                case SimIntentKind.ApplyStatModifier: ApplyStat(in i); return;
                case SimIntentKind.ApplyStack: ApplyStack(in i); return;
                case SimIntentKind.ApplyCc: ApplyCc(in i); return;
                case SimIntentKind.ApplyDot: ApplyDot(in i); return;
                case SimIntentKind.ClearCc: ClearCc(in i); return;
                case SimIntentKind.GrantShield: _world.GrantShield(Id(i.Target), Id(i.Source), i.Amount, Tick); return;
                case SimIntentKind.Taunt: Taunt(in i); return;
                case SimIntentKind.CreditThreat:
                    // unit 7d — **위협 표는 이식하지 않는다**(7d 이식 제외). 옛 표는 누적만 돌고 **읽는 자가 0** 이었다 —
                    // 보스 순간이동이 「위협 리더 근처」에서 「상대 밀집 칸」으로 바뀌며 소비자가 사라졌다
                    // (옛 `HealthThresholdSystem.cs:28-32` · `ThreatTable.cs:47-49`). 이 의도를 내는 concrete 도 0 이다.
                    // 그래도 조용히 버리지 않는다 — 누가 내기 시작하면 여기서 들린다.
                    Warn("[Intent] 위협 귀속(CreditThreat)은 소비자가 없어 이식하지 않았다(7d) — 이번 의도는 버린다.");
                    return;
                case SimIntentKind.Blink: Blink(in i); return;
                case SimIntentKind.SpawnProjectile: SpawnProjectile(in i); return;
                case SimIntentKind.EmitPattern: EmitPattern(in i); return;
                case SimIntentKind.SpawnZoneCarrier: SpawnZone(in i); return;
                case SimIntentKind.SpawnFieldCarrier: SpawnField(in i); return;
                case SimIntentKind.BeginUltimateLeap: BeginUltimateLeap(in i); return;
                case SimIntentKind.DelaySelfAttack: DelaySelfAttack(in i); return;
                case SimIntentKind.Report: ReportIntent(in i); return;
                case SimIntentKind.PlayVisual: PlayVisual(in i); return;
                case SimIntentKind.GrantCharge: GrantCharge(in i); return;
                case SimIntentKind.SpawnOrbitProjectile: SpawnOrbit(in i); return;
                case SimIntentKind.StartLethalTimer: StartLethal(in i); return;
                case SimIntentKind.BeginDreamCocoon: BeginDreamCocoon(in i); return;
                case SimIntentKind.ScaleKillReward: ScaleKillReward(in i); return;
                default:
                    Warn($"[Intent] 모르는 의도 {i.Kind} — 버린다.");
                    return;
            }
        }

        // 피해 — **출처를 싣는다.** 옛 어댑터는 출처 없는 피해를 넣었는데(`IncomingDamage { amount }`), 옛 전투의
        // 처치 점수는 킬러를 안 봤다. 새 코어는 처치 사건(`UnitSlain`)이 **귀속된 죽음에만** 나므로, 출처를 빼면
        // 스킬로 죽인 적이 점수·각성을 안 준다 — 플레이어가 겪는 규칙을 지키려면 출처가 필요하다(7a 「고친 것」).
        private void DealDamage(in SimIntent i)
        {
            var u = U(i.Target);
            if (u == null || u.HealthExternal || i.Amount <= 0f) return;
            u.Inbox.Damage.Add(new DamageEntry { Amount = i.Amount, Source = Id(i.Source) });
        }

        private void Heal(in SimIntent i)
        {
            var u = U(i.Target);
            if (!EffectEligibility.AcceptsHeal(u) || i.Amount <= 0f) return;
            u.Inbox.Heal.Add(i.Amount);
        }

        private void ApplyStat(in SimIntent i)
        {
            var u = U(i.Target);
            if (u == null) return;
            CombineOp op;
            float mag = i.Amount, cap = 0f;
            if (i.Op == SkillCombineOp.FromAuthoredMultiplier)
            {
                // 저작은 배율 — 분류와 상한은 `ModifierAuthoring` 한 곳(올리는 버프 = 가산, 깎는 디버프 = 곱).
                ModifierAuthoring.FromMultiplier(i.Amount, out op, out mag);
                cap = ModifierAuthoring.StackCap(i.Amount, (int)i.HitThreshold);
            }
            else op = ToCoreOp(i.Op);
            float seconds = i.Duration > 0f ? i.Duration : float.PositiveInfinity;
            var src = Id(i.Source);
            EffectApply.Stat(_ctx, src, _world.Find(src), u, (StatKind)i.Selector, op, mag, seconds,
                             TagFor(in i), cap, ToCoreOrigin(i.Origin));
        }

        // 병합 칸(6a 구현 3). **카드 효과는 그 규칙의 `InstanceId`**(카드마다 새 칸 — 옛 `_dcStackCounter++` 의
        // 후계이고 소급 회수의 판별자), 유닛 저작 스킬은 스택 id 를 판별자로 한 배치 칸이다(옛 `stackId`).
        private SlotTag TagFor(in SimIntent i)
        {
            if (i.Origin == SkillModifierOrigin.Dreamcatcher && _binding != null)
                return SlotTag.OfBinding(_binding.InstanceId);
            if (_binding != null && _binding.Def.RevokeOnExpire)
                return SlotTag.OfBinding(_binding.InstanceId);   // 회수할 수 있어야 하는 칸
            return new SlotTag(SlotKind.OnPlace, i.StackId);
        }

        private void ApplyStack(in SimIntent i)
        {
            var u = U(i.Target);
            if (u == null) return;
            // 상한은 스택 종류의 성질이라 여기서 안 싣는다 — 저작 줄(`StackRuleDef`)이 푼다.
            EffectApply.Stack(_ctx, Id(i.Source), u, (StackKind)i.Selector, math.clamp(i.Count, 1, 255), 0, i.Duration);
        }

        private void ApplyCc(in SimIntent i)
        {
            var u = U(i.Target);
            if (u == null) return;
            var src = Id(i.Source);
            switch ((SkillCcKind)i.Selector)
            {
                case SkillCcKind.Stun:
                    _world.RequestCc(CcRequest.Of(u.Id, CcRequestKind.Stun, i.Duration, src)); return;
                case SkillCcKind.Sleep:
                    _world.RequestCc(CcRequest.Of(u.Id, CcRequestKind.Sleep, i.Duration, src)); return;
                case SkillCcKind.Impulse:
                    _world.RequestCc(CcRequest.Push(u.Id, new float3(i.DirectionXZ.x, 0f, i.DirectionXZ.y) * i.Amount,
                                                    i.Duration, src));
                    return;
                default:
                    // `Slow`·`DoT` 는 **저작 토큰**이다 — 런타임 군중 제어 슬롯이 없다(6a 구현 9).
                    Warn($"[Intent] 군중 제어 {(SkillCcKind)i.Selector} 는 런타임 슬롯이 아니다 — 감속은 이동속도 스탯, 지속 피해는 DotSet.");
                    return;
            }
        }

        private void ApplyDot(in SimIntent i)
        {
            var u = U(i.Target);
            if (u == null) return;
            // 배치 스킬의 지속 피해 = 파이프라인 `OnPlace`(병합 키 2축의 출처 축, 6a 구현 12).
            EffectApply.Dot(_ctx, Id(i.Source), u, DotOrigin.OnPlace, DotElement.None,
                            i.Amount, i.HitThreshold, i.Duration);
        }

        private void ClearCc(in SimIntent i)
        {
            var u = U(i.Target);
            if (u == null) return;
            CcSlotKind slot;
            switch ((SkillCcKind)i.Selector)
            {
                case SkillCcKind.Stun: slot = CcSlotKind.Stun; break;
                case SkillCcKind.Sleep: slot = CcSlotKind.Sleep; break;
                case SkillCcKind.Impulse: slot = CcSlotKind.Impulse; break;
                default: return;
            }
            if (u.Cc.Clear(slot)) _bus.Publish(CoreEvent.CcCleared(Tick, u, slot, CcClearReason.WokeUp));
        }

        private void Taunt(in SimIntent i)
        {
            var enemy = U(i.Target);
            var guardian = U(i.Source);
            if (enemy == null || guardian == null) return;
            _world.AggroRequests.Add(AggroRequest.Taunted(enemy.Id, guardian.Id, i.Duration));
        }

        // 순간이동 — 위치의 주인은 이동이다(요청으로 넘긴다 · `Blinked` 는 이동이 낸다).
        private void Blink(in SimIntent i)
        {
            var u = U(i.Target);
            if (u == null) return;
            if (u.Move != null) { u.Move.HasBlink = true; u.Move.BlinkTo = i.Position; }
            else u.Position = i.Position;
        }

        private int OpponentMask(Faction owner)
            => (int)FactionRelation.OpponentUnitsOf(owner != Faction.None ? owner : _casterFaction);

        private bool ValidProjectile(int defIndex, string what)
        {
            if (defIndex >= 0 && defIndex < _def.Projectiles.Length) return true;
            // 옛 드레인은 dataIndex < 0 이면 요청을 **통째로** 버렸다(피해까지) — bake 가 막지만 여기서도 말한다.
            Warn($"[Intent] {what} 의 탄 정의({defIndex})가 없다 — 요청을 버린다.");
            return false;
        }

        private void SpawnProjectile(in SimIntent i)
        {
            if (!ValidProjectile(i.DataIndex, "스킬 탄")) return;
            ref var pd = ref _def.Projectiles[i.DataIndex];
            var owner = U(i.Source);
            var ownerFaction = owner != null ? owner.Faction : _casterFaction;

            var req = ProjectileRequest.Empty;
            req.DefIndex = i.DataIndex;
            req.Owner = owner != null ? owner.Id : SimEntityId.None;
            req.OwnerFaction = ownerFaction;
            req.TargetMask = OpponentMask(ownerFaction);
            req.TargetLayers = i.TargetTraversalLayers;
            req.Damage = i.Amount;

            // unified-effect-layer unit 1 — **갈래는 궤적 결합 종류 하나다**(H2). 궤적 = 의도 명시 > 탄 정의.
            // ⚠ 옛 자리 갈래는 「대상 없으면 SkyFall × TileAoe」를 **강제**했다. 그 강제는 걷혔다 — 자리형 concrete
            // (`TileMeteorSkill` · `SelfAreaBlastSkill` · `DeathSiteBlastSkill`)와 `ResignationBarrage` 가 의도에 궤적을
            // **명시**한다. 그들의 탄 저작(`Projectile_Meteor` · `_BruiserShock` · `_JjangssenQuake`)은 Homing 이라
            // 명시가 빠지면 대상 결합으로 새어 아래 「대상 없음」 경고로 떨어진다(조용한 오발사 대신).
            Unit victim = null;
            if (i.Target.IsValid)
            {
                victim = U(i.Target);
                if (victim == null) return;   // 조준 대상이 이미 없다(옛 대상 갈래 그대로)
            }
            req.Movement = i.ProjectileMovement != 0 ? (MovementKind)i.ProjectileMovement : (MovementKind)pd.Movement;
            req.Payload = i.ProjectilePayload != 0 ? (PayloadKind)i.ProjectilePayload : (PayloadKind)pd.Payload;
            req.Origin = i.Position;   // 발사 자리 = 의도 좌표(오늘 그대로)
            // 탄 피해는 **flat** 이다 — 시전자 공격력 배율이 안 붙는다(C5 · dc-trigger 계약 7).

            switch (Combat.Projectile.MovementBinding.Of(req.Movement))
            {
                case Combat.Projectile.BindingClass.Entity:
                    // 대상 추적(비수). `TileRange` = 재조준 반경.
                    if (victim == null)
                    {
                        Warn($"[Intent] 대상 결합 탄({req.Movement})인데 조준 대상이 없다 — 요청을 버린다(자리형이면 의도가 궤적을 명시해야 한다).");
                        return;
                    }
                    req.Target = victim.Id;
                    req.Impact = victim.Position;
                    req.Direction = math.normalizesafe((victim.Position - i.Position).xz);
                    req.RetargetTileRange = i.TileRange;
                    break;

                case Combat.Projectile.BindingClass.Cell:
                    // 칸에 떨어지는 것(운석 · 자폭 · 시체 폭발 · 퇴근 운석). 착탄 = 대상의 **현재** 좌표 · 없으면 의도 좌표.
                    // 반경 · 비행(= 예고) 시간 · 예고는 **효과 파라미터**(의도)에서 — 탄 정의가 아니다.
                    req.Impact = victim != null ? victim.Position : i.Position;
                    req.ImpactTileRange = i.TileRange;
                    // 제약 13 — **원점의 몸이 경계 너머까지 실린다**(0 = 자리에 떨어지는 것).
                    // ⚠ 대상 좌표를 쓴 칸 탄은 **자리형**이다 — 그 좌표를 «지정»한 것은 귀속이지 기하가 아니다.
                    // 맞은 적의 몸은 「대상의 몸」 항으로만 붙는다(원점 항이 아니다 · 탐침 `HardCaseMeteorProbeTests`).
                    req.OriginBodyRadius = victim != null ? 0f : i.OriginBodyRadius;
                    req.FlightTime = i.Duration;
                    // 착탄 예고 반경은 **이 효과의 판단**이다(U1 — 효과마다 켜고 끈다 · 탄 정의표에 옮길 저작이 없다).
                    req.TelegraphTileRange = i.Telegraph ? i.TileRange : 0;
                    break;

                default:
                    // 방향(부메랑). `TileRange` = 비행 사거리(칸). 조준 대상은 방향을 정할 뿐 임자가 아니다.
                    req.Impact = victim != null ? victim.Position : i.Position;
                    req.Direction = i.DirectionXZ;
                    req.DistanceOverride = i.TileRange * TileSize;
                    break;
            }
            _world.ProjectileRequests.Add(req);
        }

        // 발사 명세 — **성사와 원자**다: 슬롯의 durable 카운터 전진 + 버스트 개시가 한 자리.
        private void EmitPattern(in SimIntent i)
        {
            var host = U(i.Source);
            if (host == null || _binding == null) return;
            int pat = i.PatternIndex;
            if (pat < 0 || pat >= _def.Patterns.Length) { Warn("[Intent] 발사 명세 줄이 없다 — 발동이 소비됐다."); return; }
            int shots = _def.Patterns[pat].ShotCount;
            if (shots <= 0) return;

            PatternSlotState slot = null;
            var list = _binding.Emitters;
            for (int k = 0; k < list.Count; k++) if (!list[k].Active) { slot = list[k]; break; }
            if (slot == null) { slot = new PatternSlotState(); list.Add(slot); }

            var inst = slot.Instance;
            inst.EnsureCapacity(shots);
            inst.PatternDefIndex = pat;
            inst.LockedTarget = SimEntityId.None;
            // ⚠ 스킬 경로의 탄 피해 = **효과 줄의 피해**(U10 — 명세는 모양만. 평타 연발은 공격 실효값을 쓴다).
            inst.Damage = _binding.Effect.Damage;
            inst.FromSkill = true;
            // 조준이 필요한 패턴(방향 바인딩)은 스킬이 정한 방향·사거리로 나간다(옛 템플릿 origin/direction/maxDistance).
            inst.AimDirection = i.DirectionXZ;
            inst.MaxDistanceOverride = math.lengthsq(i.DirectionXZ) > 0f ? i.TileRange * TileSize : 0f;
            ref var pd = ref _def.Patterns[pat];
            for (int s = 0; s < shots; s++)
            {
                inst.Directions[s] = pd.Shots[s].DirectionT;
                inst.Intervals[s] = pd.Shots[s].IntervalAfterPreviousSec;
            }
            inst.Seed = Combat.Emission.PatternTargeting.ShotSeed(host.Id.Value, _binding.PatternFireCountBase);
            Combat.Emission.PatternShotRandomizer.Apply(inst.Directions, inst.Intervals, pd.RandomizeShotsPerTrigger,
                                                        pd.RandomIntervalMinSec, pd.RandomIntervalMaxSec, inst.Seed);
            slot.PatternDefIndex = pat;
            // 슬롯은 바인딩을 든 자의 목록에 들지만(수명) **쏘는 것은 발동 주체**다(귀속 · 자리 · 스코프 — U3).
            slot.Subject = host.Id;
            slot.FireCountBase = _binding.PatternFireCountBase;
            Combat.Emission.EmitterTick.Begin(ref inst.Runtime, shots, slot.FireCountBase);
            _binding.PatternFireCountBase += shots;
            slot.Active = true;
        }

        private void SpawnZone(in SimIntent i)
        {
            var src = U(i.Source);
            // U10 — 장판의 DoT 피해 = 까는 효과 줄의 피해(장판 줄은 모양·비피해 수치만).
            var h = HazardSpawn.Spawn(_world, _map, _def, i.DataIndex, i.Cell, Id(i.Source),
                                      src != null ? src.Faction : _casterFaction, i.TargetTraversalLayers, Tick,
                                      _binding != null ? _binding.Effect.Damage : 0f);
            if (h == null) Warn($"[Intent] 장판 줄 {i.DataIndex} 이 없다 — 깔지 않는다.");
        }

        private void SpawnField(in SimIntent i)
        {
            FieldCarrier f;
            switch ((SkillFieldKind)i.Selector)
            {
                case SkillFieldKind.AllyBuff:
                    f = new FieldCarrier { Kind = FieldKind.AllyBuff, Center = CenterOf(i.Cell), Range = i.TileRange,
                                           Stat = (StatKind)i.Selector2, Magnitude = i.Amount, Duration = i.Duration };
                    break;
                case SkillFieldKind.Pull:
                    f = new FieldCarrier { Kind = FieldKind.Pull, Center = CenterOf(i.Cell), Range = i.TileRange,
                                           Speed = i.Amount, Duration = i.Duration };
                    break;
                case SkillFieldKind.Portal:
                    // 입구 = **그 칸 자체**(반경 0 칸, 자리형) — 옛 `SpawnPortal(…, tileSize * 0.5, …)`.
                    // 칸 반폭은 판정 진입점(`SkillMath.ReachFromCell`)의 성질이라 여기서 싣지 않는다(제약 13).
                    f = new FieldCarrier { Kind = FieldKind.Portal, Center = CenterOf(i.Cell), Exit = CenterOf(i.Cell2),
                                           Range = 0f, Duration = i.Duration };
                    break;
                default:
                    Warn($"[Intent] 모르는 장 {(SkillFieldKind)i.Selector} — 깔지 않는다.");
                    return;
            }
            f.Faction = (int)_casterFaction;
            f.Source = Id(i.Source);
            _world.SpawnField(f, Tick);
        }

        private float3 CenterOf(int2 cell) => _map != null ? _map.CenterOf(cell) : new float3(cell.x, 0f, cell.y);

        // 궁극기 — 잠금 + 무적 **원자 개시**(S19). 굴리는 것은 `CombatPhase.StepLeap`.
        private void BeginUltimateLeap(in SimIntent i)
        {
            var u = U(i.Target);
            if (u == null) return;
            var pg = Progressive(u);
            pg.LeapActive = true;
            pg.LeapRemaining = math.max(BattleMatch.Dt, i.Duration);
            pg.LandingWorld = i.Position;
            pg.LeapOrigin = u.Position;
            pg.SlamDamage = i.Amount;
            pg.SlamTileRange = math.max(0, i.TileRange);
            pg.SlamProjectileDefIndex = i.DataIndex;
            if (u.Move != null) u.Move.Locked = true;
            _bus.Publish(CoreEvent.LeapAscend(Tick, u, i.Position, ultimate: true, pg.LeapRemaining,
                                               areaTiles: pg.SlamTileRange));
        }

        // unit 7d — 일반 도약 비행 창 개시. 굴리는 것은 `CombatPhase.StepLeap`(창 끝 = 착지 슬램). 겹쳐 오면 **새 착지점이
        // 이긴다**(옛 브리지는 비행 중 두 번째 신호를 무시했다 — 그런데 sim 은 이미 두 번째 자리로 옮겼다. 슬램이 옛 자리에
        // 터지면 「보스가 없는 곳이 터진다」가 된다).
        private void BeginHop(Unit u, float3 landing, float seconds, float slamDamage, int slamTiles, int slamDef)
        {
            var pg = Progressive(u);
            pg.HopActive = true;
            pg.HopRemaining = seconds;
            pg.HopLanding = landing;
            pg.HopSlamDamage = math.max(0f, slamDamage);
            pg.HopSlamTileRange = math.max(0, slamTiles);
            pg.HopSlamDefIndex = slamDef >= 0 && slamDef < _def.Projectiles.Length ? slamDef : -1;
            if (pg.HopSlamDamage > 0f && pg.HopSlamDefIndex < 0)
                Warn($"[Intent] 보스 {u.Id} 도약 슬램 피해 {pg.HopSlamDamage} 가 저작됐는데 슬램 탄이 없다 — 착지해도 안 터진다.");
        }

        private void DelaySelfAttack(in SimIntent i)
        {
            var u = U(i.Target);
            if (u?.Attack == null) return;
            // 이미 걸린 대기를 줄이지 않는다(옛 규칙 그대로).
            u.Attack.CooldownRemaining = math.max(u.Attack.CooldownRemaining, i.Duration);
        }

        private void ReportIntent(in SimIntent i)
        {
            if (i.Report == SkillReport.NoLandingSpot)
                Warn("[Skill] 착지점 해석 실패로 발동 skip — 상대 진영 앵커가 없거나 링 안에 갈 수 있는 칸이 없다. 경계는 소모됐고 재시도는 없다.");
            else
                Warn($"[Skill] 보고 {i.Report}");
        }

        private void PlayVisual(in SimIntent i)
        {
            switch ((SkillVisualKind)i.Selector)
            {
                case SkillVisualKind.KnockupHop:
                {
                    // 넉업의 실체는 짧은 기절이라 **띄운 쪽이 대상을 직접 신호한다**(기존 사건).
                    var u = U(i.Target);
                    if (u != null) _bus.Publish(CoreEvent.Knockup(Tick, u, i.Duration, i.Amount));
                    return;
                }
                case SkillVisualKind.LeapArc:
                {
                    // 일반 도약 — 코어는 즉시 옮기고 **뷰만** 아치로 난다(`LeapAscend` 일반).
                    // unit 7d — 그리고 **비행 창을 연다.** 스킬(무변)이 슬램 수치를 이 의도에 싣는다(옛 브리지가 뷰 도착 시각에
                    // 슬램을 쐈기 때문이다). 창의 길이는 판의 저작(`Movement.BossLeapFlightSeconds`)이고 뷰는 사건으로 받는다 —
                    // 뷰가 제 시계로 재면 슬로모 중에 두 시계가 갈린다(궁극기 예고와 같은 규율).
                    var u = U(i.Source);
                    if (u == null) return;
                    float seconds = math.max(BattleMatch.Dt, _def.Movement.BossLeapFlightSeconds);
                    BeginHop(u, CenterOf(i.Cell), seconds, i.Amount, i.TileRange, i.DataIndex);
                    _bus.Publish(CoreEvent.LeapAscend(Tick, u, CenterOf(i.Cell), ultimate: false, seconds));
                    return;
                }
                case SkillVisualKind.ShieldGranted:
                case SkillVisualKind.UltimateAscend:
                    // 이미 다른 사건이 나른다(`ShieldGranted` = 부여 관문 · `LeapAscend` = 원자 개시).
                    return;
                default:
                {
                    var src = U(i.Source);
                    var dst = U(i.Target);
                    var at = dst != null ? new Site(dst.Position, dst.HitRadius) : Site.AtCell(i.Position);
                    var from = src != null ? new Site(src.Position, src.HitRadius) : Site.AtCell(i.Position);
                    _bus.Publish(CoreEvent.SkillVisual(Tick, Id(i.Source), Id(i.Target), from, at, _casterFaction,
                                                       i.Selector, i.Duration, i.DataIndex));
                    return;
                }
            }
        }

        // 충전 — 옛 `AddComponent<NextAttackDoubleFire>` 는 기존 값을 **덮어썼다**.
        private void GrantCharge(in SimIntent i)
        {
            var u = U(i.Target);
            if (u == null) return;
            Progressive(u).Charge = math.max(1, (int)i.Amount);
        }

        private void SpawnOrbit(in SimIntent i)
        {
            if (!ValidProjectile(i.DataIndex, "궤도 탄")) return;
            var owner = U(i.Source);
            var ownerFaction = owner != null ? owner.Faction : _casterFaction;
            var req = ProjectileRequest.Empty;
            req.DefIndex = i.DataIndex;
            req.Movement = MovementKind.OrbitAroundPoint;
            req.Payload = PayloadKind.PathHit;
            req.Owner = owner != null ? owner.Id : SimEntityId.None;
            req.OwnerFaction = ownerFaction;
            req.TargetMask = OpponentMask(ownerFaction);
            req.TargetLayers = i.TargetTraversalLayers;
            req.Origin = i.Position;          // 궤도 중심(발사 시점 고정)
            req.Impact = i.Position;
            req.Damage = i.Amount;            // flat — 공격력 배율 미적용
            req.DistanceOverride = i.Radius;  // 궤도 반경(스킬이 월드로 환산했다)
            req.FlightTime = i.Duration;      // 수명
            req.OrbitPhase = i.Phase;
            _world.ProjectileRequests.Add(req);
        }

        // 치명 타이머 — 시간이 끝나면 **죽는다**(옛 `LethalTimerSystem` 이 `DeadTag` 를 붙였다 — 출처 없음 = 미귀속).
        private void StartLethal(in SimIntent i)
        {
            var u = U(i.Target);
            if (u == null) return;
            var pg = Progressive(u);
            pg.LethalActive = true;
            pg.LethalRemaining = i.Duration;
        }

        // 호접몽 — 잠 + 완주 감시 **원자 개시**(S19). 완주 판정은 `CombatPhase` 가 굴린다.
        private void BeginDreamCocoon(in SimIntent i)
        {
            var u = U(i.Target);
            if (u == null || i.Duration <= 0f) return;
            // 옛 전투는 큐가 아니라 **직접** 잠을 걸었다(`EffectSpawner.ApplyCc`) — 같은 틱에 감시와 잠이 선다.
            if (u.Cc.Apply(CcSlotKind.Sleep, i.Duration, float3.zero, u.Id))
                _bus.Publish(CoreEvent.CcApplied(Tick, u, u.Id, CcSlotKind.Sleep, i.Duration));
            var pg = Progressive(u);
            pg.CocoonActive = true;
            pg.CocoonRemaining = i.Duration - ProgressiveStates.CocoonEpsilon;
            pg.CocoonStat = i.Selector;
            pg.CocoonMult = i.Amount;
            pg.CocoonStackId = i.StackId;
        }

        // 살찌운 제물 — **가진 값을 배로** 만든다. 소비(처치 보상)는 손패 담당자(7b)가 이 배율을 읽는다.
        private void ScaleKillReward(in SimIntent i)
        {
            var u = U(i.Target);
            if (u == null || i.Amount <= 0f) return;
            u.AwakeningRewardMul *= i.Amount;
        }

        private ProgressiveStates Progressive(Unit u)
            => u.Progressive ?? (u.Progressive = _world.Parts.RentProgressive());

        // ── MetaIntent 2 ─────────────────────────────────────────────────────

        public void Apply(in MetaIntent i)
        {
            // skill-data-table unit 2 (U4) — 코스트·손패 대기는 **플레이어 자원**이다. 시전자가 플레이어 편이 아니면(적이 든 규칙)
            // **무효** — 적에게는 그 자원이 없다(플레이어 것을 뺏거나 적 전용 자원으로 바꾸지 않는다). 주체 없는 시전(판 · 액티브)의
            // 진영은 발동 문맥(`Begin`)이 싣는다. 오류가 아니라 규칙이라 말하지 않는다.
            if (((int)_casterFaction & Factions.AnyDefender) == 0) return;
            switch (i.Kind)
            {
                case MetaIntentKind.GainCost:
                    if (_cost != null) _cost.Gain((int)math.round(i.Amount), Tick);
                    else Warn("[Intent] 코스트 담당자가 없다 — 코스트 획득을 버린다.");
                    return;
                case MetaIntentKind.ReduceSkillCooldown:
                    if (_hand != null) _hand.ReduceAllCooldowns(i.Amount);
                    else Warn("[Intent] 손패 담당자가 없다 — 쿨다운 단축을 버린다.");
                    return;
                default:
                    Warn($"[Intent] 모르는 메타 의도 {i.Kind} — 버린다.");
                    return;
            }
        }

        // ── 어휘 번역(이름으로) ──────────────────────────────────────────────

        private static CombineOp ToCoreOp(SkillCombineOp op)
        {
            switch (op)
            {
                case SkillCombineOp.Additive: return CombineOp.Additive;
                case SkillCombineOp.Override: return CombineOp.Override;
                default: return CombineOp.Multiplicative;
            }
        }

        private static ModifierOrigin ToCoreOrigin(SkillModifierOrigin o)
        {
            switch (o)
            {
                case SkillModifierOrigin.OnPlace: return ModifierOrigin.OnPlace;
                case SkillModifierOrigin.Skill: return ModifierOrigin.Skill;
                case SkillModifierOrigin.Dreamcatcher: return ModifierOrigin.Dreamcatcher;
                case SkillModifierOrigin.Boss: return ModifierOrigin.Boss;
                case SkillModifierOrigin.HealthThreshold: return ModifierOrigin.HealthThreshold;
                default:
                    // unit 7b — 두 어휘는 **번호가 정렬돼 있다**(`SkillModifierOrigin` 헤더 — 「어댑터가 캐스트한다」).
                    // 스킬 어휘에 이름이 없는 코어 출처(드림스톤 — `DreamstoneStatSkill`)는 번호로 옮긴다.
                    return System.Enum.IsDefined(typeof(ModifierOrigin), (ModifierOrigin)(byte)o)
                        ? (ModifierOrigin)(byte)o : ModifierOrigin.Unspecified;
            }
        }

        private void Warn(string msg) => Report?.Invoke(msg);
    }
}
