using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Effects;
using Wassup.Battle.Combat;

namespace Wassup.Tests.PlayMode
{
    // dreamcatcher-placement-aura unit 4 — 스폰 오라("느린 각성"): host 부착 후 host·기존
    // 유닛엔 미부여, axis 매칭 신규 배치 유닛에만 부여, host 회수 시 전 수혜 유닛 원복.
    // BattleBridge 직접 구동(EffectTest 패턴). 회수는 컨트롤러가 호출하는 경로
    // (RevokeDreamcatcherEffects)를 직접 호출해 브릿지 계약을 검증한다.
    public class PlacementAuraTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        private static void NeutralizeSceneController()
        {
            var dc = Object.FindObjectOfType<DreamcatcherController>();
            if (dc != null) Object.Destroy(dc.gameObject);
        }

        [UnityTest]
        public IEnumerator Aura_GrantsToNewPlacementsOnly_AndRevokesOnHostRevoke()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            NeutralizeSceneController();
            var cat = FindCatalog();

            var host = cat.ById("fire_caster");   // 오라 host (자신은 미부여)
            var pre = cat.ById("ranger");          // 부착 전 배치 (미부여)
            var future = cat.ById("scout");        // 부착 후 배치 (부여 대상)

            bridge.SetDefenderPool(new[] { host, pre, future });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, host), "place host");
            Assert.IsTrue(PlaceFirstValid(bridge, pre), "place pre-existing");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            var hostEntity = GetEntity(bridge, em, "fire_caster");
            Assert.AreNotEqual(Entity.Null, hostEntity, "host entity resolved");

            int handle = bridge.ApplyDreamcatcherCardToUnit(hostEntity, MakeAuraCard(CardTargetAxis.All, 50f, 2f));
            Assert.Greater(handle, 0, "aura returns a revocable handle (>0)");
            for (int i = 0; i < 3; i++) yield return null;

            // host·기존 유닛 미부여
            Assert.AreEqual(1.0f, GetStat(bridge, em, "fire_caster").attackSpeedMul, 0.01f, "host 자신 미부여");
            Assert.AreEqual(1.0f, GetStat(bridge, em, "ranger").attackSpeedMul, 0.01f, "부착 전 배치 유닛 미부여");

            // 신규 배치 → 부여 (+ warmup)
            Assert.IsTrue(PlaceFirstValid(bridge, future), "place future unit");
            float cd = GetCooldown(bridge, em, "scout");
            Assert.Greater(cd, 1.5f, "신규 배치 유닛 warmup(~2s) 적용");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1.5f, GetStat(bridge, em, "scout").attackSpeedMul, 0.01f, "신규 배치 유닛 공속 +50%");

            // host 회수 → 전 수혜 유닛 원복
            bridge.RevokeDreamcatcherEffects(handle);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1.0f, GetStat(bridge, em, "scout").attackSpeedMul, 0.01f, "host 회수 시 수혜 유닛 원복");
        }

        [UnityTest]
        public IEnumerator Aura_RespectsAxis()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            NeutralizeSceneController();
            var cat = FindCatalog();

            var host = cat.ById("fire_caster");
            var ranger = cat.ById("ranger");     // 신규·비매칭(Guardian 오라)
            var guardian = cat.ById("guardian"); // 신규·매칭

            bridge.SetDefenderPool(new[] { host, ranger, guardian });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, host), "place host");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var hostEntity = GetEntity(bridge, em, "fire_caster");

            // Guardian 축 전용 오라
            int handle = bridge.ApplyDreamcatcherCardToUnit(hostEntity, MakeAuraCard(CardTargetAxis.ClassGuardian, 50f, 2f));
            Assert.Greater(handle, 0, "aura handle");

            Assert.IsTrue(PlaceFirstValid(bridge, ranger), "place new ranger");
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place new guardian");
            for (int i = 0; i < 3; i++) yield return null;

            Assert.AreEqual(1.0f, GetStat(bridge, em, "ranger").attackSpeedMul, 0.01f, "비매칭(ranger) 미부여");
            Assert.AreEqual(1.5f, GetStat(bridge, em, "guardian").attackSpeedMul, 0.01f, "매칭(guardian) 부여");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static DreamcatcherCard MakeAuraCard(CardTargetAxis axis, float asPct, float warmupSec)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.axis = axis;
            c.type = CardType.Unit;
            c.binding = CardBinding.Unit;
            c.mechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.None, period = 0 },
                    payload = new DcPayloadSpec { kind = DcPayloadKind.PlacementAura, magnitude = asPct, duration = warmupSec },
                }
            };
            return c;
        }

        private static DefenderCatalog FindCatalog()
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

        private static ModifierStats GetStat(BattleBridge bridge, EntityManager em, string id)
        {
            var e = GetEntity(bridge, em, id);
            if (e != Entity.Null && em.HasComponent<ModifierStats>(e)) return em.GetComponentData<ModifierStats>(e);
            return default;
        }

        private static float GetCooldown(BattleBridge bridge, EntityManager em, string id)
        {
            var e = GetEntity(bridge, em, id);
            if (e != Entity.Null && em.HasComponent<AttackState>(e)) return em.GetComponentData<AttackState>(e).cooldownRemaining;
            return -1f;
        }

        private static Entity GetEntity(BattleBridge bridge, EntityManager em, string id)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var val = de.Value;
                var t = val.GetType();
                var entity = (Entity)t.GetField("Item1").GetValue(val);
                var data = (DefenderUnitData)t.GetField("Item2").GetValue(val);
                if (data.id == id && em.Exists(entity)) return entity;
            }
            return Entity.Null;
        }
    }
}
