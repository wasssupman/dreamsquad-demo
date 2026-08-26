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
    // dreamcatcher-kill-and-threshold unit 3 — Spec B 두 능력의 실전투 통합 검증.
    // last_stand: HP 임계 돌파 시 자기 공격력 버프가 실제 ModifierStats 에 올라오는가.
    // devouring_craving: 적 처치 시 killer(=공격자) 에게 공속 버프가 붙는가(OnKill+킬귀속).
    // 코어 IncomingDamage.source 수술이 기존 데미지 경로를 깨지 않는지도 여기서 커버.
    public class DreamcatcherKillThresholdTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        // last_stand — HealthThreshold(fraction=0.7 → HP 30% 이하) × SelfStatBuff(공격력 +30%, 영구).
        [UnityTest]
        public IEnumerator LastStand_BelowHpThreshold_BuffsAttackDamage()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindDefenderCatalog();
            var guardian = cat.ById("guardian");

            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");

            int handle = bridge.ApplyDreamcatcherCardToUnit(defender, MakeLastStandCard());
            Assert.GreaterOrEqual(handle, 0, "last_stand attached (bake ok, maxHp snapshot)");

            // on-place 버프 감쇠 → damageMul baseline 안정화.
            yield return RunSeconds(8f);
            float preMul = em.GetComponentData<ModifierStats>(defender).damageMul;

            // 30% 경계 돌파(생존 유지) — HP 를 25% 로. 적이 없어 HP 는 고정.
            var hp = em.GetComponentData<Health>(defender);
            em.SetComponentData(defender, new Health { value = hp.max * 0.25f, max = hp.max });
            yield return RunSeconds(1.5f); // HealthThresholdSystem fire → ModifierApply → Aggregate
            float postMul = em.GetComponentData<ModifierStats>(defender).damageMul;

            Assert.Greater(postMul, preMul * 1.2f,
                $"last_stand: HP<30% 돌파 후 damageMul 상승 예상 ({preMul:0.00}->{postMul:0.00})");
        }

        // devouring_craving — OnKill × SelfStatBuff(공속 +8%, 4s). 처치 시 killer 에 부착.
        [UnityTest]
        public IEnumerator DevouringCraving_OnKill_BuffsAttackSpeed()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindDefenderCatalog();
            var guardian = cat.ById("guardian"); // melee → 직접 IncomingDamage(source=attacker)

            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");

            int handle = bridge.ApplyDreamcatcherCardToUnit(defender, MakeDevouringCard());
            Assert.GreaterOrEqual(handle, 0, "devouring attached");

            yield return RunSeconds(8f); // on-place 감쇠
            float preAs = em.GetComponentData<ModifierStats>(defender).attackSpeedMul;

            // 약한 적(HP 1) 을 사거리 안에 → guardian 이 한 방에 처치 → OnKill 발동.
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            var enemy = em.CreateEntity();
            em.AddComponentData(enemy, LocalTransform.FromPosition(defPos + new float3(0.05f, 0f, 0f)));
            em.AddComponentData(enemy, new Health { value = 1f, max = 1f });
            em.AddComponentData(enemy, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(enemy);
            // 스킬 레이어는 두 풀 안의 엔티티만 다룬다(unit 3a 함정) — 실적 아키타입 모사.
            em.AddComponent<Wassup.Battle.Units.AttackUnitTag>(enemy);
            BattleBridgeTestAccess.AttachSimEntityId(bridge, enemy);

            // ⚠ **죽음 seam 의 첫 증인.** 이 단언이 없으면 라우팅이 끊기고 legacy arm 이
            // 대신 처리해도 아래 결과 단언이 초록이다.
            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();

            float t = 0f;
            while (t < 5f && em.Exists(enemy) && em.GetComponentData<Health>(enemy).value > 0f)
            { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(!em.Exists(enemy) || em.GetComponentData<Health>(enemy).value <= 0f,
                "guardian 이 약한 적을 처치");

            for (int i = 0; i < 4; i++) yield return null; // ModifierApply/Aggregate 정착
            float postAs = em.GetComponentData<ModifierStats>(defender).attackSpeedMul;

            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Death), 1,
                "죽음 seam 이 concrete 를 안 거쳤다 — legacy arm 이 대신 처리했다면 아래 단언은 "
                + "라우팅이 끊겨도 초록이 된다");
            Assert.Greater(postAs, preAs * 1.03f,
                $"devouring: 처치 후 attackSpeedMul 상승 예상 ({preAs:0.000}->{postAs:0.000})");
        }

        private static DreamcatcherCard MakeLastStandCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.HealthThreshold, fraction = 0.7f },
                payload = new DcPayloadSpec { kind = DcPayloadKind.SelfStatBuff, buffStat = CardBuffKind.AttackDamage, magnitude = 30f, duration = 0f },
            }};
            return card;
        }

        // skill-layer-migration unit 3d — **시체폭발의 첫 행동 그물.**
        //
        // 이 payload 는 여태 「붙는다」만 검증됐고 **어디서 터지나**를 아무도 안 쟀다.
        // 그게 이 스킬의 전부인데도 그랬다 — 자기 자리 폭발과 코드가 거의 같아 보이지만
        // 게임에서는 「내가 맞은 자리」와 「내가 죽인 자리」로 완전히 다른 그림이다.
        //
        // 가르는 기하: 방어유닛에서 1칸에 처치 대상 A, 2칸에 구경꾼 B, 반경 1.
        //   · 폭발이 **A 자리**면 B 는 1칸이라 맞는다   ← 사양
        //   · 폭발이 **방어유닛 자리**면 B 는 2칸이라 안 맞는다
        // 그래서 B 의 피해 유무 하나로 자리가 갈린다.
        [UnityTest]
        public IEnumerator CorpseBurst_ExplodesAtTheVictimsSpot_NotTheCasters()
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
            // ⚠ **투사체 요청 드레인은 `_running` 아래다.** 배치 페이즈에 머물면 폭발
            // 캐리어가 만들어지고 영원히 안 풀린다(unit 2a 에서 프레임 계측으로 확인).
            // 형제 테스트(포식)는 스탯 모디파이어라 이 경로가 필요 없었다.
            bridge.StartBattle();
            BattleBridgeTestAccess.SetField(bridge, "_usingGeneratedWaves", false);
            ((System.Collections.IList)BattleBridgeTestAccess.Field(bridge, "_pending")).Clear();
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");
            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardToUnit(defender, MakeCorpseBurstCard()), 0,
                "corpse burst attached");

            float tile = (bridge.GridToWorldCenterVector(new Vector2Int(1, 0))
                          - bridge.GridToWorldCenterVector(new Vector2Int(0, 0))).magnitude;
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;

            // A: 곧 죽을 적(HP 1) — 방어유닛에서 1칸.
            var victim = SpawnBystander(em, bridge, defPos + new float3(tile, 0f, 0f), hp: 1f);
            // B: 구경꾼 — A 에서 1칸, 방어유닛에서 2칸.
            const float ByHp = 100000f;
            var bystander = SpawnBystander(em, bridge, defPos + new float3(tile * 2f, 0f, 0f), ByHp);

            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();

            float t = 0f;
            while (t < 6f && em.Exists(victim) && em.GetComponentData<Health>(victim).value > 0f)
            { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(!em.Exists(victim) || em.GetComponentData<Health>(victim).value <= 0f,
                "전제: 방어유닛이 A 를 처치해야 이 그물이 측정이 된다");

            for (int i = 0; i < 30; i++) yield return null;   // 캐리어 → 탄 → 피해
            float dealt = ByHp - em.GetComponentData<Health>(bystander).value;
            if (em.Exists(victim)) em.DestroyEntity(victim);
            em.DestroyEntity(bystander);

            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Death), 1,
                "죽음 seam 이 concrete 를 안 거쳤다");
            Assert.Greater(dealt, 0f,
                "구경꾼이 안 맞았다 — 폭발이 죽은 자리가 아니라 시전자 자리에서 터졌다(2칸은 반경 밖)");
        }

        private static DreamcatcherCard MakeCorpseBurstCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.OnKill },
                // 반경 1 이 이 그물의 기하 전제다 — 키우면 시전자 자리에서도 B 가 맞아
                // 단언이 자리를 못 가른다.
                payload = new DcPayloadSpec {
                    kind = DcPayloadKind.SelfTileAoe, magnitude = 50f, tileRange = 1,
                    projectile = FindAnyAoeProjectile(),
                },
            }};
            return card;
        }

        // `SelfTileAoe` 는 ProjectileData 가 없으면 폭발 요청이 통째로 드롭된다(피해까지).
        private static ProjectileData FindAnyAoeProjectile()
        {
            foreach (var p in Resources.FindObjectsOfTypeAll<ProjectileData>())
                if (p != null && p.id == "jjangssen_quake") return p;
            return Resources.FindObjectsOfTypeAll<ProjectileData>()[0];
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

        private static DreamcatcherCard MakeDevouringCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.OnKill },
                payload = new DcPayloadSpec { kind = DcPayloadKind.SelfStatBuff, buffStat = CardBuffKind.AttackSpeed, magnitude = 8f, duration = 4f },
            }};
            return card;
        }

        private static IEnumerator RunSeconds(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.deltaTime; yield return null; }
        }

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
