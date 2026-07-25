using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // dreamcatcher-attach-requirement unit 1 — 부착 제한(정적 술어) 게이트의 e2e.
    // 카드는 코드로 만든다(ScriptableObject.CreateInstance) — append 필드는 인스펙터로
    // 값을 넣는 순간 YAML 에 키가 기록되고 원복해도 남으므로, 에셋을 건드리지 않는 것이
    // 유일하게 깨끗한 검증이다(DreamcatcherCard.visible 선례 · orphan 키 정리 불가).
    //
    // 검증 축 둘: ① 커밋 반환 규약(제한 불일치 = -1 무차감 / 통과 = >=0)
    // ② UI 판정(WouldDreamcatcherCardApply)이 커밋과 같은 답 — 리티클 색과 커밋 일치.
    // 능력 게이트는 두 host 모두 통과하는 카드(HeavyStrike, 둘 다 데미지 output 보유)를
    // 써서 제한이 유일한 변수가 되게 한다.
    public class DreamcatcherAttachRequirementE2ETest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator AttachRequirement_GatesByClassAndUnitId()
        {
            LogAssert.ignoreFailingMessages = true; // 제한 거절 경고를 의도적으로 발생시킨다
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindDefenderCatalog();
            var guardian = cat.ById("guardian");
            var ranger = cat.ById("ranger");
            Assert.IsNotNull(guardian, "guardian defender data");
            Assert.IsNotNull(ranger, "ranger defender data");
            Assert.AreEqual(DefenderClass.Guardian, guardian.role, "guardian role 전제");
            Assert.AreEqual(DefenderClass.Ranger, ranger.role, "ranger role 전제");

            bridge.SetDefenderPool(new[] { guardian, ranger });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            Assert.IsTrue(PlaceFirstValid(bridge, ranger), "place ranger");
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var gHost = FindDefenderById(bridge, em, "guardian");
            var rHost = FindDefenderById(bridge, em, "ranger");
            Assert.AreNotEqual(Entity.Null, gHost, "guardian host resolved");
            Assert.AreNotEqual(Entity.Null, rHost, "ranger host resolved");

            var classCard = RequireCard(DcAttachRequireKind.Class, cls: DefenderClass.Guardian);
            var idCard = RequireCard(DcAttachRequireKind.UnitId, unitId: "guardian");
            var wrongIdCard = RequireCard(DcAttachRequireKind.UnitId, unitId: "ranger");
            var invalidCard = RequireCard(DcAttachRequireKind.Class, cls: DefenderClass.None);
            var freeCard = RequireCard(DcAttachRequireKind.None);

            // ── ① UI 판정 (읽기 전용 — 어떤 apply 보다 먼저 본다) ──────────────────
            Assert.IsTrue(bridge.WouldDreamcatcherCardApply(gHost, classCard),
                "가디언 전용 카드는 가디언에 valid");
            Assert.IsFalse(bridge.WouldDreamcatcherCardApply(rHost, classCard),
                "가디언 전용 카드는 레인저에 invalid (리티클 불가)");
            Assert.IsTrue(bridge.WouldDreamcatcherCardApply(gHost, idCard),
                "guardian id 전용 카드는 가디언에 valid");
            Assert.IsFalse(bridge.WouldDreamcatcherCardApply(gHost, wrongIdCard),
                "ranger id 전용 카드는 가디언에 invalid");
            Assert.IsFalse(bridge.WouldDreamcatcherCardApply(gHost, invalidCard),
                "무효 설정은 fail-closed — 어디에도 valid 아님");
            Assert.IsTrue(bridge.WouldDreamcatcherCardApply(rHost, freeCard),
                "무제한 카드는 기존대로 valid (무회귀)");

            // ── ② 커밋 반환 규약 — 거절이 먼저, 통과가 나중(쓰기 격리) ────────────
            Assert.AreEqual(-1, bridge.ApplyDreamcatcherCardToUnit(rHost, classCard),
                "제한 불일치 = -1 (무차감·카드 잔류)");
            Assert.AreEqual(-1, bridge.ApplyDreamcatcherCardToUnit(gHost, wrongIdCard),
                "다른 유닛 id 요구 = -1");
            Assert.AreEqual(-1, bridge.ApplyDreamcatcherCardToUnit(gHost, invalidCard),
                "무효 설정 = -1 (조용히 풀리지 않는다)");

            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardToUnit(gHost, classCard), 0,
                "가디언 전용 카드가 가디언에 부착");
            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardToUnit(gHost, idCard), 0,
                "guardian id 전용 카드가 가디언에 부착");
            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardToUnit(rHost, freeCard), 0,
                "무제한 카드는 레인저에도 부착 (무회귀)");
        }

        // 제한 외 조건은 두 host 모두 통과하는 카드 — HeavyStrike 는 양수 Damage output 만
        // 요구하고 가디언·레인저 둘 다 보유하므로 제한이 유일한 변수가 된다.
        private static DreamcatcherCard RequireCard(DcAttachRequireKind kind,
            DefenderClass cls = DefenderClass.None, string unitId = null)
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = $"test_require_{kind}_{cls}_{unitId}";
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.AttackN, period = 1 },
                payload = new DcPayloadSpec { kind = DcPayloadKind.HeavyStrike, magnitude = 2f },
            }};
            card.attachRequire = kind;
            card.attachRequireClass = cls;
            card.attachRequireUnitId = unitId;
            return card;
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

        // _defenderByTile 는 private — 테스트 전용 reflection 조회(GateE2E 전례).
        private static Entity FindDefenderById(BattleBridge bridge, EntityManager em, string id)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var val = de.Value;
                var entity = (Entity)val.GetType().GetField("Item1").GetValue(val);
                var data = (DefenderUnitData)val.GetType().GetField("Item2").GetValue(val);
                if (em.Exists(entity) && data != null && data.id == id) return entity;
            }
            return Entity.Null;
        }
    }
}
