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
    // bleed-fighter-defender unit 1 — OnPlaceEffectType.ApplyStackNearby (등장 난도질).
    // 검증 대상은 분기의 **반경 필터**와 스택 파라미터 전달이다. on-place 실행은
    // BattleBridge.ApplyOnPlaceEffect(Mono · 생성된 맵/쿼리 의존)라 EditMode 로는 닿지 않는다
    // — DefenderApplyStackOutputTest 와 같은 이유로 PlayMode 에 둔다.
    //
    // 함정: `_aliveAttackersQuery` 는 **AttackUnitTag** 로만 잡는다. 더미 적에 이 태그가 없으면
    // 반경 안에 있어도 조용히 0명이 되어 테스트가 vacuous 해진다.
    public class OnPlaceApplyStackNearbyTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator ApplyStackNearby_StacksEnemiesInRange_AndSkipsThoseOutside()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var catalog = FindDefenderCatalog();
            var slasher = Object.Instantiate(catalog.ById("guardian"));
            slasher.id = "test_onplace_stacker";
            slasher.onPlaceEffect = OnPlaceEffectType.ApplyStackNearby;
            slasher.onPlaceStackKind = StackKind.Bleed;
            slasher.onPlaceRange = 2f;
            // Bleed 는 누적형(atStack 5 Consume) — 배치 도포는 임계치를 한 번에 준다.
            slasher.onPlaceMagnitude = 5f;
            slasher.onPlaceDuration = 2f;

            bridge.SetDefenderPool(new[] { slasher });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);

            // 배치 **전에** 적을 세운다 — on-place 는 배치 순간의 쿼리 스냅샷을 본다.
            Vector2Int cell = FindPlaceableCell(bridge, slasher);
            Assert.AreNotEqual(new Vector2Int(int.MinValue, int.MinValue), cell, "placeable cell found");

            var near = SpawnDummyEnemy(em, bridge.GridToWorldCenterVector(new Vector2Int(cell.x + 1, cell.y)));
            var far = SpawnDummyEnemy(em, bridge.GridToWorldCenterVector(new Vector2Int(cell.x + 9, cell.y)));

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, slasher), "place slasher");

            // 큐 드레인 + 임계 발화 + CcApply 까지 몇 프레임.
            // ⚠ stackCount 로 단언하지 않는다: Bleed 는 `atStack 5 · mode Consume` 이라
            // 임계에 닿는 순간 발화하며 **스택을 도로 소모**해 0으로 돌아간다. 스택 수는
            // Bleed 의 안정적 관측값이 아니고, 도포의 관측 가능한 결과는 파생 DoT 다.
            bool nearHadBleed = false, nearHasDot = false;
            float t = 0f;
            while (t < 3f && !nearHasDot)
            {
                t += Time.deltaTime;
                nearHadBleed |= HasBleed(em, near);
                nearHasDot |= HasDot(em, near);
                yield return null;
            }
            bool farHadBleed = HasBleed(em, far);
            bool farHasDot = HasDot(em, far);

            if (em.Exists(near)) em.DestroyEntity(near);
            if (em.Exists(far)) em.DestroyEntity(far);
            Object.Destroy(slasher);

            Assert.IsTrue(nearHadBleed, "반경 내 적은 Bleed 스택 슬롯을 받아야 함");
            Assert.IsTrue(nearHasDot, "Bleed 임계가 발화해 DoT 로 이어져야 함");
            Assert.IsFalse(farHadBleed, "반경 밖 적은 스택을 받지 않아야 함");
            Assert.IsFalse(farHasDot, "반경 밖 적은 DoT 도 없어야 함");
        }

        // ── helpers ──────────────────────────────────────────────────────────
        private static bool HasBleed(EntityManager em, Entity e)
        {
            if (!em.Exists(e) || !em.HasBuffer<StackModifierSlot>(e)) return false;
            var buf = em.GetBuffer<StackModifierSlot>(e);
            for (int i = 0; i < buf.Length; i++)
                if (buf[i].kind == StackKind.Bleed) return true;
            return false;
        }

        private static bool HasDot(EntityManager em, Entity e)
        {
            if (!em.Exists(e) || !em.HasBuffer<CcEffect>(e)) return false;
            var buf = em.GetBuffer<CcEffect>(e);
            for (int i = 0; i < buf.Length; i++)
                if (buf[i].kind == CcKind.DoT && buf[i].remainingTime > 0f) return true;
            return false;
        }

        private static Entity SpawnDummyEnemy(EntityManager em, Vector3 worldPos)
        {
            const float Hp = 1_000_000f;
            var enemy = em.CreateEntity();
            em.AddComponentData(enemy, LocalTransform.FromPosition(new float3(worldPos.x, worldPos.y, worldPos.z)));
            em.AddComponentData(enemy, new Health { value = Hp, max = Hp });
            em.AddComponentData(enemy, new FactionTag { value = Faction.Enemy });
            em.AddBuffer<IncomingDamage>(enemy);
            em.AddBuffer<CcEffect>(enemy);         // 임계 파생 DoT 의 소비처
            em.AddComponent<AttackUnitTag>(enemy); // ← _aliveAttackersQuery 가 보는 유일한 조건
            return enemy;
        }

        private static DefenderCatalog FindDefenderCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static Vector2Int FindPlaceableCell(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return new Vector2Int(x, y);
            return new Vector2Int(int.MinValue, int.MinValue);
        }
    }
}
