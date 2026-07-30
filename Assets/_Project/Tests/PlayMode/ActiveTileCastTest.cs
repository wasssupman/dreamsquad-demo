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

namespace Wassup.Tests.PlayMode
{
    // active-dreamcatcher-tile-aim unit 3 — Active 의 아군 버프가 "타일 반경 내 아군 전부"로
    // 재정의된 것을 검증한다(구 CastSkillOnDefender = 타일의 유닛 1기).
    //
    // 기대값은 **실제 배치된 셀**에서 파생한다 — PlaceFirstValid 의 스캔 순서에 의존하지 않고
    // 체비셰프 거리로 판정하므로 맵/배치 가능 영역이 바뀌어도 유효하다.
    //
    // 캐스트는 `if (!_running)` 게이트 뒤에 있어 bridge.StartBattle() 이 필수다(레포 관례).
    public class ActiveTileCastTest
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
        public IEnumerator AllyBuff_AppliesToEveryAllyInRange_AndCountMatches()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var cat = FindCatalog();

            var a = cat.ById("ranger");
            var b = cat.ById("scout");
            var far = cat.ById("guardian");

            bridge.SetDefenderPool(new[] { a, b, far });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            // 반경 1 안에 2기가 들어가야 "전부 버프"를 검증할 수 있다 — 스캔 순서에 맡기지 않고
            // 첫 유닛의 **이웃 8칸**에 둘째를 놓는다. (배치 가능 셀마다 이웃 9칸을 재검사하는
            // 전면 탐색은 CanPlaceDefenderAt 가 경로 검증을 도는 비용 때문에 한 프레임을 잡아먹는다.)
            Assert.IsTrue(PlaceFirstValid(bridge, a), "place ranger");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var center = CellOf(bridge, em, "ranger");
            Assert.IsTrue(PlaceAdjacentTo(bridge, b, center), "place scout adjacent to ranger");

            // 반경 밖(체비셰프 > 1) 대조군. 맵이 좁아 못 찾으면 그 자체가 테스트 전제 붕괴다.
            Assert.IsTrue(PlaceFirstValidFarFrom(bridge, far, center, 2), "place a defender outside radius 1");
            bridge.StartBattle();
            yield return null;

            // 속사(AttackSpeedMul)로 검증한다 — 공격폭증의 DamageMul 은 **인접 시너지**가 같은
            // stat 을 쓰는 채널이라(stackId 만 다름) 절대값이 배치 인접성에 따라 흔들린다.
            // attackSpeedMul 은 다른 writer 가 없어 기본 1.0 이 보장된다(PlacementAuraTest 선례).
            var skill = MakeSkill(SkillEffectType.RapidFire, magnitude: 2f, durationSec: 6f, range: 1f);

            // 예고(조준 UI)와 실제 적용이 같은 판정을 쓰는지 — 계약 4.
            int expected = CountAlliesWithin(bridge, em, center, 1);
            Assert.AreEqual(expected, bridge.CountDefendersInRange(center, skill),
                "CountDefendersInRange = 반경 내 실제 아군 수");
            Assert.GreaterOrEqual(expected, 2, "반경 1 안에 아군이 2기 이상 있는 배치여야 의미가 있다");

            Assert.IsTrue(bridge.CastSkillAtTile(skill, center, out int affected), "타일 캐스트 성공");
            Assert.AreEqual(expected, affected, "affectedCount = 반경 내 아군 수 (1기가 아니다)");
            for (int i = 0; i < 3; i++) yield return null;

            foreach (var (id, cell) in AllDefenderCells(bridge, em))
            {
                bool inRange = Chebyshev(cell, center) <= 1;
                float mul = GetStat(bridge, em, id).attackSpeedMul;
                if (inRange) Assert.AreEqual(2f, mul, 0.01f, $"{id}@{cell} 반경 내 → 공속 x2");
                else Assert.AreEqual(1f, mul, 0.01f, $"{id}@{cell} 반경 밖 → 불변");
            }
        }

        [UnityTest]
        public IEnumerator AllyBuff_NoAllyInRange_RejectsWithoutSpend_ButEnemyFieldStillCasts()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = FindCatalog();
            var only = cat.ById("ranger");

            bridge.SetDefenderPool(new[] { only });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, only), "place ranger");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var occupied = CellOf(bridge, em, "ranger");
            bridge.StartBattle();
            yield return null;

            // 아군에게서 충분히 먼 타일(반경 1 로 아무도 못 닿는 곳).
            var empty = new Vector2Int(occupied.x + 6, occupied.y + 6);
            Assert.AreEqual(0, bridge.CountDefendersInRange(empty,
                MakeSkill(SkillEffectType.RapidFire, 2f, 6f, 1f)), "예고도 0기");

            Assert.IsFalse(
                bridge.CastSkillAtTile(MakeSkill(SkillEffectType.RapidFire, 2f, 6f, 1f), empty, out int affected),
                "아군 버프는 대상 0기면 실패 — 호출자가 차감/순환을 하지 않는다 (계약 5)");
            Assert.AreEqual(0, affected, "affected 0");

            // 대칭 확인: 적 대상 장판은 아무도 안 맞아도 성공이다(빈 곳 선점이 전술).
            Assert.IsTrue(
                bridge.CastSkillAtTile(MakeSkill(SkillEffectType.SlowField, 0.6f, 5f, 2f), empty, out _),
                "적 장판은 0기여도 성공");
        }

        // rev(ECS 리뷰 H2, 사용자 결정 2026-07-30) — 스킬 아군 버프는 배치 오라와 **합산**된다.
        // 구현 전에는 둘이 stackId 0 을 공유해 나중 값이 앞 값을 덮었다(가디언 오라 ×1.3 이
        // 지불한 공격폭증 ×2.0 을 내려버림). 절대값이 아니라 **증분 +1.0(= ×2.0 의 Additive 표현)**
        // 을 재는 이유: 오라 기여가 이미 깔린 상태에서 스킬이 그 위에 얹히는지가 검증 대상이다.
        [UnityTest]
        public IEnumerator AllyBuff_StacksOnTopOfPlacementAura_NotReplacing()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = FindCatalog();
            var guardian = cat.ById("guardian"); // 배치 시 주변 공격력 오라(DamageMul, on-place 슬롯)
            var ranger = cat.ById("ranger");

            bridge.SetDefenderPool(new[] { guardian, ranger });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var center = CellOf(bridge, em, "guardian");
            Assert.IsTrue(PlaceAdjacentTo(bridge, ranger, center), "place ranger adjacent");
            bridge.StartBattle();
            for (int i = 0; i < 3; i++) yield return null;

            float beforeRanger = GetStat(bridge, em, "ranger").damageMul;
            float beforeGuardian = GetStat(bridge, em, "guardian").damageMul;

            var surge = MakeSkill(SkillEffectType.PowerSurge, magnitude: 2f, durationSec: 8f, range: 1f);
            Assert.IsTrue(bridge.CastSkillAtTile(surge, center, out int affected), "공격폭증 시전");
            Assert.GreaterOrEqual(affected, 2, "가디언 + 레인저");
            for (int i = 0; i < 3; i++) yield return null;

            Assert.AreEqual(1f, GetStat(bridge, em, "ranger").damageMul - beforeRanger, 0.01f,
                "레인저: 오라 기여를 덮지 않고 +100%p 가 얹힌다");
            Assert.AreEqual(1f, GetStat(bridge, em, "guardian").damageMul - beforeGuardian, 0.01f,
                "가디언: 자기 오라 위에도 +100%p 가 얹힌다");
        }

        // rev(리뷰 H2) — 입구 == 출구 포탈은 창구에서 거절한다. MovementSystem 의 포탈 스냅이
        // flow step 앞에 돌아서 같은 타일 링크는 반경 안 적을 매 프레임 되돌리는 정지 필드가 된다.
        [UnityTest]
        public IEnumerator Portal_SameEntryAndExit_Rejected()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            bridge.BeginPlacement();
            bridge.StartBattle();
            yield return null;

            var portal = MakeSkill(SkillEffectType.Portal, magnitude: 1f, durationSec: 8f, range: 0f);
            var tile = new Vector2Int(3, 3);

            Assert.IsFalse(bridge.CastPortal(portal, tile, tile, out _), "퇴화 링크(입구==출구) 거절");
            Assert.IsTrue(bridge.CastPortal(portal, tile, new Vector2Int(6, 3), out _), "서로 다른 타일은 성립");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static SkillData MakeSkill(SkillEffectType effect, float magnitude, float durationSec, float range)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.id = $"test_{effect}";
            s.effect = effect;
            s.magnitude = magnitude;
            s.durationSec = durationSec;
            s.range = range;
            s.cooldownSec = 0f;
            s.cost = 0;
            return s;
        }

        private static int Chebyshev(Vector2Int a, Vector2Int b)
            => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        private static int CountAlliesWithin(BattleBridge bridge, EntityManager em, Vector2Int center, int tiles)
        {
            int n = 0;
            foreach (var (_, cell) in AllDefenderCells(bridge, em))
                if (Chebyshev(cell, center) <= tiles) n++;
            return n;
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

        // center 의 이웃 8칸 중 처음 배치 가능한 곳(= 체비셰프 1). 호출 8회 상한.
        private static bool PlaceAdjacentTo(BattleBridge bridge, DefenderUnitData u, Vector2Int center)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int x = center.x + dx, y = center.y + dy;
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
                }
            return false;
        }

        private static bool PlaceFirstValidFarFrom(BattleBridge bridge, DefenderUnitData u,
            Vector2Int center, int minDist)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                {
                    if (Chebyshev(new Vector2Int(x, y), center) < minDist) continue;
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
                }
            return false;
        }

        private static ModifierStats GetStat(BattleBridge bridge, EntityManager em, string id)
        {
            foreach (var (defId, cell) in AllDefenderCells(bridge, em))
            {
                if (defId != id) continue;
                if (bridge.TryGetDefenderAt(cell, out var e) && em.HasComponent<ModifierStats>(e))
                    return em.GetComponentData<ModifierStats>(e);
            }
            return default;
        }

        // _defenderByTile: cell → (entity, DefenderUnitData). 배치 결과를 셀과 함께 읽는다
        // (테스트 관례: 이 dict 가 bridge 의 배치 사실이다).
        private static IEnumerable<(string id, Vector2Int cell)> AllDefenderCells(
            BattleBridge bridge, EntityManager em)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            var found = new List<(string, Vector2Int)>();
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var val = de.Value;
                var t = val.GetType();
                var entity = (Entity)t.GetField("Item1").GetValue(val);
                var data = (DefenderUnitData)t.GetField("Item2").GetValue(val);
                if (entity == Entity.Null || !em.Exists(entity) || data == null) continue;
                found.Add((data.id, (Vector2Int)de.Key));
            }
            return found;
        }

        private static Vector2Int CellOf(BattleBridge bridge, EntityManager em, string id)
        {
            foreach (var (defId, cell) in AllDefenderCells(bridge, em))
                if (defId == id) return cell;
            Assert.Fail($"defender '{id}' not placed");
            return default;
        }
    }
}
