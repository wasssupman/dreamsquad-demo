using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // bonus-wave-pull unit 0 — 사냥 게이트가 `BossTag` 에서 `DefenderHunterTag` 로 옮겨간
    // 것을 고정한다. 이 spec 이전에는 `DefenderFieldSystem` 에 대한 테스트가 **리포 전체에
    // 0건**이었고, 계약 5 의 「보스 무회귀」를 사람 눈으로만 주장하고 있었다.
    //
    // 여기서 잡는 회귀 3종:
    //  1. 헌터가 하나도 없으면 필드를 **재빌드하지 않는다**(초기값 보존 = skip 의 관측 가능한 형태).
    //  2. 헌터가 있으면 방어유닛 쪽으로 유한한 dist 가 깔린다.
    //  3. `BossTag` 만 달고 `DefenderHunterTag` 가 없는 엔티티는 헌터가 **아니다** —
    //     게이트가 옛 태그로 되돌아가면 이 케이스가 빨개진다.
    public class DefenderHunterGateTests
    {
        private const int W = 8;
        private const int H = 6;

        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private Entity _fieldEntity;
        private FlowFieldSingleton _goalField;
        private DefenderFieldSingleton _huntField;

        [SetUp]
        public void SetUp()
        {
            _world = new World("DefenderHunterGateTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<DefenderFieldSystem>());

            int n = W * H;

            // 전 셀 통행 가능한 평지 — 벽 규칙은 이 테스트의 관심사가 아니다.
            var walkMask = new NativeArray<byte>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++) walkMask[i] = 1;

            _goalField = new FlowFieldSingleton
            {
                flow = new NativeArray<float2>(n, Allocator.Persistent),
                dist = new NativeArray<int>(n, Allocator.Persistent),
                walkMask = walkMask,
                gridSize = new int2(W, H),
                tileSize = 1f,
                origin = float3.zero,
            };

            _huntField = new DefenderFieldSingleton
            {
                flow = new NativeArray<float2>(n, Allocator.Persistent),
                dist = new NativeArray<int>(n, Allocator.Persistent),
                gridSize = new int2(W, H),
                tileSize = 1f,
                origin = float3.zero,
            };
            // 재빌드 여부를 관측하려면 초기값이 «시스템이 절대 쓰지 않을 값» 이어야 한다.
            for (int i = 0; i < n; i++) _huntField.dist[i] = Sentinel;

            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, _goalField);
            _em.AddComponentData(_fieldEntity, _huntField);
        }

        private const int Sentinel = -12345;

        [TearDown]
        public void TearDown()
        {
            _goalField.flow.Dispose();
            _goalField.dist.Dispose();
            _goalField.walkMask.Dispose();
            _huntField.Dispose();
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        private Entity MakeDefender(int x, int y)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x + 0.5f, 0f, y + 0.5f)));
            return e;
        }

        // range 는 소스 반경 R 의 입력이다(헌터들 사거리의 min fold).
        private Entity MakeEnemy(int x, int y, bool hunter, bool boss, float range = 1f)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 50f, max = 50f });
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x + 0.5f, 0f, y + 0.5f)));
            _em.AddComponentData(e, new AttackState { range = range });
            if (hunter) _em.AddComponent<DefenderHunterTag>(e);
            if (boss) _em.AddComponent<BossTag>(e);
            return e;
        }

        private int DistAt(int x, int y)
        {
            var f = _em.GetComponentData<DefenderFieldSingleton>(_fieldEntity);
            return f.dist[y * W + x];
        }

        private bool Rebuilt()
        {
            var f = _em.GetComponentData<DefenderFieldSingleton>(_fieldEntity);
            for (int i = 0; i < f.dist.Length; i++)
                if (f.dist[i] != Sentinel) return true;
            return false;
        }

        [Test]
        public void 헌터가_없으면_재빌드하지_않는다()
        {
            MakeDefender(2, 2);
            MakeEnemy(6, 2, hunter: false, boss: false);
            _simGroup.Update();
            Assert.IsFalse(Rebuilt(), "헌터 부재 시 필드 재빌드를 건너뛰어야 한다");
        }

        [Test]
        public void 헌터가_있으면_방어유닛_방향_필드가_깔린다()
        {
            MakeDefender(2, 2);
            MakeEnemy(6, 2, hunter: true, boss: false);
            _simGroup.Update();

            Assert.IsTrue(Rebuilt(), "헌터가 있으면 재빌드해야 한다");
            // 방어유닛 인접 칸은 소스라 dist 0, 멀리 있는 헌터 칸은 유한하되 더 크다.
            Assert.AreEqual(0, DistAt(3, 2), "방어유닛 인접 칸은 소스(dist 0)");
            int atHunter = DistAt(6, 2);
            Assert.AreNotEqual(int.MaxValue, atHunter, "헌터 칸에서 소스로 가는 경로가 있어야 한다");
            Assert.Greater(atHunter, 0);
        }

        // ★게이트 회귀의 본체 — 옛 `BossTag` 로 되돌리면 여기가 빨개진다.
        [Test]
        public void BossTag_만으로는_헌터가_아니다()
        {
            MakeDefender(2, 2);
            MakeEnemy(6, 2, hunter: false, boss: true);
            _simGroup.Update();
            Assert.IsFalse(Rebuilt(),
                "BossTag 는 보스 특권 축이다 — 사냥은 DefenderHunterTag 가 소유한다");
        }

        // 계약 5 — 보스는 tier == Boss 로 같은 태그를 함께 받으므로 무회귀여야 한다.
        // (부착은 브리지 bake 의 몫이고, 여기서는 «둘 다 달렸을 때 사냥한다» 를 고정한다.)
        [Test]
        public void 보스는_두_태그를_다_달고_사냥한다()
        {
            MakeDefender(2, 2);
            MakeEnemy(6, 2, hunter: true, boss: true);
            _simGroup.Update();
            Assert.IsTrue(Rebuilt(), "보스 무회귀 — 사냥 필드가 여전히 깔려야 한다");
            Assert.AreEqual(0, DistAt(3, 2));
        }

        [Test]
        public void 방어유닛이_없으면_전_셀이_도달불가다()
        {
            MakeEnemy(6, 2, hunter: true, boss: false);
            _simGroup.Update();

            Assert.IsTrue(Rebuilt(), "헌터가 있으면 재빌드는 돈다(소스가 없을 뿐)");
            // 계약 5 — 소스 0 → 전 셀 MaxValue → Movement 가 goal flow 로 폴백한다.
            var f = _em.GetComponentData<DefenderFieldSingleton>(_fieldEntity);
            for (int i = 0; i < f.dist.Length; i++)
                Assert.AreEqual(int.MaxValue, f.dist[i], $"cell {i}");
        }
    }
}
