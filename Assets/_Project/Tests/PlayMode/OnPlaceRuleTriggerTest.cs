using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;
using Wassup.Battle.Combat;

namespace Wassup.Tests.PlayMode
{
    // on-place-skill-rework unit 0 — 배치 스킬이 적/보스와 **같은 트리거×페이로드 배관** 위에
    // 앉았는가.
    //
    // 회귀 대상 둘:
    //  ① **배치 확정 지점이 셋이다**(D&D `TriggerDeploymentOnPlaceSkill` · 탭
    //     `TriggerOnPlaceAndSynergy` · 재배치가 재호출하는 `ActivateDeployedDefender`).
    //     기존 on-place PlayMode 테스트는 전부 탭 경로라, 한 곳만 후킹하면 **라이브에서
    //     안 나가는 채로 초록**이 난다. 그래서 두 경로를 각각 검사한다.
    //  ② **`PatternSlot[0]` 은 다연발 것이어야 한다.** `AttackSystem` 이 slots[0] 하나만 읽고
    //     index 0 을 누가 갖느냐는 bake 호출 순서로만 정해진다 — 순서가 뒤집히면 머신거너가
    //     자기 다연발 대신 배치 스킬 패턴을 쏜다.
    public class OnPlaceRuleTriggerTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        // ── 발화 관측 방법 ────────────────────────────────────────────────────
        // unit 0 이 소유하는 계약은 **「배치 사건이 슬롯을 발화시켜 payload 디스패치까지
        // 도달한다」** 까지다. 그래서 관측도 거기서 끊는다 — arm 이 없는 payload 를 주고
        // 시스템이 내는 loud 경고("slot fired with unhandled payload kind")를 잰다.
        //
        // 왜 실제 payload 효과로 재지 않는가: 그러면 남의 arm 이 그 host 진영에서 옳은지까지
        // 이 테스트가 책임지게 되고, 실패했을 때 원인이 «내 트리거» 인지 «그 arm» 인지 갈리지
        // 않는다(초판이 AllyMoveSpeedAura 로 재다가 정확히 그 모호함에 걸렸다). 실제 효과는
        // 자기 arm 을 갖는 unit 2(캐논 패턴)·unit 4(AreaTaunt)가 각자 검증한다.

        // 탭 배치(PlaceDefenderAs) — 기존 on-place 테스트가 전부 쓰는 경로.
        [UnityTest]
        public IEnumerator TapPlacement_FiresOnPlaceRule()
        {
            yield return LoadBattle();
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var caster = MakeArmlessRuleUnit("test_onplace_rule_tap");
            Prepare(bridge, gm, caster);
            var cell = FindPlaceableCells(bridge, caster, 1, minGap: 1)[0];

            LogAssert.Expect(LogType.Warning, new Regex("slot fired with unhandled payload"));
            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, caster), "배치");
            yield return Frames(8);
            Object.Destroy(caster);
        }

        // D&D 배치(TryBeginDefenderDeployment → ActivateDeployedDefender). 재배치도 같은 함수를
        // 재호출하므로 이 핀이 재무장 경로까지 덮는다.
        [UnityTest]
        public IEnumerator DragDropPlacement_FiresOnPlaceRule()
        {
            yield return LoadBattle();
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var caster = MakeArmlessRuleUnit("test_onplace_rule_dnd");
            Prepare(bridge, gm, caster);
            var cell = FindPlaceableCells(bridge, caster, 1, minGap: 1)[0];

            LogAssert.Expect(LogType.Warning, new Regex("slot fired with unhandled payload"));
            Assert.IsTrue(bridge.TryBeginDefenderDeployment(cell.x, cell.y, caster, out var placed),
                "pending 배치 시작");
            yield return Frames(2);
            bridge.ActivateDeployedDefender(cell, placed);
            yield return Frames(8);
            Object.Destroy(caster);
        }

        // 반증 대조군 — 규칙이 **없는** 유닛은 슬롯도 태그도 얻지 못한다.
        //
        // 이게 없으면 위 두 핀이 새는 통과를 낼 수 있다: 슬롯/태그가 유닛과 무관하게 항상
        // 붙는 구현이어도 전부 초록이 된다. 로그가 아니라 **상태**로 재는 이유는
        // `LogAssert.ignoreFailingMessages = true`(전투 씬이 경고를 쏟는다) 하에서 음성
        // 로그 단언이 신뢰할 수 없기 때문이다.
        [UnityTest]
        public IEnumerator UnitWithoutRule_GetsNoSlotAndNoTag()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var plain = MakePlainUnit("test_onplace_rule_control");
            Prepare(bridge, gm, plain);
            var cell = FindPlaceableCells(bridge, plain, 1, minGap: 1)[0];

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, plain), "배치");
            var e = FindDefenderAt(bridge, cell);
            Assert.AreNotEqual(Entity.Null, e, "엔티티");

            bool tagged = em.HasComponent<JustDeployed>(e);
            int onPlaceSlots = 0;
            if (em.HasBuffer<DcTriggerSlot>(e))
            {
                var slots = em.GetBuffer<DcTriggerSlot>(e);
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i].trigger == DcTriggerKind.OnPlace) onPlaceSlots++;
            }
            yield return Frames(4);
            Object.Destroy(plain);

            Assert.AreEqual(0, onPlaceSlots, "규칙 없는 유닛에 OnPlace 슬롯이 생기면 안 된다");
            Assert.IsFalse(tagged,
                "규칙 없는 유닛에 JustDeployed 가 붙으면 안 된다 — 태그는 DcTriggerSlot 보유 " +
                "유닛에만 붙어야 하고(시스템 미실행 시 누수 방지), 위 발화 핀의 반증 대조군이다.");
        }

        // 태그는 1프레임이다 — 남으면 다음 배치 사건과 섞인다.
        [UnityTest]
        public IEnumerator JustDeployed_IsConsumedWithinOneSimTick()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var caster = MakeArmlessRuleUnit("test_onplace_rule_tag_life");
            Prepare(bridge, gm, caster);
            var cell = FindPlaceableCells(bridge, caster, 1, minGap: 1)[0];

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, caster), "배치");
            var e = FindDefenderAt(bridge, cell);
            Assert.AreNotEqual(Entity.Null, e, "엔티티");

            // 경계 ①: 규칙이 실제로 슬롯으로 구워졌는가(DcTrigger.HasDetector 통과 + bake 호출).
            Assert.IsTrue(em.HasBuffer<DcTriggerSlot>(e), "DcTriggerSlot 버퍼");
            var slots = em.GetBuffer<DcTriggerSlot>(e);
            int onPlaceSlots = 0;
            string dump = "";
            for (int i = 0; i < slots.Length; i++)
            {
                dump += $"[{i}] trigger={slots[i].trigger} payload={slots[i].payload} mag={slots[i].magnitude} dur={slots[i].duration} range={slots[i].tileRange}; ";
                if (slots[i].trigger == DcTriggerKind.OnPlace) onPlaceSlots++;
            }
            Assert.AreEqual(1, onPlaceSlots, $"OnPlace 슬롯이 1개 구워져야 한다. 실제 슬롯: {dump}");

            // 경계 ②: 배치 사건 태그.
            Assert.IsTrue(em.HasComponent<JustDeployed>(e), "배치 직후엔 태그가 붙어 있어야 한다");

            yield return Frames(6);
            bool stillTagged = em.HasComponent<JustDeployed>(e);
            Object.Destroy(caster);

            Assert.IsFalse(stillTagged,
                "JustDeployed 가 소비되지 않았다. 남으면 다음 프레임에도 OnPlace 슬롯이 계속 발화한다.");
        }

        // bake 호출 순서 핀. 다연발 유닛에 배치 규칙을 얹어도 PatternSlot[0] 은 다연발 것이다.
        [UnityTest]
        public IEnumerator DirectionalVolley_KeepsPatternSlotZero()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("machine_gunner"));
            unit.id = "test_onplace_rule_slot_order";
            unit.cost = 0;
            unit.maxOnBoard = 100;
            var volley = unit.GetAbility<DirectionalVolleyAbility>();
            Assert.IsNotNull(volley, "머신거너는 다연발 능력을 가진다");
            Assert.IsNotNull(volley.pattern, "다연발 패턴");
            int volleyShots = volley.pattern.shots != null ? volley.pattern.shots.Length : 0;

            // 배치 규칙도 패턴을 쓰게 해 두 슬롯이 같은 버퍼에 공존하게 만든다.
            var rule = ScriptableObject.CreateInstance<UnitSkillAbility>();
            rule.id = "test_rule_pattern";
            rule.mechanics = new[] { MakeMechanic(DcPayloadKind.EmitProjectilePattern, pattern: MakeOneShotPattern(volley.pattern.barrel)) };
            unit.abilities = new System.Collections.Generic.List<DefenderAbilityData>(unit.abilities) { rule };

            Prepare(bridge, gm, unit);
            var cell = FindPlaceableCells(bridge, unit, 1, minGap: 1)[0];
            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, unit), "배치");
            yield return Frames(4);

            var e = FindDefenderAt(bridge, cell);
            Assert.AreNotEqual(Entity.Null, e, "엔티티");
            var slots = em.GetBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(e);
            int slot0Shots = slots.Length > 0 ? slots[0].spec.shots.Length : -1;
            Object.Destroy(unit);

            Assert.AreEqual(volleyShots, slot0Shots,
                "PatternSlot[0] 이 다연발 패턴이 아니다. BakeUnitMechanics 가 " +
                "BakeDefenderDirectionalPattern 보다 먼저 불리면 머신거너가 배치 스킬을 쏜다.");
        }

        // skill-layer-migration unit 2g — **「레거시 enum 과 규칙이 동시에 돈다」 테스트가
        // 은퇴했다.** 그 상태를 만들 방법이 없어졌다 — 레거시 필드군 자체가 철거돼
        // 「동시에 선언」이 표현 불가능하다. 경고도 함께 은퇴했다.
        //
        // 이 자리에 남는 계약은 위 테스트들이다: 규칙 하나가 배치 순간에 한 번 발화하고,
        // 재배치하면 재무장한다.

        // ── helpers ──────────────────────────────────────────────────────────

        private static IEnumerator LoadBattle()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        // 배치 스킬도 능력도 없는 순수 대상. 오라 버프의 수신자로만 쓴다.
        private static DefenderUnitData MakePlainUnit(string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("guardian"));
            unit.id = testId;
            unit.cost = 0;
            unit.maxOnBoard = 100;
            unit.abilities = new System.Collections.Generic.List<DefenderAbilityData>();
            return unit;
        }

        // OnPlace × (arm 없는 payload) — 발화 자체만 관측하는 도구. `NextAttackDoubleFire` 는
        // bake 를 통과하지만 BossPeriodicTriggerSystem 에 arm 이 없어 loud 경고로 떨어진다.
        // 그 경고가 곧 "슬롯이 발화해 payload 디스패치에 도달했다"는 신호다.
        private static DefenderUnitData MakeArmlessRuleUnit(string testId)
        {
            var unit = MakePlainUnit(testId);
            var ability = ScriptableObject.CreateInstance<UnitSkillAbility>();
            ability.id = "test_rule_armless";
            ability.mechanics = new[] { MakeMechanic(DcPayloadKind.NextAttackDoubleFire) };
            unit.abilities = new System.Collections.Generic.List<DefenderAbilityData> { ability };
            return unit;
        }

        private static DcMechanic MakeMechanic(DcPayloadKind kind, ProjectilePatternData pattern = null)
        {
            var m = new DcMechanic();
            m.trigger.kind = DcTriggerKind.OnPlace;
            m.payload.kind = kind;
            m.payload.magnitude = 50f;  // +50% 이동속도
            m.payload.duration = 5f;
            m.payload.tileRange = 3;
            m.payload.pattern = pattern;
            return m;
        }

        private static ProjectilePatternData MakeOneShotPattern(ProjectileData barrel)
        {
            var p = ScriptableObject.CreateInstance<ProjectilePatternData>();
            p.id = "test_pattern_one_shot";
            p.barrel = barrel;
            p.damage = 1f;
            p.selection = PatternSelectionRule.RoundRobin;
            p.shots = new[] { new ProjectileShotStep { directionT = 0f, intervalAfterPreviousSec = 0f } };
            p.telegraphSec = 0.1f;
            return p;
        }

        private static void Prepare(BattleBridge bridge, GameManager gm, params DefenderUnitData[] units)
        {
            bridge.SetDefenderPool(units);
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
        }

        private static Entity FindDefenderAt(BattleBridge bridge, Vector2Int cell)
            => bridge.TryGetDefenderAt(cell, out Entity e) ? e : Entity.Null;

        private static System.Collections.Generic.List<Vector2Int> FindPlaceableCells(
            BattleBridge bridge, DefenderUnitData u, int count, int minGap)
        {
            var result = new System.Collections.Generic.List<Vector2Int>();
            for (int x = -24; x < 48 && result.Count < count; x++)
                for (int y = -24; y < 48 && result.Count < count; y++)
                {
                    if (!bridge.CanPlaceDefenderAt(x, y, u, out _)) continue;
                    bool tooClose = false;
                    for (int i = 0; i < result.Count; i++)
                        if (Mathf.Max(Mathf.Abs(result[i].x - x), Mathf.Abs(result[i].y - y)) < minGap)
                            tooClose = true;
                    if (tooClose) continue;
                    result.Add(new Vector2Int(x, y));
                }
            return result;
        }
    }
}
