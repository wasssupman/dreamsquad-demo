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
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // combat-action-lock unit 5 — 상태 lifecycle 검증(CC 채널·merge·decay). defender 에
    // EffectSpawner.ApplyCc 로 상태 주입 + IncomingDamage 로 피격 구동.
    // (공격/이동 GATE 동작은 코드리뷰 + placement-aura Sleep 경로로 커버; 적 이동정지
    //  PlayMode 는 wave 하네스 필요 → follow-up.)
    public class ActionLockTest
    {
        [TearDown] public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Sleep_WakesOnHit_Stun_Persists_AndCoexist()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();            var cat = FindCatalog();
            var a = cat.ById("guardian"); // Sleep → 피격 시 해제
            var b = cat.ById("ranger");   // Sleep+Stun → 피격 시 Sleep 만 해제

            bridge.SetDefenderPool(new[] { a, b });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart(); gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, a), "place guardian");
            Assert.IsTrue(PlaceFirstValid(bridge, b), "place ranger");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var ea = GetEntity(bridge, em, "guardian");
            var eb = GetEntity(bridge, em, "ranger");

            // 상태 주입: guardian=Sleep, ranger=Sleep+Stun (긴 지속으로 decay 무관하게)
            EffectSpawner.ApplyCc(em, ea, new CcEffect { kind = CcKind.Sleep, remainingTime = 100f });
            EffectSpawner.ApplyCc(em, eb, new CcEffect { kind = CcKind.Sleep, remainingTime = 100f });
            EffectSpawner.ApplyCc(em, eb, new CcEffect { kind = CcKind.Stun, remainingTime = 100f });
            Assert.IsTrue(HasCc(em, ea, CcKind.Sleep), "guardian Sleep 부여됨");
            Assert.IsTrue(HasCc(em, eb, CcKind.Sleep) && HasCc(em, eb, CcKind.Stun), "ranger Sleep+Stun 공존");

            // 피격(비치명) — DamageApplicationSystem → CcClearSystem 이 Sleep 만 제거.
            em.GetBuffer<IncomingDamage>(ea).Add(new IncomingDamage { amount = 1f });
            em.GetBuffer<IncomingDamage>(eb).Add(new IncomingDamage { amount = 1f });
            for (int i = 0; i < 4; i++) yield return null;

            Assert.IsFalse(HasCc(em, ea, CcKind.Sleep), "wake-on-hit: guardian Sleep 해제");
            Assert.IsFalse(HasCc(em, eb, CcKind.Sleep), "ranger Sleep 해제(피격)");
            Assert.IsTrue(HasCc(em, eb, CcKind.Stun), "Stun 은 피격에도 유지(no-wake)");
        }

        [UnityTest]
        public IEnumerator InfiniteSleep_Persists()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();            var cat = FindCatalog();
            var g = cat.ById("guardian");
            bridge.SetDefenderPool(new[] { g });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart(); gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, g), "place guardian");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var e = GetEntity(bridge, em, "guardian");

            EffectSpawner.ApplyCc(em, e, new CcEffect { kind = CcKind.Sleep, remainingTime = float.PositiveInfinity });
            for (int i = 0; i < 12; i++) yield return null; // CcDecay 여러 프레임 — +∞ 는 만료 안 됨
            Assert.IsTrue(HasCc(em, e, CcKind.Sleep), "무한 Sleep 은 decay 로 만료되지 않음");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static bool HasCc(EntityManager em, Entity e, CcKind kind)
        {
            if (e == Entity.Null || !em.HasBuffer<CcEffect>(e)) return false;
            var buf = em.GetBuffer<CcEffect>(e);
            for (int i = 0; i < buf.Length; i++) if (buf[i].kind == kind) return true;
            return false;
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

        private static Entity GetEntity(BattleBridge bridge, EntityManager em, string id)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var val = de.Value; var t = val.GetType();
                var entity = (Entity)t.GetField("Item1").GetValue(val);
                var data = (DefenderUnitData)t.GetField("Item2").GetValue(val);
                if (data.id == id && em.Exists(entity)) return entity;
            }
            return Entity.Null;
        }
    }
}
