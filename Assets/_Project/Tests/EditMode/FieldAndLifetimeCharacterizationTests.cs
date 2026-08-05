// battle-sim-extraction unit 18-E/1 — **특성화 테스트(구 sim)**.
//
// 18-E 클러스터 8시스템 중 오라클이 **0** 인 넷: `LastRunSystem`(#1) ·
// `HazardLifetimeSystem`(#2) · `AllyBuffFieldSystem`(#3) · `DefenderFieldSystem`(#7).
// 계획서 §증인 4 — 구 sim 에 먼저 붙여 초록을 확인하고, 이식 후 어서션 그대로 복제한다.
// 신 코드에 먼저 붙이면 오라클이 아니라 자기 확인이다.
//
// 박제 대상은 **시스템 골격**이다: self-gate 위치 · 매 프레임 재빌드의 "갱신이 곧 회수" ·
// 만료와 인덱스 기여의 순서 · 겹침 승자 규칙 · 스냅샷 필터.
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;   // GridMath
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // ── #1 LastRunSystem ──────────────────────────────────────────────────────

    public class LastRunSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _grp;

        [SetUp]
        public void SetUp()
        {
            _world = new World("LastRunTests");
            _em = _world.EntityManager;
            _grp = _world.CreateSystemManaged<SimulationSystemGroup>();
            _grp.AddSystemToUpdateList(_world.CreateSystem<LastRunSystem>());
        }

        [TearDown]
        public void TearDown() => _world?.Dispose();

        private void Configure(float fraction)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new RedBullGimmickConfig { lastRunDamageFraction = fraction });
        }

        private Entity Victim(float remaining, float maxHp = 100f, bool withDamageBuffer = true)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new LastRun { remaining = remaining });
            _em.AddComponentData(e, new Health { value = maxHp, max = maxHp });
            if (withDamageBuffer) _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private void Tick(float dt)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _grp.Update();
        }

        [Test]
        public void NoGimmickConfig_SelfGate_DoesNotEvenTick()
        {
            var e = Victim(remaining: 1f);
            Tick(5f);

            Assert.IsTrue(_em.HasComponent<LastRun>(e), "기믹 비활성이면 시스템이 안 돈다.");
            Assert.AreEqual(1f, _em.GetComponentData<LastRun>(e).remaining, 1e-5f, "감소조차 없다.");
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(e).Length);
        }

        [Test]
        public void TicksDown_WithoutFiring_WhileRemainingPositive()
        {
            Configure(0.5f);
            var e = Victim(remaining: 1f);
            Tick(0.25f);

            Assert.AreEqual(0.75f, _em.GetComponentData<LastRun>(e).remaining, 1e-5f);
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(e).Length, "만료 전엔 피해 없음.");
        }

        [Test]
        public void OnExpiry_DealsMaxHpFraction_AndRemovesComponent()
        {
            Configure(0.5f);
            var e = Victim(remaining: 0.1f, maxHp: 200f);
            Tick(1f);

            var dmg = _em.GetBuffer<IncomingDamage>(e);
            Assert.AreEqual(1, dmg.Length, "만료 프레임에 1건.");
            Assert.AreEqual(100f, dmg[0].amount, 1e-5f, "최대체력(200) × fraction(0.5).");
            Assert.AreEqual(Entity.Null, dmg[0].source, "자해 — 킬 미귀속(DoT·환경 컨벤션).");
            Assert.IsFalse(_em.HasComponent<LastRun>(e), "만료 후 컴포넌트 제거.");
        }

        [Test]
        public void Expiry_IsAtOrBelowZero_NotStrictlyBelow()
        {
            // 가드가 `remaining > 0f continue` 라 정확히 0 이면 **발동**한다.
            Configure(0.5f);
            var e = Victim(remaining: 1f, maxHp: 100f);
            Tick(1f);   // 정확히 0

            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(e).Length, "remaining==0 은 만료다.");
            Assert.IsFalse(_em.HasComponent<LastRun>(e));
        }

        [Test]
        public void MissingDamageBuffer_StillRemovesComponent_ButDealsNoDamage()
        {
            // 피해 기록은 `Health` **와** `IncomingDamage` 둘 다 있을 때만. 제거는 무조건이다.
            Configure(0.5f);
            var e = Victim(remaining: 0.1f, withDamageBuffer: false);
            Tick(1f);

            Assert.IsFalse(_em.HasComponent<LastRun>(e), "버퍼가 없어도 컴포넌트는 제거된다.");
        }
    }

    // ── #2 HazardLifetimeSystem ───────────────────────────────────────────────

    public class HazardLifetimeSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _grp;
        private NativeParallelMultiHashMap<int2, HazardEffect> _map;

        [SetUp]
        public void SetUp()
        {
            _world = new World("HazardLifetimeTests");
            _em = _world.EntityManager;
            _grp = _world.CreateSystemManaged<SimulationSystemGroup>();
            _grp.AddSystemToUpdateList(_world.CreateSystem<HazardLifetimeSystem>());

            _map = new NativeParallelMultiHashMap<int2, HazardEffect>(64, Allocator.Persistent);
            var s = _em.CreateEntity();
            _em.AddComponentData(s, new HazardSingleton { cellToEffects = _map });
        }

        [TearDown]
        public void TearDown()
        {
            if (_map.IsCreated) _map.Dispose();
            _world?.Dispose();
        }

        private Entity Hazard(float life, int2[] cells, params HazardEffect[] effects)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Hazard { remainingLife = life });
            var cb = _em.AddBuffer<HazardCellsBuffer>(e);
            foreach (var c in cells) cb.Add(new HazardCellsBuffer { cell = c });
            var eb = _em.AddBuffer<HazardEffectsBuffer>(e);
            foreach (var f in effects) eb.Add(new HazardEffectsBuffer { effect = f });
            return e;
        }

        private void Tick(float dt)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _grp.Update();
        }

        [Test]
        public void RebuildsIndex_EveryFrame_SoStaleEntriesCannotSurvive()
        {
            // "갱신이 곧 회수" — 매 프레임 Clear 후 재적재다. 증분 인덱스로 바꾸면
            // 만료된 셀이 남고, 그게 tie-break ⑥(HazardSingleton 셀 순회)의 뿌리다.
            var e = Hazard(10f, new[] { new int2(1, 1) },
                new HazardEffect { kind = CcKind.Slow, param1 = 0.5f });
            Tick(0.1f);
            Assert.AreEqual(1, _map.CountValuesForKey(new int2(1, 1)));

            Tick(0.1f);
            Assert.AreEqual(1, _map.CountValuesForKey(new int2(1, 1)),
                "매 프레임 Clear + 재적재 — 두 배로 쌓이지 않는다.");
            Assert.IsTrue(_em.Exists(e));
        }

        [Test]
        public void ExpiredHazard_IsDestroyed_AndDoesNotContributeToIndex()
        {
            // 만료 판정이 인덱스 적재보다 **앞**이다(continue). 순서를 뒤집으면 죽는 프레임에
            // 한 번 더 장판이 먹는다.
            var e = Hazard(0.05f, new[] { new int2(2, 2) },
                new HazardEffect { kind = CcKind.DoT, param1 = 10f });
            Tick(1f);

            Assert.AreEqual(0, _map.CountValuesForKey(new int2(2, 2)),
                "만료 프레임엔 인덱스에 기여하지 않는다.");
            Assert.IsFalse(_em.Exists(e), "만료 = 파괴(**P12 가 아니라 여기서**).");
        }

        [Test]
        public void IndexIs_CellsCrossEffects()
        {
            var cells = new[] { new int2(0, 0), new int2(0, 1), new int2(1, 0) };
            Hazard(10f, cells,
                new HazardEffect { kind = CcKind.Slow, param1 = 0.5f },
                new HazardEffect { kind = CcKind.DoT, param1 = 3f });
            Tick(0.1f);

            foreach (var c in cells)
                Assert.AreEqual(2, _map.CountValuesForKey(c), $"{c} 에 효과 2개(셀 × 효과 교차곱).");
            Assert.AreEqual(6, _map.Count(), "3 셀 × 2 효과 = 6.");
        }

        [Test]
        public void LifeIsDecremented_BeforeExpiryCheck()
        {
            var e = Hazard(1f, new[] { new int2(0, 0) }, new HazardEffect { kind = CcKind.Slow });
            Tick(0.25f);
            Assert.AreEqual(0.75f, _em.GetComponentData<Hazard>(e).remainingLife, 1e-5f);
        }
    }

    // ── #3 AllyBuffFieldSystem ────────────────────────────────────────────────

    public class AllyBuffFieldSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _grp;
        private NativeQueue<StatModifierApplyEvent> _statQueue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("AllyBuffFieldTests");
            _em = _world.EntityManager;
            _grp = _world.CreateSystemManaged<SimulationSystemGroup>();
            _grp.AddSystemToUpdateList(_world.CreateSystem<AllyBuffFieldSystem>());

            _statQueue = new NativeQueue<StatModifierApplyEvent>(Allocator.Persistent);
            var s = _em.CreateEntity();
            _em.AddComponentData(s, new StatModifierApplyEventsSingleton { queue = _statQueue });
        }

        [TearDown]
        public void TearDown()
        {
            if (_statQueue.IsCreated) _statQueue.Dispose();
            _world?.Dispose();
        }

        private void Field(int2 center, int range, StatKind stat, float magnitude)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new AllyBuffField
            {
                centerCell = center, tileRange = range, stat = stat,
                magnitude = magnitude, remaining = 99f,
            });
        }

        private Entity Defender(int2 cell, bool pending = false, bool dead = false)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new DefenderTile { cell = cell });
            if (pending) _em.AddComponent<PendingDeployment>(e);
            if (dead) _em.AddComponent<DeadTag>(e);
            return e;
        }

        private void Tick()
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + 0.016f, 0.016f));
            _grp.Update();
        }

        [Test]
        public void NoField_SelfGate_EmitsNothing()
        {
            Defender(new int2(0, 0));
            Tick();
            Assert.AreEqual(0, _statQueue.Count);
        }

        [Test]
        public void ReemitsEveryFrame_SoLeavingTheFieldRevokesNaturally()
        {
            // "갱신이 곧 회수" — 매 프레임 재발행이고, 벗어나면 재발행이 끊겨 자연 소멸한다.
            Field(new int2(0, 0), range: 1, stat: StatKind.DamageMul, magnitude: 2f);
            Defender(new int2(0, 0));

            Tick();
            Assert.AreEqual(1, _statQueue.Count);
            _statQueue.Clear();
            Tick();
            Assert.AreEqual(1, _statQueue.Count, "매 프레임 재발행.");
        }

        [Test]
        public void ChebyshevRange_GatesMembership()
        {
            Field(new int2(0, 0), range: 1, stat: StatKind.DamageMul, magnitude: 2f);
            Defender(new int2(1, 1));    // 체비셰프 1 — 포함
            Defender(new int2(2, 0));    // 체비셰프 2 — 제외
            Tick();

            Assert.AreEqual(1, _statQueue.Count, "대각선은 거리 1 이다(체비셰프).");
        }

        [Test]
        public void OverlappingFields_StrongestWins_NotAccumulated()
        {
            // 누적하면 "어느 값이 이기나" 가 청크 순회 순서에 맡겨지고, 만료 swap-back 이
            // 그 순서를 런타임에 바꿔 승자가 무작위가 된다 → 최댓값으로 못박는다.
            Field(new int2(0, 0), range: 2, stat: StatKind.DamageMul, magnitude: 1.5f);
            Field(new int2(0, 0), range: 2, stat: StatKind.DamageMul, magnitude: 3.0f);
            Defender(new int2(0, 0));
            Tick();

            Assert.AreEqual(1, _statQueue.Count, "장판 2장이어도 stat 당 1건.");
            var ev = _statQueue.Dequeue();
            ModifierAuthoring.FromMultiplier(3.0f, out var op, out var mag);
            Assert.AreEqual(op, ev.op);
            Assert.AreEqual(mag, ev.magnitude, 1e-5f, "가장 강한 배율이 이긴다.");
        }

        [Test]
        public void PayloadUsesApplySecDuration_AndSkillOriginAndDedicatedStackId()
        {
            // ⚠ duration 이 **항상** AllyBuffApplySec 이어야 한다. 스킬 지속시간(8초)을 한 번이라도
            // 넣으면 refresh 가 max(old,new) 라 이후 갱신이 그 값을 못 내리고, 장판을 벗어나도
            // 8초간 버프가 남는다 = 장판화가 없애려던 스냅샷 동작으로 회귀.
            Field(new int2(0, 0), range: 1, stat: StatKind.AttackSpeedMul, magnitude: 1.2f);
            var d = Defender(new int2(0, 0));
            Tick();

            var ev = _statQueue.Dequeue();
            Assert.AreEqual(d, ev.target);
            Assert.AreEqual(d, ev.source, "source 는 대상 자신.");
            Assert.AreEqual(StatKind.AttackSpeedMul, ev.stat);
            Assert.AreEqual(EffectSpawner.AllyBuffApplySec, ev.duration, 1e-5f,
                "duration 은 항상 AllyBuffApplySec — 스킬 지속시간이 아니다.");
            Assert.AreEqual(AllyBuffField.StackId, ev.stackId, "전용 슬롯(3) — 배치 오라(0)와 합산.");
            Assert.AreEqual(ModifierOrigin.Skill, ev.origin);
        }

        [Test]
        public void TwoStats_EmitSeparately()
        {
            Field(new int2(0, 0), range: 1, stat: StatKind.DamageMul, magnitude: 2f);
            Field(new int2(0, 0), range: 1, stat: StatKind.AttackSpeedMul, magnitude: 1.5f);
            Defender(new int2(0, 0));
            Tick();

            Assert.AreEqual(2, _statQueue.Count, "stat 별로 1건씩.");
        }

        [Test]
        public void PendingOrDead_AreExcluded()
        {
            Field(new int2(0, 0), range: 2, stat: StatKind.DamageMul, magnitude: 2f);
            Defender(new int2(0, 0), pending: true);
            Defender(new int2(1, 0), dead: true);
            Tick();

            Assert.AreEqual(0, _statQueue.Count,
                "배치 대기는 아직 판에 없고(on-place 오라와 같은 규칙), 죽은 유닛은 제외.");
        }
    }

    // ── #7 DefenderFieldSystem ────────────────────────────────────────────────

    public class DefenderFieldSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _grp;
        private DefenderFieldSingleton _field;

        private static readonly int2 Grid = new int2(8, 8);

        [SetUp]
        public void SetUp()
        {
            _world = new World("DefenderFieldTests");
            _em = _world.EntityManager;
            _grp = _world.CreateSystemManaged<SimulationSystemGroup>();
            _grp.AddSystemToUpdateList(_world.CreateSystem<DefenderFieldSystem>());

            int n = Grid.x * Grid.y;
            _field = new DefenderFieldSingleton
            {
                walkMask = new NativeArray<byte>(n, Allocator.Persistent),
                flow = new NativeArray<float2>(n, Allocator.Persistent),
                dist = new NativeArray<int>(n, Allocator.Persistent),
                gridSize = Grid,
                tileSize = 1f,
                origin = float3.zero,
            };
            for (int i = 0; i < n; i++) _field.walkMask[i] = 1;   // 전 셀 walkable
            var s = _em.CreateEntity();
            _em.AddComponentData(s, _field);
        }

        [TearDown]
        public void TearDown()
        {
            _field.Dispose();
            _world?.Dispose();
        }

        private Entity Boss(float range)
        {
            var e = _em.CreateEntity();
            _em.AddComponent<BossTag>(e);
            _em.AddComponentData(e, new AttackState { range = range });
            return e;
        }

        private Entity DefenderAt(int2 cell, bool pending = false, bool dead = false)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(e, new Health { value = 10f, max = 10f });
            _em.AddComponentData(e, LocalTransform.FromPosition(
                new float3(cell.x + 0.5f, 0f, cell.y + 0.5f)));
            if (pending) _em.AddComponent<PendingDeployment>(e);
            if (dead) _em.AddComponent<DeadTag>(e);
            return e;
        }

        private void Tick()
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + 0.016f, 0.016f));
            _grp.Update();
        }

        private void PoisonDist(int value)
        {
            for (int i = 0; i < _field.dist.Length; i++) _field.dist[i] = value;
        }

        [Test]
        public void NoBoss_SkipsRebuild_LeavingFieldUntouched()
        {
            // 필드 소비자는 보스뿐 — 보스 부재 시 재빌드하지 않는다(스폰 프레임에 Movement 앞에서
            // 다시 돌아 신선한 필드가 보장된다). 재빌드하면 아래 오염값이 지워질 것이다.
            DefenderAt(new int2(2, 2));
            PoisonDist(12345);
            Tick();

            Assert.AreEqual(12345, _field.dist[0], "보스가 없으면 손대지 않는다.");
        }

        [Test]
        public void WithBoss_AndNoDefender_ResetsAllCellsToMaxValue_ForGoalFallback()
        {
            // 방어유닛 0 → 전 셀 int.MaxValue → MovementSystem 의 hunting 판정이 false 로
            // 떨어져 자동으로 기존 goal 마칭(계약 5).
            Boss(1f);
            PoisonDist(7);
            Tick();

            Assert.AreEqual(int.MaxValue, _field.dist[0], "방어유닛 0 = 전 셀 도달불가.");
        }

        [Test]
        public void WithBossAndDefender_BuildsFiniteDistanceNearTheDefender()
        {
            Boss(1f);
            DefenderAt(new int2(4, 4));
            PoisonDist(7);
            Tick();

            int idx = GridMath.CellIndex(new int2(4, 4), Grid);
            Assert.AreNotEqual(int.MaxValue, _field.dist[idx],
                "방어유닛 인접 셀은 도달 가능해야 한다.");
        }

        [Test]
        public void PendingOrDeadDefenders_AreNotSources()
        {
            // FSM 후보 풀과 같은 필터 — 배치 대기·사망은 사냥 대상이 아니다.
            Boss(1f);
            DefenderAt(new int2(4, 4), pending: true);
            DefenderAt(new int2(5, 5), dead: true);
            PoisonDist(7);
            Tick();

            Assert.AreEqual(int.MaxValue, _field.dist[GridMath.CellIndex(new int2(4, 4), Grid)],
                "배치 대기 방어유닛은 소스가 아니다.");
        }

        [Test]
        public void NonDefenderFaction_IsNotASource()
        {
            Boss(1f);
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            _em.AddComponentData(e, new Health { value = 10f, max = 10f });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(4.5f, 0f, 4.5f)));
            PoisonDist(7);
            Tick();

            Assert.AreEqual(int.MaxValue, _field.dist[GridMath.CellIndex(new int2(4, 4), Grid)],
                "진영 비트가 Defender 인 것만 소스다.");
        }

        [Test]
        public void RangeTiles_IsMinFoldAcrossBosses_NotMaxNorFirst()
        {
            // 소스는 "**모든** 헌터가 발사 가능한 셀" 이어야, 사거리 짧은 보스가 dist-0 셀에서
            // 발사 불가로 서버리는 스톨이 구조적으로 불가능하다.
            //
            // `RangeToTiles` 의 구체 변환값을 단정하지 않는다 — 그건 이 시스템의 계약이 아니고,
            // 값을 추측하면 테스트가 변환 구현에 묶인다. 대신 **소스 집합의 크기 관계**로
            // fold 종류를 가른다(변환 무관).
            int minFold = ZeroDistCells(1f, 5f);
            int shortOnly = ZeroDistCells(1f);
            int longOnly = ZeroDistCells(5f);

            Assert.Less(shortOnly, longOnly, "전제: 사거리가 길수록 소스가 넓다.");
            Assert.AreEqual(shortOnly, minFold,
                "두 보스의 fold 는 **min** — 짧은 쪽만 있을 때와 같은 소스 집합이다.");
            Assert.AreNotEqual(longOnly, minFold, "max fold 였다면 긴 쪽과 같아진다.");
        }

        /// 주어진 보스 사거리들로 독립 월드를 세워 한 틱 돌리고 `dist == 0`(=소스) 셀 수를 센다.
        private static int ZeroDistCells(params float[] bossRanges)
        {
            using (var w = new World("DefenderFieldRig"))
            {
                var em = w.EntityManager;
                var grp = w.CreateSystemManaged<SimulationSystemGroup>();
                grp.AddSystemToUpdateList(w.CreateSystem<DefenderFieldSystem>());

                int n = Grid.x * Grid.y;
                var field = new DefenderFieldSingleton
                {
                    walkMask = new NativeArray<byte>(n, Allocator.Temp),
                    flow = new NativeArray<float2>(n, Allocator.Temp),
                    dist = new NativeArray<int>(n, Allocator.Temp),
                    gridSize = Grid, tileSize = 1f, origin = float3.zero,
                };
                try
                {
                    for (int i = 0; i < n; i++) field.walkMask[i] = 1;
                    em.AddComponentData(em.CreateEntity(), field);

                    foreach (float r in bossRanges)
                    {
                        var b = em.CreateEntity();
                        em.AddComponent<BossTag>(b);
                        em.AddComponentData(b, new AttackState { range = r });
                    }

                    var d = em.CreateEntity();
                    em.AddComponentData(d, new FactionTag { value = Faction.Defender });
                    em.AddComponentData(d, new Health { value = 10f, max = 10f });
                    em.AddComponentData(d, LocalTransform.FromPosition(new float3(4.5f, 0f, 4.5f)));

                    w.SetTime(new TimeData(0.016, 0.016f));
                    grp.Update();

                    int zero = 0;
                    for (int i = 0; i < n; i++) if (field.dist[i] == 0) zero++;
                    return zero;
                }
                finally { field.Dispose(); }
            }
        }
    }
}
