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
    // elite-whirlpot unit 0 — 어그로와 광역 폭의 관계를 고정한다.
    //
    // 예전엔 `aggro-targeting` unit 8(MEDIUM 2)이 어그로된 적의 `attackTargetCount` 를 1 로
    // 접었고 **그 판단에 테스트가 없었다.** 그래서 「어그로가 적의 공격 형태를 바꾼다」는
    // 부작용(광역 적이 붙잡히면 단일 적이 되어 덜 때린다)이 아무 그물에도 걸리지 않았다.
    // 이번엔 두 축을 각각 못 박는다:
    //
    //   ① 광역 «폭» 은 어그로와 무관하다        ← 이 unit 이 바꾼 것
    //   ② primary «선정» 은 여전히 어그로가 지배 ← 절대 되돌리면 안 되는 것
    //
    // ② 가 load-bearing 인 이유: 「가디언이 사거리 밖이면 미발사」를 풀면, 가디언에게 걸어가는
    // 도중 옆 방어유닛이 사거리에 들어오는 순간 `EngageMovement.Halt` 로 멈춰 싸우고 가디언에
    // 영영 도착하지 않는다 — 어그로 루프(적이 스스로 가디언으로 보행해 겹쳐 정지)가 깨진다.
    public class AggroAoeWidthTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("AggroAoeWidthTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        // 광역 근접 적. attackTargetCount 를 저작으로 받는다 — 이 축이 테스트의 주어다.
        private Entity MakeAoeEnemy(float3 pos, int attackTargetCount)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<AttackUnitTag>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = 4f, cooldownDuration = 1f, cooldownRemaining = 0f,
                attackTargetCount = attackTargetCount,
                targetMask = (int)Faction.DefenderUnit,
            });
            var ob = _em.AddBuffer<AttackOutputElement>(e);
            ob.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 7f },
            });
            return e;
        }

        private Entity MakeDefender(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private int Hits(Entity e) => _em.GetBuffer<IncomingDamage>(e).Length;

        // ─── ① 폭은 어그로와 무관하다 (이 unit 이 바꾼 것) ───

        [Test]
        public void AggroedEnemy_AoeStillReachesNeighbor_NotFoldedToSingleTarget()
        {
            var enemy = MakeAoeEnemy(float3.zero, attackTargetCount: 2);
            // 가디언을 **더 멀리** 둔다 — 이웃이 primary 로 뽑힐 수 없게 해서
            // 「가디언이 primary + 이웃이 보조」라는 조합만 관측되게 만든다.
            var guardian = MakeDefender(new float3(3f, 0f, 0f));
            var neighbor = MakeDefender(new float3(1f, 0f, 0f));
            _em.AddComponentData(enemy, new Aggroed { guardian = guardian });

            _simGroup.Update();

            Assert.Greater(Hits(guardian), 0,
                "어그로된 적의 primary 는 가디언이다(sticky override).");
            Assert.Greater(Hits(neighbor), 0,
                "★어그로여도 광역 폭은 줄지 않는다 — 이웃 방어유닛도 맞아야 한다. "
                + "0 이면 attackTargetCount 를 1 로 접는 로직이 되살아난 것이다.");
        }

        [Test]
        public void NotAggroed_Aoe_HitsBoth_Control()
        {
            var enemy = MakeAoeEnemy(float3.zero, attackTargetCount: 2);
            var far = MakeDefender(new float3(3f, 0f, 0f));
            var near = MakeDefender(new float3(1f, 0f, 0f));

            _simGroup.Update();

            Assert.Greater(Hits(near), 0, "비어그로 primary 는 최근접이다.");
            Assert.Greater(Hits(far), 0, "비어그로 광역이 둘째 대상까지 닿는다(대조군).");
        }

        [Test]
        public void AggroedEnemy_SingleTargetAuthoring_StillHitsOnlyGuardian()
        {
            // 저작이 1 이면 어그로와 무관하게 1 이다 — 이 unit 이 «광역이 아닌 적» 을
            // 광역으로 만들지 않았음을 고정한다(적 17종 중 12종이 count 1).
            var enemy = MakeAoeEnemy(float3.zero, attackTargetCount: 1);
            var guardian = MakeDefender(new float3(3f, 0f, 0f));
            var neighbor = MakeDefender(new float3(1f, 0f, 0f));
            _em.AddComponentData(enemy, new Aggroed { guardian = guardian });

            _simGroup.Update();

            Assert.Greater(Hits(guardian), 0, "가디언만 맞는다.");
            Assert.AreEqual(0, Hits(neighbor),
                "attackTargetCount 1 은 어그로와 무관하게 단일 대상이다.");
        }

        // ─── ② primary 선정의 배타성 (되돌리면 어그로 루프가 깨진다) ───

        [Test]
        public void AggroedEnemy_HoldsFire_WhenGuardianOutOfRange_EvenWithNeighborAdjacent()
        {
            var enemy = MakeAoeEnemy(float3.zero, attackTargetCount: 2);
            // 가디언은 사거리(4) 밖, 이웃은 바로 옆.
            var guardian = MakeDefender(new float3(20f, 0f, 0f));
            var neighbor = MakeDefender(new float3(1f, 0f, 0f));
            _em.AddComponentData(enemy, new Aggroed { guardian = guardian });

            _simGroup.Update();

            Assert.AreEqual(0, Hits(neighbor),
                "★가디언이 사거리 밖이면 **아무도** 때리지 않는다(미발사). 여기서 이웃이 맞으면 "
                + "sticky primary override 가 풀린 것이고, 적이 가디언에 도착하지 못한다.");
            Assert.AreEqual(0, Hits(guardian), "사거리 밖 가디언도 당연히 안 맞는다.");
        }
    }
}
