using System.Collections;
using System.Reflection;
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
    // knockup-fighter-defender units 0·1 — 공중 띄우기의 심(= 짧은 Stun) 두 경로.
    //
    // unit 0: knockupOnHitSec 은 히트한 **전 대상**에 건다. 이것이 기존 sleepOnHitSec(주 타겟
    //         1체)과 갈리는 지점이라, 다중 타겟(attackTargetCount>1)에서만 차이가 드러난다
    //         — 단일 타겟으로 테스트하면 두 계약이 구분되지 않아 vacuous 해진다.
    // unit 1: OnPlaceEffectType.StunNearby 는 배치 반경 내 적 전원.
    //
    // 함정: on-place 쿼리(_aliveAttackersQuery)는 **AttackUnitTag** 로만 잡는다.
    public class KnockupOnHitTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator KnockupOnHit_StunsEveryHitTarget_NotJustThePrimary()
        {
            yield return Setup();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var catalog = FindDefenderCatalog();
            var malphite = Object.Instantiate(catalog.ById("guardian"));
            malphite.id = "test_knockup";
            malphite.attackTargetCount = 3;      // ← 전 대상 계약을 드러내는 유일한 조건
            malphite.knockupOnHitSec = 0.8f;
            malphite.sleepOnHitSec = 0f;
            malphite.attackRange = 2f;

            bridge.SetDefenderPool(new[] { malphite });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            Assert.IsTrue(PlaceFirstValid(bridge, malphite), "place malphite");

            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");

            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            var a = SpawnDummyEnemy(em, defPos + new float3(0.05f, 0f, 0f));
            var b = SpawnDummyEnemy(em, defPos + new float3(0.10f, 0f, 0f));
            var c = SpawnDummyEnemy(em, defPos + new float3(0.15f, 0f, 0f));

            // 세 대상이 **동시에** 스턴 상태인 프레임을 찾는다. 순차 관측(각각 한 번씩)으로는
            // "주 타겟만 걸고 타겟이 돌아가며 바뀌는" 오답과 구분되지 않는다.
            bool allThreeStunnedAtOnce = false;
            float t = 0f;
            while (t < 8f && !allThreeStunnedAtOnce)
            {
                t += Time.deltaTime;
                allThreeStunnedAtOnce = IsStunned(em, a) && IsStunned(em, b) && IsStunned(em, c);
                yield return null;
            }

            foreach (var e in new[] { a, b, c }) if (em.Exists(e)) em.DestroyEntity(e);
            Object.Destroy(malphite);

            Assert.IsTrue(allThreeStunnedAtOnce,
                "knockupOnHitSec 은 히트한 전 대상(3체)을 같은 타격에 스턴시켜야 함");
        }

        [UnityTest]
        public IEnumerator StunNearby_OnPlace_StunsEnemiesInRange_AndSkipsThoseOutside()
        {
            yield return Setup();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var catalog = FindDefenderCatalog();
            var malphite = Object.Instantiate(catalog.ById("guardian"));
            malphite.id = "test_onplace_stun";
            malphite.onPlaceEffect = OnPlaceEffectType.StunNearby;
            malphite.onPlaceRange = 1f;
            malphite.onPlaceDuration = 0.8f;
            malphite.knockupOnHitSec = 0f;   // 배치 스킬만 격리 — 공격 넉업이 섞이면 판정 불가

            bridge.SetDefenderPool(new[] { malphite });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);

            var cell = FindPlaceableCell(bridge, malphite);
            Assert.AreNotEqual(new Vector2Int(int.MinValue, int.MinValue), cell, "placeable cell found");

            // 배치 **전에** 세운다 — on-place 는 배치 순간의 쿼리 스냅샷을 본다.
            var near = SpawnDummyEnemy(em, ToFloat3(bridge.GridToWorldCenterVector(new Vector2Int(cell.x + 1, cell.y))));
            var far = SpawnDummyEnemy(em, ToFloat3(bridge.GridToWorldCenterVector(new Vector2Int(cell.x + 9, cell.y))));

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, malphite), "place malphite");

            bool nearStunned = false;
            float t = 0f;
            while (t < 3f && !nearStunned)
            {
                t += Time.deltaTime;
                nearStunned = IsStunned(em, near);
                yield return null;
            }
            bool farStunned = IsStunned(em, far);

            if (em.Exists(near)) em.DestroyEntity(near);
            if (em.Exists(far)) em.DestroyEntity(far);
            Object.Destroy(malphite);

            Assert.IsTrue(nearStunned, "반경 내 적은 배치 순간 스턴되어야 함");
            Assert.IsFalse(farStunned, "반경 밖 적은 영향 없어야 함");
        }

        // ── helpers ──────────────────────────────────────────────────────────
        private static IEnumerator Setup()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static float3 ToFloat3(Vector3 v) => new float3(v.x, v.y, v.z);

        private static bool IsStunned(EntityManager em, Entity e)
        {
            if (!em.Exists(e) || !em.HasBuffer<CcEffect>(e)) return false;
            var buf = em.GetBuffer<CcEffect>(e);
            for (int i = 0; i < buf.Length; i++)
                if (buf[i].kind == CcKind.Stun && buf[i].remainingTime > 0f) return true;
            return false;
        }

        private static Entity SpawnDummyEnemy(EntityManager em, float3 pos)
        {
            const float Hp = 1_000_000f; // 죽지 않게 — 공격이 계속 이어지도록
            var enemy = em.CreateEntity();
            em.AddComponentData(enemy, LocalTransform.FromPosition(pos));
            em.AddComponentData(enemy, new Health { value = Hp, max = Hp });
            em.AddComponentData(enemy, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(enemy);
            em.AddBuffer<CcEffect>(enemy);
            em.AddComponent<AttackUnitTag>(enemy);
            return enemy;
        }

        private static DefenderCatalog FindDefenderCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u)
        {
            var cell = FindPlaceableCell(bridge, u);
            if (cell.x == int.MinValue) return false;
            return bridge.PlaceDefenderAs(cell.x, cell.y, u);
        }

        private static Vector2Int FindPlaceableCell(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return new Vector2Int(x, y);
            return new Vector2Int(int.MinValue, int.MinValue);
        }

        private static Entity FindDefender(BattleBridge bridge, EntityManager em)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var val = de.Value;
                var entity = (Entity)val.GetType().GetField("Item1").GetValue(val);
                if (em.Exists(entity)) return entity;
            }
            return Entity.Null;
        }
    }
}
