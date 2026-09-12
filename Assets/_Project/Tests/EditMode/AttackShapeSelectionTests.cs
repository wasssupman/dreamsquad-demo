using AttackShapeBaked = Wassup.Data.AttackShapeBaked;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // directional-attack-shape rev 3 — `AttackSystem` 이 도형을 **부가 타격에만** 적용하는지 sim 으로 증명한다.
    //
    // 배치(공격자 = 원점, 사거리 4, 몸 0):
    //   R  (2, 0)     오른쪽 축        · L (−2.4, 0) 왼쪽 축
    //   RU (2, 1.5)   오른쪽 36.9°     · RF (3.5, 0) 오른쪽 축 멀리
    //   UP (0, 2.2)   정확히 위
    // 규칙: 획득 = **원**(최근접 R) → 주 대상 방향(+X)으로 도형 → 부가 타격은 원 ∩ 도형에서 가까운 순.
    public class AttackShapeSelectionTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("AttackShapeSelectionTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        private static AttackShapeBaked Sector(float fullDeg) => new AttackShapeBaked
        {
            kind = AttackShapeBaked.SectorKind,
            sinHalf = math.sin(math.radians(fullDeg * 0.5f)),
            cosHalf = math.cos(math.radians(fullDeg * 0.5f)),
        };
        private static AttackShapeBaked Band(float width) => new AttackShapeBaked
        {
            kind = AttackShapeBaked.BandKind, halfWidth = width * 0.5f,
        };

        private Entity MakeAttacker(AttackShapeBaked shape, int count)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(float3.zero));
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<AttackUnitTag>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = 4f, cooldownDuration = 1f, cooldownRemaining = 0f,
                attackTargetCount = count,
                targetMask = (int)Faction.DefenderUnit,
                shape = shape,
            });
            var ob = _em.AddBuffer<AttackOutputElement>(e);
            ob.Add(new AttackOutputElement { value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 7f } });
            return e;
        }

        private Entity MakeGuardian(AttackShapeBaked shape, int count)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(float3.zero));
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = 4f, cooldownDuration = 1f, cooldownRemaining = 0f,
                attackTargetCount = count,
                targetMask = (int)Faction.EnemyUnit,
                shape = shape,
            });
            _em.AddComponentData(e, new AggroCapacity { held = 0, max = 3 });
            var ob = _em.AddBuffer<AttackOutputElement>(e);
            ob.Add(new AttackOutputElement { value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 7f } });
            return e;
        }

        private Entity MakeTarget(float3 pos, Faction faction)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = faction });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            if (faction == Faction.DefenderUnit) _em.AddComponent<DefenderUnitTag>(e);
            else _em.AddComponent<AttackUnitTag>(e);
            return e;
        }

        private int Hits(Entity e) => _em.GetBuffer<IncomingDamage>(e).Length;

        private (Entity r, Entity l, Entity ru, Entity rf, Entity up) Layout(Faction f) => (
            MakeTarget(new float3(2f, 0f, 0f), f),
            MakeTarget(new float3(-2.4f, 0f, 0f), f),
            MakeTarget(new float3(2f, 0f, 1.5f), f),
            MakeTarget(new float3(3.5f, 0f, 0f), f),
            MakeTarget(new float3(0f, 0f, 2.2f), f));

        [Test]
        public void Omni_HitsEveryoneInRange_Control()
        {
            MakeAttacker(AttackShapeBaked.Omni, count: 5);
            var t = Layout(Faction.DefenderUnit);
            _simGroup.Update();
            Assert.Greater(Hits(t.r), 0); Assert.Greater(Hits(t.l), 0); Assert.Greater(Hits(t.ru), 0);
            Assert.Greater(Hits(t.rf), 0); Assert.Greater(Hits(t.up), 0);
        }

        [Test]
        public void Acquisition_IsCircle_TargetDirectlyAboveIsHit_EvenWithNarrowSector()
        {
            // ★ rev 3 의 얼굴 — 「사거리 안이면 반드시 반응한다」. 위에만 적이 있으면 30° 도형이라도 때린다.
            MakeAttacker(Sector(30f), count: 3);
            var up = MakeTarget(new float3(0f, 0f, 2.2f), Faction.DefenderUnit);
            _simGroup.Update();
            Assert.Greater(Hits(up), 0, "획득은 원 — 도형은 주 대상을 절대 막지 않는다");
        }

        [Test]
        public void Sector60_SecondariesOnlyInsideTheSectorTowardPrimary()
        {
            MakeAttacker(Sector(60f), count: 3);
            var t = Layout(Faction.DefenderUnit);
            _simGroup.Update();
            Assert.Greater(Hits(t.r), 0, "주 대상 = 최근접(R)");
            Assert.Greater(Hits(t.rf), 0, "축 위 멀리 — 60° 안");
            Assert.AreEqual(0, Hits(t.ru), "36.9° 는 반각 30° 밖(몸 0 이라 가장자리 거리 0.299 > 0)");
            Assert.AreEqual(0, Hits(t.l), "반대편");
            Assert.AreEqual(0, Hits(t.up), "위 — 주 대상이 아니면 도형 밖");
        }

        [Test]
        public void Sector90_WidensToIncludeTheDiagonal()
        {
            MakeAttacker(Sector(90f), count: 3);
            var t = Layout(Faction.DefenderUnit);
            _simGroup.Update();
            Assert.Greater(Hits(t.r), 0);
            Assert.Greater(Hits(t.ru), 0, "36.9° 는 반각 45° 안");
            Assert.Greater(Hits(t.rf), 0);
            Assert.AreEqual(0, Hits(t.l));
            Assert.AreEqual(0, Hits(t.up));
        }

        [Test]
        public void Sector_FollowsThePrimary_WhenPrimaryIsOnTheLeft()
        {
            MakeAttacker(Sector(90f), count: 3);
            var l = MakeTarget(new float3(-1.5f, 0f, 0f), Faction.DefenderUnit);
            var r = MakeTarget(new float3(2f, 0f, 0f), Faction.DefenderUnit);
            var lu = MakeTarget(new float3(-2f, 0f, 1.5f), Faction.DefenderUnit);
            _simGroup.Update();
            Assert.Greater(Hits(l), 0, "왼쪽이 최근접 → 주 대상");
            Assert.Greater(Hits(lu), 0, "왼쪽 부채꼴 안 — 부가");
            Assert.AreEqual(0, Hits(r), "반대편 — 제외");
        }

        [Test]
        public void Band_HitsOnlyAlongTheAxisTowardPrimary()
        {
            MakeAttacker(Band(1.0f), count: 3);
            var t = Layout(Faction.DefenderUnit);
            _simGroup.Update();
            Assert.Greater(Hits(t.r), 0);
            Assert.Greater(Hits(t.rf), 0, "축 위 멀리 — 띠 안");
            Assert.AreEqual(0, Hits(t.ru), "세로 1.5 는 반폭 0.5 밖");
            Assert.AreEqual(0, Hits(t.l), "반대편");
            Assert.AreEqual(0, Hits(t.up));
        }

        [Test]
        public void Guardian_AggroPath_UsesTheSameShapeRule()
        {
            MakeGuardian(Sector(90f), count: 3);
            var t = Layout(Faction.EnemyUnit);
            _simGroup.Update();
            Assert.Greater(Hits(t.r), 0);
            Assert.Greater(Hits(t.ru), 0);
            Assert.Greater(Hits(t.rf), 0);
            Assert.AreEqual(0, Hits(t.l), "가디언 경로도 주 대상 쪽만");
            Assert.AreEqual(0, Hits(t.up));
        }

        [Test]
        public void Guardian_Acquisition_IsCircle_AboveOnlyIsHit()
        {
            MakeGuardian(Sector(30f), count: 3);
            var up = MakeTarget(new float3(0f, 0f, 2.2f), Faction.EnemyUnit);
            _simGroup.Update();
            Assert.Greater(Hits(up), 0, "가디언 경로도 획득은 원");
        }
    }
}
