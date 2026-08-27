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
    // skill-layer-migration unit 3e — **실드가 깨지는 순간**의 두 payload.
    //
    // ⚠ 이 축은 여태 PlayMode 그물이 **0개**였다. 라우팅만 열면 슬롯은 붙고 브리지 로그도
    // 뜨는데 아무 데서도 안 터지는 상태가 되고, 컴파일러도 기존 테스트도 그걸 안 잡는다 —
    // 이 spec 이 반복해서 경고한 실패 유형이 정확히 그것이다. 그래서 이전과 **같은 커밋**에서
    // 그물을 만든다.
    public class ShieldBreakSkillLayerTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        // 파열 폭발 — 실드가 피격으로 완전히 깨진 프레임에 자기 자리에서 터진다.
        [UnityTest]
        public IEnumerator ShieldBreakBlast_RunsThroughTheSkillLayer_AndHitsNearby()
        {
            LogAssert.ignoreFailingMessages = true;
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

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var host = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, host, "host resolved");

            var card = MakeShieldBreakBlastCard();
            if (card == null) Assert.Ignore("AOE 뷰 저작이 없다 — 이 그물의 전제가 성립하지 않는다");
            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardToUnit(host, card), 0, "파열 폭발 부착");

            // 라우팅 키 — bake 가 스킬 레이어로 보냈나.
            int routedId = int.MinValue;
            foreach (var sl in em.GetBuffer<Wassup.Battle.Combat.DcTriggerSlot>(host))
                if (sl.trigger == DcTriggerKind.OnShieldBreak) routedId = sl.skillId;
            Assert.AreEqual(Wassup.Skills.Concrete.SelfAreaBlastSkill.Id, routedId,
                "bake 가 OnShieldBreak×SelfTileAoe 를 스킬 레이어로 안 보냈다");

            var hostPos = em.GetComponentData<LocalTransform>(host).Position;
            float tile = (bridge.GridToWorldCenterVector(new Vector2Int(1, 0))
                          - bridge.GridToWorldCenterVector(new Vector2Int(0, 0))).magnitude;
            var near = SpawnBystander(em, bridge, hostPos + new float3(tile, 0f, 0f), hp: 9999f);
            yield return null;
            float before = em.GetComponentData<Health>(near).value;

            // 실드 10 을 얹고 그것을 정확히 넘기는 피해를 준다 → Sum>0→0 = 파열.
            var shield = em.HasBuffer<ShieldSlot>(host)
                ? em.GetBuffer<ShieldSlot>(host) : em.AddBuffer<ShieldSlot>(host);
            shield.Clear();
            shield.Add(new ShieldSlot { source = host, value = 10f });

            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();
            em.GetBuffer<IncomingDamage>(host).Add(new IncomingDamage { amount = 12f });

            // ⚠ **「줄었나」로 물으면 안 된다** — 가디언의 평타(수십)로도 줄어서 폭발이 안
            // 터져도 초록이 된다. 폭발은 300 이므로 **폭발 크기**로 묻는다.
            float t = 0f;
            while (t < 3f && before - em.GetComponentData<Health>(near).value < 200f)
            { t += Time.deltaTime; yield return null; }
            float after = em.GetComponentData<Health>(near).value;
            em.DestroyEntity(near);

            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Death), 1,
                "파열이 스킬 레이어를 안 거쳤다 — 라우팅이 조용히 죽었다");
            Assert.GreaterOrEqual(before - after, 200f,
                $"실드가 깨졌는데 폭발 크기의 피해가 안 들어왔다({before}->{after}) — "
                + "안 터졌거나 엉뚱한 자리에서 터졌다(평타로는 이 폭이 안 나온다)");
        }

        // 파열 수면 — 같은 사건, 다른 payload. 실행기는 같고 concrete 만 갈린다.
        [UnityTest]
        public IEnumerator ShieldBreakSleep_RunsThroughTheSkillLayer_AndSleepsNearby()
        {
            LogAssert.ignoreFailingMessages = true;
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

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var host = FindDefender(bridge, em);
            Assert.GreaterOrEqual(
                bridge.ApplyDreamcatcherCardToUnit(host, MakeShieldBreakSleepCard()), 0, "파열 수면 부착");

            var hostPos = em.GetComponentData<LocalTransform>(host).Position;
            float tile = (bridge.GridToWorldCenterVector(new Vector2Int(1, 0))
                          - bridge.GridToWorldCenterVector(new Vector2Int(0, 0))).magnitude;
            // ⚠ **사거리 밖**에 둔다. 자장가 concrete 는 「내가 지금 때릴 자리」를 빼므로,
            // 붙여 두면 그 배제에 걸려 이 그물이 스킬의 잘못을 못 가린다.
            var sleeper = SpawnBystander(em, bridge, hostPos + new float3(tile * 3f, 0f, 0f), hp: 9999f);
            yield return null;

            var shield = em.HasBuffer<ShieldSlot>(host)
                ? em.GetBuffer<ShieldSlot>(host) : em.AddBuffer<ShieldSlot>(host);
            shield.Clear();
            shield.Add(new ShieldSlot { source = host, value = 10f });

            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();
            em.GetBuffer<IncomingDamage>(host).Add(new IncomingDamage { amount = 12f });

            bool slept = false;
            float t = 0f;
            while (t < 3f && !slept)
            {
                t += Time.deltaTime; yield return null;
                if (!em.Exists(sleeper) || !em.HasBuffer<CcEffect>(sleeper)) continue;
                foreach (var cc in em.GetBuffer<CcEffect>(sleeper))
                    if (cc.kind == CcKind.Sleep && cc.remainingTime > 0f) slept = true;
            }
            if (em.Exists(sleeper)) em.DestroyEntity(sleeper);

            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Death), 1,
                "파열 수면이 스킬 레이어를 안 거쳤다");
            Assert.IsTrue(slept, "실드가 깨졌는데 주변 적이 안 잤다");
        }

        private static DreamcatcherCard MakeShieldBreakBlastCard()
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
                trigger = new DcTriggerSpec { kind = DcTriggerKind.OnShieldBreak },
                payload = new DcPayloadSpec
                {
                    kind = DcPayloadKind.SelfTileAoe, magnitude = 300f, tileRange = 2, projectile = vfx,
                },
            }};
            return card;
        }

        private static DreamcatcherCard MakeShieldBreakSleepCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.OnShieldBreak },
                payload = new DcPayloadSpec
                {
                    kind = DcPayloadKind.AreaSleep, magnitude = 3f, tileRange = 4, duration = 3f,
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
