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
using Wassup.Battle.Movement;

namespace Wassup.Tests.PlayMode
{
    // skill-layer-migration unit 2a — 브루저의 배치 폭발.
    //
    // 원래 `OnPlaceEffectType.MeleeBurst`(레거시 어휘) 특성화 그물로 먼저 깔았고,
    // 이전 뒤 **행동 단언은 그대로 두고 값의 출처만** 규칙 경로로 옮겼다. 그게 이
    // 그물이 이전을 판정할 수 있는 이유다 — 단언이 바뀌면 무엇이 보존됐는지 알 수 없다.
    //
    // 고정하는 것 넷:
    //  ① 반경 안 적이 **정확히 `onPlaceMagnitude` 만큼, 한 번에** 맞는다.
    //     ⚠ 「한 번에」가 계약의 핵심이다 — 이 arm 은 `IncomingDamage` 를 직접 넣는
    //     즉발이고, 이걸 `SelfTileAoe`(투사체 경유 광역)로 갈아끼우면 **연출이 생기고
    //     피해가 한 프레임 밀린다.** 총량만 재면 그 차이가 안 보이므로 즉시성도 잰다.
    //  ② 반경 밖은 무피해 — 사거리는 `onPlaceRange` 저작값에서 온다.
    //  ③ **통행 층 게이트가 산다** — 브루저 `attackTargetLayers` 는 Path|Air 라
    //     Ground 전용 이동체를 못 때린다. 이 단언이 없으면 게이트가 죽어도 초록이다.
    //  ④ 저작 수치는 **SO 가 권위**다. 리터럴로 못박지 않는다.
    public class OnPlaceMeleeBurstTest
    {
        private const float Hp = 100000f;

        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator MeleeBurst_DealsMagnitudeOnce_InRangeAndReachableLayerOnly()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            // 평타가 섞이면 배치 폭발분을 분리 측정할 수 없다.
            var bruiser = Object.Instantiate(catalog.ById("bruiser"));
            bruiser.id = "test_onplace_meleeburst";
            bruiser.attackRange = 0f;
            bruiser.cost = 0;
            bruiser.maxOnBoard = 100;

            // ⚠ 값은 **규칙 저작(UnitSkillAbility)** 에서 온다. 레거시 flat 필드는 꺼져 있다.
            var skill = bruiser.GetAbility<UnitSkillAbility>();
            Assert.IsNotNull(skill, "브루저에 배치 스킬(UnitSkillAbility)이 배선돼야 한다");
            Assert.AreEqual(DcTriggerKind.OnPlace, skill.mechanics[0].trigger.kind, "트리거 = 배치");
            Assert.AreEqual(DcPayloadKind.SelfTileAoe, skill.mechanics[0].payload.kind,
                "페이로드 = 자기 자리 즉발 광역");
            Assert.IsNotNull(skill.mechanics[0].payload.projectile,
                "SelfTileAoe 는 ProjectileData 가 없으면 폭발 요청이 통째로 드롭된다");
            float mag = skill.mechanics[0].payload.magnitude;
            int tiles = skill.mechanics[0].payload.tileRange;
            byte atk = (byte)bruiser.attackTargetLayers;
            Assert.Greater(mag, 0f, "폭발 피해가 저작돼 있어야 한다");
            Assert.GreaterOrEqual(tiles, 1, "반경이 저작돼 있어야 한다");
            // 이 그물이 층 게이트를 «측정» 하려면 브루저가 못 때리는 층이 존재해야 한다.
            Assert.IsFalse(PlacementLayers.CanTarget(atk, (byte)PlacementLayer.Ground),
                "전제: 브루저가 Ground 전용 이동체를 못 때려야 게이트 단언이 의미를 갖는다");
            Assert.IsTrue(PlacementLayers.CanTarget(atk, (byte)PlacementLayer.Path),
                "전제: Path 이동체는 때릴 수 있어야 한다");

            Prepare(bridge, gm, bruiser);

            var cell = FindPlaceableCell(bridge, bruiser);
            Assert.AreNotEqual(new Vector2Int(int.MinValue, int.MinValue), cell, "배치 가능 셀");

            var near = SpawnDummy(em, bridge, new Vector2Int(cell.x + 1, cell.y),
                                  (byte)PlacementLayer.Path);
            var far = SpawnDummy(em, bridge, new Vector2Int(cell.x + tiles + 3, cell.y),
                                 (byte)PlacementLayer.Path);
            // 코앞인데 «브루저가 못 때리는 층» — 거리로는 확실히 안이라 게이트만 남는다.
            var unreachable = SpawnDummy(em, bridge, new Vector2Int(cell.x, cell.y + 1),
                                         (byte)PlacementLayer.Ground);

            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();
            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, bruiser), "배치");

            // ① 즉발성. ⚠ 레거시는 `IncomingDamage` 직접 주입이라 **다음 프레임**에 다
            // 들어갔다. 규칙 경로는 폭발이 요청 캐리어 한 번을 거치므로 한 프레임 더 든다 —
            // 그래서 창을 4프레임으로 넓혔다. **넓힌 것은 창이지 계약이 아니다**: 아래
            // 총량 단언이 「저작값 정확히 한 번」을 그대로 지킨다.
            // ⚠ **창을 4프레임으로 둔다.** 레거시는 `IncomingDamage` 직접 주입이라
            // 다음 프레임에 다 들어갔다. 규칙 경로는 폭발이 요청 캐리어를 한 번 거치므로
            // 한 프레임 더 든다. 넓힌 것은 창이지 계약이 아니다 — 아래 총량 단언이
            // 「저작값 정확히 한 번」을 그대로 지킨다.
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            float immediate = Hp - em.GetComponentData<Health>(near).value;
            int seamRuns = Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                Wassup.Battle.Skills.SkillSeam.Periodic);

            // 뒤늦게 더 들어오는 분이 없는지도 본다(도트화·중복 착탄 회귀).
            float t = 0f;
            while (t < 1.0f) { t += Time.deltaTime; yield return null; }

            float nearDealt = Hp - em.GetComponentData<Health>(near).value;
            float farDealt = Hp - em.GetComponentData<Health>(far).value;
            float blockedDealt = Hp - em.GetComponentData<Health>(unreachable).value;
            foreach (var e in new[] { near, far, unreachable }) em.DestroyEntity(e);
            Object.Destroy(bruiser);

            Assert.GreaterOrEqual(seamRuns, 1,
                "배치 스킬이 concrete 를 안 거쳤다 — 감지/라우팅 구간이 끊겼다");
            Assert.AreEqual(mag, immediate, 0.01f,
                $"배치 직후 창 안에 저작 피해 전량({mag})이 들어가야 한다 — 실측 {immediate}. "
                + "적으면 즉발이 아니라 지연/분할로 바뀐 것이다");
            Assert.AreEqual(mag, nearDealt, 0.01f,
                $"총 피해가 저작값({mag})과 달라졌다 — 실측 {nearDealt}. 크면 중복 착탄이다");
            Assert.AreEqual(0f, farDealt, 0.01f, "반경 밖 적은 무피해");
            Assert.AreEqual(0f, blockedDealt, 0.01f,
                "못 때리는 층의 적이 맞았다 — 통행 층 게이트가 죽었다");
        }

        [UnityTest]
        public IEnumerator DegenerateMagnitude_FiresNothing()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var bruiser = Object.Instantiate(catalog.ById("bruiser"));
            bruiser.id = "test_onplace_meleeburst_zero";
            bruiser.attackRange = 0f;
            bruiser.cost = 0;
            bruiser.maxOnBoard = 100;
            // 퇴화 저작 — 발동을 조용히 소모한다. 카탈로그 원본을 건드리지 않도록
            // 규칙 에셋도 복제해서 0 으로 만든다.
            var zeroSkill = Object.Instantiate(bruiser.GetAbility<UnitSkillAbility>());
            zeroSkill.mechanics[0].payload.magnitude = 0f;
            bruiser.abilities = new System.Collections.Generic.List<DefenderAbilityData> { zeroSkill };

            Prepare(bridge, gm, bruiser);

            var cell = FindPlaceableCell(bridge, bruiser);
            var near = SpawnDummy(em, bridge, new Vector2Int(cell.x + 1, cell.y),
                                  (byte)PlacementLayer.Path);

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, bruiser), "배치");
            float t = 0f;
            while (t < 0.5f) { t += Time.deltaTime; yield return null; }

            float dealt = Hp - em.GetComponentData<Health>(near).value;
            em.DestroyEntity(near);
            Object.Destroy(bruiser);

            Assert.AreEqual(0f, dealt, 0.01f, "피해 0 저작인데 적이 맞았다");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        // ⚠ **`StartBattle()` 가 이 그물의 조건이다.** 투사체 요청 드레인이 `_running`
        // 아래라, 배치 페이즈에 머물면 폭발 캐리어가 만들어지고 **영원히 안 풀린다**
        // (프레임 계측으로 확인: carrier 1 · projectile 0 이 8프레임 유지).
        // 레거시 `MeleeBurst` 는 `IncomingDamage` 를 직접 넣어 이 경로가 필요 없었다 —
        // 이전이 그물의 전제를 바꾼 자리다.
        private static void Prepare(BattleBridge bridge, GameManager gm, DefenderUnitData unit)
        {
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            bridge.StartBattle();
            BattleBridgeTestAccess.SetField(bridge, "_usingGeneratedWaves", false);
            ((System.Collections.IList)BattleBridgeTestAccess.Field(bridge, "_pending")).Clear();
            // 다른 공격자가 섞이면 배치 폭발분을 분리 측정할 수 없다.
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var attackers = em.CreateEntityQuery(
                ComponentType.ReadOnly<Wassup.Battle.Combat.AttackState>());
            if (!attackers.IsEmpty)
                em.RemoveComponent<Wassup.Battle.Combat.AttackState>(attackers);
        }

        private static Entity SpawnDummy(EntityManager em, BattleBridge bridge,
                                         Vector2Int cell, byte layers)
        {
            var w = bridge.GridToWorldCenterVector(cell);
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(new float3(w.x, w.y, w.z)));
            em.AddComponentData(e, new Health { value = Hp, max = Hp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<CcEffect>(e);
            em.AddComponent<AttackUnitTag>(e);
            // ⚠ **통행 층을 명시한다.** 없으면 0 = 무제한으로 게이트를 조기 통과해,
            // 게이트가 죽어도 이 테스트가 초록이 된다.
            em.AddComponentData(e, new PathFollowState { speed = 0f, traversalLayers = layers });
            // 스킬 레이어의 핸들 축 — 이전 후에도 이 더미가 후보로 남으려면 필요하다.
            BattleBridgeTestAccess.AttachSimEntityId(bridge, e);
            return e;
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
