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
    // bomb-barrel-on-place unit 0·7 — 「설치물이 부서지면 터진다」 축.
    //
    // 이 파일이 지키는 것 두 가지:
    //  ① 죽은 설치물이 **전용 캐리어**에 폭발 요청을 남긴다. 죽는 엔티티 자신에 걸면
    //     같은 프레임에 파괴되어 브리지 드레인이 영영 못 본다.
    //  ② 배럴은 **시간으로 죽지 않는다**(unit 7). 폭발은 적이 부순 사건이지 시계 사건이
    //     아니다 — 시한이 되살아나면 그 성격이 통째로 바뀐다.
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
            // unit 7 로 수명 만료 경로는 은퇴했지만 이 핀은 **남긴다** — M0 unit 0 이 얼린
            // BattleSimGroup 총순서를 유지하는 유일한 장치이고, 떼면 정렬기가 자리를
            // 옮겨 골든 트레이스가 이유 없이 갈린다.
            CollectionAssert.Contains(System.Array.ConvertAll(after, a => a.SystemType),
                typeof(ObstacleLifetimeSystem), "M0 가 얼린 실행 순서를 유지한다");
            CollectionAssert.Contains(System.Array.ConvertAll(before, a => a.SystemType),
                typeof(UnitLifecycleSystem), "설치물이 치워지기 전에 봐야 한다");
        }

        // ── unit 9 — 스스로 닳는 체력 ───────────────────────────────────

        private void TickDecay(float dt)
        {
            _world.SetTime(new Unity.Core.TimeData(_world.Time.ElapsedTime + dt, dt));
            _world.GetOrCreateSystem<ObstacleLifetimeSystem>().Update(_world.Unmanaged);
        }

        [Test]
        public void Barrel_DecaysItsOwnHealth_ThroughTheDamageChannel()
        {
            _so.healthDecayPerSec = 10f;
            var barrel = SpawnBarrel(new int2(4, 4), explodeDamage: 120f);

            TickDecay(0.5f);

            var buf = _em.GetBuffer<IncomingDamage>(barrel);
            Assert.AreEqual(1, buf.Length, "노후화는 별도 죽음 경로가 아니라 **피해**로 흐른다");
            Assert.AreEqual(5f, buf[0].amount, 1e-4f, "10/s × 0.5s");
            Assert.AreEqual(Entity.Null, buf[0].source,
                "환경 피해는 귀속이 없다 — source 를 채우면 킬 귀속이 엉뚱한 대상에게 간다");
        }

        [Test]
        public void HazardWithoutDecay_TakesNoSelfDamage()
        {
            _so.healthDecayPerSec = 0f;
            var rock = SpawnBarrel(new int2(4, 4), explodeDamage: 0f);

            for (int i = 0; i < 10; i++) TickDecay(1f);

            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(rock).Length,
                "0 = 안 닳음. 기존 길막 설치물이 여기 해당하므로 무회귀가 걸려 있다");
        }

        [Test]
        public void DecayingBarrel_DiesAndExplodes_ThroughTheSameDoorAsBeingSmashed()
        {
            _so.healthDecayPerSec = 100f; // maxHp 100 → 무방비 1초
            var barrel = SpawnBarrel(new int2(4, 4), explodeDamage: 120f);
            using var carriers = CarrierQuery();

            // 노후화 → 정산. 죽을 때까지 민다.
            int ticks = 0;
            while (!_em.HasComponent<DeadTag>(barrel) && ticks++ < 20)
            {
                TickDecay(0.5f);
                _world.GetOrCreateSystem<DamageApplicationSystem>().Update(_world.Unmanaged);
            }

            Assert.IsTrue(_em.HasComponent<DeadTag>(barrel), "노후화도 「부서짐」으로 나간다");
            Assert.AreEqual(2, ticks, "maxHp 100 / 100초당 = 무방비 1초 = 0.5초 틱 두 번");

            // 그리고 죽은 프레임의 폭발은 맞아 죽었을 때와 **같은 한 발**이다.
            // ⚠ 여기서 더 틱하면 카운트가 는다 — 격리 월드엔 죽은 엔티티를 치우는
            // `UnitLifecycleSystem` 이 없어서다. 라이브에선 같은 프레임에 치워진다.
            TickExplosion();
            Assert.AreEqual(1, carriers.CalculateEntityCount(),
                "문이 하나이므로 노후화로 죽어도 그대로 터진다 — 폭발에 두 번째 경로를 만들지 말 것");
        }

        // ── unit 7 — 시간으로는 죽지 않는다 ─────────────────────────────

        // 배럴이 «부서져야만 터진다» 는 계약의 회귀 방어다. 시한이 되살아나면 폭발이
        // 적의 사건이 아니라 시계 사건으로 되돌아간다.
        // ⚠ 이 테스트의 앞선 판은 `healthDecayPerSec = 0` 을 깔고 돌았다. 통과했지만
        // **실게임이 쓰지 않는 설정만** 방어하는 테스트였다 — 배럴은 노후화를 저작한다.
        // 노후화를 켠 채로 물어야 계약("시한은 은퇴했다")이 라이브 구성에서 지켜진다.
        // 값 자체는 계약이 아니므로 단언하지 않는다(밸런스 수치 리터럴 금지).
        [Test]
        public void ObstacleLifetimeSystem_NeverKillsAHazardItself_EvenWithDecayAuthored()
        {
            _so.healthDecayPerSec = 16.67f; // 0 만 아니면 된다 — 노후화가 켜진 상태를 만든다
            var barrel = SpawnBarrel(new int2(4, 4), explodeDamage: 120f);

            // 구 시한(12초)의 다섯 배를 민다. 정산 시스템은 **일부러 안 돌린다** —
            // 이 시스템 혼자서는 죽이지 못한다는 것이 이 테스트의 주장이다.
            for (int i = 0; i < 60; i++)
            {
                _world.SetTime(new Unity.Core.TimeData(_world.Time.ElapsedTime + 1f, 1f));
                _world.GetOrCreateSystem<ObstacleLifetimeSystem>().Update(_world.Unmanaged);
            }

            Assert.IsFalse(_em.HasComponent<DeadTag>(barrel),
                "시한은 은퇴했다. 노후화조차 **피해로** 나가므로 이 시스템은 두 번째 죽음 문이 아니다");
            Assert.IsTrue(_blockedCells.Contains(new int2(4, 4)), "살아 있는 동안은 계속 길을 막는다");
            Assert.Greater(_em.GetBuffer<IncomingDamage>(barrel).Length, 0,
                "대신 피해가 쌓여 있어야 한다 — 아무 일도 안 일어난 것과 구분된다");
        }
    }
}
