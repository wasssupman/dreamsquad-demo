using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // elite-enemy-tier unit 4 — 콘 브레스 적용 루프(`AttackSystem.ApplyConeBreath`)의 세 술어.
    //
    // ★**이 파일이 지키는 것은 이 spec 이 가장 크게 틀릴 뻔한 지점이다.** 초판 스펙은
    // 「후보 배열이 시전자 마스크로 걸러진 진영 대칭 풀」이라고 적었는데 거짓이었다 —
    // `targetCandidatesQuery` 는 `FactionTag + Health + LocalTransform` 의 **전 진영 통합 풀**
    // 이고 진영 판정은 공격자 루프 안의 `AttackState.targetMask` 가 한다. 그 전제를 믿고
    // 순회를 짰으면 드래곤이 **같은 웨이브 동료와 적 마음을 태웠다.**
    //
    // `TileAoeTests` 는 순수 술어(`IsInCone`)의 기하만 본다 — 부채꼴 안이냐 밖이냐. 진영·통행층·
    // 자기 제외는 **그 바깥의 적용 루프**가 하는 일이라 거기서 덮이지 않는다. 그래서 여기서 못 박는다.
    //
    // e2e 대신 plain 배열 단위 테스트인 이유: 부채꼴 방향은 런타임 타게팅이 고른 대상으로 정해져
    // 씬 안에서 「안/밖」을 결정적으로 배치하기 어렵다. 술어 자체는 배열 입력으로 완전히 관측된다.
    public class ConeBreathPredicateTests
    {
        // 반각 50°(저작 초기값) → 런타임은 코사인². bake 가 하는 변환과 같은 식.
        private static readonly float ConeCosSq50 =
            math.cos(math.radians(50f)) * math.cos(math.radians(50f));

        private const float RangeWorld = 3f;
        private const float Damage = 50f;

        // 공격자(드래곤)가 때릴 수 있는 층 — 지상 계열. Air 만 다니는 대상은 걸러져야 한다.
        private const byte AttackerTargetLayers = (byte)(PlacementLayer.Ground | PlacementLayer.Path);

        private static Entity NewCandidate(EntityManager em, float3 pos, Faction faction)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(pos));
            em.AddComponentData(e, new FactionTag { value = faction });
            em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private static float TotalDamage(EntityManager em, Entity e)
        {
            var buf = em.GetBuffer<IncomingDamage>(e);
            float sum = 0f;
            for (int i = 0; i < buf.Length; i++) sum += buf[i].amount;
            return sum;
        }

        // `ApplyConeBreath` 는 private static 이다(시스템을 키우지 않으려고 뺀 순수 조각).
        // 이름이 바뀌면 NRE 가 아니라 이 단언이 뜬다.
        private static void Invoke(
            ref EntityCommandBuffer ecb, Entity self, float2 selfXZ, float2 dir,
            float damage, int targetMask, byte selfTargetLayers,
            NativeArray<Entity> entities, NativeArray<LocalTransform> transforms,
            NativeArray<FactionTag> factions, NativeArray<byte> layers)
        {
            var mi = typeof(AttackSystem).GetMethod(
                "ApplyConeBreath", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, "AttackSystem.ApplyConeBreath 를 찾지 못했다(이름 변경?)");

            var args = new object[]
            {
                ecb, self, selfXZ, dir, ConeCosSq50, RangeWorld, damage,
                targetMask, selfTargetLayers, entities, transforms, factions, layers,
            };
            mi.Invoke(null, args);
            ecb = (EntityCommandBuffer)args[0]; // ref 인자 회수
        }

        // 한 판에 다섯 경우를 같이 세운다 — 술어끼리 서로를 가리지 않는지가 요점이라
        // 케이스를 쪼개면 «다른 술어가 대신 걸러줘서» 통과하는 착시를 못 잡는다.
        [Test]
        public void Cone_Damages_OnlyInCone_Enemies_Are_Spared_Self_Excluded_Layer_Respected()
        {
            using var world = new World("ConeBreathPredicateTests");
            var em = world.EntityManager;

            // 시전자는 원점에서 +x 를 본다.
            var self = NewCandidate(em, new float3(0f, 0f, 0f), Faction.EnemyUnit);

            var inCone       = NewCandidate(em, new float3(2f, 0f, 0f),  Faction.DefenderUnit);
            var offAngle     = NewCandidate(em, new float3(0f, 0f, 2f),  Faction.DefenderUnit);
            var behind       = NewCandidate(em, new float3(-2f, 0f, 0f), Faction.DefenderUnit);
            var beyondRange  = NewCandidate(em, new float3(5f, 0f, 0f),  Faction.DefenderUnit);
            var allyEnemy    = NewCandidate(em, new float3(1.5f, 0f, 0f), Faction.EnemyUnit);
            var enemyCore    = NewCandidate(em, new float3(1f, 0f, 0f),  Faction.EnemyCore);
            var flyingTarget = NewCandidate(em, new float3(1f, 0f, 0f),  Faction.DefenderUnit);

            var entities = new NativeArray<Entity>(new[]
            {
                inCone, offAngle, behind, beyondRange, allyEnemy, enemyCore, flyingTarget, self,
            }, Allocator.Temp);

            var transforms = new NativeArray<LocalTransform>(entities.Length, Allocator.Temp);
            var factions = new NativeArray<FactionTag>(entities.Length, Allocator.Temp);
            var layers = new NativeArray<byte>(entities.Length, Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                transforms[i] = em.GetComponentData<LocalTransform>(entities[i]);
                factions[i] = em.GetComponentData<FactionTag>(entities[i]);
                // flyingTarget 만 Air — 공격자의 Ground|Path 와 교집합이 비어 거절돼야 한다.
                layers[i] = entities[i] == flyingTarget
                    ? (byte)PlacementLayer.Air
                    : (byte)PlacementLayer.Ground;
            }

            // 적이 쓰는 마스크 = 방어 진영. 「배열이 이미 걸러져 있다」가 거짓이므로 이 값이 유일한 방어선.
            int mask = (int)(Faction.DefenderUnit | Faction.DefenderCore | Faction.BlockingHazard);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            Invoke(ref ecb, self, float2.zero, new float2(1f, 0f), Damage,
                mask, AttackerTargetLayers, entities, transforms, factions, layers);
            ecb.Playback(em);
            ecb.Dispose();

            Assert.AreEqual(Damage, TotalDamage(em, inCone), 0.001f,
                "부채꼴 정면·사거리 안 방어유닛이 피해를 받지 않았다");

            // ① 진영 — 이 두 줄이 이 spec 의 가장 큰 위험을 막는다.
            Assert.AreEqual(0f, TotalDamage(em, allyEnemy), 0.001f,
                "★같은 웨이브 동료를 태웠다 — 후보 배열은 전 진영 통합 풀이고 진영 판정은 이 루프의 몫이다");
            Assert.AreEqual(0f, TotalDamage(em, enemyCore), 0.001f,
                "★적 마음을 태웠다 — 같은 원인(진영 마스크 미적용)");

            // ② 통행층
            Assert.AreEqual(0f, TotalDamage(em, flyingTarget), 0.001f,
                "지상 전용 공격이 Air 대상까지 번졌다(PlacementLayers.CanTarget 미적용)");

            // ③ 자기 제외는 여기서 단언하지 않는다 — 시전자가 EnemyUnit 이라 **①이 먼저 걸러서**
            //    통과가 자기 제외의 증거가 되지 못한다(엉뚱한 이유로 초록). 아래 전용 테스트로 뺀다.

            // 기하 — 적용 루프가 IsInCone 을 실제로 부르는지(술어를 통과시켜 버리지 않는지)
            Assert.AreEqual(0f, TotalDamage(em, offAngle), 0.001f, "부채꼴 밖(수직)이 맞았다");
            Assert.AreEqual(0f, TotalDamage(em, behind), 0.001f, "등 뒤가 맞았다");
            Assert.AreEqual(0f, TotalDamage(em, beyondRange), 0.001f, "사거리 밖이 맞았다");

            entities.Dispose();
            transforms.Dispose();
            factions.Dispose();
            layers.Dispose();
        }

        // ★위 테스트에서 동료 적이 살아남은 이유가 **진영 마스크**임을 증명한다.
        // 같은 배치에서 마스크에만 EnemyUnit 을 넣으면 맞아야 한다 — 안 맞으면 그건 기하가
        // 애초에 부채꼴 밖으로 뒀다는 뜻이고, 위의 「아군 오사 방지」 단언은 진공이 된다.
        [Test]
        public void Cone_Hits_AllyEnemy_WhenMaskIncludesIt_ProvingGeometryWasNotTheReason()
        {
            using var world = new World("ConeBreathPredicateTests_MaskProof");
            var em = world.EntityManager;

            var self = NewCandidate(em, float3.zero, Faction.EnemyUnit);
            var allyEnemy = NewCandidate(em, new float3(1.5f, 0f, 0f), Faction.EnemyUnit);

            var entities = new NativeArray<Entity>(new[] { allyEnemy }, Allocator.Temp);
            var transforms = new NativeArray<LocalTransform>(
                new[] { em.GetComponentData<LocalTransform>(allyEnemy) }, Allocator.Temp);
            var factions = new NativeArray<FactionTag>(
                new[] { em.GetComponentData<FactionTag>(allyEnemy) }, Allocator.Temp);
            var layers = new NativeArray<byte>(
                new[] { (byte)PlacementLayer.Ground }, Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            Invoke(ref ecb, self, float2.zero, new float2(1f, 0f), Damage,
                (int)Faction.EnemyUnit, AttackerTargetLayers,
                entities, transforms, factions, layers);
            ecb.Playback(em);
            ecb.Dispose();

            Assert.AreEqual(Damage, TotalDamage(em, allyEnemy), 0.001f,
                "마스크에 넣었는데도 안 맞는다 — 이 좌표는 부채꼴 밖이고, 위 오사 단언은 진공이었다");

            entities.Dispose();
            transforms.Dispose();
            factions.Dispose();
            layers.Dispose();
        }

        // ③ 자기 제외 — 시전자 진영이 **마스크 안에 있을 때만** 관측 가능하다.
        // 드래곤(적→방어 마스크)에서는 ①이 먼저 걸러 이 술어가 드러나지 않는다. 방어유닛 host 가
        // AreaBreath 를 쓰게 되는 날을 위한 방어선이므로 그 조건을 만들어 확인한다.
        [Test]
        public void Cone_Excludes_Self_EvenWhenFactionMatchesTheMask()
        {
            using var world = new World("ConeBreathPredicateTests_Self");
            var em = world.EntityManager;

            var self = NewCandidate(em, float3.zero, Faction.DefenderUnit);
            var other = NewCandidate(em, new float3(1f, 0f, 0f), Faction.DefenderUnit);

            var entities = new NativeArray<Entity>(new[] { self, other }, Allocator.Temp);
            var transforms = new NativeArray<LocalTransform>(new[]
            {
                em.GetComponentData<LocalTransform>(self),
                em.GetComponentData<LocalTransform>(other),
            }, Allocator.Temp);
            var factions = new NativeArray<FactionTag>(new[]
            {
                em.GetComponentData<FactionTag>(self),
                em.GetComponentData<FactionTag>(other),
            }, Allocator.Temp);
            var layers = new NativeArray<byte>(new[]
            {
                (byte)PlacementLayer.Ground, (byte)PlacementLayer.Ground,
            }, Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            Invoke(ref ecb, self, float2.zero, new float2(1f, 0f), Damage,
                (int)Faction.DefenderUnit, AttackerTargetLayers,
                entities, transforms, factions, layers);
            ecb.Playback(em);
            ecb.Dispose();

            // 이웃이 맞았다 = 마스크·기하가 통과하는 배치다(진공 아님).
            Assert.AreEqual(Damage, TotalDamage(em, other), 0.001f,
                "같은 진영 이웃이 안 맞았다 — 이 배치는 자기 제외를 관측할 수 없다");
            // 시전자는 같은 마스크·같은 셀(SameSpotEps 로 «부채꼴 안») 인데도 빠져야 한다.
            Assert.AreEqual(0f, TotalDamage(em, self), 0.001f,
                "시전자가 자기 브레스에 맞았다 — 자기 제외 술어가 없다");

            entities.Dispose();
            transforms.Dispose();
            factions.Dispose();
            layers.Dispose();
        }

        // 저작 사고 방어 — bake 는 피해 0 을 warning 으로만 통과시킨다(거절하지 않는다).
        // 그때 루프가 0 피해 엔트리를 버퍼에 쌓으면 DamageApplicationSystem 이 헛돈다.
        [Test]
        public void Cone_With_ZeroDamage_Appends_Nothing()
        {
            using var world = new World("ConeBreathPredicateTests_ZeroDamage");
            var em = world.EntityManager;

            var self = NewCandidate(em, float3.zero, Faction.EnemyUnit);
            var target = NewCandidate(em, new float3(1f, 0f, 0f), Faction.DefenderUnit);

            var entities = new NativeArray<Entity>(new[] { target }, Allocator.Temp);
            var transforms = new NativeArray<LocalTransform>(
                new[] { em.GetComponentData<LocalTransform>(target) }, Allocator.Temp);
            var factions = new NativeArray<FactionTag>(
                new[] { em.GetComponentData<FactionTag>(target) }, Allocator.Temp);
            var layers = new NativeArray<byte>(
                new[] { (byte)PlacementLayer.Ground }, Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            Invoke(ref ecb, self, float2.zero, new float2(1f, 0f), 0f,
                (int)Faction.DefenderUnit, AttackerTargetLayers,
                entities, transforms, factions, layers);
            ecb.Playback(em);
            ecb.Dispose();

            Assert.AreEqual(0, em.GetBuffer<IncomingDamage>(target).Length,
                "피해 0 인데 IncomingDamage 엔트리를 남겼다");

            entities.Dispose();
            transforms.Dispose();
            factions.Dispose();
            layers.Dispose();
        }
    }
}
