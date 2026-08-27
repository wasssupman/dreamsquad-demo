using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // subconscious-curse-expansion unit 0 — 호접몽(DreamCocoon) lifecycle 검증.
    // ① 무피격 완주 → self 영구 버프(DamageMul) + 코쿤/잠 소멸
    // ② 잠 중 피격 → wake-on-hit → 꿈 파탄(버프 없음) + 코쿤 소멸
    // ③ 이중 부착 → 사전검증 거절(기존 코쿤 무변화, 무차감)
    public class DreamCocoonTest
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
        public IEnumerator Completion_GrantsPermanentBuff()
        {
            yield return Setup();

            float dmgMulBefore = _em.GetComponentData<ModifierStats>(_defender).damageMul;
            var card = MakeCocoonCard(durationSec: 0.4f, percent: 35f);
            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();
            int handle = _bridge.ApplyDreamcatcherCardToUnit(_defender, card);
            Assert.AreEqual(0, handle, "attach 성공(엔티티 부착형 = 무회수 핸들 0)");
            // ⚠ 아래 둘은 **`yield` 없이** 참이어야 한다 — 부착 seam 이 브리지 콜스택
            // 안에서 끝난다는 것이 그 계약이다(skill-layer-migration unit 4b).
            // 그리고 **둘이 같이** 참이어야 한다: 잠만 붙으면 손해만 보는 카드고,
            // 감시만 붙으면 공짜 버프다.
            Assert.IsTrue(_em.HasComponent<DreamCocoon>(_defender), "코쿤 부여");
            Assert.IsTrue(HasCc(_em, _defender, CcKind.Sleep), "Sleep 부여");
            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Immediate), 1,
                "부착 seam 이 concrete 를 안 거쳤다 — 위 둘은 legacy arm 이 붙여도 참이다");

            yield return WaitSeconds(1.2f); // 잠 0.4s 완주 + modifier 적용 여유

            Assert.IsFalse(_em.HasComponent<DreamCocoon>(_defender), "완주 후 코쿤 소멸");
            Assert.IsFalse(HasCc(_em, _defender, CcKind.Sleep), "잠 자연 만료");
            float dmgMulAfter = _em.GetComponentData<ModifierStats>(_defender).damageMul;
            Assert.Greater(dmgMulAfter, dmgMulBefore + 0.30f, "완주 버프 +35% (Additive 버킷) 활성");
            Object.Destroy(card);
        }

        [UnityTest]
        public IEnumerator HitDuringSleep_BreaksDream_NoBuff()
        {
            yield return Setup();

            float dmgMulBefore = _em.GetComponentData<ModifierStats>(_defender).damageMul;
            var card = MakeCocoonCard(durationSec: 5f, percent: 35f);
            Assert.AreEqual(0, _bridge.ApplyDreamcatcherCardToUnit(_defender, card), "attach 성공");
            Assert.IsTrue(_em.HasComponent<DreamCocoon>(_defender), "코쿤 부여");

            // 비치명 피격 — DamageApplication → CcClear(wake) → DreamCocoonSystem 파탄.
            _em.GetBuffer<IncomingDamage>(_defender).Add(new IncomingDamage { amount = 1f });
            for (int i = 0; i < 6; i++) yield return null;

            Assert.IsFalse(HasCc(_em, _defender, CcKind.Sleep), "wake-on-hit: 잠 해제");
            Assert.IsFalse(_em.HasComponent<DreamCocoon>(_defender), "꿈 파탄 — 코쿤 소멸");

            yield return WaitSeconds(0.3f); // 파탄 후에도 버프가 늦게 오지 않음을 확인
            float dmgMulAfter = _em.GetComponentData<ModifierStats>(_defender).damageMul;
            Assert.AreEqual(dmgMulBefore, dmgMulAfter, 0.001f, "버프 없음");
            Object.Destroy(card);
        }

        [UnityTest]
        public IEnumerator DoubleAttach_RejectedWithoutPartialWrites()
        {
            yield return Setup();

            var card = MakeCocoonCard(durationSec: 30f, percent: 35f);
            Assert.AreEqual(0, _bridge.ApplyDreamcatcherCardToUnit(_defender, card), "첫 부착 성공");
            ushort firstStackId = _em.GetComponentData<DreamCocoon>(_defender).stackId;

            var card2 = MakeCocoonCard(durationSec: 30f, percent: 35f);
            // 문구 정본은 BattleBridge.Dreamcatcher.cs 의 DuplicateState 가드 —
            // «already has {payload.kind} state» 형식 (fast-lane unit 2 에서 동기).
            LogAssert.Expect(LogType.Warning,
                "[BattleBridge] ApplyDreamcatcherCardToUnit('cocoon_test'): target already has DreamCocoon state — card not attached.");
            int handle = _bridge.ApplyDreamcatcherCardToUnit(_defender, card2);

            Assert.AreEqual(-1, handle, "이중 부착 거절(무차감 규약)");
            Assert.IsTrue(_em.HasComponent<DreamCocoon>(_defender), "기존 코쿤 유지");
            Assert.AreEqual(firstStackId, _em.GetComponentData<DreamCocoon>(_defender).stackId,
                "기존 코쿤 무변화(타이머/버프 리셋 없음)");
            Object.Destroy(card);
            Object.Destroy(card2);
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

        private static IEnumerator WaitSeconds(float seconds)
        {
            float deadline = Time.time + seconds;
            while (Time.time < deadline) yield return null;
        }

        private static DreamcatcherCard MakeCocoonCard(float durationSec, float percent)
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = "cocoon_test";
            card.type = CardType.Unit;
            card.axis = CardTargetAxis.All;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.None },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.DreamCocoon,
                        magnitude = percent,
                        duration = durationSec,
                        buffStat = CardBuffKind.AttackDamage,
                    },
                },
            };
            return card;
        }

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
