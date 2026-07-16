using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Battle.Effects;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // subconscious-curse-expansion unit 1 — 몽마의 계약(유출 허용치 선불) lifecycle.
    // ① 지불 + hosted 버프 활성 → host 회수(revoke) 시 버프만 중립화, 허용치 비가역
    // ② 지불 바닥(잔여 1) 아래로 거절 → 매치 리셋 시 오프셋 소멸 + SO 불변 검증
    //    (BeginPlacement 후 잔여가 원값 복귀 = deck.defeatGoalReachedCount 무변형 증명)
    public class IncubusPactTest
    {
        private BattleBridge _bridge;
        private EntityManager _em;
        private Entity _defender;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PayApplyRevoke_AllowanceIrreversible()
        {
            yield return Setup();

            int before = _bridge.RemainingLeakAllowance();
            Assert.Greater(before, 1, "기본 허용치에 게이트 여지(≥2) 필요");

            float dmgBefore = _em.GetComponentData<ModifierStats>(_defender).damageMul;
            var card = MakePactCard();
            int handle = _bridge.ApplyDreamcatcherCardHosted(card);
            Assert.Greater(handle, 0, "hosted 부착 = 회수 핸들(>0)");
            Assert.IsTrue(_bridge.TryPayLeakAllowance(card.leakAllowanceCost), "선불 지불 성공");
            Assert.AreEqual(before - 1, _bridge.RemainingLeakAllowance(), "허용치 −1");

            for (int i = 0; i < 4; i++) yield return null; // modifier 적용 프레임
            float dmgDuring = _em.GetComponentData<ModifierStats>(_defender).damageMul;
            Assert.Greater(dmgDuring, dmgBefore + 0.20f, "전군 공격력 +25% 활성");

            _bridge.RevokeDreamcatcherEffects(handle); // host 사망 경로와 동일한 회수
            for (int i = 0; i < 4; i++) yield return null;
            float dmgAfter = _em.GetComponentData<ModifierStats>(_defender).damageMul;
            Assert.AreEqual(dmgBefore, dmgAfter, 0.01f, "revoke = 버프 중립화");
            Assert.AreEqual(before - 1, _bridge.RemainingLeakAllowance(), "허용치는 비가역(환불 없음)");
            Object.Destroy(card);
        }

        [UnityTest]
        public IEnumerator PayFloor_AndMatchReset_LeavesSoUntouched()
        {
            yield return Setup();

            int before = _bridge.RemainingLeakAllowance();
            Assert.Greater(before, 1, "기본 허용치에 게이트 여지(≥2) 필요");

            // 바닥(잔여 1)까지 지불 — 그 아래로는 거절(즉시 패배 금지).
            int paid = 0;
            while (_bridge.TryPayLeakAllowance(1)) paid++;
            Assert.AreEqual(before - 1, paid, "잔여 1 까지만 지불 가능");
            Assert.AreEqual(1, _bridge.RemainingLeakAllowance(), "바닥 1 유지");
            Assert.IsFalse(_bridge.TryPayLeakAllowance(1), "바닥 아래 지불 거절");

            // 매치 리셋 → 오프셋 소멸. 잔여가 원값으로 복귀한다는 것이 곧
            // deck.defeatGoalReachedCount(SO)가 변형되지 않았다는 증명이다.
            _bridge.BeginPlacement();
            yield return null;
            Assert.AreEqual(before, _bridge.RemainingLeakAllowance(),
                "리셋 후 원값 복귀 — 이전 매치 지불 이월 없음 + SO 불변");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            _bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindCatalog();
            Assert.IsNotNull(_bridge, "BattleBridge present");
            Assert.IsNotNull(gm, "GameManager present");
            Assert.IsNotNull(cat, "DefenderCatalog present");

            var guardian = cat.ById("guardian");
            _bridge.SetDefenderPool(new[] { guardian });
            _bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(_bridge, guardian), "place guardian");
            yield return null;

            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
            _defender = FindDefender(_bridge, _em);
            Assert.AreNotEqual(Entity.Null, _defender, "defender resolved");
        }

        private static DreamcatcherCard MakePactCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = "pact_test";
            card.type = CardType.Squad;
            card.axis = CardTargetAxis.All;
            card.effects = new[] { new CardEffect { kind = CardBuffKind.AttackDamage, percent = 25f } };
            card.mechanics = new DcMechanic[0];
            card.attackMods = new DcAttackModSpec[0];
            card.leakAllowanceCost = 1;
            return card;
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

        private static Entity FindDefender(BattleBridge bridge, EntityManager em)
        {
            var field = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var defenders = (System.Collections.IDictionary)field.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry entry in defenders)
            {
                var value = entry.Value;
                var entity = (Entity)value.GetType().GetField("Item1").GetValue(value);
                if (em.Exists(entity)) return entity;
            }
            return Entity.Null;
        }
    }
}
