using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Effects;

namespace Wassup.Tests.PlayMode
{
    // dreamcatcher-attach-requirement units 1·10 — Unit/Squad 부착 제한 게이트의 e2e.
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
            var scout = cat.ById("scout");
            var fighter = cat.ById("bruiser");
            Assert.IsNotNull(guardian, "guardian defender data");
            Assert.IsNotNull(ranger, "ranger defender data");
            Assert.IsNotNull(scout, "second ranger defender data");
            Assert.IsNotNull(fighter, "fighter defender data");
            Assert.AreEqual(DefenderClass.Guardian, guardian.role, "guardian role 전제");
            Assert.AreEqual(DefenderClass.Ranger, ranger.role, "ranger role 전제");
            Assert.AreEqual(DefenderClass.Ranger, scout.role, "scout role 전제");
            Assert.AreEqual(DefenderClass.Fighter, fighter.role, "bruiser role 전제");

            bridge.SetDefenderPool(new[] { guardian, ranger, scout, fighter });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            Assert.IsTrue(PlaceFirstValid(bridge, ranger), "place ranger");
            Assert.IsTrue(PlaceFirstValid(bridge, scout), "place second ranger");
            Assert.IsTrue(PlaceFirstValid(bridge, fighter), "place fighter");
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var gHost = FindDefenderById(bridge, em, "guardian");
            var rHost = FindDefenderById(bridge, em, "ranger");
            var sHost = FindDefenderById(bridge, em, "scout");
            var fHost = FindDefenderById(bridge, em, "bruiser");
            Assert.AreNotEqual(Entity.Null, gHost, "guardian host resolved");
            Assert.AreNotEqual(Entity.Null, rHost, "ranger host resolved");
            Assert.AreNotEqual(Entity.Null, sHost, "second ranger host resolved");
            Assert.AreNotEqual(Entity.Null, fHost, "fighter host resolved");

            var classCard = RequireCard(DcAttachType.Class, "Guardian");
            var idCard = RequireCard(DcAttachType.UnitId, "guardian");
            var wrongIdCard = RequireCard(DcAttachType.UnitId, "ranger");
            var invalidCard = RequireCard(DcAttachType.Class, "");
            var freeCard = RequireCard(DcAttachType.None);
            var rangerSquadCard = RequireSquadCard(DcAttachType.Class, "Ranger");
            var freeSquadCard = RequireSquadCard(DcAttachType.None);

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
            Assert.IsTrue(bridge.WouldDreamcatcherCardApply(rHost, rangerSquadCard),
                "Ranger 제한 Squad는 Ranger host에 valid");
            Assert.IsTrue(bridge.WouldDreamcatcherCardApply(sHost, rangerSquadCard),
                "Ranger 제한 Squad는 다른 Ranger host에도 valid");
            Assert.IsFalse(bridge.WouldDreamcatcherCardApply(fHost, rangerSquadCard),
                "Ranger 제한 Squad는 Fighter host에 invalid");
            Assert.IsTrue(bridge.WouldDreamcatcherCardApply(fHost, freeSquadCard),
                "제한 없는 Squad는 Fighter host에도 기존대로 valid");

            // ── ② 커밋 반환 규약 — 거절이 먼저, 통과가 나중(쓰기 격리) ────────────
            Assert.AreEqual(-1, bridge.ApplyDreamcatcherCardToUnit(rHost, classCard),
                "제한 불일치 = -1 (무차감·카드 잔류)");
            Assert.AreEqual(-1, bridge.ApplyDreamcatcherCardToUnit(gHost, wrongIdCard),
                "다른 유닛 id 요구 = -1");
            Assert.AreEqual(-1, bridge.ApplyDreamcatcherCardToUnit(gHost, invalidCard),
                "무효 설정 = -1 (조용히 풀리지 않는다)");
            Assert.AreEqual(-1, bridge.ApplyDreamcatcherCard(fHost, rangerSquadCard),
                "Ranger 제한 Squad를 Fighter에 부착하면 -1");

            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardToUnit(gHost, classCard), 0,
                "가디언 전용 카드가 가디언에 부착");
            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardToUnit(gHost, idCard), 0,
                "guardian id 전용 카드가 가디언에 부착");
            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardToUnit(rHost, freeCard), 0,
                "무제한 카드는 레인저에도 부착 (무회귀)");
            Assert.Greater(bridge.ApplyDreamcatcherCard(rHost, rangerSquadCard), 0,
                "Ranger 제한 Squad가 Ranger host에 부착");
            yield return null; yield return null; yield return null;
            Assert.AreEqual(1.1f, em.GetComponentData<ModifierStats>(rHost).attackSpeedMul, 0.01f,
                "부착 Ranger가 클래스 버프 수혜");
            Assert.AreEqual(1.1f, em.GetComponentData<ModifierStats>(sHost).attackSpeedMul, 0.01f,
                "다른 현재 배치 Ranger도 클래스 버프 수혜");
            Assert.AreEqual(1f, em.GetComponentData<ModifierStats>(fHost).attackSpeedMul, 0.01f,
                "Fighter host 후보는 클래스 버프 비수혜");
            Assert.Greater(bridge.ApplyDreamcatcherCard(fHost, freeSquadCard), 0,
                "제한 없는 Squad는 Fighter host에도 부착 성공 (무회귀)");
        }

        // review §5 — 위 테스트는 반환값 -1 만 어서션하고 "무차감·카드 잔류"는 컨트롤러
        // 코드를 읽어 추론했을 뿐이라, 정작 이 feature 의 헤드라인 보장이 아무것도
        // 핀되지 않았다. 실제 CommitAttach 를 구동해 각성 코스트와 손패를 어서션한다.
        // (PlacementAuraTest 의 컨트롤러 구성 패턴 재사용 — 비활성 → 필드 주입 → 활성.)
        [UnityTest]
        public IEnumerator RejectedAttach_DoesNotSpendAwakening_AndKeepsCardInHand()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindDefenderCatalog();
            var guardian = cat.ById("guardian");
            var ranger = cat.ById("ranger");
            var fighter = cat.ById("bruiser");

            bridge.SetDefenderPool(new[] { guardian, ranger, fighter });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            Assert.IsTrue(PlaceFirstValid(bridge, ranger), "place ranger");
            Assert.IsTrue(PlaceFirstValid(bridge, fighter), "place fighter");
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var gHost = FindDefenderById(bridge, em, "guardian");
            var rHost = FindDefenderById(bridge, em, "ranger");
            var fHost = FindDefenderById(bridge, em, "bruiser");

            // 코스트가 0 이 아니어야 '무차감'이 의미를 갖는다.
            var cfg = ScriptableObject.CreateInstance<AwakeningConfig>();
            cfg.costUnit = 30; cfg.costSquad = 20;
            cfg.gaugeMax = 100; cfg.handSize = 5; cfg.maxAttachPerUnit = 3;
            var card = RequireCard(DcAttachType.Class, "Guardian");
            var squadCard = RequireSquadCard(DcAttachType.Class, "Ranger");
            var deck = new DreamcatcherCycleDeck(new List<DreamcatcherCard> { card, squadCard }, 0);

            var go = new GameObject("HandController_AttachRequirement");
            go.SetActive(false);
            var ctrl = go.AddComponent<DreamcatcherHandController>();
            SetPrivate(ctrl, "bridge", bridge);
            SetPrivate(ctrl, "config", cfg);
            SetPrivate(ctrl, "_deck", deck);
            go.SetActive(true);

            // 게이지는 프로덕션 경로(GainAwakening)로 채운다 — 백킹필드 직접 쓰기 회피.
            InvokePrivate(ctrl, "GainAwakening", 60, Vector3.zero, null);
            Assert.AreEqual(60, ctrl.Gauge, "테스트 전제: 게이지 60");

            int entryId = EntryIdOf(deck, cfg.handSize, card);
            int squadEntryId = EntryIdOf(deck, cfg.handSize, squadCard);
            Assert.IsTrue(ctrl.CanUse(entryId), "전제: 60 >= costUnit 30 이라 사용 가능");

            // ── 거절: 가디언 전용 카드를 레인저에 ─────────────────────────────
            Assert.IsFalse(ctrl.CommitAttach(entryId, rHost), "제한 불일치면 커밋 실패");
            Assert.AreEqual(60, ctrl.Gauge, "거절 시 각성 무차감 (이 feature 의 헤드라인 보장)");
            Assert.IsTrue(HandContains(ctrl, entryId), "거절 시 카드가 손패에 잔류");

            // ── 통과: 같은 카드를 가디언에 ────────────────────────────────────
            Assert.IsTrue(ctrl.CommitAttach(entryId, gHost), "제한 일치면 커밋 성공");
            Assert.AreEqual(30, ctrl.Gauge, "성공 시에는 정상 차감(60-30) — 무차감이 전면 무효가 아님을 대조");
            Assert.IsFalse(HandContains(ctrl, entryId), "성공 시 카드가 손패를 떠난다");

            // ── Squad 회귀: Fighter 거절도 무차감·잔류, Ranger 통과 시 costSquad 차감 ──
            Assert.IsTrue(ctrl.CanUse(squadEntryId), "전제: 30 >= costSquad 20");
            Assert.IsFalse(ctrl.CommitAttach(squadEntryId, fHost), "Ranger 제한 Squad는 Fighter에서 거절");
            Assert.AreEqual(30, ctrl.Gauge, "Squad 제한 거절 시 각성 무차감");
            Assert.IsTrue(HandContains(ctrl, squadEntryId), "Squad 제한 거절 시 카드 손패 잔류");

            Assert.IsTrue(ctrl.CommitAttach(squadEntryId, rHost), "Ranger 제한 Squad는 Ranger에 성공");
            Assert.AreEqual(10, ctrl.Gauge, "Squad 성공 시 costSquad 정상 차감");
            Assert.IsFalse(HandContains(ctrl, squadEntryId), "Squad 성공 시 카드가 손패를 떠난다");

            Object.Destroy(go);
        }

        private static bool HandContains(DreamcatcherHandController ctrl, int entryId)
        {
            foreach (var e in ctrl.Hand())
                if (e.entryId == entryId) return true;
            return false;
        }

        private static int EntryIdOf(DreamcatcherCycleDeck deck, int handSize, DreamcatcherCard card)
        {
            foreach (var e in deck.Hand(handSize))
                if (e.card == card) return e.entryId;
            return -1;
        }

        private static void SetPrivate(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);

        private static void InvokePrivate(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(target, args);

        // 제한 외 조건은 두 host 모두 통과하는 카드 — HeavyStrike 는 양수 Damage output 만
        // 요구하고 가디언·레인저 둘 다 보유하므로 제한이 유일한 변수가 된다.
        private static DreamcatcherCard RequireCard(DcAttachType type, string value = null)
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = $"test_require_{type}_{value}";
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.AttackN, period = 1 },
                payload = new DcPayloadSpec { kind = DcPayloadKind.HeavyStrike, magnitude = 2f },
            }};
            card.attachType = type;
            card.attachValue = value;
            return card;
        }

        private static DreamcatcherCard RequireSquadCard(DcAttachType type, string value = null)
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = $"test_squad_require_{type}_{value}";
            card.type = CardType.Squad;
            card.axis = CardTargetAxis.ClassRanger;
            card.effects = new[] {
                new CardEffect { kind = CardBuffKind.AttackSpeed, percent = 10f }
            };
            card.mechanics = new DcMechanic[0];
            card.attackMods = new DcAttackModSpec[0];
            card.attachType = type;
            card.attachValue = value;
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
