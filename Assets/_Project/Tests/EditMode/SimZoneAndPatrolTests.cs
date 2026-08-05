// battle-sim-extraction unit 18-E/5 — 존/순찰 이식 핀.
// 구 오라클(`PatrolAreaMathTests` · PlayMode `DotCoexistenceTest` 등)은 unit 20 까지 계속 진다.
// 여기서 새로 박는 것: **#2 가 구운 역-삽입 순서를 #5 가 그대로 소비하는가**(tie-break ⑥ 의
// 소비측 절반 — 생산측은 18-E/3 이 박았다) · 존의 진영 게이트 · `FillAreaMask` 의 자체 소거.
using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    public class SimZoneApplyTests
    {
        private static readonly SimInt2 Grid = new SimInt2(8, 8);

        private SimWorld _world;
        private SimChannels _ch;
        private HazardCellIndex _index;
        private ZoneApplySystem _sys;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _ch = new SimChannels();
            _index = new HazardCellIndex();
            _world.Set(_world.Create(), new HazardSingleton { cellToEffects = _index });

            int n = Grid.x * Grid.y;
            var field = new FlowFieldSingleton
            {
                flow = new SimVec2[n], dist = new int[n],
                gridSize = Grid, tileSize = 1f, origin = SimVec3.Zero,
                goalCell = new SimInt2(7, 7),
            };
            _world.Set(_world.Create(), field);

            _sys = new ZoneApplySystem(_ch.EnemyCc, _ch.DotApply, _ch.StatApply, _ch.HazardRuntime);
        }

        private SimEntityId Unit(SimInt2 cell, Faction faction, bool mover = true)
        {
            var e = _world.Create();
            _world.Set(e, new FactionTag { value = faction });
            _world.Set(e, SimTransform.FromPosition(new SimVec3(cell.x, 0f, cell.y)));
            if (mover) _world.Set(e, new PathFollowState { speed = 1f });
            return e;
        }

        private void Tick() => _sys.Run(_world);

        [Test]
        public void EmptyIndex_IsANoOp()
        {
            Unit(new SimInt2(1, 1), Faction.Enemy);
            Tick();
            Assert.AreEqual(0, _ch.StatApply.Count + _ch.DotApply.Count + _ch.EnemyCc.Count);
        }

        [Test]
        public void OnlyEnemyFaction_IsAffected()
        {
            _index.Add(new SimInt2(1, 1), new HazardEffect
            { kind = CcKind.Slow, param1 = 0.5f, restDuration = 1f });
            Unit(new SimInt2(1, 1), Faction.Defender);
            Tick();
            Assert.AreEqual(0, _ch.StatApply.Count,
                "아군은 아군 장판에 오폭당하지 않는다 — '이동체 = 적' 암묵 전제를 명시 진영 판정으로 바꿨다.");

            Unit(new SimInt2(1, 1), Faction.Enemy);
            Tick();
            Assert.AreEqual(1, _ch.StatApply.Count);
        }

        [Test]
        public void RequiresPathFollowState()
        {
            _index.Add(new SimInt2(1, 1), new HazardEffect
            { kind = CcKind.Slow, param1 = 0.5f, restDuration = 1f });
            Unit(new SimInt2(1, 1), Faction.Enemy, mover: false);
            Tick();
            Assert.AreEqual(0, _ch.StatApply.Count, "고정 대상은 존의 대상이 아니다.");
        }

        [Test]
        public void SlowToken_RoutesToStatChannel_AsMoveSpeedMultiplier()
        {
            _index.Add(new SimInt2(2, 2), new HazardEffect
            { kind = CcKind.Slow, param1 = 0.4f, restDuration = 3f });
            var e = Unit(new SimInt2(2, 2), Faction.Enemy);
            Tick();

            Assert.AreEqual(0, _ch.EnemyCc.Count, "Slow 는 CC 버퍼로 가지 않는다(저작 토큰일 뿐).");
            var ev = _ch.StatApply.Drain()[0];
            Assert.AreEqual(e, ev.target);
            Assert.AreEqual(StatKind.MoveSpeedMul, ev.stat);
            Assert.AreEqual(CombineOp.Multiplicative, ev.op);
            Assert.AreEqual(0.4f, ev.magnitude, 1e-5f);
            Assert.AreEqual(3f, ev.duration, 1e-5f);
            Assert.AreEqual(SimEntityId.Null, ev.source, "존은 source 를 비운다.");
            Assert.AreEqual(0, ev.stackId);
            Assert.AreEqual(ModifierOrigin.Zone, ev.origin);
        }

        [Test]
        public void DotToken_RoutesToDotChannel_WithZoneOriginAndAuthoredElement()
        {
            _index.Add(new SimInt2(2, 2), new HazardEffect
            {
                kind = CcKind.DoT, param1 = 7f, restDuration = 4f,
                tickInterval = 0.5f, element = DotElement.Poison,
            });
            var e = Unit(new SimInt2(2, 2), Faction.Enemy);
            Tick();

            Assert.AreEqual(0, _ch.EnemyCc.Count);
            var ev = _ch.DotApply.Drain()[0];
            Assert.AreEqual(e, ev.target);
            Assert.AreEqual(DotOrigin.Zone, ev.effect.origin, "해저드가 만들면 언제나 Zone 이다.");
            Assert.AreEqual(DotElement.Poison, ev.effect.element,
                "원소가 없으면 Fire·Poison 해저드가 구분되지 않는다.");
            Assert.AreEqual(7f, ev.effect.scalar, 1e-5f);
            Assert.AreEqual(0.5f, ev.effect.tickInterval, 1e-5f);
            Assert.AreEqual(0f, ev.effect.tickTimer, 1e-5f,
                "tickTimer 는 비운다 — 병합 add-path 가 첫 tick 즉발용으로 초기화한다.");
        }

        [Test]
        public void OtherKinds_RouteToCcChannel()
        {
            _index.Add(new SimInt2(2, 2), new HazardEffect
            { kind = CcKind.Stun, param1 = 1f, restDuration = 2f });
            Unit(new SimInt2(2, 2), Faction.Enemy);
            Tick();

            Assert.AreEqual(1, _ch.EnemyCc.Count);
            Assert.AreEqual(CcKind.Stun, _ch.EnemyCc.Drain()[0].effect.kind);
        }

        // ── tie-break ⑥ 의 소비측 절반 ─────────────────────────────────────────

        [Test]
        public void ConsumesEffects_InIndexOrder_WhichIsReverseInsertion()
        {
            // 생산측(#2)이 역-삽입순으로 읽히게 만들었고, 소비측이 그 순서 그대로 채널에 싣는지 본다.
            // 이 순서가 곧 병합 순서다 — 뒤집히면 겹친 장판의 승자가 달라진다.
            var cell = new SimInt2(3, 3);
            _index.Add(cell, new HazardEffect { kind = CcKind.Slow, param1 = 0.1f, restDuration = 1f });
            _index.Add(cell, new HazardEffect { kind = CcKind.Slow, param1 = 0.2f, restDuration = 1f });
            _index.Add(cell, new HazardEffect { kind = CcKind.Slow, param1 = 0.3f, restDuration = 1f });
            Unit(cell, Faction.Enemy);
            Tick();

            var mags = new List<float>();
            foreach (var ev in _ch.StatApply.Drain()) mags.Add(ev.magnitude);
            CollectionAssert.AreEqual(new[] { 0.3f, 0.2f, 0.1f }, mags,
                "인덱스 순서(가장 최근 추가분 먼저) 그대로 발행된다.");
        }

        [Test]
        public void EmitsRuntimeLog_PerEffect()
        {
            var cell = new SimInt2(3, 3);
            _index.Add(cell, new HazardEffect { kind = CcKind.Slow, param1 = 0.5f, restDuration = 1f });
            _index.Add(cell, new HazardEffect { kind = CcKind.DoT, param1 = 5f, restDuration = 1f });
            Unit(cell, Faction.Enemy);
            Tick();

            Assert.AreEqual(2, _ch.HazardRuntime.Count);
            var log = _ch.HazardRuntime.Drain()[0];
            Assert.AreEqual(HazardRuntimeEventType.ZoneApply, log.eventType);
            Assert.AreEqual(cell, log.cell);
        }

        [Test]
        public void ReemitsEveryFrame_SoLeavingTheZoneRevokesNaturally()
        {
            _index.Add(new SimInt2(1, 1), new HazardEffect
            { kind = CcKind.Slow, param1 = 0.5f, restDuration = 1f });
            Unit(new SimInt2(1, 1), Faction.Enemy);

            Tick();
            Assert.AreEqual(1, _ch.StatApply.Count);
            _ch.StatApply.Drain();
            Tick();
            Assert.AreEqual(1, _ch.StatApply.Count, "매 프레임 재발행.");
        }
    }

    public class SimPatrolAreaMathTests
    {
        private static readonly SimInt2 Grid = new SimInt2(9, 9);

        private static byte[] OpenMask()
        {
            var m = new byte[Grid.x * Grid.y];
            for (int i = 0; i < m.Length; i++) m[i] = 1;
            return m;
        }

        [Test]
        public void IsInArea_IsChebyshevBox_NotCircle()
        {
            var anchor = new SimInt2(4, 4);
            Assert.IsTrue(PatrolAreaMath.IsInArea(new SimInt2(5, 5), anchor, 1), "대각선도 반경 1");
            Assert.IsFalse(PatrolAreaMath.IsInArea(new SimInt2(6, 4), anchor, 1));
        }

        [Test]
        public void FillAreaMask_ClearsItself_SoReusedBuffersCannotLeakBetweenPatrols()
        {
            // 이것이 이 함수가 스스로 지우는 이유다 — 버퍼를 재사용하는 신 sim 에서 앞 엔티티의
            // 구역이 남으면 **뒤 엔티티가 자기 구역 밖을 walkable 로 본다** = 거점을 벗어나 걸어나간다.
            byte[] full = OpenMask();
            var outMask = new byte[full.Length];

            PatrolAreaMath.FillAreaMask(full, Grid, new SimInt2(1, 1), 1, outMask);
            Assert.AreEqual(1, outMask[GridMath.CellIndex(new SimInt2(1, 1), Grid)]);

            // 같은 버퍼로 멀리 떨어진 두 번째 구역을 채운다.
            PatrolAreaMath.FillAreaMask(full, Grid, new SimInt2(7, 7), 1, outMask);
            Assert.AreEqual(1, outMask[GridMath.CellIndex(new SimInt2(7, 7), Grid)], "B 구역은 켜진다");
            Assert.AreEqual(0, outMask[GridMath.CellIndex(new SimInt2(1, 1), Grid)],
                "A 구역은 **꺼져야** 한다 — 남으면 순찰병 2기에서만 재현되는 이탈 버그가 된다.");
        }

        [Test]
        public void FillAreaMask_RespectsWalkMask_AndGridBounds()
        {
            byte[] full = OpenMask();
            full[GridMath.CellIndex(new SimInt2(4, 4), Grid)] = 0;   // 구역 안에 벽
            var outMask = new byte[full.Length];

            // 코너 앵커 — 박스가 그리드를 넘어간다(클램프되어야 한다).
            PatrolAreaMath.FillAreaMask(full, Grid, new SimInt2(0, 0), 2, outMask);
            Assert.AreEqual(1, outMask[GridMath.CellIndex(new SimInt2(0, 0), Grid)]);

            PatrolAreaMath.FillAreaMask(full, Grid, new SimInt2(4, 4), 1, outMask);
            Assert.AreEqual(0, outMask[GridMath.CellIndex(new SimInt2(4, 4), Grid)],
                "구역 안이라도 벽은 walkable 이 아니다.");
        }

        [Test]
        public void StepDir_AtAnchor_WithNoEnemy_IsStop()
        {
            byte[] full = OpenMask();
            var area = new byte[full.Length];
            var anchor = new SimInt2(4, 4);
            PatrolAreaMath.FillAreaMask(full, Grid, anchor, 2, area);

            var srcArray = new SimInt2[16];
            SimVec2 dir = PatrolAreaMath.StepDir(area, full, Grid, anchor, 2, anchor, 1,
                new SimInt2[1], 0, new SimVec2[full.Length], new int[full.Length],
                new List<SimInt2>(), new List<SimInt2>(), ref srcArray);

            Assert.AreEqual(SimVec2.Zero, dir, "거점에 있고 적이 없으면 정지.");
        }

        [Test]
        public void StepDir_DisplacedInsideBox_WithNoEnemy_DescendsTowardAnchor()
        {
            byte[] full = OpenMask();
            var area = new byte[full.Length];
            var anchor = new SimInt2(4, 4);
            PatrolAreaMath.FillAreaMask(full, Grid, anchor, 2, area);

            var srcArray = new SimInt2[16];
            SimVec2 dir = PatrolAreaMath.StepDir(area, full, Grid, anchor, 2, new SimInt2(6, 4), 1,
                new SimInt2[1], 0, new SimVec2[full.Length], new int[full.Length],
                new List<SimInt2>(), new List<SimInt2>(), ref srcArray);

            Assert.AreEqual(new SimVec2(-1, 0), dir, "적이 없으면 거점으로 하강한다(-x).");
        }

        [Test]
        public void StepDir_PushedOutsideBox_UsesFullMaskToReturn()
        {
            // 포털/토네이도/임펄스는 진영을 안 보므로 순찰병을 박스 밖으로 민다.
            // areaMask 로는 박스 밖 셀 dist 가 MaxValue 라 **영구 정지**한다 — fullMask 를 써야 한다.
            byte[] full = OpenMask();
            var area = new byte[full.Length];
            var anchor = new SimInt2(2, 2);
            PatrolAreaMath.FillAreaMask(full, Grid, anchor, 1, area);

            var srcArray = new SimInt2[16];
            SimVec2 dir = PatrolAreaMath.StepDir(area, full, Grid, anchor, 1, new SimInt2(7, 2), 1,
                new SimInt2[1], 0, new SimVec2[full.Length], new int[full.Length],
                new List<SimInt2>(), new List<SimInt2>(), ref srcArray);

            Assert.AreEqual(new SimVec2(-1, 0), dir, "박스 밖에서도 거점 방향이 나온다(정지 아님).");
        }

        [Test]
        public void StepDir_EnemyInArea_MovesTowardAFiringPosition()
        {
            byte[] full = OpenMask();
            var area = new byte[full.Length];
            var anchor = new SimInt2(4, 4);
            PatrolAreaMath.FillAreaMask(full, Grid, anchor, 3, area);

            var srcArray = new SimInt2[64];
            var enemies = new[] { new SimInt2(6, 4) };
            SimVec2 dir = PatrolAreaMath.StepDir(area, full, Grid, anchor, 3, new SimInt2(3, 4), 1,
                enemies, 1, new SimVec2[full.Length], new int[full.Length],
                new List<SimInt2>(), new List<SimInt2>(), ref srcArray);

            Assert.AreEqual(new SimVec2(1, 0), dir, "구역 안 적의 사격 위치로 향한다(+x).");
        }

        [Test]
        public void StepDir_EnemyOutsideArea_IsIgnored_AndPatrolReturnsToAnchor()
        {
            byte[] full = OpenMask();
            var area = new byte[full.Length];
            var anchor = new SimInt2(2, 2);
            PatrolAreaMath.FillAreaMask(full, Grid, anchor, 1, area);

            var srcArray = new SimInt2[64];
            var enemies = new[] { new SimInt2(8, 8) };   // 구역 밖
            SimVec2 dir = PatrolAreaMath.StepDir(area, full, Grid, anchor, 1, new SimInt2(3, 2), 1,
                enemies, 1, new SimVec2[full.Length], new int[full.Length],
                new List<SimInt2>(), new List<SimInt2>(), ref srcArray);

            Assert.AreEqual(new SimVec2(-1, 0), dir,
                "구역 밖 적은 쫓지 않는다 — 좀비 추격을 만들지 않는다.");
        }
    }
}
