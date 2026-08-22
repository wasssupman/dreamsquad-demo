using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // bomb-barrel-on-place unit 0·1 — 「설치물이 부서지면 터진다」 축.
    //
    // 이 파일이 지키는 것 두 가지:
    //  ① 죽은 설치물이 **전용 캐리어**에 폭발 요청을 남긴다. 죽는 엔티티 자신에 걸면
    //     같은 프레임에 파괴되어 브리지 드레인이 영영 못 본다.
    //  ② 수명 만료가 **파괴와 같은 문**(DeadTag)으로 나가고, 만료 프레임에 이미 차단 칸에서
    //     빠진다. 「DeadTag 를 달면 WithNone 이 걸러 준다」는 ECB 재생 시점 때문에 거짓이다.
    public class BarrelExplosionTests
    {
        private World _world;
        private EntityManager _em;
        private NativeArray<float2> _flow;
        private NativeArray<int> _dist;
        private NativeHashSet<int2> _blockedCells;
        private BlockingHazardSO _so;

        [SetUp]
        public void SetUp()
        {
            _world = new World("BarrelExplosionTests");
            _em = _world.EntityManager;

            _flow = new NativeArray<float2>(100, Allocator.Persistent);
            _dist = new NativeArray<int>(100, Allocator.Persistent);
            var ff = _em.CreateEntity();
            _em.AddComponentData(ff, new FlowFieldSingleton
            {
                flow = _flow,
                dist = _dist,
                gridSize = new int2(10, 10),
                goalCell = new int2(9, 9),
                tileSize = 1f,
                version = 1,
            });

            _blockedCells = new NativeHashSet<int2>(16, Allocator.Persistent);
            var obstacleSingleton = _em.CreateEntity();
            _em.AddComponentData(obstacleSingleton, new ObstacleSingleton { blockedCells = _blockedCells });

            _so = ScriptableObject.CreateInstance<BlockingHazardSO>();
            _so.shape = HazardShape.SingleCell;
            _so.maxHp = 100f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_flow.IsCreated) _flow.Dispose();
            if (_dist.IsCreated) _dist.Dispose();
            if (_blockedCells.IsCreated) _blockedCells.Dispose();
            if (_so != null) Object.DestroyImmediate(_so);
            _world?.Dispose();
        }

        private Entity SpawnBarrel(int2 cell, float explodeDamage, int tileRange = 1, int cap = 0, int dataIndex = 7)
        {
            _so.explodeDamage = explodeDamage;
            _so.explodeTileRange = tileRange;
            _so.explodeTargetCap = cap;
            return EffectSpawner.SpawnBlockingHazard(_em, _so, cell, hazardSoIndex: 0, explodeDataIndex: dataIndex);
        }

        private void TickExplosion()
        {
            var sys = _world.GetOrCreateSystem<BarrelExplosionSystem>();
            sys.Update(_world.Unmanaged);
        }

        private EntityQuery CarrierQuery() => _em.CreateEntityQuery(
            ComponentType.ReadOnly<ProjectileRequestCarrier>(),
            ComponentType.ReadOnly<ProjectileSpawnRequest>());

        [Test]
        public void DeadBarrel_StagesOneBlastOnADedicatedCarrier()
        {
            var barrel = SpawnBarrel(new int2(4, 4), explodeDamage: 120f, tileRange: 2, cap: 3, dataIndex: 7);
            _em.AddComponent<DeadTag>(barrel);

            using var carriers = CarrierQuery();
            TickExplosion();

            Assert.AreEqual(1, carriers.CalculateEntityCount(), "죽은 배럴은 폭발 요청 하나를 남긴다");
            var entities = carriers.ToEntityArray(Allocator.Temp);
            var req = _em.GetComponentData<ProjectileSpawnRequest>(entities[0]);
            // 요청이 **죽는 배럴이 아닌** 별도 엔티티에 실려야 브리지 드레인이 볼 수 있다.
            Assert.AreNotEqual(barrel, entities[0], "요청을 죽는 배럴에 걸면 드레인이 못 본다");
            entities.Dispose();

            Assert.AreEqual(PayloadKind.TileAoe, req.payload, "폭발은 기존 칸 광역 해결을 그대로 쓴다");
            Assert.AreEqual(0f, req.flightTime, 1e-4f, "부서지는 그 순간이 폭발이다(예고 없음)");
            Assert.AreEqual(4f, req.impact.x, 1e-4f);
            Assert.AreEqual(4f, req.impact.z, 1e-4f);
            Assert.AreEqual(120f, req.damage, 1e-4f);
            Assert.AreEqual(2, req.impactTileRange);
            Assert.AreEqual(3, req.aoeTargetCap);
            Assert.AreEqual(7, req.dataIndex, "폭발 탄 index 가 없으면 엉뚱한 탄 비주얼로 터진다");
        }

        [Test]
        public void BarrelWithoutExplodeDamage_StagesNothing()
        {
            var barrel = SpawnBarrel(new int2(4, 4), explodeDamage: 0f);
            _em.AddComponent<DeadTag>(barrel);

            using var carriers = CarrierQuery();
            TickExplosion();

            Assert.AreEqual(0, carriers.CalculateEntityCount(),
                "폭발값 0 = 기존 길막 설치물. 여기서 터지면 무회귀가 깨진다");
        }

        [Test]
        public void LivingBarrel_StagesNothing()
        {
            SpawnBarrel(new int2(4, 4), explodeDamage: 120f);

            using var carriers = CarrierQuery();
            TickExplosion();

            Assert.AreEqual(0, carriers.CalculateEntityCount(), "살아 있는 배럴은 안 터진다");
        }

        [Test]
        public void SystemOrder_SeesDeadTagBeforeTheHazardIsSweptAway()
        {
            // 순서가 어긋나면 「가끔 안 터진다」가 되고 Play 로는 간헐 재현이라 못 잡는다.
            // DeadTag 생산자 둘과 파괴자 하나 — 셋 다와의 상대 순서를 어트리뷰트로 못박는다.
            var t = typeof(BarrelExplosionSystem);
            var after = (Unity.Entities.UpdateAfterAttribute[])t.GetCustomAttributes(
                typeof(Unity.Entities.UpdateAfterAttribute), false);
            var before = (Unity.Entities.UpdateBeforeAttribute[])t.GetCustomAttributes(
                typeof(Unity.Entities.UpdateBeforeAttribute), false);

            CollectionAssert.Contains(System.Array.ConvertAll(after, a => a.SystemType),
                typeof(DamageApplicationSystem), "체력 사망 경로가 DeadTag 를 붙인 뒤에 봐야 한다");
            CollectionAssert.Contains(System.Array.ConvertAll(after, a => a.SystemType),
                typeof(ObstacleLifetimeSystem), "수명 만료 경로가 DeadTag 를 붙인 뒤에 봐야 한다");
            CollectionAssert.Contains(System.Array.ConvertAll(before, a => a.SystemType),
                typeof(UnitLifecycleSystem), "설치물이 치워지기 전에 봐야 한다");
        }

        // ── unit 1 수명 ────────────────────────────────────────────────────

        // ⚠ 격리 월드는 시간이 안 흐른다 — dt 를 직접 밀어 주지 않으면 수명이 영원히 안 준다.
        private void TickLifetime(float dt = 0.016f)
        {
            _world.SetTime(new Unity.Core.TimeData(_world.Time.ElapsedTime + dt, dt));
            _world.GetOrCreateSystem<ObstacleLifetimeSystem>().Update(_world.Unmanaged);
        }

        [Test]
        public void LegacyBarrelWithoutLifetime_NeverExpires()
        {
            _so.lifetime = 0f; // 기존 에셋 전부가 여기 해당한다
            var barrel = SpawnBarrel(new int2(4, 4), explodeDamage: 0f);

            for (int i = 0; i < 5; i++) TickLifetime();

            Assert.IsFalse(_em.HasComponent<DeadTag>(barrel), "수명 0 = 무한. 사라지면 무회귀가 깨진다");
            Assert.IsTrue(_blockedCells.Contains(new int2(4, 4)), "살아 있는 동안은 길을 막는다");
        }

        [Test]
        public void ExpiredBarrel_GetsDeadTag_AndLeavesBlockedCellsSameFrame()
        {
            _so.lifetime = 0.001f;
            var barrel = SpawnBarrel(new int2(4, 4), explodeDamage: 120f);

            TickLifetime(); // dt 가 수명을 넘긴다

            Assert.IsTrue(_em.HasComponent<DeadTag>(barrel),
                "만료는 파괴와 **같은 문**으로 나가야 폭발이 두 경우를 함께 덮는다");
            Assert.IsFalse(_blockedCells.Contains(new int2(4, 4)),
                "만료 프레임에 이미 빠져야 한다 — ECB 는 루프 뒤에 재생되므로 WithNone 이 안 걸러 준다");
        }
    }
}
