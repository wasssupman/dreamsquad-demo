using System.Collections.Generic;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Effects
{
    // ── 어휘 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// battle-sim-extraction unit 18-J/2 — 온천 열기 누적 상태. 구 `HeatAccrual` 이식.
    /// `elapsed` 는 주기 누산기(잔여 이월), `stacks` 는 상한까지만 오르는 래치다.
    /// </summary>
    public struct HeatAccrual
    {
        public float elapsed;
        public byte stacks;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/2 — 온천 기믹 저작값. 구 `OnsenGimmickConfig` 이식.
    ///
    /// ⚠ **싱글턴 엔티티**로 둔다(`SimConfig` 가 아니라) — `RedBullGimmickConfig` 선례다.
    /// 부재 = 기믹 비활성이고, 그것이 구 `RequireForUpdate`(분류 B)가 이사한 자리다.
    /// (`ClockOutConfig` 가 `SimConfig` 로 간 것과 갈리지만 그건 18-G 가 이미 그렇게 정했다 —
    /// 여기서 통일하려 들면 그 조각의 계약을 건드린다.)
    /// </summary>
    public struct OnsenGimmickConfig
    {
        public float heatInterval;
        /// 이 스택 **이하**면 회복, 초과면 손실.
        public byte flipThreshold;
        public float healPercent;
        public float lossPercent;
        public byte heatMaxStack;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/2 — 열기 델타. 구 `HeatMath` 이식(본문 그대로).
    /// 반환: `&gt;0` 회복 · `&lt;0` 피해 · `0` no-op.
    /// </summary>
    public static class HeatMath
    {
        public static float Delta(int stacks, int flipThreshold, float maxHp, float currentHp,
                                  float healPercent, float lossPercent)
        {
            if (stacks <= flipThreshold)
            {
                // ⚠ **오버힐을 잘라낸다** — 만피 유닛이 매 주기 회복 VFX 를 뿜지 않게 한다.
                float headroom = SimMath.Max(0f, maxHp - currentHp);
                return SimMath.Min(maxHp * healPercent, headroom);
            }

            // ⚠ **HP 1 밑으로 내리지 않는다** — 열기는 사망 원인이 될 수 없다.
            float floorRoom = SimMath.Max(0f, currentHp - 1f);
            return -SimMath.Min(maxHp * lossPercent, floorRoom);
        }
    }

    /// 맵 위 소비형 픽업의 종류. 구 `PickupKind` 이식. ⚠ append-only.
    public enum PickupKind : byte
    {
        /// 야근 기믹 — 소비 시 라스트런(공속 버프 → 만료 시 자해).
        Redbull = 0,
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/2 — 맵 위 소비형 픽업. 구 `Pickup` 이식.
    /// 미소비 시 `remainingLife` 만료로 사라진다. **one-shot 소비형**이라 지속 영역(해저드)과
    /// 별개 아키타입이다.
    /// </summary>
    public struct Pickup
    {
        public SimInt2 cell;
        public PickupKind kind;
        public float remainingLife;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/2 — 픽업 스폰 상태(싱글턴). 구 `PickupSpawnState` 이식.
    ///
    /// ⚠ <see cref="rng"/> 는 **상태 해시에 실린다**(구 트레이스가 이 싱글턴을 통째로 남긴다).
    /// 초기 시드는 `SimConfig.PickupSeed` 이고, draw 한 번만 어긋나도 그 뒤 스폰 셀이 전부 갈린다.
    ///
    /// ⚠ <see cref="candidateCells"/> 는 **맵 빌드가 채운다**. 비어 있으면(맵 미빌드) 스폰이 없다 —
    /// 구 `RequireForUpdate&lt;PickupSpawnState&gt;` 가 하던 게이트의 후계다.
    /// </summary>
    public struct PickupSpawnState
    {
        public SimInt2[] candidateCells;
        public float elapsed;
        public SimRandom rng;
    }

    // ── 시스템 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// battle-sim-extraction unit 18-J/2 — 캡처 **#21** · <see cref="SimPhase.PostMoveCast"/>(P5).
    /// 구 `HeatAccrualSystem` 이식.
    ///
    /// 모든 유닛(방어+적)에 주기마다 열기 +1 → <see cref="HeatMath.Delta"/> → **부호별로**
    /// 회복/피해 인박스에 append 한다. 피해 정산(#34, P9)이 그 뒤라 **같은 프레임에 반영**된다.
    ///
    /// ⚠ 구 sim 의 2-pass(lazy attach → tick)를 유지한다. 신 sim 에는 `BufferLookup` 무효화
    /// 문제가 없지만 **부착 프레임에 이미 한 번 tick 하는 동작**이 그 순서에서 나온다.
    ///
    /// ⚠ `heatInterval &lt;= 0` 은 저작 오류 방어다(0 이면 while 이 끝나지 않는다).
    ///
    /// ⚠ **프레임 내 누적을 로컬 투영값으로 추적**한다 — 큰 dt 로 여러 주기가 한 번에 돌 때도
    /// HP 1 바닥과 오버힐 클램프가 성립해야 한다. 실제 HP 는 아직 안 바뀌었으므로
    /// `health.value` 를 다시 읽으면 매 주기 같은 값이 나와 클램프가 무너진다.
    /// </summary>
    public sealed class HeatAccrualSystem
    {
        private readonly List<SimEntityId> _attach = new List<SimEntityId>();
        private readonly List<SimEntityId> _units = new List<SimEntityId>();

        public void Run(SimWorld world)
        {
            if (!SimSingleton.TryGet<OnsenGimmickConfig>(world, out var config)) return;
            if (config.heatInterval <= 0f) return;

            // Pass 1 — lazy attach. 적은 회복 인박스가 없으므로 함께 연다(회복 수신 가능하게).
            _attach.Clear();
            CollectUnits(world, _units);
            for (int i = 0; i < _units.Count; i++)
                if (!world.Has<HeatAccrual>(_units[i])) _attach.Add(_units[i]);
            for (int i = 0; i < _attach.Count; i++)
            {
                world.Set(_attach[i], new HeatAccrual { elapsed = 0f, stacks = 0 });
                if (world.GetBuffer<IncomingHeal>(_attach[i]) == null)
                    world.AddBuffer<IncomingHeal>(_attach[i]);
            }

            // Pass 2 — 주기마다 열기 +1 → 델타 → 부호별 append.
            float dt = world.DeltaTime;
            CollectUnits(world, _units);
            for (int i = 0; i < _units.Count; i++)
            {
                SimEntityId e = _units[i];
                if (!world.TryGet<HeatAccrual>(e, out var accrual)) continue;
                if (!world.TryGet<Health>(e, out var health)) continue;

                accrual.elapsed += dt;

                float projectedHp = health.value;
                float maxHp = health.max;

                while (accrual.elapsed >= config.heatInterval)
                {
                    accrual.elapsed -= config.heatInterval;
                    if (accrual.stacks < config.heatMaxStack) accrual.stacks = (byte)(accrual.stacks + 1);

                    float delta = HeatMath.Delta(accrual.stacks, config.flipThreshold, maxHp, projectedHp,
                                                 config.healPercent, config.lossPercent);

                    if (delta > 0f)
                    {
                        world.GetBuffer<IncomingHeal>(e)?.Add(new IncomingHeal { amount = delta });
                    }
                    else if (delta < 0f)
                    {
                        // ⚠ `source` 는 비운다 — 환경 피해는 킬을 귀속시키지 않는다.
                        world.GetBuffer<IncomingDamage>(e)?.Add(new IncomingDamage
                        {
                            amount = -delta, source = SimEntityId.Null,
                        });
                    }

                    projectedHp = SimMath.Clamp(projectedHp + delta, 1f, maxHp);
                }

                world.Set(e, accrual);
            }
        }

        /// 대상 = `Health` 를 가진, 살아 있고 배치 완료된 방어유닛 ∪ 적.
        private static void CollectUnits(SimWorld world, List<SimEntityId> into)
        {
            into.Clear();
            foreach (SimEntityId e in world.With<Health>())
            {
                if (!world.Has<DefenderUnitTag>(e) && !world.Has<AttackUnitTag>(e)) continue;
                if (world.Has<DeadTag>(e) || world.Has<PendingDeployment>(e)) continue;
                into.Add(e);
            }
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/2 — 캡처 **#22** · <see cref="SimPhase.PostMoveCast"/>(P5).
    /// 구 `PickupSpawnSystem` 이식.
    ///
    /// 주기마다 후보 셀 중 하나에 픽업을 놓고, 미소비 픽업은 수명이 다하면 치운다.
    ///
    /// ⚠ **만료 판정이 스폰보다 먼저**다 — 만료 예정 픽업은 점유에서 빠지므로 그 자리에 이번
    /// 프레임 새 픽업이 들어올 수 있다.
    ///
    /// ⚠ 상한 도달 시 **debt 를 interval 로 clamp** 한다 — 안 하면 상한이 풀리는 순간 밀린
    /// 주기가 한꺼번에 터진다. 그 대신 슬롯이 비면 다음 프레임에 정확히 1개가 즉시 나간다.
    /// </summary>
    public sealed class PickupSpawnSystem
    {
        /// dt 급증(에디터 복귀 등) 시 폭주 방지.
        private const int MaxSpawnsPerFrame = 4;
        /// 셀 중복 회피 재시도 상한 — 초과 시 이번 주기는 건너뛴다(보드 포화).
        private const int MaxPickAttempts = 8;

        private readonly List<SimEntityId> _expired = new List<SimEntityId>();
        private readonly HashSet<SimInt2> _occupied = new HashSet<SimInt2>();

        public void Run(SimWorld world)
        {
            if (!SimSingleton.TryGet<RedBullGimmickConfig>(world, out var config)) return;
            if (config.redbullSpawnInterval <= 0f) return;
            if (!SimSingleton.TryGet<PickupSpawnState>(world, out var spawnState)) return;

            float dt = world.DeltaTime;
            _expired.Clear();
            _occupied.Clear();

            // 만료 tick + despawn. 만료 예정은 점유에서 제외한다.
            foreach (SimEntityId e in world.With<Pickup>())
            {
                var pickup = world.Get<Pickup>(e);
                pickup.remainingLife -= dt;
                world.Set(e, pickup);
                if (pickup.remainingLife <= 0f) { _expired.Add(e); continue; }
                _occupied.Add(pickup.cell);
            }
            for (int i = 0; i < _expired.Count; i++) world.Destroy(_expired[i]);

            SimEntityId stateEntity = SimSingleton.FindEntity<PickupSpawnState>(world);
            var cells = spawnState.candidateCells;
            spawnState.elapsed += dt;

            if (cells != null && cells.Length > 0)
            {
                int spawned = 0;
                while (spawnState.elapsed >= config.redbullSpawnInterval && spawned < MaxSpawnsPerFrame)
                {
                    if (_occupied.Count >= config.redbullMaxActive)
                    {
                        spawnState.elapsed = SimMath.Min(spawnState.elapsed, config.redbullSpawnInterval);
                        break;
                    }
                    spawnState.elapsed -= config.redbullSpawnInterval;

                    // ⚠ rng 는 **찾든 못 찾든** 소비한 만큼 전진한다(구 sim 그대로).
                    var rng = spawnState.rng;
                    SimInt2 chosen = default;
                    bool found = false;
                    for (int attempt = 0; attempt < MaxPickAttempts; attempt++)
                    {
                        SimInt2 candidate = cells[rng.NextInt(0, cells.Length)];
                        if (_occupied.Contains(candidate)) continue;
                        chosen = candidate;
                        found = true;
                        break;
                    }
                    spawnState.rng = rng;

                    if (!found) continue; // 보드 포화 — 이번 주기 건너뛴다

                    _occupied.Add(chosen);
                    var pickupEntity = world.CreateInternal();
                    world.Set(pickupEntity, new Pickup
                    {
                        cell = chosen,
                        kind = PickupKind.Redbull,
                        remainingLife = config.redbullLifetime,
                    });
                    spawned++;
                }
            }

            world.Set(stateEntity, spawnState);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/2 — 캡처 **#23** · <see cref="SimPhase.PostMoveCast"/>(P5).
    /// 구 `PickupConsumeSystem` 이식.
    ///
    /// 같은 셀에 선 유닛이 픽업을 먹으면 **라스트런**(공속 버프 + 만료 시 자해 타이머)을 얻는다.
    ///
    /// ⚠ **소비 락**: 라스트런 진행 중인 유닛은 밟아도 먹지 않는다 — 픽업은 보드에 남아 만료되거나
    /// 다른 유닛이 먹는다. 이게 없으면 재소비로 타이머를 리셋해 crash 를 무한히 회피할 수 있다.
    ///
    /// ⚠ **방어유닛은 배치 셀, 적은 현재 위치**로 판정한다 — 배치 셀이 권위값이다.
    /// 방어유닛이 먼저 순회되므로 같은 셀 경합에서 **방어유닛이 이긴다**(구 sim 순서).
    /// </summary>
    public sealed class PickupConsumeSystem
    {
        private readonly SimChannels _channels;
        private readonly Dictionary<SimInt2, SimEntityId> _byCell = new Dictionary<SimInt2, SimEntityId>();
        private readonly List<SimEntityId> _consumers = new List<SimEntityId>();

        public PickupConsumeSystem(SimChannels channels) => _channels = channels;

        public void Run(SimWorld world)
        {
            if (!SimSingleton.TryGet<RedBullGimmickConfig>(world, out var config)) return;
            if (!SimSingleton.TryGet<FlowFieldSingleton>(world, out var flow)) return;

            _byCell.Clear();
            foreach (SimEntityId e in world.With<Pickup>())
            {
                // 같은 셀 중복이면 **첫 픽업만**(스폰이 중복을 피하므로 정상 경로엔 없다).
                var cell = world.Get<Pickup>(e).cell;
                if (!_byCell.ContainsKey(cell)) _byCell[cell] = e;
            }
            if (_byCell.Count == 0) return;

            // 방어유닛 — 배치 셀(권위값).
            _consumers.Clear();
            foreach (SimEntityId e in world.With<DefenderTile>())
            {
                if (!world.Has<DefenderUnitTag>(e)) continue;
                if (world.Has<PendingDeployment>(e) || world.Has<DeadTag>(e)) continue;
                _consumers.Add(e);
            }
            for (int i = 0; i < _consumers.Count; i++)
                TryConsume(world, world.Get<DefenderTile>(_consumers[i]).cell, _consumers[i], config);

            // 적 — 현재 위치 → 셀.
            _consumers.Clear();
            foreach (SimEntityId e in world.With<AttackUnitTag>())
            {
                if (world.Has<PendingDeployment>(e) || world.Has<DeadTag>(e)) continue;
                if (!world.Has<SimTransform>(e)) continue;
                _consumers.Add(e);
            }
            for (int i = 0; i < _consumers.Count; i++)
            {
                SimVec3 pos = world.Get<SimTransform>(_consumers[i]).Position;
                SimInt2 cell = GridMath.WorldToCell(pos, flow.tileSize, flow.gridSize, flow.origin);
                TryConsume(world, cell, _consumers[i], config);
            }
        }

        private void TryConsume(SimWorld world, SimInt2 cell, SimEntityId unit, RedBullGimmickConfig config)
        {
            if (world.Has<LastRun>(unit)) return;              // 라스트런 중 — 재소비 락(픽업 잔존)
            if (!_byCell.TryGetValue(cell, out var pickup)) return;
            _byCell.Remove(cell);
            world.Destroy(pickup);

            _channels.StatApply.Enqueue(new StatModifierApplyEvent
            {
                target = unit,
                stat = StatKind.AttackSpeedMul,
                op = CombineOp.Multiplicative,
                magnitude = config.lastRunAttackSpeedMul,
                duration = config.lastRunDuration,
                source = unit,
                stackId = 0,
                origin = ModifierOrigin.Gimmick,
            });

            // 소비 락을 통과했다 = `LastRun` 미보유 → 단순 부착(refresh 분기 불요).
            world.Set(unit, new LastRun { remaining = config.lastRunDuration });
        }
    }
}
