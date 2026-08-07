using NUnit.Framework;
using Unity.Entities;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // goal-tower-siege unit 0 — 공유 체력 풀의 감산 계약.
    //
    // 이 파일의 존재 이유는 초안 설계의 치명적 오류다. 초안은 각 타워의 Health 감소분을
    // 역산해 풀에서 깎았는데(taken = Σ(max − value)), write-back 이 value = pool 이라
    // 다음 프레임의 (max − value) 가 **누적 결손**이 되어 pool' = 2·pool − max 로 발산했다.
    // 골이 2개면 3·pool − 2·max 라 첫 피격 후 5~7프레임에 허위 패배가 났다.
    //
    // 그래서 **무피해 프레임에 풀이 불변**인지가 이 파일의 핵심 케이스다. 지금 설계는
    // IncomingDamage 를 DamageApplicationSystem 보다 먼저 직접 소비하므로 역산 자체가 없다.
    public class GoalTowerPoolTests
    {
        private World _world;
        private EntityManager _em;
        private SystemHandle _handle;

        [SetUp]
        public void SetUp()
        {
            _world = new World("GoalTowerPoolTests");
            _em = _world.EntityManager;
            _handle = _world.CreateSystem<GoalTowerDamageSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        private void CreatePool(float max)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new GoalTowerHealth { value = max, max = max });
        }

        private Entity CreateTower(float max)
        {
            var e = _em.CreateEntity();
            _em.AddComponent<GoalTowerTag>(e);
            _em.AddComponentData(e, new Health { value = max, max = max });
            _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private void Hit(Entity tower, float amount)
            => _em.GetBuffer<IncomingDamage>(tower).Add(new IncomingDamage { amount = amount });

        private void Update() { _handle.Update(_world.Unmanaged); }

        private float Pool()
        {
            using var q = _em.CreateEntityQuery(ComponentType.ReadOnly<GoalTowerHealth>());
            return q.GetSingleton<GoalTowerHealth>().value;
        }

        private float TowerHp(Entity tower) => _em.GetComponentData<Health>(tower).value;

        [Test]
        public void NoDamageFrame_LeavesPoolUnchanged()
        {
            CreatePool(20f);
            var a = CreateTower(20f);
            var b = CreateTower(20f);

            Hit(a, 5f);
            Update();
            Assert.AreEqual(15f, Pool(), 0.0001f, "첫 프레임 피해 5");

            // 초안 버그의 재현 지점: 여기서 풀이 또 줄면 누적 결손을 재차감하고 있는 것이다.
            for (int i = 0; i < 5; i++) Update();
            Assert.AreEqual(15f, Pool(), 0.0001f, "무피해 프레임에는 풀이 불변이어야 한다");
            Assert.AreEqual(15f, TowerHp(a), 0.0001f, "미러도 그대로");
            Assert.AreEqual(15f, TowerHp(b), 0.0001f);
        }

        [Test]
        public void DamageAcrossTwoTowers_SubtractsOnce()
        {
            CreatePool(20f);
            var a = CreateTower(20f);
            var b = CreateTower(20f);

            Hit(a, 3f);
            Hit(b, 4f);
            Update();

            Assert.AreEqual(13f, Pool(), 0.0001f, "두 타워 피해 합(7)이 풀에서 한 번만 빠진다");
            Assert.AreEqual(13f, TowerHp(a), 0.0001f, "골이 여럿이어도 체력은 공유 1풀");
            Assert.AreEqual(13f, TowerHp(b), 0.0001f);
        }

        [Test]
        public void BuffersAreConsumed_SoDamageIsNotAppliedTwice()
        {
            CreatePool(20f);
            var a = CreateTower(20f);

            Hit(a, 6f);
            Update();
            Update();

            Assert.AreEqual(14f, Pool(), 0.0001f, "버퍼를 비우지 않으면 다음 프레임에 또 빠진다");
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(a).Length, "소비 후 버퍼는 비어 있다");
        }

        [Test]
        public void Overkill_FloorsAtZero()
        {
            CreatePool(10f);
            var a = CreateTower(10f);

            Hit(a, 999f);
            Update();

            Assert.AreEqual(0f, Pool(), 0.0001f, "음수 금지 — 초과분은 버린다");
            Assert.AreEqual(0f, TowerHp(a), 0.0001f);
        }

        [Test]
        public void MultipleEntriesInOneFrame_Accumulate()
        {
            CreatePool(20f);
            var a = CreateTower(20f);

            Hit(a, 2f);
            Hit(a, 3f);
            Hit(a, 1f);
            Update();

            Assert.AreEqual(14f, Pool(), 0.0001f, "한 프레임 다중 히트는 합산된다");
        }

        // 순수 함수 단독 계약(시스템 없이도 고정).
        [Test]
        public void ApplyDamage_IsPureAndFloored()
        {
            Assert.AreEqual(7f, GoalTowerHealth.ApplyDamage(10f, 3f), 0.0001f);
            Assert.AreEqual(0f, GoalTowerHealth.ApplyDamage(10f, 10f), 0.0001f);
            Assert.AreEqual(0f, GoalTowerHealth.ApplyDamage(10f, 99f), 0.0001f);
            Assert.AreEqual(10f, GoalTowerHealth.ApplyDamage(10f, 0f), 0.0001f, "0 피해는 무변");
            Assert.AreEqual(10f, GoalTowerHealth.ApplyDamage(10f, -5f), 0.0001f, "음수 피해는 회복이 아니다");
        }

        [Test]
        public void ComputeRatio_HandlesDegenerateMax()
        {
            Assert.AreEqual(0f, GoalTowerHealth.ComputeRatio(5f, 0f), 0.0001f);
            Assert.AreEqual(1f, GoalTowerHealth.ComputeRatio(20f, 20f), 0.0001f);
            Assert.AreEqual(0.5f, GoalTowerHealth.ComputeRatio(10f, 20f), 0.0001f);
        }
    }
}
