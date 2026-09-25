using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.Skills;
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
        // unit 6a2 — 관문이 이번 발사에서 접는 칸들. 발사마다 비운다.
        private readonly List<FoldSlot> _foldSlots = new List<FoldSlot>(4);

        // unit 6b2 — 기믹 셈판의 게이트. 「그 기믹이 뽑혔나」 하나다(옛 config 싱글턴 4 의 후계).
        private readonly GimmickHost _gimmick;
        private readonly List<SimEntityId> _pickupsGone = new List<SimEntityId>(8);

        public TickProjectilePhase(MapRuntime map, GimmickHost gimmick = null)
        {
            _map = map;
            _gimmick = gimmick;
        }

        public void Run(TickContext ctx)
        {
            SpawnRequested(ctx);
            StepFlight(ctx);
            Despawn(ctx);

            // unit 6b2 — 사직서 임계. **스택 틱 앞**이다(옛 캡처 22 `[UpdateBefore(StackModifierTickSystem)]`).
            // 드랍은 사망 seam(피해 단계 안 = 이 단계 **뒤**)이라, 틱 N 에 떨어진 사직서는 틱 N+1 에
            // 임계를 본다 — 옛 순서(드랍 37 → 다음 프레임 임계 22)와 같다.
            StepResignations(ctx);

            // unit 6a — 효과 슬롯. 해저드는 6b 다.
            // ⚠ **「피해 그릇이 없으면 같이 멈춘다」를 재현하지 말 것**(C24). 옛 전투는 한
            // 단계가 성격이 다른 일을 겸직해서 그 결합이 생겼다 — 여기서는 재생이 인박스를
            // 안 본다(그 값은 `Unit.RegenPerSec` 로 나가고 소비는 피해 단계가 한다).
            StepEffects(ctx);

            // unit 6b2 — 스택 누적 요청(번아웃 피로). **스탯 적용 뒤**다(옛 캡처 30
            // `[UpdateAfter(ModifierApplySystem)]`) — 여기서 쌓은 피로는 **다음 틱의** 스택 단계가
            // 임계를 본다. 그 1틱이 현행이고(`FatigueAccrualSystem.cs:18` 이 「그대로 박제한다」),
            // 이 줄을 `StepEffects` 앞으로 당기면 그것이 곧 밸런스 변경이다.
            StepStackAccruals(ctx);

            // unit 6b — 판 위에 깔린 것의 시계. **이동 뒤**다: 옛 `EffectTickSystem`(27)은
            // `[UpdateAfter(MovementSystem)]` 라서 캐리어가 사라지는 틱에도 이동은 그것을 한 번
            // 더 본다. 길막 노후화는 피해라서 **피해 단계 앞**이면 같은 틱에 정산된다.
            StepCarriers(ctx);
            StepBlockerDecay(ctx);

            // unit 6b2 — 라스트런 crash → 픽업(수명 → 소비). 둘 다 **피해 단계 앞**이다(옛 캡처 0 ·
            // 24·25). crash 가 소비보다 먼저인 것도 옛 순서다 — 이번 틱에 먹은 유닛의 타이머는
            // 다음 틱부터 흐른다.
            StepLastRun(ctx);
            StepPickups(ctx);
        }

        // ── ⑧ 사직서 임계 ───────────────────────────────────────────────────
        //
        // **level 폴링**이다 — 판 위 장수가 임계 이상이면 임계마다 1건, 한 틱에 여러 번 넘을 수
        // 있고 그것이 사양이다(rev 3 §2 · 옛 `count / threshold`). 소모는 가장 오래된 것부터.
        // 임계 도달은 **사건만 낸다** — 운석 barrage 실행은 unit 7 이다.
        private void StepResignations(TickContext ctx)
        {
            var world = ctx.World;
            if (world.Resignations.Count == 0) return;
            if (_gimmick == null || !_gimmick.TryActive(GimmickKind.ClockOut, out var g)) return;
            int threshold = g.ClockOut.ResignationThreshold;
            if (threshold <= 0) return;   // 0 이면 무한 발화 — 옛 가드와 같다(fail-closed)

            int barrages = world.Resignations.Count / threshold;
            if (barrages == 0) return;
            world.ConsumeResignations(barrages * threshold, ctx.Tick);
            for (int b = 0; b < barrages; b++)
                ctx.Bus.Publish(CoreEvent.ResignationThreshold(ctx.Tick, g.ClockOut.MeteorCount, threshold));
        }

        // ── ⑨ 스택 누적 요청 ─────────────────────────────────────────────────
        //
        // 요청 순서(= 넣은 순서)대로 적용한다. 게이트(거점 면역·죽음)는 부여 관문 하나다.
        private static void StepStackAccruals(TickContext ctx)
        {
            var reqs = ctx.World.StackAccruals;
            if (reqs.Count == 0) return;
            for (int i = 0; i < reqs.Count; i++)
            {
                var r = reqs[i];
                var u = ctx.World.Find(r.Target);
                if (u == null || u.Dead) continue;
                EffectApply.Stack(ctx, r.Source, u, r.Kind, r.Amount, 0, 0f, r.RuleIndex);
            }
            reqs.Clear();
        }

        // ── ⑩ 라스트런 crash ─────────────────────────────────────────────────
        //
        // 공속 버프는 먹는 순간 스탯 슬롯으로 걸렸고 **스스로 만료된다** — 여기는 지연 crash 만 본다.
        // 타이머의 집은 `ProgressiveStates` 하나다(중단 정책 표가 한 곳).
        // ⚠ crash 는 **출처 없는 피해**다 — 자해라 이것으로 죽어도 처치 보상이 안 난다(옛 `IncomingDamage`
        // 무출처 · 치명 타이머와 같은 규약).
        private static void StepLastRun(TickContext ctx)
        {
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var pg = u.Progressive;
                if (pg == null || !pg.LastRunActive || u.Dead) continue;
                pg.LastRunRemaining -= ctx.Dt;
                if (pg.LastRunRemaining > 0f) continue;
                ctx.World.CrashLastRun(u, ctx.Tick);
                float amount = u.MaxHealth * pg.LastRunFraction;
                if (amount > 0f)
                    u.Inbox.Damage.Add(new DamageEntry { Amount = amount, Source = SimEntityId.None });
            }
        }

        // ── ⑪ 픽업 ───────────────────────────────────────────────────────────
        //
        // 수명 → 소비. 수명이 먼저라 **만료되는 틱에는 못 먹는다**(옛 스폰 시스템 24 의 만료 패스가
        // 소비 25 보다 앞이었다). 놓인 틱에는 안 깎인다(`Pickup.SpawnTick`).
        private void StepPickups(TickContext ctx)
        {
            var world = ctx.World;
            var pickups = world.Pickups;
            if (pickups.Count == 0) return;

            _pickupsGone.Clear();
            for (int i = 0; i < pickups.Count; i++)
            {
                var p = pickups[i];
                if (p.SpawnTick == ctx.Tick) continue;
                p.Remaining -= ctx.Dt;
                if (p.Remaining <= 0f) _pickupsGone.Add(p.Id);
            }
            for (int i = 0; i < _pickupsGone.Count; i++) world.RemovePickup(_pickupsGone[i], null, ctx.Tick);
            if (pickups.Count == 0) return;

            // 소비 효과의 수치는 뽑힌 기믹에서 온다. 안 뽑힌 판에 픽업이 있을 수 없지만(생산자가 전부
            // 게이트를 지난다) 여기서도 한 번 더 막는다 — 막지 않으면 수치 0 짜리 라스트런이 걸린다.
            if (_gimmick == null || !_gimmick.TryActive(GimmickKind.RedBull, out var g)) return;

            float inv = _map != null && _map.TileSize > 1e-6f ? 1f / _map.TileSize : 1f;
            var units = world.Units;
            for (int i = 0; i < pickups.Count;)
            {
                var p = pickups[i];
                var taker = FirstConsumer(units, p, inv);
                if (taker == null) { i++; continue; }
                BeginLastRun(ctx, taker, in g.RedBull);
                world.RemovePickup(p.Id, taker, ctx.Tick);   // 목록이 줄었으니 i 는 그대로
            }
        }

        // 픽업 하나를 먹을 **첫 유닛**(`SimEntityId` 오름차순 — 계약 5).
        //
        // 대상 필터 셋: 방어유닛·적만(거점·길막은 먹지 않는다) · 살아 있고 배치 중이 아님 ·
        // **라스트런 중이 아님**(재소비 락 — 재소비로 타이머를 리셋해 crash 를 무한히 피하던 문제의 수정,
        // 옛 review #2). 락에 걸린 유닛이 밟으면 픽업은 판 위에 남는다.
        private static Unit FirstConsumer(IReadOnlyList<Unit> units, Pickup p, float inv)
        {
            for (int u = 0; u < units.Count; u++)
            {
                var c = units[u];
                if (c.Kind != UnitKind.Defender && c.Kind != UnitKind.Enemy) continue;
                if (c.Dead || c.Deploying) continue;
                if (c.Progressive != null && c.Progressive.LastRunActive) continue;
                // 제약 13 — 픽업은 「자리에 떨어지는 것」. 원점 항 = 칸 반폭(진입점의 성질),
                // 범위 = 0(그 칸 하나), 대상의 몸 = 소비자 몸.
                if (!Wassup.Skills.SkillMath.ReachFromCell(
                        (c.Position.x - p.Center.x) * inv, (c.Position.z - p.Center.z) * inv,
                        PickupReachTiles, c.HitRadius)) continue;
                return c;
            }
            return null;
        }

        /// <summary>
        /// 픽업의 판정 범위(칸). **0 = 놓인 칸 하나**다 — 원점 항(칸 반폭)과 소비자의 몸은 진입점이
        /// 붙인다. 밸런스 값이 아니라 「픽업은 한 칸을 차지한다」의 표현이다.
        /// </summary>
        private const float PickupReachTiles = 0f;

        // 라스트런 개시 — 공속 버프(스탯 슬롯, 자체 만료) + 지연 crash 타이머(진행형 상태).
        // 버프의 출처는 **먹은 자 자신**(옛 `source = unit`), 꼬리표는 `Gimmick` 이다.
        // ⚠ 칸은 **일반 칸**(`SlotTag.Default`)이다 — 옛 인큐가 `stackId = 0` 이었다. `SlotKind.Gimmick`
        // 으로 옮기면 「자기 출처 일반 칸 공속 곱」과 따로 쌓이게 돼 규칙이 바뀐다(옮긴 것은 규칙이다).
        private static void BeginLastRun(TickContext ctx, Unit u, in RedBullSpec spec)
        {
            EffectApply.Stat(ctx, u.Id, u, u, StatKind.AttackSpeedMul, CombineOp.Multiplicative,
                             spec.LastRunAttackSpeedMul, spec.LastRunDuration,
                             SlotTag.Default, 0f, ModifierOrigin.Gimmick);
            if (u.Progressive == null) u.Progressive = ctx.World.Parts.RentProgressive();
            u.Progressive.BeginLastRun(spec.LastRunDuration, spec.LastRunDamageFraction);
        }

        // ── ⑥ 장 캐리어 수명 ─────────────────────────────────────────────────
        //
        // `Duration > 0` 만 깎는다 — 0 이하는 무기한 센티널이다(`FieldCarrier.Duration`).
        // 소멸은 `BattleWorld.DespawnField` 한 문이고 반드시 `FieldDespawned` 가 난다(계약 7).
        private void StepCarriers(TickContext ctx)
        {
            var fields = ctx.World.Fields;
            if (fields.Count == 0) return;
            _expired.Clear();
            for (int i = 0; i < fields.Count; i++)
            {
                var f = fields[i];
                if (f.Duration <= 0f) continue;
                f.Duration -= ctx.Dt;
                if (f.Duration <= 0f) _expired.Add(f.Id);
            }
            for (int i = 0; i < _expired.Count; i++) ctx.World.DespawnField(_expired[i], ctx.Tick);
        }

        // ── ⑦ 길막 노후화 ────────────────────────────────────────────────────
        //
        // ⚠ **죽음 경로가 아니라 피해다.** 부서짐이 문 하나(F11 — 체력 ÷ 초당 감소 = 무간섭 수명)
        // 라서 노후화로 부서진 것도 맞아서 부서진 것과 같은 사망·폭발 문으로 나간다.
        // 출처를 비운다 — 환경 피해는 귀속이 없다(채우면 처치 귀속이 엉뚱한 대상에게 간다).
        private static void StepBlockerDecay(TickContext ctx)
        {
            var rows = ctx.Def.BlockingHazards;
            if (rows.Length == 0) return;
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.BlockingHazard || u.Dead) continue;
                if (u.DefIndex < 0 || u.DefIndex >= rows.Length) continue;
                float decay = rows[u.DefIndex].DecayPerSec;
                if (decay <= 0f) continue;
                u.Inbox.Damage.Add(new DamageEntry { Amount = decay * ctx.Dt, Source = SimEntityId.None });
            }
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
                p.TelegraphTileRange = req.TelegraphTileRange;
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
        // **같은 칸은 합으로 접는다**(사용자 결정 ② · 리뷰 F2). 한 키의 최종 크기는
        // `min(상한, 저작 + 부여 전부의 합)` 이고 지속은 긴 쪽이며 **행은 하나**다.
        // ⚠ 초판은 「먼저 온 쪽이 이긴다」로 잠갔는데, 그러면 킨들러 저작 불 1 + 카드 부여
        // 불 3 이 **1만 실려** 「합이라면서 왜 안 더해지나」가 된다. 덮어쓰기로 저작이
        // 사라지던 위험(6a `ModifierSet.Apply` 는 같은 키면 크기를 덮어쓴다)은 **행이 하나**
        // 라는 사실이 이미 막는다 — 잠글 이유가 없었다.
        //
        // **예외는 요청 명시값 하나**다. 그 발사에만 적용되는 덮어쓰기(`req.OnHitCc`)는
        // 칸을 통째로 가져가고 부여는 얹지 않는다.
        //
        // ⚠ **상한은 부여가 기여한 칸에만 건다.** 저작만 있는 칸에 상한을 걸면 ⑴ 줄이 없는
        // 키의 저작이 통째로 떨어지고 ⑵ 있어도 「한 발이 나르는 부여의 상한」이 저작 밸런스를
        // 조용히 깎는다. 상한 줄이 없으면 **부여분만 탈락하고 저작은 남는다.**
        private void FoldOnHit(TickContext ctx, Unit owner, in ProjectileRequest req, Projectile p)
        {
            p.OnHitCount = 0;
            _foldSlots.Clear();

            // ① 정의표 — 시전자가 저작한 착탄 출력. **`Damage` 만 뺀다**(요청이 발사 시점에
            //    배율까지 접어 이미 스냅샷했다). `Heal` 은 칸이 없어 그대로 실린다 — 결정 ①의
            //    귀결이고, 그 전까지 탄을 쏘는 힐러는 아무도 못 고쳤다.
            var authored = owner != null && owner.Attack != null ? owner.Attack.Outputs : null;
            if (authored != null)
                for (int o = 0; o < authored.Length; o++)
                {
                    if (authored[o].Kind == AttackOutputKind.Damage) continue;
                    var key = ImbueKey.OfOutput(in authored[o]);
                    if (key.IsNone) { Carry(p, in authored[o]); continue; }
                    Accumulate(in key, in authored[o], authored[o].Magnitude, authored[o].Duration);
                }

            // ② 요청 — 그 발사에만 적용되는 덮어쓰기. 칸이 하나뿐이라 **종류를 가리지 않고**
            //    부여를 막는다(같은 종류만 막으면 부여가 다른 종류로 그 칸을 가져간다).
            bool ccFromRequest = req.OnHitCc != CcRequestKind.None && req.OnHitCcSeconds > 0f;

            // ③ 부여 — 같은 칸에 더한다.
            var imbue = owner != null ? owner.Imbue : null;
            if (imbue != null && imbue.Any)
                for (int i = 0; i < imbue.Count; i++)
                {
                    var key = imbue.Slots[i].Key;
                    if (key.Kind == ImbueKind.Cc)
                    {
                        // 탄의 군중 제어 칸은 **하나**다 — 요청이 썼거나 앞 부여가 썼으면 끝이다.
                        if (ccFromRequest || p.OnHitCc != CcRequestKind.None) continue;
                        if (!Capped(ctx, in key, imbue.SumOf(in key), out float seconds)) continue;
                        p.OnHitCc = (CcRequestKind)key.Target;
                        // 군중 제어의 «세기»는 곧 지속이다(`StackDerivedKind.ApplyStun` 과 같은 규약).
                        p.OnHitCcSeconds = seconds;
                        continue;
                    }

                    int at = IndexOfKey(in key);
                    // 같은 키의 둘째 슬롯은 여기서 걸러진다 — 합은 `SumOf` 가 이미 냈다.
                    if (at >= 0 && _foldSlots[at].Folded) continue;

                    var line = at >= 0 ? _foldSlots[at].Line : ImbueGate.ToOutput(in key, 0f, 0f);
                    float authoredMag = at >= 0 ? _foldSlots[at].Magnitude : 0f;
                    if (!Capped(ctx, in key, authoredMag + imbue.SumOf(in key), out float magnitude))
                        continue;   // 상한 줄이 없다 — **부여분만** 떨어지고 저작은 남는다

                    Accumulate(in key, in line, magnitude - authoredMag, imbue.SecondsOf(in key));
                    MarkFolded(in key);
                }

            for (int i = 0; i < _foldSlots.Count; i++)
            {
                var slot = _foldSlots[i];
                var line = slot.Line;
                line.Magnitude = slot.Magnitude;
                line.Duration = slot.Seconds;
                Carry(p, in line);
            }
        }

        /// <summary>
        /// 합을 상한으로 접는다. 반환 = **실을 수 있나**(상한 줄이 없으면 false).
        /// 「저작이 없다」가 「상한이 없다」로 읽히면 근거 없는 무한 부여가 조용히 성립한다.
        /// </summary>
        private static bool Capped(TickContext ctx, in ImbueKey key, float sum, out float capped)
        {
            if (!ImbueGate.TryCapOf(ctx.Def.ImbueCaps, in key, out float cap))
            {
                ctx.Warn("[Imbue] 상한 저작이 없는 부여가 탄에 실리려 했다 — 부여분은 안 싣는다.");
                capped = 0f;
                return false;
            }
            capped = math.min(cap, sum);
            return true;
        }

        // 한 칸에 더한다. 칸의 «모양»(종류·스탯·연산자·스택 종류·최대 중첩)은 **첫 기여자**의
        // 것이고, 뒤에 오는 것은 크기와 지속만 보탠다.
        private void Accumulate(in ImbueKey key, in AttackOutputDef line, float magnitude, float seconds)
        {
            int at = IndexOfKey(in key);
            if (at < 0)
            {
                _foldSlots.Add(new FoldSlot
                {
                    Key = key,
                    Line = line,
                    Magnitude = magnitude,
                    Seconds = seconds,
                });
                return;
            }
            var slot = _foldSlots[at];
            slot.Magnitude += magnitude;
            slot.Seconds = math.max(slot.Seconds, seconds);
            _foldSlots[at] = slot;
        }

        private int IndexOfKey(in ImbueKey key)
        {
            for (int i = 0; i < _foldSlots.Count; i++) if (_foldSlots[i].Key == key) return i;
            return -1;
        }

        private void MarkFolded(in ImbueKey key)
        {
            int at = IndexOfKey(in key);
            if (at < 0) return;
            var slot = _foldSlots[at];
            slot.Folded = true;
            _foldSlots[at] = slot;
        }

        /// <summary>접는 중인 한 칸. 발사 하나의 수명이고 재사용 버퍼에 산다(틱 중 할당 0).</summary>
        private struct FoldSlot
        {
            public ImbueKey Key;
            /// <summary>첫 기여자의 «모양». 크기·지속은 아래 둘이 이긴다.</summary>
            public AttackOutputDef Line;
            public float Magnitude;
            public float Seconds;
            /// <summary>부여를 이미 접었나. 같은 키의 둘째 슬롯을 두 번 더하지 않게 한다.</summary>
            public bool Folded;
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
                    if (u == direct || !u.IsTargetable() || !IsUnitPoolLegal(u, p)) continue;
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
                if (!u.IsTargetable() || !IsAreaLegal(u, p)) continue;
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
            // 관통 소진 튕김의 기준점 — 이 틱에 맞힌 적 중 **스윕 진행 방향으로 가장 앞**
            // (옛 전투는 앞에서부터 맞혔으므로 그 순서의 마지막 피해자와 같다).
            Unit lastVictim = null;
            float lastAlong = float.MinValue;
            float2 sweepDir = p.Position.xz - p.PrevPos.xz;

            for (int i = 0; i < units.Count && p.PierceRemaining > 0; i++)
            {
                var u = units[i];
                if (!u.IsTargetable() || !IsUnitPoolLegal(u, p)) continue;
                float reach = p.HitThreshold + u.HitRadius * tileSize;
                if (!SweepHitMath.SegmentHits(p.PrevPos.xz, p.Position.xz, u.Position.xz, reach)) continue;
                if (!PathHits.CanHit(p.HitRecords, u.Id, p.Elapsed, p.RehitCooldown, out int slot)) continue;

                Deal(ctx, p, u, p.Damage);
                hits++;
                float along = math.dot(u.Position.xz - p.PrevPos.xz, sweepDir);
                if (lastVictim == null || along > lastAlong) { lastVictim = u; lastAlong = along; }

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

            // unit 9c — **더 뚫을 수 없게 된 틱**(관통 소진 또는 사거리 끝)에 튕김이 남아 있으면
            // 마지막으로 맞힌 적에서 다음 적으로 **호밍·단일 착탄으로 바꿔** 다시 난다(옛
            // `ProjectileHitSystem.cs:648-676`). 머신거너 탄은 관통 1 이라 실사용 형태는 「맞히고 튕김」이다.
            // · 튕김은 **그 틱에 맞힌 적이 있을 때만** — 아무도 못 맞히고 사거리 끝에 닿으면 기준점이 없다.
            // · 맞힌 기록(`HitRecords`)은 승계하지 않는다 — 전환 뒤 단일 착탄은 그것을 읽지 않는다.
            // · 옛 코드가 전환 때 산출물 표를 떼어 낸 것은 「스윕엔 안 걸리던 상태이상이 홉에만 걸리는」
            //   비대칭을 막으려던 것이다. 코어는 스윕 피격도 같은 `Deal` 로 산출물을 얹으므로 그
            //   비대칭이 애초에 없다 — 떼어 내면 오히려 홉에서만 빠지는 반대 비대칭이 된다.
            bool spent = p.PierceRemaining <= 0 || p.ImpactReached;
            if (spent && lastVictim != null && p.BounceRemaining > 0
                && TryBounceFrom(ctx, p, lastVictim, tileSize))
            {
                p.Movement = MovementKind.HomingToEntity;   // 방향 → 호밍
                p.Payload = PayloadKind.SingleSplash;       // 스윕 → 단일 착탄
                return;
            }
            if (p.PierceRemaining <= 0) p.Expired = true;
        }

        // 방향탄 튕김 — **맞힌 적의 자리**에서 그 적을 빼고 찾는다(착탄 튕김은 탄의 자리·직격 대상 제외).
        private bool TryBounceFrom(TickContext ctx, Projectile p, Unit from, float tileSize)
        {
            int n = CollectBounceCandidates(ctx, p, from.Id);
            int pick = BounceRetarget.FindNext(from.Position, -1, _bounceCands, n,
                                               p.TargetLayers, p.TargetMask,
                                               p.BounceTileRange, tileSize);
            if (pick < 0) return false;
            p.Target = _bounceIds[pick];
            p.Damage *= p.BounceDamageMul;
            p.BounceRemaining--;
            p.ImpactReached = false;
            return true;
        }

        // 길막 설치물을 세운다. **피해는 0 이다** — 배럴은 폭탄이 아니라 물건이고,
        // 터지는 것은 부서질 때다(그 폭발은 unit 7 의 사망 seam 이 낸다).
        private void ResolveSpawnBlocker(TickContext ctx, Projectile p)
        {
            // unit 6b — 정의 줄이 있으면 **그 문**(`BlockerSpawn`)으로 세운다: 자리 검증(골·막힌
            // 칸·점유)과 노후화·모양이 따라온다. 없으면 탄에 실린 수치로 세우는 unit 3 폴백이다.
            int row = ctx.Def.BlockerOfProjectile(p.DefIndex);
            if (row >= 0)
            {
                var cell = _map != null ? _map.CellOf(p.Impact) : new int2((int)p.Impact.x, (int)p.Impact.z);
                if (BlockerSpawn.TrySpawn(ctx.World, _map, ctx.Def, row, cell, ctx.Tick, out var why) == null)
                    ctx.Warn($"[TickProjectile] 길막 거절 ({cell.x},{cell.y}) — {why}");
            }
            else if (p.BlockerHealth > 0f)
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

        // ── 직격이 아닌 피해자 풀(unit 9c) ──────────────────────────────────
        //
        // 공격 마스크는 **겨눠서 치는** 권리다 — 적의 마스크는 방벽·마음을 품는다(`TargetDefaults.EnemyMask`).
        // 직격은 `IsLegal` 그대로라 그것들을 겨눈 탄은 맞는다(안 그러면 길막이 무적이 되고 공성이 안 된다).
        // 직격이 아닌 풀은 옛 `ProjectileHitSystem` 이 **페이로드마다 다른 진영 그룹**으로 골랐고, 그 차이를 옮긴다.

        /// <summary>
        /// **칸 광역** 피해자인가. 옛 풀 = 진영 파생 그룹(`AnyDefender`/`AnyEnemy`) — 거점은 품고
        /// **길막(방벽)은 어느 쪽에도 없다**(옛 GoalProjectileTests::TileAoe_BlockingHazard_IsVictimOfNeitherPool).
        /// </summary>
        private static bool IsAreaLegal(Unit u, Projectile p)
            => u.Faction != Faction.BlockingHazard && IsLegal(u, p);

        /// <summary>
        /// **스플래시·경로 스윕·튕김·재조준** 피해자인가 — **유닛만**. 옛 풀 = `OpponentUnitsOf`
        /// (`ProjectileHitSystem.cs:330`·`:384`·`:504`·`:651`) · 재조준 = 적 유닛(`ProjectileMoveSystem.cs:78`).
        /// 거점(마음·본능)과 방벽은 빠진다 — 적 스플래시가 마음을 치지 않고, 스윕이 적 본능을 뚫지 않는다.
        /// 공격 마스크와 교집합을 쓰므로 힐러처럼 아군 유닛을 겨누는 저작도 그대로 따라간다.
        /// </summary>
        private static bool IsUnitPoolLegal(Unit u, Projectile p)
            => ((int)u.Faction & Factions.AnyUnit) != 0 && IsLegal(u, p);

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
                if (((int)u.Faction & Factions.AnyUnit) == 0) continue;   // 유닛만 — `IsUnitPoolLegal` 과 같은 이유
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
