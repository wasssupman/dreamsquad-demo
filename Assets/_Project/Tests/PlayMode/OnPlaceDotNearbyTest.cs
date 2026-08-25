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
    // beam-ranger-defender unit 2 — 개점 일제 조사(OnPlaceEffectType.DotNearby).
    //
    // 검증 대상은 **틱당 피해 계약**이다. `DotEffect.tickInterval > 0` 이면 `scalar` 는 DPS 가
    // 아니라 **틱당 피해**다(dot-tick-cadence). 이걸 DPS 로 오해해 환산하면 피해가 배로 틀린다
    // — 실제로 spec 초안이 그렇게 적혀 있었고 여기서 바로잡았다.
    //
    // 총 피해 = magnitude × (duration / tickInterval) = 7 × (2 / 0.2) = 70.
    public class OnPlaceDotNearbyTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator DotNearby_DealsPerTickDamage_InRangeOnly()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var catalog = FindDefenderCatalog();
            // 카탈로그 버스터즈를 복제해 **공격은 못 하게** 사거리를 0 으로 만든다 —
            // 배치 조사분과 평소 공격분이 섞이면 틱당 피해를 분리 측정할 수 없다.
            var busters = Object.Instantiate(catalog.ById("busters"));
            busters.id = "test_onplace_dot";
            busters.attackRange = 0f;
            // skill-layer-migration unit 2e — 레거시 flat 필드에서 규칙 저작으로 이사했다.
            Assert.AreEqual(OnPlaceEffectType.None, busters.onPlaceEffect,
                "레거시 배치 필드가 아직 켜져 있다 — 두 경로가 동시에 돈다");
            var dotSpec = busters.GetAbility<UnitSkillAbility>()?.mechanics[0].payload;
            Assert.IsNotNull(dotSpec, "버스터즈에 배치 스킬(UnitSkillAbility)이 배선돼야 한다");
            Assert.AreEqual(DcPayloadKind.AreaDot, dotSpec.Value.kind, "페이로드 = 광역 지속 피해");
            Assert.Greater(dotSpec.Value.tickIntervalSec, 0f,
                "틱 간격이 0 이면 magnitude 가 DPS 로 해석돼 이 그물의 축이 달라진다");

            bridge.SetDefenderPool(new[] { busters });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);

            var cell = FindPlaceableCell(bridge, busters);
            Assert.AreNotEqual(new Vector2Int(int.MinValue, int.MinValue), cell, "placeable cell");

            const float Hp = 100000f;
            var near = SpawnDummy(em, bridge, bridge.GridToWorldCenterVector(new Vector2Int(cell.x + 1, cell.y)), Hp);
            var far = SpawnDummy(em, bridge, bridge.GridToWorldCenterVector(new Vector2Int(cell.x + 9, cell.y)), Hp);

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, busters), "place busters");

            // duration(2s) + 여유. DoT 가 다 닳을 때까지.
            float t = 0f;
            while (t < 3.0f) { t += Time.deltaTime; yield return null; }

            float nearDealt = Hp - em.GetComponentData<Health>(near).value;
            float farDealt = Hp - em.GetComponentData<Health>(far).value;
            em.DestroyEntity(near);
            em.DestroyEntity(far);
            Object.Destroy(busters);

            // 총 70 기대. 프레임 경계로 마지막 틱이 잘릴 수 있어 범위로 본다.
            // 하한 42(=6틱)는 "DPS 로 오해해 7/0.2=35 를 넣었다면" 350 이 되어 상한에서 걸린다.
            Assert.Greater(nearDealt, 42f, $"반경 내 적에게 틱당 피해가 누적돼야 함(실측 {nearDealt})");
            Assert.Less(nearDealt, 100f,
                $"총 피해는 magnitude×(duration/tickInterval)=70 근처여야 한다(실측 {nearDealt}). "
                + "크게 넘으면 scalar 를 DPS 로 오해해 환산한 것");
            Assert.AreEqual(0f, farDealt, 0.01f, "반경 밖 적은 무피해");
        }

        // ── helpers ──────────────────────────────────────────────────────────
        private static Entity SpawnDummy(EntityManager em, BattleBridge bridge, Vector3 worldPos, float hp)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(new float3(worldPos.x, worldPos.y, worldPos.z)));
            em.AddComponentData(e, new Health { value = hp, max = hp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<CcEffect>(e);
            em.AddBuffer<DotEffect>(e); // dot-effect-extraction unit 0 — 배치 도트의 소비처
            em.AddComponent<AttackUnitTag>(e);
            em.AddComponentData(e, new Wassup.Battle.Movement.PathFollowState
            {
                speed = 0f, traversalLayers = (byte)Wassup.Data.PlacementLayer.Path,
            });
            BattleBridgeTestAccess.AttachSimEntityId(bridge, e);
            return e;
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
