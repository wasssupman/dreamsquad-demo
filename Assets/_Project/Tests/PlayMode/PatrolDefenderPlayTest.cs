using System.Collections;
using System.Reflection;
using NUnit.Framework;
using PrimeTween;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // summon-patrol-defender unit 7 — 라이브 씬/에셋을 태우는 e2e.
    // EditMode PatrolSystemIntegrationTests가 시스템 seam을 고정하고, 이 테스트는
    // 배치 SO bake → blind 요청 → Bridge 스폰/뷰 → 수명 순환이 실제로 이어지는지 본다.
    public class PatrolDefenderPlayTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Summoner_SpawnsOnePatrol_ThatReceivesSupport_AndRespawns()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattleScene();

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var catalog = FindCatalog();
            Assert.IsNotNull(bridge, "BattleBridge present");
            Assert.IsNotNull(gm, "GameManager present");
            Assert.IsNotNull(catalog, "DefenderCatalog present");

            var summonerData = catalog.ById("summoner");
            var healerData = catalog.ById("healer");
            var shieldData = catalog.ById("shield_shuttle");
            Assert.IsNotNull(summonerData, "summoner is directly deployable");
            Assert.IsNotNull(healerData, "healer in catalog");
            Assert.IsNotNull(shieldData, "shield shuttle in catalog");
            Assert.IsNull(catalog.ById("patrol_soldier"),
                "patrol soldier must not be directly exposed in the roster");

            var ability = summonerData.GetAbility<SummonPatrolAbility>();
            Assert.IsNotNull(ability, "summoner ability baked from SO");
            Assert.IsNotNull(ability.patrolUnit, "patrol unit SO wired");

            bridge.SetDefenderPool(new[] { summonerData, healerData, shieldData });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;

            Assert.IsTrue(FindSummonerCell(bridge, summonerData, ability.leashTileRadius, out var ownerCell),
                "placeable summoner cell with a leash-local walk anchor");
            Assert.IsTrue(bridge.PlaceDefenderAs(ownerCell.x, ownerCell.y, summonerData), "place summoner");
            Assert.IsTrue(PlaceFirstValid(bridge, healerData, out var healerCell), "place healer");
            Assert.IsTrue(PlaceFirstValid(bridge, shieldData, out var shieldCell), "place shield shuttle");

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var summoner = EntityAt(bridge, em, ownerCell);
            var healer = EntityAt(bridge, em, healerCell);
            var shield = EntityAt(bridge, em, shieldCell);
            Assert.AreNotEqual(Entity.Null, summoner, "summoner entity");
            Assert.AreNotEqual(Entity.Null, healer, "healer entity");
            Assert.AreNotEqual(Entity.Null, shield, "shield shuttle entity");
            Assert.IsTrue(em.HasComponent<SummonerState>(summoner), "SummonerState baked");

            bridge.StartBattle();
            OpenSummonGate(em, summoner);
            ForceAttackReady(em, summoner);

            Entity patrol = Entity.Null;
            for (int i = 0; i < 120 && patrol == Entity.Null; i++)
            {
                yield return null;
                patrol = ResolveLivePatrol(em, summoner);
            }
            Assert.AreNotEqual(Entity.Null, patrol, "summon request reaches the live patrol entity");
            Assert.AreEqual(1, CountWith<PatrolAnchor>(em), "one patrol per summoner");

            Assert.IsTrue(em.HasComponent<DefenderUnitTag>(patrol));
            Assert.IsTrue(em.HasComponent<DefenderClassTag>(patrol));
            Assert.IsTrue(em.HasComponent<FactionTag>(patrol));
            Assert.AreEqual(Faction.DefenderUnit, em.GetComponentData<FactionTag>(patrol).value);
            Assert.IsFalse(em.HasComponent<DefenderTile>(patrol),
                "no placement/death-event/awakening farming path");
            Assert.IsFalse(em.HasComponent<AttackUnitTag>(patrol), "not an enemy/leak unit");
            Assert.IsTrue(em.HasComponent<SummonedBy>(patrol));
            Assert.AreEqual(summoner, em.GetComponentData<SummonedBy>(patrol).owner);
            Assert.IsFalse(em.HasBuffer<DcTriggerSlot>(patrol), "dreamcatcher cards are not baked onto patrol");
            Assert.IsFalse(HasModifierOrigin(em, patrol, ModifierOrigin.Tile),
                "effect tiles are DefenderTile placement effects, not moving-patrol effects");
            Assert.IsFalse(HasModifierOrigin(em, patrol, ModifierOrigin.Dreamcatcher),
                "active dreamcatcher effects are not copied onto patrol");

            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(bridge.TryGetUnitView(patrol, out var patrolView), "patrol Spine view spawned");
            Assert.IsTrue(HasAllyMarker(patrolView), "moving ally marker attached to patrol view");

            // 실제 지원 시스템 대상 집합에 순찰병이 들어오는지 검증한다. 배치 셀은 그대로 두고
            // 테스트에서만 캐스터의 sim 위치를 순찰병 곁으로 옮겨 range 변수를 제거한다.
            var patrolPos = em.GetComponentData<LocalTransform>(patrol).Position;
            MoveTo(em, healer, patrolPos);
            MoveTo(em, shield, patrolPos);
            var hp = em.GetComponentData<Health>(patrol);
            hp.value = math.max(1f, hp.max * 0.2f);
            em.SetComponentData(patrol, hp);
            float damagedHp = hp.value;
            ForceAttackReady(em, healer);
            var shieldCast = em.GetComponentData<ShieldCastState>(shield);
            shieldCast.cooldownRemaining = 0f;
            em.SetComponentData(shield, shieldCast);

            bool healed = false;
            bool shielded = false;
            for (int i = 0; i < 240 && (!healed || !shielded); i++)
            {
                yield return null;
                if (!em.Exists(patrol)) break;
                healed = em.GetComponentData<Health>(patrol).value > damagedHp;
                shielded = ShieldMath.Sum(em.GetBuffer<ShieldSlot>(patrol)) > 0f;
            }
            Assert.IsTrue(healed, "healer targets and heals the moving defender");
            Assert.IsTrue(shielded, "shield shuttle grants a shield to the moving defender");

            var firstPatrol = patrol;
            hp = em.GetComponentData<Health>(firstPatrol);
            hp.value = 0f;
            em.SetComponentData(firstPatrol, hp);
            for (int i = 0; i < 120 && em.Exists(firstPatrol); i++) yield return null;
            Assert.IsFalse(em.Exists(firstPatrol), "dead patrol is destroyed through the general unit path");

            ForceAttackReady(em, summoner);   // 게이트는 첫 소환에서 이미 소비됨 — 재소환은 무게이트
            Entity respawned = Entity.Null;
            for (int i = 0; i < 120 && respawned == Entity.Null; i++)
            {
                yield return null;
                var candidate = ResolveLivePatrol(em, summoner);
                if (candidate != firstPatrol) respawned = candidate;
            }
            Assert.AreNotEqual(Entity.Null, respawned, "stale current handle is replaced by a respawn");
            Assert.AreNotEqual(firstPatrol, respawned, "respawn is a new entity version");
            Assert.AreEqual(1, CountWith<PatrolAnchor>(em), "respawn keeps the one-patrol cap");

            bridge.StopBattle();
            yield return null;
            Assert.AreEqual(0, CountWith<PatrolAnchor>(em), "match boundary clears patrol entities");
            Assert.AreEqual(0, CountWith<PatrolRequestCarrier>(em), "match boundary clears staged requests");
        }

        [UnityTest]
        public IEnumerator SummonerDeath_RemovesItsPatrolAndView()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattleScene();

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var catalog = FindCatalog();
            var summonerData = catalog.ById("summoner");
            var ability = summonerData.GetAbility<SummonPatrolAbility>();

            bridge.SetDefenderPool(new[] { summonerData });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;

            Assert.IsTrue(FindSummonerCell(bridge, summonerData, ability.leashTileRadius, out var ownerCell));
            Assert.IsTrue(bridge.PlaceDefenderAs(ownerCell.x, ownerCell.y, summonerData));
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var summoner = EntityAt(bridge, em, ownerCell);

            bridge.StartBattle();
            OpenSummonGate(em, summoner);
            ForceAttackReady(em, summoner);

            Entity patrol = Entity.Null;
            for (int i = 0; i < 120 && patrol == Entity.Null; i++)
            {
                yield return null;
                patrol = ResolveLivePatrol(em, summoner);
            }
            Assert.AreNotEqual(Entity.Null, patrol, "initial patrol spawned");
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(bridge.TryGetUnitView(patrol, out _), "patrol view exists before owner death");

            var ownerHp = em.GetComponentData<Health>(summoner);
            ownerHp.value = 0f;
            em.SetComponentData(summoner, ownerHp);
            for (int i = 0; i < 180 && (em.Exists(summoner) || em.Exists(patrol)); i++) yield return null;

            Assert.IsFalse(em.Exists(summoner), "summoner destroyed");
            Assert.IsFalse(em.Exists(patrol), "owner-linked patrol destroyed");
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsFalse(bridge.TryGetUnitView(patrol, out _), "patrol view is reclaimed");
            Assert.AreEqual(0, CountWith<PatrolAnchor>(em), "no ghost patrol remains");
        }

        private static IEnumerator LoadBattleScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool FindSummonerCell(
            BattleBridge bridge,
            DefenderUnitData data,
            int leashRadius,
            out Vector2Int cell)
        {
            var method = typeof(BattleBridge).GetMethod(
                "TryGetPatrolAnchorCell",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "patrol anchor snap method");

            for (int x = -24; x < 48; x++)
            for (int y = -24; y < 48; y++)
            {
                if (!bridge.CanPlaceDefenderAt(x, y, data, out _)) continue;
                object[] args = { new int2(x, y), leashRadius, default(int2) };
                if (!(bool)method.Invoke(bridge, args)) continue;
                cell = new Vector2Int(x, y);
                return true;
            }
            cell = default;
            return false;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData data, out Vector2Int cell)
        {
            for (int x = -24; x < 48; x++)
            for (int y = -24; y < 48; y++)
            {
                if (!bridge.CanPlaceDefenderAt(x, y, data, out _)) continue;
                cell = new Vector2Int(x, y);
                return bridge.PlaceDefenderAs(x, y, data);
            }
            cell = default;
            return false;
        }

        private static System.Collections.IDictionary DefenderBindings(BattleBridge bridge)
        {
            var field = typeof(BattleBridge).GetField(
                "_defenderByTile",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (System.Collections.IDictionary)field.GetValue(bridge);
        }

        private static Entity EntityAt(BattleBridge bridge, EntityManager em, Vector2Int cell)
        {
            var dict = DefenderBindings(bridge);
            if (!dict.Contains(cell)) return Entity.Null;
            var tuple = dict[cell];
            var entity = (Entity)tuple.GetType().GetField("Item1").GetValue(tuple);
            return em.Exists(entity) ? entity : Entity.Null;
        }

        // 초회 소환 게이트를 연다. 이 테스트가 보는 것은 게이트가 아니라
        // AttackSystem → 캐리어 → Bridge 드레인 → CreatePatrolEntity 파이프라인이라,
        // "구역 안에 적이 있어야 첫 소환" 조건을 라이브 웨이브 타이밍에 맡기면 flaky 해진다.
        // 게이트 자체는 EditMode PatrolSystemIntegrationTests 가 5 케이스로 덮는다.
        private static void OpenSummonGate(EntityManager em, Entity summoner)
        {
            var state = em.GetComponentData<SummonerState>(summoner);
            state.hasSummonedOnce = true;
            em.SetComponentData(summoner, state);
        }

        private static void ForceAttackReady(EntityManager em, Entity entity)
        {
            var attack = em.GetComponentData<AttackState>(entity);
            attack.cooldownRemaining = 0f;
            em.SetComponentData(entity, attack);
        }

        private static Entity ResolveLivePatrol(EntityManager em, Entity summoner)
        {
            if (!em.Exists(summoner) || !em.HasComponent<SummonerState>(summoner)) return Entity.Null;
            var current = em.GetComponentData<SummonerState>(summoner).current;
            return current != Entity.Null
                   && em.Exists(current)
                   && em.HasComponent<PatrolAnchor>(current)
                   && em.HasComponent<Health>(current)
                   && em.GetComponentData<Health>(current).value > 0f
                ? current
                : Entity.Null;
        }

        private static int CountWith<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private static bool HasModifierOrigin(EntityManager em, Entity entity, ModifierOrigin origin)
        {
            if (!em.HasBuffer<StatModifierSlot>(entity)) return false;
            var slots = em.GetBuffer<StatModifierSlot>(entity);
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].header.origin == origin) return true;
            return false;
        }

        private static bool HasAllyMarker(Wassup.Presentation.SpineUnitView view)
        {
            if (view == null) return false;
            var renderers = view.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i].sprite != null && renderers[i].sprite.name == "AllyMarkerRing")
                    return true;
            return false;
        }

        private static void MoveTo(EntityManager em, Entity entity, float3 position)
        {
            var transform = em.GetComponentData<LocalTransform>(entity);
            transform.Position = position;
            em.SetComponentData(entity, transform);
        }
    }
}
