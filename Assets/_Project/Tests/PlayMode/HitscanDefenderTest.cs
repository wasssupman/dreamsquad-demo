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

namespace Wassup.Tests.PlayMode
{
    // beam-ranger-defender unit 0 — 버스터즈의 심 전제를 고정한다:
    // **투사체 SO 가 없는 원거리 유닛은 직접 데미지 경로를 탄다.**
    //
    // AttackSystem 의 발사 분기는 `projectileRefLookup.HasComponent(attacker)` 게이트이고,
    // else 가 Outputs path(직접 IncomingDamage)다. 사거리는 근접 전용이 아니라 타게팅 질의에
    // 그대로 쓰이므로 "사거리 3 + 투사체 없음" 이 성립한다 — 이 조합을 쓰는 첫 유닛이라
    // 전제가 조용히 깨지면(예: 무투사체 = 근접 강제) 빔 유닛이 통째로 죽는다.
    //
    // 빔 비주얼(unit 1)은 여기 없다 — 심은 "0.2초마다 7 피해"일 뿐이고 빔은 뷰의 해석이다.
    public class HitscanDefenderTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator ProjectilelessRangedDefender_DealsDirectDamage_AtRange()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var catalog = FindDefenderCatalog();
            var busters = catalog.ById("busters");
            Assert.IsNotNull(busters, "busters 가 카탈로그에 있어야 함");
            Assert.IsNull(busters.projectile, "히트스캔 사양 — projectile 은 비어 있어야 한다(실수 아님)");
            Assert.Greater(busters.attackRange, 1f, "원거리여야 이 테스트가 의미를 가진다");

            bridge.SetDefenderPool(new[] { busters });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            Assert.IsTrue(PlaceFirstValid(bridge, busters), "place busters");

            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");

            // 사거리 안이되 **근접이 아닌** 거리에 세운다 — 1타일 넘게 떨어뜨려야
            // "무투사체=근접" 으로 퇴화했을 때 검출된다.
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            const float Hp = 100000f;
            var enemy = em.CreateEntity();
            em.AddComponentData(enemy, LocalTransform.FromPosition(defPos + new float3(1.6f, 0f, 0f)));
            em.AddComponentData(enemy, new Health { value = Hp, max = Hp });
            em.AddComponentData(enemy, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(enemy);
            // 배치 조사(DotNearby)가 이 적에게도 DoT 를 건다 — 실적 아키타입처럼 CcEffect 버퍼가
            // 없으면 CcApply 가 던진다. 더미는 실제 적이 가진 것을 갖춰야 한다.
            em.AddBuffer<Wassup.Battle.Effects.CcEffect>(enemy);
            em.AddComponent<AttackUnitTag>(enemy);

            // 배치 스킬(개점 일제 조사)이 끝날 때까지 기다린 뒤에 잰다. 조사 중에는 기본 공격을
            // 하지 않는 것이 사양이고(BeamPresentationTest 가 그 계약을 지킨다), 조사의 tick DoT
            // 자체도 이 적에게 들어오므로 그 구간을 섞으면 "기본 공격이 도는가"를 못 잰다.
            float settle = busters.onPlaceDuration + 0.6f; // 조사 지속 + DoT 꼬리
            float t = 0f;
            while (t < settle) { t += Time.deltaTime; yield return null; }

            // 1.2초 = 쿨다운 0.2 기준 5~6틱. 정확한 틱 수는 프레임 경계에 걸리므로
            // "고속으로 여러 번 들어왔다" 만 단언한다(틱 수 고정은 취약).
            float before = em.GetComponentData<Health>(enemy).value;
            t = 0f;
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            float dealt = before - em.GetComponentData<Health>(enemy).value;
            em.DestroyEntity(enemy);

            Assert.Greater(dealt, 7f * 2f,
                $"1.2초 동안 고속 틱 직접 피해가 누적돼야 함(실측 {dealt}). "
                + "0 이면 무투사체 원거리가 발사도 직접타격도 못 한다는 뜻 — 이 유닛의 전제가 깨진 것");
        }

        // ── helpers ──────────────────────────────────────────────────────────
        private static DefenderCatalog FindDefenderCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
            return false;
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
