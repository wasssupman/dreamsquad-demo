// battle-sim-extraction unit 18-E/4 — 필드 빌더의 **특성화 복제**.
// 원본: `AllyBuffFieldSystemTests`(7) · `DefenderFieldSystemTests`(6) — 18-E/1 이 구 sim 에
// 붙여 초록을 확인한 오라클이다. 어서션을 그대로 옮긴다.
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    public class SimAllyBuffFieldTests
    {
        private SimWorld _world;
        private SimChannels _ch;
        private AllyBuffFieldSystem _sys;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _ch = new SimChannels();
            _sys = new AllyBuffFieldSystem(_ch.StatApply);
        }

        private void Field(SimInt2 center, int range, StatKind stat, float magnitude)
            => _world.Set(_world.Create(), new AllyBuffField
            {
                centerCell = center, tileRange = range, stat = stat,
                magnitude = magnitude, remaining = 99f,
            });

        private SimEntityId Defender(SimInt2 cell, bool pending = false, bool dead = false)
        {
            var e = _world.Create();
            _world.Set(e, new DefenderTile { cell = cell });
            if (pending) _world.Set(e, default(PendingDeployment));
            if (dead) _world.Set(e, default(DeadTag));
            return e;
        }

        private void Tick()
        {
            _world.SetDeltaTime(0.016f);
            _sys.Run(_world);
        }

        [Test]
        public void NoField_SelfGate_EmitsNothing()
        {
            Defender(new SimInt2(0, 0));
            Tick();
            Assert.AreEqual(0, _ch.StatApply.Count);
        }

        [Test]
        public void ReemitsEveryFrame_SoLeavingTheFieldRevokesNaturally()
        {
            Field(new SimInt2(0, 0), 1, StatKind.DamageMul, 2f);
            Defender(new SimInt2(0, 0));

            Tick();
            Assert.AreEqual(1, _ch.StatApply.Count);
            _ch.StatApply.Drain();
            Tick();
            Assert.AreEqual(1, _ch.StatApply.Count, "매 프레임 재발행.");
        }

        [Test]
        public void ChebyshevRange_GatesMembership()
        {
            Field(new SimInt2(0, 0), 1, StatKind.DamageMul, 2f);
            Defender(new SimInt2(1, 1));   // 체비셰프 1 — 포함
            Defender(new SimInt2(2, 0));   // 체비셰프 2 — 제외
            Tick();

            Assert.AreEqual(1, _ch.StatApply.Count, "대각선은 거리 1 이다(체비셰프).");
        }

        [Test]
        public void OverlappingFields_StrongestWins_NotAccumulated()
        {
            Field(new SimInt2(0, 0), 2, StatKind.DamageMul, 1.5f);
            Field(new SimInt2(0, 0), 2, StatKind.DamageMul, 3.0f);
            Defender(new SimInt2(0, 0));
            Tick();

            Assert.AreEqual(1, _ch.StatApply.Count, "장판 2장이어도 stat 당 1건.");
            var ev = _ch.StatApply.Drain()[0];
            SimModifierAuthoring.FromMultiplier(3.0f, out CombineOp op, out float mag);
            Assert.AreEqual(op, ev.op);
            Assert.AreEqual(mag, ev.magnitude, 1e-5f, "가장 강한 배율이 이긴다.");
        }

        [Test]
        public void PayloadUsesApplySecDuration_AndSkillOriginAndDedicatedStackId()
        {
            Field(new SimInt2(0, 0), 1, StatKind.AttackSpeedMul, 1.2f);
            var d = Defender(new SimInt2(0, 0));
            Tick();

            var ev = _ch.StatApply.Drain()[0];
            Assert.AreEqual(d, ev.target);
            Assert.AreEqual(d, ev.source, "source 는 대상 자신.");
            Assert.AreEqual(StatKind.AttackSpeedMul, ev.stat);
            Assert.AreEqual(AllyBuffField.ApplySec, ev.duration, 1e-5f,
                "duration 은 항상 ApplySec — 스킬 지속시간이 아니다(refresh 가 max 라 못 내린다).");
            Assert.AreEqual(AllyBuffField.StackId, ev.stackId, "전용 슬롯(3) — 배치 오라(0)와 합산.");
            Assert.AreEqual(ModifierOrigin.Skill, ev.origin);
        }

        [Test]
        public void TwoStats_EmitSeparately()
        {
            Field(new SimInt2(0, 0), 1, StatKind.DamageMul, 2f);
            Field(new SimInt2(0, 0), 1, StatKind.AttackSpeedMul, 1.5f);
            Defender(new SimInt2(0, 0));
            Tick();

            Assert.AreEqual(2, _ch.StatApply.Count, "stat 별로 1건씩.");
        }

        [Test]
        public void PendingOrDead_AreExcluded()
        {
            Field(new SimInt2(0, 0), 2, StatKind.DamageMul, 2f);
            Defender(new SimInt2(0, 0), pending: true);
            Defender(new SimInt2(1, 0), dead: true);
            Tick();

            Assert.AreEqual(0, _ch.StatApply.Count,
                "배치 대기는 아직 판에 없고, 죽은 유닛은 제외.");
        }
    }

    public class SimDefenderFieldTests
    {
        private static readonly SimInt2 Grid = new SimInt2(8, 8);

        private SimWorld _world;
        private DefenderFieldSingleton _field;
        private DefenderFieldSystem _sys;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _field = NewField();
            _world.Set(_world.Create(), _field);
            _sys = new DefenderFieldSystem();
        }

        private static DefenderFieldSingleton NewField()
        {
            int n = Grid.x * Grid.y;
            var f = new DefenderFieldSingleton
            {
                walkMask = new byte[n], flow = new SimVec2[n], dist = new int[n],
                gridSize = Grid, tileSize = 1f, origin = SimVec3.Zero,
            };
            for (int i = 0; i < n; i++) f.walkMask[i] = 1;
            return f;
        }

        private void Boss(float range)
        {
            var e = _world.Create();
            _world.Set(e, default(BossTag));
            _world.Set(e, new AttackState { range = range });
        }

        private void DefenderAt(SimInt2 cell, bool pending = false, bool dead = false)
        {
            var e = _world.Create();
            _world.Set(e, new FactionTag { value = Faction.Defender });
            _world.Set(e, new Health { value = 10f, max = 10f });
            _world.Set(e, SimTransform.FromPosition(new SimVec3(cell.x + 0.5f, 0f, cell.y + 0.5f)));
            if (pending) _world.Set(e, default(PendingDeployment));
            if (dead) _world.Set(e, default(DeadTag));
        }

        private void Tick()
        {
            _world.SetDeltaTime(0.016f);
            _sys.Run(_world);
        }

        private void PoisonDist(int v)
        {
            for (int i = 0; i < _field.dist.Length; i++) _field.dist[i] = v;
        }

        [Test]
        public void NoBoss_SkipsRebuild_LeavingFieldUntouched()
        {
            DefenderAt(new SimInt2(2, 2));
            PoisonDist(12345);
            Tick();

            Assert.AreEqual(12345, _field.dist[0], "보스가 없으면 손대지 않는다.");
        }

        [Test]
        public void WithBoss_AndNoDefender_ResetsAllCellsToMaxValue_ForGoalFallback()
        {
            Boss(1f);
            PoisonDist(7);
            Tick();

            Assert.AreEqual(int.MaxValue, _field.dist[0], "방어유닛 0 = 전 셀 도달불가.");
        }

        [Test]
        public void WithBossAndDefender_BuildsFiniteDistanceNearTheDefender()
        {
            Boss(1f);
            DefenderAt(new SimInt2(4, 4));
            PoisonDist(7);
            Tick();

            Assert.AreNotEqual(int.MaxValue, _field.dist[GridMath.CellIndex(new SimInt2(4, 4), Grid)],
                "방어유닛 인접 셀은 도달 가능해야 한다.");
        }

        [Test]
        public void PendingOrDeadDefenders_AreNotSources()
        {
            Boss(1f);
            DefenderAt(new SimInt2(4, 4), pending: true);
            DefenderAt(new SimInt2(5, 5), dead: true);
            PoisonDist(7);
            Tick();

            Assert.AreEqual(int.MaxValue, _field.dist[GridMath.CellIndex(new SimInt2(4, 4), Grid)],
                "배치 대기 방어유닛은 소스가 아니다.");
        }

        [Test]
        public void NonDefenderFaction_IsNotASource()
        {
            Boss(1f);
            var e = _world.Create();
            _world.Set(e, new FactionTag { value = Faction.Enemy });
            _world.Set(e, new Health { value = 10f, max = 10f });
            _world.Set(e, SimTransform.FromPosition(new SimVec3(4.5f, 0f, 4.5f)));
            PoisonDist(7);
            Tick();

            Assert.AreEqual(int.MaxValue, _field.dist[GridMath.CellIndex(new SimInt2(4, 4), Grid)],
                "진영 비트가 Defender 인 것만 소스다.");
        }

        [Test]
        public void RangeTiles_IsMinFoldAcrossBosses_NotMaxNorFirst()
        {
            int minFold = ZeroDistCells(1f, 5f);
            int shortOnly = ZeroDistCells(1f);
            int longOnly = ZeroDistCells(5f);

            Assert.Less(shortOnly, longOnly, "전제: 사거리가 길수록 소스가 넓다.");
            Assert.AreEqual(shortOnly, minFold,
                "두 보스의 fold 는 **min** — 짧은 쪽만 있을 때와 같은 소스 집합이다.");
            Assert.AreNotEqual(longOnly, minFold, "max fold 였다면 긴 쪽과 같아진다.");
        }

        /// 독립 월드로 한 틱 돌리고 `dist == 0`(=소스) 셀 수를 센다.
        private static int ZeroDistCells(params float[] bossRanges)
        {
            var w = new SimWorld(new SimConfig(1u, 1u));
            var field = NewField();
            w.Set(w.Create(), field);

            foreach (float r in bossRanges)
            {
                var b = w.Create();
                w.Set(b, default(BossTag));
                w.Set(b, new AttackState { range = r });
            }

            var d = w.Create();
            w.Set(d, new FactionTag { value = Faction.Defender });
            w.Set(d, new Health { value = 10f, max = 10f });
            w.Set(d, SimTransform.FromPosition(new SimVec3(4.5f, 0f, 4.5f)));

            w.SetDeltaTime(0.016f);
            new DefenderFieldSystem().Run(w);

            int zero = 0;
            for (int i = 0; i < field.dist.Length; i++) if (field.dist[i] == 0) zero++;
            return zero;
        }
    }
}
