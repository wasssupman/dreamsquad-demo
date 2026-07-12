using System.Collections;
using System.Collections.Generic;
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

        [UnityTest]
        public IEnumerator Aura_GrantsToNewPlacementsOnly_AndRevokesOnHostRevoke()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");            var cat = FindCatalog();

            var host = cat.ById("fire_caster");   // 오라 host (자신은 미부여)
            var pre = cat.ById("ranger");          // 부착 전 배치 (미부여)
            var future = cat.ById("scout");        // 부착 후 배치 (부여 대상)
            var afterRevoke = cat.ById("guardian"); // 회수 후 배치 (미부여 확인)

            bridge.SetDefenderPool(new[] { host, pre, future, afterRevoke });
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
            // warmup→Sleep 승격(combat-action-lock unit 4): 신규 배치 유닛에 Sleep 상태 부여.
            Assert.IsTrue(HasCc(bridge, em, "scout", CcKind.Sleep), "신규 배치 유닛에 Sleep 부여(warmup 대체)");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1.5f, GetStat(bridge, em, "scout").attackSpeedMul, 0.01f, "신규 배치 유닛 공속 +50%");

            // host 회수 → 전 수혜 유닛 원복
            bridge.RevokeDreamcatcherEffects(handle);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1.0f, GetStat(bridge, em, "scout").attackSpeedMul, 0.01f, "host 회수 시 수혜 유닛 원복");

            // review test-gap — 회수 후 신규 배치는 상속 중단(레지스트리 제거 확인)
            Assert.IsTrue(PlaceFirstValid(bridge, afterRevoke), "place post-revoke unit");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1.0f, GetStat(bridge, em, "guardian").attackSpeedMul, 0.01f, "회수 후 신규 배치 미부여");
        }

        [UnityTest]
        public IEnumerator Aura_RespectsAxis()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();            var cat = FindCatalog();

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

        // 컨트롤러 배선 통합(review M2): 실제 CommitUnit → _attachedTo(handle 저장) →
        // OnDefenderDied(handle>0 라우팅) → RevokeDreamcatcherEffects. 물리적 사망 발화
        // (DrainDefenderDeathEvents → DefenderDied)는 기존/Squad unit 9 공유 경로라 여기선
        // 컨트롤러 핸들러를 직접 구동해 신규 plumbing(핸들 저장·라우팅)만 검증한다.
        [UnityTest]
        public IEnumerator Aura_RevokedWhenHostDies_ViaController()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();            var cat = FindCatalog();
            var host = cat.ById("fire_caster");
            var future = cat.ById("scout");

            bridge.SetDefenderPool(new[] { host, future });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, host), "place host");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var hostEntity = GetEntity(bridge, em, "fire_caster");

            // 실제 컨트롤러 구성(비활성 → 필드 주입 → 활성). Placement 재진입은 유발하지 않는다
            // (OnPhaseChanged 가 _deck 를 재빌드하므로). gauge 0 >= costUnit 0 로 사용 가능.
            var cfg = ScriptableObject.CreateInstance<AwakeningConfig>();
            cfg.costUnit = 0; cfg.handSize = 5; cfg.maxAttachPerUnit = 3;
            var deck = new DreamcatcherCycleDeck(new List<DreamcatcherCard> { MakeAuraCard(CardTargetAxis.All, 50f, 2f) }, 0);

            var go = new GameObject("HandController_Test");
            go.SetActive(false);
            var ctrl = go.AddComponent<DreamcatcherHandController>();
            SetField(ctrl, "bridge", bridge);
            SetField(ctrl, "config", cfg);
            SetField(ctrl, "_deck", deck);
            go.SetActive(true);

            int entryId = deck.Hand(5)[0].entryId;
            Assert.IsTrue(ctrl.CommitUnit(entryId, hostEntity), "CommitUnit(오라, host) 성공");
            Assert.Greater(GetAttachedHandle(ctrl, entryId), 0, "회수핸들(>0)이 _attachedTo 에 저장됨");

            Assert.IsTrue(PlaceFirstValid(bridge, future), "place future unit");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1.5f, GetStat(bridge, em, "scout").attackSpeedMul, 0.01f, "CommitUnit 경로 오라가 신규 배치 부여");

            // host 사망 → 컨트롤러가 handle>0 을 revoke 로 라우팅.
            InvokeOnDefenderDied(ctrl, hostEntity, host);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1.0f, GetStat(bridge, em, "scout").attackSpeedMul, 0.01f, "host 사망 시 컨트롤러 revoke → 원복");

            Object.Destroy(go);
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static void SetField(object obj, string name, object value)
        {
            obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
        }

        private static int GetAttachedHandle(DreamcatcherHandController ctrl, int entryId)
        {
            var f = typeof(DreamcatcherHandController).GetField("_attachedTo", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(ctrl);
            if (!dict.Contains(entryId)) return 0;
            var val = dict[entryId];
            return (int)val.GetType().GetField("Item2").GetValue(val);
        }

        private static void InvokeOnDefenderDied(DreamcatcherHandController ctrl, Entity host, DefenderUnitData data)
        {
            var m = typeof(DreamcatcherHandController).GetMethod("OnDefenderDied", BindingFlags.NonPublic | BindingFlags.Instance);
            m.Invoke(ctrl, new object[] { host, data });
        }

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

        private static bool HasCc(BattleBridge bridge, EntityManager em, string id, CcKind kind)
        {
            var e = GetEntity(bridge, em, id);
            if (e == Entity.Null || !em.HasBuffer<CcEffect>(e)) return false;
            var buf = em.GetBuffer<CcEffect>(e);
            for (int i = 0; i < buf.Length; i++) if (buf[i].kind == kind) return true;
            return false;
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
