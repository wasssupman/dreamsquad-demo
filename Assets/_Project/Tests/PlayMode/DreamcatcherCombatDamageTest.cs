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
    // dreamcatcher effect verification — deterministic combat integration. A melee
    // defender attacks a stationary high-HP dummy enemy in range; we measure damage
    // dealt over a fixed window with and without a damageMul buff. Proves the buff
    // actually increases damage in real combat (not just the ModifierStats value).
    public class DreamcatcherCombatDamageTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator DamageMulBuff_IncreasesDealtDamage()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindDefenderCatalog();
            Assert.IsNotNull(cat, "DefenderCatalog loaded");
            var guardian = cat.ById("guardian"); // melee (no projectile) → direct IncomingDamage

            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender entity resolved");

            // Stationary dummy enemy at the defender's cell (always in range), huge HP.
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            const float EnemyHp = 1_000_000f;
            var enemy = em.CreateEntity();
            em.AddComponentData(enemy, LocalTransform.FromPosition(defPos + new float3(0.05f, 0f, 0f)));
            em.AddComponentData(enemy, new Health { value = EnemyHp, max = EnemyHp });
            em.AddComponentData(enemy, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(enemy);

            // Let any temporary on-place buff (guardian gets one) decay so the
            // multiplier is stable before we measure.
            yield return RunSeconds(8f);
            float preMul = em.GetComponentData<ModifierStats>(defender).damageMul;

            // Window 1 — baseline at the stable multiplier. Long window so cooldown
            // -phase noise (±1 attack) doesn't dominate the ratio.
            em.SetComponentData(enemy, new Health { value = EnemyHp, max = EnemyHp });
            yield return RunSeconds(6f);
            float baseDealt = EnemyHp - em.GetComponentData<Health>(enemy).value;

            // Apply a +200% damage buff via the real dreamcatcher path. modifier-additive-
            // authoring: this increase authors as an additive +2.0 delta on the damageMul
            // bucket. Baseline here is 1.0 (on-place decayed, solo guardian → no synergy),
            // so 1.0 + 2.0 == 3.0 — identical to the old ×3 for a single card on baseline.
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.ClassGuardian;             // guardian = ClassGuardian
            card.effects = new[] { new CardEffect { kind = CardBuffKind.AttackDamage, percent = 200f } }; // +200% → +2.0 additive
            bridge.ApplyDreamcatcherCardHosted(card);
            for (int i = 0; i < 3; i++) yield return null; // let ModifierApply/Aggregate run
            float buffedMul = em.GetComponentData<ModifierStats>(defender).damageMul;

            // Window 2 — buffed (same duration). Reset HP so only post-buff damage counts.
            em.SetComponentData(enemy, new Health { value = EnemyHp, max = EnemyHp });
            yield return RunSeconds(6f);
            float buffedDealt = EnemyHp - em.GetComponentData<Health>(enemy).value;

            if (em.Exists(enemy)) em.DestroyEntity(enemy);

            Assert.Greater(baseDealt, 0f, "baseline defender dealt some damage");
            // +2.0 additive delta lifts the multiplier well above baseline.
            Assert.Greater(buffedMul, preMul * 2.5f, $"damageMul buff raised the multiplier ({preMul:0.00}->{buffedMul:0.00})");
            // Damage scales with damageMul (~3x). Lenient threshold absorbs ±1 attack
            // of cooldown-phase noise across the 6s windows.
            Assert.Greater(buffedDealt, baseDealt * 2f,
                $"buffed dealt {buffedDealt} should exceed 2x baseline {baseDealt} (mul {preMul:0.00}->{buffedMul:0.00})");
        }

        // dreamcatcher-new-abilities unit 3 — shatter_hymn: CC 걸린 적에게 DamageVsCc
        // 배율이 실제 전투 데미지를 올린다. 멜리 guardian(직접 IncomingDamage 경로)이
        // Stun(CcEffect) 걸린 더미를 때린다 — 같은 창을 shatter 유무로 비교.
        [UnityTest]
        public IEnumerator DamageVsCc_BoostsDamage_AgainstCcdEnemy()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindDefenderCatalog();
            var guardian = cat.ById("guardian"); // melee → 직접 데미지 경로(멜리 vsCc 분기)

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

            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            const float EnemyHp = 1_000_000f;
            var enemy = em.CreateEntity();
            em.AddComponentData(enemy, LocalTransform.FromPosition(defPos + new float3(0.05f, 0f, 0f)));
            em.AddComponentData(enemy, new Health { value = EnemyHp, max = EnemyHp });
            em.AddComponentData(enemy, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(enemy);
            // CC 상태 세팅: 긴 Stun(창 내내 활성). AnyActiveCc(remaining>0) 게이트 만족.
            var cc = em.AddBuffer<CcEffect>(enemy);
            cc.Add(new CcEffect { kind = CcKind.Stun, remainingTime = 1000f });

            yield return RunSeconds(8f); // on-place 버프 감쇠

            em.SetComponentData(enemy, new Health { value = EnemyHp, max = EnemyHp });
            yield return RunSeconds(6f);
            float baseDealt = EnemyHp - em.GetComponentData<Health>(enemy).value;

            // shatter_hymn: CC 걸린 적 대상 +200% (DamageVsCc). 축=ClassGuardian.
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.ClassGuardian;
            card.effects = new[] { new CardEffect { kind = CardBuffKind.DamageVsCc, percent = 200f } };
            bridge.ApplyDreamcatcherCardHosted(card);
            for (int i = 0; i < 3; i++) yield return null;

            em.SetComponentData(enemy, new Health { value = EnemyHp, max = EnemyHp });
            yield return RunSeconds(6f);
            float boostedDealt = EnemyHp - em.GetComponentData<Health>(enemy).value;

            if (em.Exists(enemy)) em.DestroyEntity(enemy);

            Assert.Greater(baseDealt, 0f, "baseline(무 shatter)도 CC 적에게 정상 데미지 — 무적 회귀 없음");
            Assert.Greater(boostedDealt, baseDealt * 2f,
                $"shatter 로 CC 적 데미지 급증 예상: boosted {boostedDealt} > 2x base {baseDealt}");
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
            // map-diorama-stage US-004b — 이격(스트라이드 3) 배치: 열린 마당에서 연속 셀 배치가
            // 인접 오라류 효과(OnPlace 등)를 서로에게 옮겨 고정값 계측을 오염시켰다(프로브 실증).
            // 구 복도 맵의 «배치지가 흩어져 있던» 기하 의미론을 테스트 쪽에서 복원한다.
            for (int x = -24; x < 48; x += 3)
                for (int y = -24; y < 48; y += 3)
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
