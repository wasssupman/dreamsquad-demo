using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Effects;
using Wassup.BattleCore.Map;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 3 — **날아가는 것이 간다.**
    //
    // 하위 단계의 순서가 계약이다:
    //   ① 발사 요청 소비(스폰)  ② 궤적 전진  ③ 페이로드 해결  ④ 소멸
    //
    // ⚠ **요청은 한 틱 늦게 나간다.** 공격 루프(`CombatPhase`)는 이 단계 **뒤**에 돌므로
    // 틱 N 의 요청은 틱 N+1 에 탄이 된다. 옛 전투도 같았다(요청 캐리어 → ECB → 다음 프레임
    // 브리지 드레인). 이 지연을 없애려고 이 단계를 뒤로 옮기지 말 것 — 그러면 같은 틱에
    // 쏜 탄이 같은 틱에 착탄해 선딜이 사라지고, 즉발 폭발이 공격 사건보다 먼저 배달된다.
    //
    // ⚠ 축이 둘인 이유(궤적 × 페이로드): **도착 조건은 궤적이 소유한다.** 페이로드는
    // 「도착했다」만 듣는다 — 그래서 경로 스윕에게 그 신호는 「착탄」이 아니라 **「비행 종료」**다.
    //
    // 이 단계가 여는 정거장(`object-pipeline-map` 대조용): 탄 스폰 → 이동 → 착탄 → 소멸.
    //
    // unit 6a 가 **끝**에 한 단계를 더했다: 스탯 만료 → 집계 → 최대 체력 → 스택 만료·임계.
    // 여기인 이유는 「이동은 이미 지났고 피해는 아직 안 왔다」이기 때문이다 — 이동이 읽는
    // 값(이동 배율)은 **다음 틱**에 들고, 피해가 읽는 값(받는 피해 배율)은 **이번 틱**에 든다.
    // 그 비대칭이 곧 F29 의 계승이다(지연을 만든 것은 큐가 아니라 단계 순서다).
    public sealed class TickProjectilePhase : ITickPhase
    {
        public string Name => "TickProjectile";

        private readonly MapRuntime _map;

        // 재사용 버퍼 — 틱 중 할당 0.
        private readonly List<SimEntityId> _expired = new List<SimEntityId>(16);
        private readonly List<ModifierSlot> _revoked = new List<ModifierSlot>(8);
        private readonly List<StackSlot> _stacksGone = new List<StackSlot>(4);
        private BounceCandidate[] _bounceCands = new BounceCandidate[64];
        private SimEntityId[] _bounceIds = new SimEntityId[64];
        private Unit[] _victims = new Unit[64];
        private float[] _victimDistSq = new float[64];
        private int[] _victimPick = new int[64];
        // unit 6a2 — 관문이 이번 발사에서 «이미 찬 칸»을 세는 자리. 발사마다 비운다.
        private readonly List<ImbueKey> _foldKeys = new List<ImbueKey>(4);

        public TickProjectilePhase(MapRuntime map) => _map = map;

        public void Run(TickContext ctx)
        {
            SpawnRequested(ctx);
            StepFlight(ctx);
            Despawn(ctx);

            // unit 6a — 효과 슬롯. 열기/피로·픽업은 6b2, 해저드는 6b 다.
            // ⚠ **「피해 그릇이 없으면 같이 멈춘다」를 재현하지 말 것**(C24). 옛 전투는 한
            // 단계가 성격이 다른 일을 겸직해서 그 결합이 생겼다 — 여기서는 재생이 인박스를
            // 안 본다(그 값은 `Unit.RegenPerSec` 로 나가고 소비는 피해 단계가 한다).
            StepEffects(ctx);
        }

        // ── ⑤ 효과 슬롯 ──────────────────────────────────────────────────────
        //
        // 순회는 `SimEntityId` 오름차순(목록 순서)이라 결정론이다.
        private void StepEffects(TickContext ctx)
        {
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];

                // ① 만료 → 회수 사건. **만료도 「다시 접어라」를 켠다**(F21) — 안 켜면
                //    만료가 영원히 안 돌고 모디파이어가 무한 지속된다(실제 이력).
                if (u.Modifiers.Any)
                {
                    _revoked.Clear();
                    if (u.Modifiers.Expire(ctx.Dt, _revoked) > 0)
                        for (int k = 0; k < _revoked.Count; k++)
                            ctx.Bus.Publish(CoreEvent.ModifierRevoked(
                                ctx.Tick, u, _revoked[k].Key.Source, _revoked[k].Key.Stat));
                }

                // ② 집계 → 개체 미러. 읽는 순간 접힌다(dirty 가 아니면 캐시 그대로).
                var eff = u.Modifiers.Effective;
                u.RegenPerSec = eff.RegenPerSec;
                u.DamageTakenMul = eff.DmgTakenMul;

                // ③ 최대 체력.
                StepMaxHealth(u, eff.MaxHealthMul);

                // ④ 스택.
                StepStacks(ctx, u);
            }
        }

        /// <summary>
        /// 최대 체력 배율의 적용. **기준은 스폰 시점 원본**이고 배율이 1 에서 벗어난 첫 틱에
        /// 잡는다(lazy-attach) — 스폰 경로를 하나도 안 고치는 방식이다.
        ///
        /// ⚠ 체력을 다른 담당자가 드는 개체(마음 타워)는 건너뛴다 — 그 최대치는 담당자의 것이다.
        /// ⚠ 배율이 1 로 돌아와도 **무료 회복은 없다**(축소 때 잘린 것은 잘린 채다).
        /// </summary>
        private static void StepMaxHealth(Unit u, float mul)
        {
            if (u.HealthExternal || mul <= 0f) return;

            if (u.BaseMaxHealth <= 0f)
            {
                if (mul == 1f || u.MaxHealth <= 0f) return;
                u.BaseMaxHealth = u.MaxHealth;
            }

            MaxHealthScale.Apply(u.Health, u.BaseMaxHealth, mul,
                                 out float value, out float max);
            u.Health = value;
            u.MaxHealth = max;
        }

        // 스택 — 틱 → 임계 → 만료. **옛 `StackModifierTickSystem` 과 같은 차례**다(지속을
        // 먼저 깎아야 경계값의 발화 횟수가 이식 전후로 같다).
        private void StepStacks(TickContext ctx, Unit u)
        {
            var stacks = u.Stacks;
            if (!stacks.Any) return;

            for (int k = 0; k < stacks.Count; k++)
            {
                stacks.Tick(k, ctx.Dt);
                // **올라가는 길에만** 발화한다 — 차감·만료로 내려온 것은 재발화가 아니다.
                if (stacks.Slots[k].Count > stacks.Slots[k].LastTriggered)
                    DispatchThresholds(ctx, u, k);
            }

            _stacksGone.Clear();
            if (stacks.RemoveExpired(_stacksGone) == 0) return;
            for (int k = 0; k < _stacksGone.Count; k++)
                ctx.Bus.Publish(CoreEvent.StackChanged(ctx.Tick, u, _stacksGone[k].Source,
                                                       _stacksGone[k].Kind, 0));
        }

        // 이번에 넘은 임계를 **전부** 발화한다(4 → 7 이면 5·6·7 이 다 난다).
        // 상한은 **그때그때의 중첩**이라 소비형이 깎은 뒤의 값을 본다.
        private static void DispatchThresholds(TickContext ctx, Unit u, int index)
        {
            var slot = u.Stacks.Slots[index];
            var rules = ctx.Def.StackRules;
            int ruleIndex = StackRules.Resolve(rules, slot.Kind, slot.RuleIndex);

            if (ruleIndex < 0 || rules[ruleIndex].ThresholdCount == 0)
            {
                // 규칙이 없으면 발화도 없다 — 다만 **경계 캐시는 올린다**(안 올리면 매 틱 재진입).
                u.Stacks.Commit(index, slot.Count, slot.Count);
                return;
            }

            var thresholds = rules[ruleIndex].Thresholds;
            int prev = slot.LastTriggered;
            int count = slot.Count;

            for (int t = 0; t < thresholds.Length; t++)
            {
                if (!StackRules.Fires(in thresholds[t], prev, count)) continue;
                Fire(ctx, u, slot.Kind, in thresholds[t]);
                count = StackRules.AfterConsume(in thresholds[t], count);
            }

            // 기준은 **차감된 최종 중첩**이다(F13) — 안 맞추면 같은 임계가 재발화한다.
            u.Stacks.Commit(index, count, count);
            if (count != slot.Count)
                ctx.Bus.Publish(CoreEvent.StackChanged(ctx.Tick, u, slot.Source, slot.Kind, count));
        }

        private static void Fire(TickContext ctx, Unit u, StackKind kind, in StackThresholdDef rule)
        {
            ctx.Bus.Publish(CoreEvent.StackThreshold(ctx.Tick, u, kind, rule.AtStack));

            switch (rule.Derived)
            {
                case StackDerivedKind.ApplyDot:
                    // ⚠ 스택 파생 지속 피해는 **보스에게도 통한다** — 전용 파이프라인이라
                    // 행동불능 면역 술어를 지나지 않는다(옛 전투와 같다. 의도).
                    // 출처가 `None` 인 것도 의도다 — 스택이 터진 것이지 누가 때린 것이 아니다.
                    EffectApply.Dot(ctx, SimEntityId.None, u, DotOrigin.Stack,
                                    DotElementMap.FromStack(kind),
                                    rule.Magnitude, rule.TickInterval, rule.Duration);
                    return;

                case StackDerivedKind.ApplyStun:
                    // 기절은 행동불능이라 **보스 면역에 걸린다**(문이 `RequestCc` 하나다).
                    // `Magnitude` 가 곧 지속이다(`Duration` 은 안 읽는다).
                    ctx.World.RequestCc(CcRequest.Of(u.Id, CcRequestKind.Stun,
                                                     rule.Magnitude, SimEntityId.None));
                    return;

                case StackDerivedKind.ApplyStat:
                    // ⚠ 출처가 **피해자 자신**이라 배치·스킬 감속과 4키가 전부 겹친다 —
                    // 그래서 칸을 종류별로 가른다(`SlotTag.OfStack`). 접으면 강한 배치
                    // 감속이 약한 스택 감속으로 깎이던 버그가 그대로 재현된다(F26).
                    EffectApply.Stat(ctx, u.Id, u, u, (StatKind)rule.Stat, (CombineOp)rule.Op,
                                     rule.Magnitude, rule.Duration, SlotTag.OfStack(kind), 0f,
                                     kind == StackKind.Fatigue
                                         ? ModifierOrigin.Burnout : ModifierOrigin.Stack);
                    return;
            }
        }

        // ── ① 발사 요청 → 탄 ─────────────────────────────────────────────────
        private void SpawnRequested(TickContext ctx)
        {
            var reqs = ctx.World.ProjectileRequests;
            if (reqs.Count == 0) return;

            for (int r = 0; r < reqs.Count; r++)
            {
                var req = reqs[r];
                if (req.DefIndex < 0 || req.DefIndex >= ctx.Def.Projectiles.Length) continue;
                ref var d = ref ctx.Def.Projectiles[req.DefIndex];

                var p = ctx.World.SpawnProjectile(ctx.Tick);
                p.DefIndex = req.DefIndex;
                p.Movement = req.Movement;
                p.Payload = req.Payload;
                p.Owner = req.Owner;
                p.OwnerFaction = req.OwnerFaction;
                p.TargetMask = req.TargetMask;
                p.TargetLayers = req.TargetLayers;
                p.Target = req.Target;
                p.Damage = req.Damage;
                // 제약 13 — 원점의 몸은 **경계 너머까지 실린다.** 0 = 자리에 떨어지는 것.
                p.OriginBodyRadius = req.OriginBodyRadius;

                p.Origin = req.Origin;
                p.Impact = req.Impact;
                p.Position = StartPosition(req);
                p.PrevPos = p.Position;
                p.Direction = math.lengthsq(req.Direction) > 1e-8f
                    ? math.normalize(req.Direction) : new float2(0f, 1f);

                p.Speed = d.Speed;
                p.HitThreshold = d.HitThreshold;
                p.ArcHeight = d.ArcHeight;
                p.SplashRadius = d.SplashRadius;
                p.SplashDamageMul = d.SplashDamageMul;
                p.ImpactTileRange = req.ImpactTileRange > 0 ? req.ImpactTileRange : d.ImpactTileRange;
                p.AoeTargetCap = req.AoeTargetCap;
                p.OnHitCc = req.OnHitCc;
                p.OnHitCcSeconds = req.OnHitCcSeconds;
                p.PierceRemaining = math.max(1, d.PierceCount);
                p.RehitCooldown = d.RehitCooldownSec;
                p.SweepKnockbackSpeed = d.KnockbackDuration > 0f
                    ? d.KnockbackDistance / d.KnockbackDuration : 0f;
                p.SweepKnockbackDuration = d.KnockbackDuration;
                p.ImpactKnockbackDistance = req.ImpactKnockbackDistance;
                p.ImpactKnockbackDuration = req.ImpactKnockbackDuration;
                p.BounceRemaining = req.BounceCount;
                p.BounceTileRange = req.BounceTileRange;
                p.BounceDamageMul = req.BounceDamageMul > 0f ? req.BounceDamageMul : 1f;
                // ⚠ **방향 바인딩의 재조준 반경은 0 이다** — 겨눌 임자가 없다.
                // 같은 필드가 두 뜻을 겸하지 않게 여기서 한 번 접는다.
                p.RetargetTileRange = MovementBinding.Of(req.Movement) == BindingClass.Direction
                    ? 0 : req.RetargetTileRange;
                p.BlockerHealth = d.BlockerHealth;
                p.BlockerBodyRadius = d.BlockerBodyRadius;
                p.OrbitPhase = req.OrbitPhase;
                p.FuseSeconds = req.FuseSeconds;

                float distance = req.DistanceOverride > 0f ? req.DistanceOverride : d.MaxDistance;
                p.MaxDistance = distance;
                p.OrbitRadius = distance;
                // 궤도의 각속도는 선속도 ÷ 반경이다 — 저작은 선속도 하나이고 변환은 여기 한 곳.
                p.AngularSpeed = distance > 1e-4f ? d.Speed / distance : 0f;

                p.FlightTime = ResolveFlightTime(in req, in d, p);
                if (req.Movement == MovementKind.BezierHomingToEntity)
                    Bezier3.ControlPoints(p.Origin, p.Impact, req.SwingIndex,
                                          d.BezierLateral, d.BezierForwardBias,
                                          out p.Control1, out p.Control2);

                // unit 6a2 — **관문.** 요청이 탄이 되기 직전에 시전자의 (저작 착탄 출력 +
                // 부여 슬롯)을 한 번 접어 싣는다. 생산자는 자기가 무엇에 얹히는지 모른다.
                FoldOnHit(ctx, ctx.World.Find(req.Owner), in req, p);

                ctx.Bus.Publish(CoreEvent.ProjectileSpawned(ctx.Tick, p));
            }
            reqs.Clear();
        }

        // ── 관문 — 「이 탄이 무엇을 나르나」(unit 6a2) ───────────────────────
        //
        // **시전자가 쏘는 모든 탄이 시전자의 착탄 효과를 싣는다**(사용자 결정 2026-09-24 ①).
        // 접는 자리가 여기 하나인 것이 이 unit 의 전부다 — 평타 팔 안에서만 주입하던 옛
        // 구조는 포물선탄·카드탄·배치 스킬탄을 원천 배제했고, 생산자마다 접으면 그 배제가
        // 다른 모양으로 되돌아온다.
        //
        // **순서 = 정의표 → 요청 → 부여이고, 뒤에 오는 것이 앞의 것을 덮지 않는다.**
        // 한 키에 한 값이다. 이 규율이 없으면 저작 감속과 부여 감속이 같은 4키 슬롯으로
        // 가서 **나중 것이 먼저 것을 조용히 지운다**(6a `ModifierSet.Apply` 는 같은 키면
        // 크기를 덮어쓴다). 「요청이 명시한 값은 부여가 덮지 않는다」가 그 규율의 이름이다.
        //
        // 겹치는 것은 **부여끼리**다: 같은 키에 여럿이 걸면 **합**이고 상한은 정의표가 준다
        // (사용자 결정 ②). 상한 저작이 없는 키는 부여 시점에 이미 거절됐지만, 정의표가
        // 중간에 바뀌는 경로는 없어도 여기서 한 번 더 확인한다 — 못 찾으면 **안 싣는다**.
        private void FoldOnHit(TickContext ctx, Unit owner, in ProjectileRequest req, Projectile p)
        {
            p.OnHitCount = 0;
            _foldKeys.Clear();

            // ① 정의표 — 시전자가 저작한 착탄 출력. **피해는 뺀다**(요청이 이미 스냅샷했다).
            var authored = owner != null && owner.Attack != null ? owner.Attack.Outputs : null;
            if (authored != null)
                for (int o = 0; o < authored.Length; o++)
                {
                    if (authored[o].Kind == AttackOutputKind.Damage) continue;
                    var key = ImbueKey.OfOutput(in authored[o]);
                    if (!key.IsNone && !Claim(in key)) continue;
                    Carry(p, in authored[o]);
                }

            // ② 요청 — 오늘 요청이 «명시하는» 착탄 출력은 군중 제어 하나다. 값은 이미
            //    `p.OnHitCc` 에 실렸으므로 여기서는 **칸만 잠근다.**
            if (req.OnHitCc != CcRequestKind.None && req.OnHitCcSeconds > 0f)
                Claim(ImbueKey.Cc(req.OnHitCc));

            // ③ 부여 — 빈 칸에만 얹는다.
            var imbue = owner != null ? owner.Imbue : null;
            if (imbue == null || !imbue.Any) return;
            for (int i = 0; i < imbue.Count; i++)
            {
                var key = imbue.Slots[i].Key;
                // 같은 키의 둘째 슬롯은 여기서 걸러진다 — 합은 `SumOf` 가 이미 냈다.
                if (!Claim(in key)) continue;
                if (!ImbueGate.TryCapOf(ctx.Def.ImbueCaps, in key, out float cap))
                {
                    ctx.Warn("[Imbue] 상한 저작이 없는 부여가 탄에 실리려 했다 — 안 싣는다.");
                    continue;
                }
                float magnitude = math.min(cap, imbue.SumOf(in key));
                float seconds = imbue.SecondsOf(in key);
                if (key.Kind == ImbueKind.Cc)
                {
                    p.OnHitCc = (CcRequestKind)key.Target;
                    // 군중 제어의 «세기»는 곧 지속이다(`StackDerivedKind.ApplyStun` 과 같은 규약).
                    p.OnHitCcSeconds = magnitude;
                    continue;
                }
                var line = ImbueGate.ToOutput(in key, magnitude, seconds);
                if (line.Kind != AttackOutputKind.Damage || line.Magnitude > 0f) Carry(p, in line);
            }
        }

        /// <summary>이번 발사에서 그 칸을 처음 차지했나. 이미 찼으면 false(덮지 않는다).</summary>
        private bool Claim(in ImbueKey key)
        {
            for (int i = 0; i < _foldKeys.Count; i++) if (_foldKeys[i] == key) return false;
            _foldKeys.Add(key);
            return true;
        }

        // 탄의 표에 한 줄 얹는다. 배열은 **탄이 들고 돌려쓴다** — 발사마다 정확한 크기로
        // 새로 잡으면 그것이 틱 중 할당이 된다.
        private static void Carry(Projectile p, in AttackOutputDef line)
        {
            if (p.OnHitCount >= p.OnHit.Length)
                System.Array.Resize(ref p.OnHit, p.OnHit.Length == 0 ? 2 : p.OnHit.Length * 2);
            p.OnHit[p.OnHitCount++] = line;
        }

        // 퇴화 저작(속도 0 · 거리 0)은 여기서 클램프하지 않는다 — 그러면 도착 조건이 영원히
        // 거짓인 **불멸 탄**이 조용히 살아남는다. 대신 수명을 유한으로 만든다.
        private static float ResolveFlightTime(in ProjectileRequest req, in ProjectileDef d, Projectile p)
        {
            if (req.FlightTime > 0f) return req.FlightTime;
            switch (req.Movement)
            {
                case MovementKind.BallisticArcToPoint:
                case MovementKind.GrenadeToCell:
                case MovementKind.BezierHomingToEntity:
                    return BallisticArc.FlightTime(p.Origin, p.Impact, d.Speed, math.max(0.01f, d.MinFlightTime));
                case MovementKind.OrbitAroundPoint:
                    return math.max(0.01f, d.MinFlightTime);
                case MovementKind.SkyFall:
                case MovementKind.SkyFallOnEntity:
                    return 0f;   // 예고 없음 = 첫 틱에 도착
                default:
                    return 0f;   // 직선·왕복·호밍은 거리/속도가 수명을 정한다
            }
        }

        private static float3 StartPosition(in ProjectileRequest req)
        {
            switch (req.Movement)
            {
                // 「자리에 떨어지는 것」은 처음부터 착탄점에 있다 — 떨어지는 그림은 뷰의 것이다.
                case MovementKind.SkyFall: return req.Impact;
                default: return req.Origin;
            }
        }

        // ── ② 궤적 전진 + ③ 페이로드 ─────────────────────────────────────────
        private void StepFlight(TickContext ctx)
        {
            var list = ctx.World.Projectiles;
            if (list.Count == 0) return;

            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p.Expired) continue;

                p.PrevPos = p.Position;
                p.Elapsed += ctx.Dt;

                Advance(ctx, p);
                if (p.Expired) continue;

                // 경로 스윕은 **매 틱** 훑는다 — 착탄이 없는 페이로드다.
                if (p.Payload == PayloadKind.PathHit) SweepPath(ctx, p);

                if (!p.ImpactReached) continue;

                switch (p.Payload)
                {
                    case PayloadKind.SingleSplash: ResolveSingleSplash(ctx, p); break;
                    case PayloadKind.TileAoe: ResolveTileAoe(ctx, p); break;
                    case PayloadKind.SpawnBlocker: ResolveSpawnBlocker(ctx, p); break;
                    // PathHit 에게 도착은 **비행 종료**다 — 위에서 마지막 스윕을 이미 했다.
                    case PayloadKind.PathHit: p.Expired = true; break;
                }
            }
        }

        private void Advance(TickContext ctx, Projectile p)
        {
            float tileSize = _map != null ? _map.TileSize : 1f;

            switch (p.Movement)
            {
                case MovementKind.HomingToEntity:
                {
                    var t = Resolve(ctx, p, tileSize);
                    if (t == null) return;
                    float3 to = t.Position - p.Position;
                    to.y = 0f;
                    float dist = math.length(to);
                    float reach = p.HitThreshold + t.HitRadius * tileSize;
                    if (dist <= reach) { p.ImpactReached = true; return; }
                    float step = p.Speed * ctx.Dt;
                    p.Position += (dist > 1e-5f ? to / dist : new float3(0f, 0f, 1f)) * math.min(step, dist);
                    if (dist - step <= reach) p.ImpactReached = true;
                    return;
                }

                case MovementKind.BezierHomingToEntity:
                {
                    var t = Resolve(ctx, p, tileSize);
                    if (t == null) return;
                    float prog = p.FlightTime > 0f ? math.saturate(p.Elapsed / p.FlightTime) : 1f;
                    p.Position = Bezier3.Position(p.Origin, p.Control1, p.Control2, t.Position, prog);
                    if (prog >= 1f) p.ImpactReached = true;
                    return;
                }

                case MovementKind.SkyFallOnEntity:
                {
                    var t = Resolve(ctx, p, tileSize);
                    if (t == null) return;
                    // 엔티티 바인딩 — 자리는 임자의 live 위치를 따른다. 예외가 아니라 **바인딩의 정의**다.
                    p.Position = t.Position;
                    p.Impact = t.Position;
                    if (SkyFall.Arrived(p.Elapsed, p.FlightTime)) p.ImpactReached = true;
                    return;
                }

                case MovementKind.BallisticArcToPoint:
                {
                    float prog = p.FlightTime > 0f ? math.saturate(p.Elapsed / p.FlightTime) : 1f;
                    p.Position = BallisticArc.ArcPosition(p.Origin, p.Impact, p.ArcHeight, prog);
                    if (prog >= 1f) p.ImpactReached = true;
                    return;
                }

                case MovementKind.GrenadeToCell:
                {
                    float prog = p.FlightTime > 0f ? math.saturate(p.Elapsed / p.FlightTime) : 1f;
                    p.Position = BallisticArc.ArcPosition(p.Origin, p.Impact, p.ArcHeight, prog);
                    // 굴러 도착한 뒤 도화선만큼 더 기다린다 — 그 시간은 **이동이 소유한다.**
                    if (p.Elapsed >= p.FlightTime + p.FuseSeconds) p.ImpactReached = true;
                    return;
                }

                case MovementKind.SkyFall:
                {
                    p.Position = p.Impact;   // 자리는 움직이지 않는다
                    if (SkyFall.Arrived(p.Elapsed, p.FlightTime)) p.ImpactReached = true;
                    return;
                }

                case MovementKind.DirectionalLinear:
                {
                    p.Position += new float3(p.Direction.x, 0f, p.Direction.y) * (p.Speed * ctx.Dt);
                    float traveled = math.length((p.Position - p.Origin).xz);
                    // 거리 저작이 없으면 수명이 무한이 된다 — 그 경우 한 칸만 날고 끝낸다.
                    float limit = p.MaxDistance > 0f ? p.MaxDistance : tileSize;
                    if (traveled >= limit) p.ImpactReached = true;
                    return;
                }

                case MovementKind.BoomerangReturn:
                {
                    // ⚠ 발사 축(`Direction`)은 **입력**이다. 되먹이면 발사점 뒤로 날아간다.
                    p.Position = Boomerang.Position(p.Origin, p.Direction, p.MaxDistance,
                                                    p.Speed, p.Elapsed, out _);
                    if (Boomerang.IsComplete(p.MaxDistance, p.Speed, p.Elapsed)) p.ImpactReached = true;
                    return;
                }

                case MovementKind.OrbitAroundPoint:
                {
                    // **주인이 사라지면 구슬도 사라진다** — 궤도는 «누구 주위를 돈다» 가 정의다.
                    if (!p.Owner.IsNone && ctx.World.Find(p.Owner) == null) { p.Expired = true; return; }
                    p.Position = Orbit.Position(p.Origin, p.OrbitRadius, p.AngularSpeed,
                                                p.Elapsed, p.OrbitPhase);
                    p.Direction = Orbit.Tangent(p.AngularSpeed, p.Elapsed, p.OrbitPhase);
                    if (p.Elapsed >= p.FlightTime) p.ImpactReached = true;
                    return;
                }
            }
        }

        /// <summary>
        /// 엔티티 바인딩의 임자를 푼다. 없으면 **같은 반경에서 다시 겨누고**, 그래도 없으면
        /// 소멸한다. 재조준은 「맞히기도 전에 대상이 사라진 경우」이고 튕김(맞고 나서 남은 홉)과
        /// 다른 축이다 — 감쇠도 없고 소비도 안 한다.
        /// </summary>
        private Unit Resolve(TickContext ctx, Projectile p, float tileSize)
        {
            var t = ctx.World.Find(p.Target);
            if (t != null && t.IsTargetable()) return t;

            if (p.RetargetTileRange > 0 && TryRetarget(ctx, p, tileSize)) return ctx.World.Find(p.Target);
            p.Expired = true;
            return null;
        }

        private bool TryRetarget(TickContext ctx, Projectile p, float tileSize)
        {
            int n = CollectBounceCandidates(ctx, p, SimEntityId.None);
            int pick = BounceRetarget.FindNext(p.Position, -1, _bounceCands, n,
                                               p.TargetLayers, p.TargetMask,
                                               p.RetargetTileRange, tileSize);
            if (pick < 0) return false;
            p.Target = _bounceIds[pick];
            return true;
        }

        // ── 페이로드 ──────────────────────────────────────────────────────────

        private void ResolveSingleSplash(TickContext ctx, Projectile p)
        {
            float tileSize = _map != null ? _map.TileSize : 1f;
            var direct = ctx.World.Find(p.Target);
            int hits = 0;

            if (direct != null && direct.IsTargetable() && IsLegal(direct, p))
            {
                Deal(ctx, p, direct, p.Damage);
                // 착탄 넉백 — 유도탄은 **착탄까지 미룬다**(발사 시점에 밀면 빗나간 탄도 민다).
                if (p.ImpactKnockbackDistance > 0f && p.ImpactKnockbackDuration > 0f)
                    PushAway(ctx, direct, p.ImpactKnockbackDistance, p.ImpactKnockbackDuration, p.Owner);
                hits++;
            }

            if (p.SplashRadius > 0f && p.SplashDamageMul > 0f)
            {
                var units = ctx.World.Units;
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u == direct || !u.IsTargetable() || !IsLegal(u, p)) continue;
                    float dx = u.Position.x - p.Position.x;
                    float dz = u.Position.z - p.Position.z;
                    float reach = p.SplashRadius + u.HitRadius * tileSize;
                    if (dx * dx + dz * dz > reach * reach) continue;
                    Deal(ctx, p, u, p.Damage * p.SplashDamageMul);
                    hits++;
                }
            }

            ctx.Bus.Publish(CoreEvent.ProjectileHit(ctx.Tick, p, p.Target, hits));

            // 튕김 — 맞고 나서 남은 홉. 감쇠가 있고 소비형이다.
            if (p.BounceRemaining > 0 && TryBounce(ctx, p, tileSize)) return;
            p.Expired = true;
        }

        private bool TryBounce(TickContext ctx, Projectile p, float tileSize)
        {
            int n = CollectBounceCandidates(ctx, p, p.Target);
            int pick = BounceRetarget.FindNext(p.Position, -1, _bounceCands, n,
                                               p.TargetLayers, p.TargetMask,
                                               p.BounceTileRange, tileSize);
            if (pick < 0) return false;
            p.Target = _bounceIds[pick];
            p.Damage *= p.BounceDamageMul;
            p.BounceRemaining--;
            p.ImpactReached = false;
            return true;
        }

        private void ResolveTileAoe(TickContext ctx, Projectile p)
        {
            var center = _map != null ? _map.CellOf(p.Impact) : (int2)(int2)math.round(p.Impact.xz);
            float tileSize = _map != null ? _map.TileSize : 1f;
            var units = ctx.World.Units;

            int n = 0;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (!u.IsTargetable() || !IsLegal(u, p)) continue;
                // 제약 13 — **착탄 지점** 진입점. 원점에 주인이 있으면 그 몸, 없으면 칸 반폭.
                float dx = (u.Position.x - p.Impact.x) / tileSize;
                float dz = (u.Position.z - p.Impact.z) / tileSize;
                if (!Wassup.Skills.SkillMath.ReachFromImpact(dx, dz, p.ImpactTileRange,
                                                             p.OriginBodyRadius, u.HitRadius)) continue;
                if (n >= _victims.Length) Grow(ref _victims, ref _victimDistSq, ref _victimPick);
                _victims[n] = u;
                _victimDistSq[n] = dx * dx + dz * dz;
                n++;
            }

            int take = AoeTargetCap.SelectNearest(_victimDistSq, n, p.AoeTargetCap, _victimPick);
            for (int k = 0; k < take; k++)
            {
                var u = _victims[_victimPick[k]];
                // 군중 제어는 `Deal` 안에서 함께 나간다 — 칸 광역 전용이던 것이 unit 6a2 에서
                // **착탄 전부**로 넓어졌고, 그 한 곳이 이제 이 함수다.
                Deal(ctx, p, u, p.Damage);
            }

            ctx.Bus.Publish(CoreEvent.ProjectileHit(ctx.Tick, p, SimEntityId.None, take));
            _ = center;   // 칸 좌표는 트레이스 읽기용 — 판정은 위에서 연속 자로 끝났다
            p.Expired = true;
        }

        private void SweepPath(TickContext ctx, Projectile p)
        {
            float tileSize = _map != null ? _map.TileSize : 1f;
            var units = ctx.World.Units;
            int hits = 0;

            for (int i = 0; i < units.Count && p.PierceRemaining > 0; i++)
            {
                var u = units[i];
                if (!u.IsTargetable() || !IsLegal(u, p)) continue;
                float reach = p.HitThreshold + u.HitRadius * tileSize;
                if (!SweepHitMath.SegmentHits(p.PrevPos.xz, p.Position.xz, u.Position.xz, reach)) continue;
                if (!PathHits.CanHit(p.HitRecords, u.Id, p.Elapsed, p.RehitCooldown, out int slot)) continue;

                Deal(ctx, p, u, p.Damage);
                hits++;

                // 기록은 **창**이다. 슬롯을 제자리에 덮어쓴다 — 매 바퀴 append 하면 버퍼가 자란다.
                var rec = new PathHitRecord { Victim = u.Id, NextHitAt = p.Elapsed + p.RehitCooldown };
                if (slot >= 0) p.HitRecords[slot] = rec; else p.HitRecords.Add(rec);

                // 재타격이 열린 탄은 관통을 **소모하지 않는다** — 유일한 종료 조건이 수명이다.
                if (p.RehitCooldown <= 0f) p.PierceRemaining--;

                if (p.SweepKnockbackSpeed > 0f && p.SweepKnockbackDuration > 0f)
                {
                    // 방향은 상태가 아니라 **그 틱 스윕**에서 뽑는다 — 왕복이 두 다리에서
                    // 반대 힘이 되는 것은 그 결과다(저장하지 않는 것이 계약이다).
                    float2 dir = p.Position.xz - p.PrevPos.xz;
                    if (math.lengthsq(dir) > 1e-8f)
                    {
                        dir = math.normalize(dir) * p.SweepKnockbackSpeed;
                        ctx.World.RequestCc(CcRequest.Push(
                            u.Id, new float3(dir.x, 0f, dir.y), p.SweepKnockbackDuration, p.Owner));
                    }
                }
            }

            if (hits > 0) ctx.Bus.Publish(CoreEvent.ProjectileHit(ctx.Tick, p, SimEntityId.None, hits));
            if (p.PierceRemaining <= 0) p.Expired = true;
        }

        // 길막 설치물을 세운다. **피해는 0 이다** — 배럴은 폭탄이 아니라 물건이고,
        // 터지는 것은 부서질 때다(그 폭발은 unit 7 의 사망 seam 이 낸다).
        private void ResolveSpawnBlocker(TickContext ctx, Projectile p)
        {
            if (p.BlockerHealth > 0f)
            {
                var cell = _map != null ? _map.CellOf(p.Impact) : new int2((int)p.Impact.x, (int)p.Impact.z);
                float3 pos = _map != null ? _map.CenterOf(cell) : p.Impact;
                ctx.World.Spawn(UnitKind.BlockingHazard, Faction.BlockingHazard, -1,
                                pos, p.BlockerBodyRadius, p.BlockerHealth,
                                deploying: false, tick: ctx.Tick);
            }
            ctx.Bus.Publish(CoreEvent.ProjectileHit(ctx.Tick, p, SimEntityId.None, 0));
            p.Expired = true;
        }

        // ── ④ 소멸 ───────────────────────────────────────────────────────────
        private void Despawn(TickContext ctx)
        {
            var list = ctx.World.Projectiles;
            _expired.Clear();
            for (int i = 0; i < list.Count; i++)
                if (list[i].Expired) _expired.Add(list[i].Id);
            // 소멸은 `BattleWorld.Destroy` **한 곳**이다(계약 7 — 유닛과 같은 함수).
            for (int i = 0; i < _expired.Count; i++) ctx.World.Destroy(_expired[i], ctx.Tick);
        }

        // ── 공통 ─────────────────────────────────────────────────────────────

        private static bool IsLegal(Unit u, Projectile p)
        {
            if (p.TargetMask != 0 && ((int)u.Faction & p.TargetMask) == 0) return false;
            byte theirs = u.Move != null ? u.Move.TraversalLayers : (byte)0;
            return LayerBits.CanTarget(p.TargetLayers, theirs);
        }

        /// <summary>
        /// 한 피해자에게 **이 착탄이 내는 것 전부**를 얹는다(unit 6a2).
        ///
        /// ⚠ 피해가 0 이어도 나머지는 든다 — 순수 디버프 탄이 그 모양이다(옛 구조는 피해가
        /// 0 이면 착탄이 아무 일도 안 한 것과 같았다).
        /// ⚠ 산출물은 **평타와 같은 함수**(`EffectApply.Outputs`)를 지난다. 평타와 탄이 다른
        /// 자를 쓰면 언젠가 한쪽만 가드를 갖는다(거점 면역이 그 자리다).
        /// ⚠ 출처는 **발사자**다(F30) — 탄 자신이 아니다. 탄으로 보내면 발사마다 새 슬롯이
        /// 생겨 디버프가 곱으로 누적된다(라이브 결함이었다).
        /// ⚠ 피해 배율은 **1** 이다. 배율은 발사 시점에 `Damage` 에 이미 접혔고, 이 표에는
        /// 애초에 피해 줄이 없다(관문이 뺀다).
        /// </summary>
        private static void Deal(TickContext ctx, Projectile p, Unit victim, float amount)
        {
            if (amount > 0f)
                victim.Inbox.Damage.Add(new DamageEntry { Amount = amount, Source = p.Owner });

            if (p.OnHitCount > 0)
                EffectApply.Outputs(ctx, p.Owner, ctx.World.Find(p.Owner), victim,
                                    p.OnHit, p.OnHitCount, 1f);

            // 군중 제어는 출력 표가 아니라 `RequestCc` 로 간다 — 면역 판정의 문이 거기 하나다.
            if (p.OnHitCc != CcRequestKind.None && p.OnHitCcSeconds > 0f)
                ctx.World.RequestCc(CcRequest.Of(victim.Id, p.OnHitCc, p.OnHitCcSeconds, p.Owner));
        }

        // **방향을 모르는 대상은 밀리지 않는다**(C8) — 스폰 직후·고정 구조물이 그렇다.
        // 0 방향으로 밀면 원점으로 빨려든다.
        private static void PushAway(TickContext ctx, Unit victim, float distance, float duration,
                                     SimEntityId source)
        {
            if (victim.Move == null) return;
            float2 travel = victim.Move.LastMoveDir;
            if (math.lengthsq(travel) <= 1e-6f) return;
            float2 v = -math.normalize(travel) * (distance / duration);
            ctx.World.RequestCc(CcRequest.Push(victim.Id, new float3(v.x, 0f, v.y), duration, source));
        }

        private int CollectBounceCandidates(TickContext ctx, Projectile p, SimEntityId exclude)
        {
            var units = ctx.World.Units;
            int n = 0;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (!u.IsTargetable()) continue;
                if (u.Id == exclude) continue;
                if (u.Id == p.Owner) continue;
                if (n >= _bounceCands.Length)
                {
                    System.Array.Resize(ref _bounceCands, _bounceCands.Length * 2);
                    System.Array.Resize(ref _bounceIds, _bounceIds.Length * 2);
                }
                _bounceCands[n] = new BounceCandidate
                {
                    Pos = u.Position,
                    TraversalLayers = u.Move != null ? u.Move.TraversalLayers : (byte)0,
                    Faction = (int)u.Faction,
                    BodyRadius = u.HitRadius,
                };
                _bounceIds[n] = u.Id;
                n++;
            }
            return n;
        }

        private static void Grow(ref Unit[] a, ref float[] b, ref int[] c)
        {
            System.Array.Resize(ref a, a.Length * 2);
            System.Array.Resize(ref b, b.Length * 2);
            System.Array.Resize(ref c, c.Length * 2);
        }
    }
}
