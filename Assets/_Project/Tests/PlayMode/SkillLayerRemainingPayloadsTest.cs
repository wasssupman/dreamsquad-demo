using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;
using Wassup.Battle.Effects;

namespace Wassup.Tests.PlayMode
{
    // skill-layer-migration unit 3f — 카드 경로에 마지막까지 닫혀 있던 두 payload.
    //
    // 둘 다 seam 은 진작 열려 있었고 **카드 게이트만** 닫혀 있었다(유닛 bake 는 이미
    // 보내고 있었다). 그래서 「열기만 하면 되는」 것처럼 보이는데, 열어 놓고 그물이 없으면
    // 조용히 죽어도 아무도 모른다 — 이 파일이 그걸 막는다.
    public class SkillLayerRemainingPayloadsTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        // 불꽃 팽이 — host 를 도는 화염구. 스킬이 정하는 것은 개수·시작 각도·각속도다.
        [UnityTest]
        public IEnumerator FlameSpinner_SpawnsOrbitingProjectiles_ThroughTheSkillLayer()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return Boot();
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var host = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, host, "host resolved");

            const int OrbCount = 3;
            Assert.GreaterOrEqual(
                bridge.ApplyDreamcatcherCardToUnit(host, MakeFlameSpinnerCard(OrbCount)), 0,
                "불꽃 팽이 부착");

            int routedId = int.MinValue;
            foreach (var sl in em.GetBuffer<Wassup.Battle.Combat.DcTriggerSlot>(host))
                if (sl.payload == DcPayloadKind.SelfOrbitProjectile) routedId = sl.skillId;
            Assert.AreEqual(Wassup.Skills.Concrete.OrbitProjectileSkill.Id, routedId,
                "bake 가 PeriodicTimer×SelfOrbitProjectile 를 스킬 레이어로 안 보냈다");

            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();
            using var orbQ = em.CreateEntityQuery(
                ComponentType.ReadOnly<Wassup.Battle.Combat.Projectile.ProjectileState>());

            int orbs = 0;
            float t = 0f;
            while (t < 4f && orbs < OrbCount)
            {
                t += Time.deltaTime; yield return null;
                orbs = 0;
                foreach (var st in orbQ.ToComponentDataArray<
                             Wassup.Battle.Combat.Projectile.ProjectileState>(
                             Unity.Collections.Allocator.Temp))
                    if (st.movement == Wassup.Battle.Combat.Projectile.MovementKind.OrbitAroundPoint)
                        orbs++;
            }

            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Periodic), 1,
                "주기 pulse 가 스킬 레이어를 안 거쳤다");
            // ⚠ **개수까지 묻는다.** 「하나라도 떴나」로 물으면 균등 배치가 무너져도 초록이다 —
            // 개수는 스킬이 정하는 셋 중 하나다.
            Assert.AreEqual(OrbCount, orbs, $"화염구가 {OrbCount}개 떠야 한다(실제 {orbs})");
        }

        // 진동갑주 — 체력 경계를 넘는 순간 자기 자리에서 터진다.
        [UnityTest]
        public IEnumerator TremorPlate_BlastsAtTheThreshold_ThroughTheSkillLayer()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return Boot();
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var host = FindDefender(bridge, em);

            var card = MakeTremorPlateCard();
            if (card == null) Assert.Ignore("AOE 뷰 저작이 없다 — 이 그물의 전제가 성립하지 않는다");
            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardToUnit(host, card), 0, "진동갑주 부착");

            int routedId = int.MinValue;
            foreach (var sl in em.GetBuffer<Wassup.Battle.Combat.DcTriggerSlot>(host))
                if (sl.trigger == DcTriggerKind.HealthThreshold) routedId = sl.skillId;
            Assert.AreEqual(Wassup.Skills.Concrete.SelfAreaBlastSkill.Id, routedId,
                "bake 가 HealthThreshold×SelfTileAoe 를 스킬 레이어로 안 보냈다");

            var hostPos = em.GetComponentData<LocalTransform>(host).Position;
            float tile = (bridge.GridToWorldCenterVector(new Vector2Int(1, 0))
                          - bridge.GridToWorldCenterVector(new Vector2Int(0, 0))).magnitude;
            var near = SpawnBystander(em, bridge, hostPos + new float3(tile, 0f, 0f), hp: 9999f);
            yield return null;
            float before = em.GetComponentData<Health>(near).value;

            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();
            var hh = em.GetComponentData<Health>(host);
            em.SetComponentData(host, new Health { value = hh.max * 0.2f, max = hh.max });

            // 평타(수십)로도 줄어드니 **폭발 크기**로 묻는다.
            float t = 0f;
            while (t < 4f && before - em.GetComponentData<Health>(near).value < 200f)
            { t += Time.deltaTime; yield return null; }
            float after = em.GetComponentData<Health>(near).value;
            em.DestroyEntity(near);

            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Threshold), 1,
                "경계 자폭이 스킬 레이어를 안 거쳤다");
            Assert.GreaterOrEqual(before - after, 200f,
                $"경계를 넘었는데 폭발 크기의 피해가 안 들어왔다({before}->{after})");
        }

        private static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var guardian = FindDefenderCatalog().ById("guardian");
            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            bridge.StartBattle();
            BattleBridgeTestAccess.SetField(bridge, "_usingGeneratedWaves", false);
            ((System.Collections.IList)BattleBridgeTestAccess.Field(bridge, "_pending")).Clear();
            yield return null;
        }

        private static DreamcatcherCard MakeFlameSpinnerCard(int orbCount)
        {
            // bake 가 탄 SO 의 speed·hitThreshold 양수를 강제한다 — 런타임 인스턴스로 만든다.
            var orb = ScriptableObject.CreateInstance<ProjectileData>();
            orb.speed = 4f; orb.hitThreshold = 0.5f; orb.visualScale = 1f;
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.PeriodicTimer, periodSeconds = 0.5f },
                payload = new DcPayloadSpec
                {
                    kind = DcPayloadKind.SelfOrbitProjectile,
                    magnitude = 10f, tileRange = 2, duration = 6f,
                    orbitCount = orbCount, projectile = orb,
                },
            }};
            return card;
        }

        private static DreamcatcherCard MakeTremorPlateCard()
        {
            ProjectileData vfx = null;
            foreach (var pd in Resources.FindObjectsOfTypeAll<ProjectileData>())
                if (pd != null) { vfx = pd; break; }
            if (vfx == null) return null;
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.HealthThreshold, fraction = 0.7f },
                payload = new DcPayloadSpec
                {
                    kind = DcPayloadKind.SelfTileAoe, magnitude = 300f, tileRange = 2, projectile = vfx,
                },
            }};
            return card;
        }

        private static Entity SpawnBystander(EntityManager em, BattleBridge bridge, float3 pos, float hp)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(pos));
            em.AddComponentData(e, new Health { value = hp, max = hp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<CcEffect>(e);
            em.AddComponent<AttackUnitTag>(e);
            em.AddComponentData(e, new Wassup.Battle.Movement.PathFollowState
            {
                speed = 0f, traversalLayers = (byte)PlacementLayer.Path,
            });
            BattleBridgeTestAccess.AttachSimEntityId(bridge, e);
            return e;
        }

        private static DefenderCatalog FindDefenderCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData unit)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, unit, out _))
                        return bridge.PlaceDefenderAs(x, y, unit);
            return false;
        }

        private static Entity FindDefender(BattleBridge bridge, EntityManager em)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<DefenderUnitTag>(), ComponentType.ReadOnly<LocalTransform>());
            var arr = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            return arr.Length > 0 ? arr[0] : Entity.Null;
        }
    }
}
