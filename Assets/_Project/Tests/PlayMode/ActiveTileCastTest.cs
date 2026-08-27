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

        // active-ally-zone unit 1 — 아군 버프 케이스 3건은 이 파일을 떠났다.
        // 즉시 버프가 **시간제 장판**으로 바뀌어(반경 내 전부 / 0기 거절 폐지 / 이탈·만료 소멸)
        // 전제가 통째로 달라졌고, 프레임 경과를 재야 한다 → `ActiveAllyZoneTest` 가 소유한다.
        // 이 파일에는 장판화와 무관하게 유효한 케이스만 남긴다.


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
            // ⚠ 조준 사양은 **저작 필드**다(unit 7e) — 라이브 에셋이 그렇게 저작돼 있고
            // (`ActiveSkillAimingTests` 가 그것을 못박는다) 런타임으로 만드는 카드도
            // 같아야 한다. 안 켜면 포탈이 조용히 한 칸 스킬이 된다.
            s.needsTwoTiles = effect == SkillEffectType.Portal;
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
