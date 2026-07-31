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
    // effect-tile-icons unit 1 — 버프 3종(공격력·공속·재생) 타일이 **저작값 그대로** 배치
    // 유닛에 붙는지. 기존 EffectTileModifierTests 는 이벤트 shape 을 손으로 재현하므로
    // "어느 에셋이 어느 스탯을 주는가"(EffectTileData.effects → StatKind/op/magnitude)는
    // 아무도 잡고 있지 않았다 — 시트/에셋에서 stat 을 잘못 고쳐도 전부 green 이었다.
    // 여기서는 실제 SO → BattleBridge.AddEffectTile → ModifierStats 까지 통과시킨다.
    //
    // 부착 **전 baseline 대비 델타**로 본다: 유닛 고유 on-place 버프가 같은 stat 을
    // 건드려도(현재는 없지만) 대조가 깨지지 않는다.
    public class EffectTileBuffApplyTest
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
        public IEnumerator BuffTiles_ApplyAuthoredStatsToOccupant()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var cat = FindCatalog();
            Assert.IsNotNull(cat, "DefenderCatalog loaded");

            var units = new[] { cat.ById("ranger"), cat.ById("scout"), cat.ById("guardian") };
            bridge.SetDefenderPool(units);
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            // 공격력 +25% (DamageMul ×1.25)
            yield return AssertTileEffect(bridge, em, units[0], "effect_tile_damage",
                s => s.damageMul, 1.25f, isMultiplier: true);

            // 공속 +20% (AttackSpeedMul ×1.2)
            yield return AssertTileEffect(bridge, em, units[1], "effect_tile_attack_speed",
                s => s.attackSpeedMul, 1.2f, isMultiplier: true);

            // 재생 +1 HP/s (RegenPerSec 는 base 0 이라 Additive 저작 — 곱이면 영영 0)
            yield return AssertTileEffect(bridge, em, units[2], "effect_tile_regen",
                s => s.regenPerSec, 1f, isMultiplier: false);
        }

        // 효과 타일이 없는 빈 셀에 유닛을 놓고 baseline 을 읽은 뒤, 그 셀에 타일을 붙여
        // 델타를 확인한다. AddEffectTile 은 점유 셀이면 즉시 적용한다(순서 무관 불변식).
        private static IEnumerator AssertTileEffect(BattleBridge bridge, EntityManager em,
            DefenderUnitData unit, string tileId,
            System.Func<ModifierStats, float> read, float authored, bool isMultiplier)
        {
            var data = FindEffectTile(tileId);
            Assert.IsNotNull(data, $"'{tileId}' 에셋이 로드돼 있어야 한다(테마 풀 소속).");

            Assert.IsTrue(PlaceOnTileFreeCell(bridge, unit, out var cell),
                $"효과 타일 없는 셀에 '{unit.id}' 배치");
            for (int i = 0; i < 3; i++) yield return null;

            var entity = EntityAt(bridge, cell);
            Assert.AreNotEqual(Entity.Null, entity, "배치 엔티티 해석");
            Assert.IsTrue(em.HasComponent<ModifierStats>(entity), "ModifierStats 보유");
            float before = read(em.GetComponentData<ModifierStats>(entity));

            bridge.AddEffectTile(cell, data);
            for (int i = 0; i < 3; i++) yield return null;

            float after = read(em.GetComponentData<ModifierStats>(entity));
            float expected = isMultiplier ? before * authored : before + authored;
            Assert.AreEqual(expected, after, 1e-3f,
                $"'{tileId}' 부착 후 기대값(before={before}, authored={authored}, " +
                $"{(isMultiplier ? "곱" : "합")})");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static EffectTileData FindEffectTile(string id)
        {
            foreach (var d in Resources.FindObjectsOfTypeAll<EffectTileData>())
                if (d != null && d.id == id) return d;
            return null;
        }

        // 맵 빌드가 이미 효과 타일을 깐 셀은 피한다 — 그 셀은 배치 시점에 이미 다른 stat 을
        // 붙여 baseline 대조가 오염된다.
        private static bool PlaceOnTileFreeCell(BattleBridge bridge, DefenderUnitData u, out Vector2Int cell)
        {
            var taken = EffectTileCells(bridge);
            for (int x = -24; x < 48; x++)
            for (int y = -24; y < 48; y++)
            {
                var c = new Vector2Int(x, y);
                if (taken.Contains(c)) continue;
                if (!bridge.CanPlaceDefenderAt(x, y, u, out _)) continue;
                cell = c;
                return bridge.PlaceDefenderAs(x, y, u);
            }
            cell = default;
            return false;
        }

        private static HashSet<Vector2Int> EffectTileCells(BattleBridge bridge)
        {
            var result = new HashSet<Vector2Int>();
            var f = typeof(BattleBridge).GetField("_effectTilesByCell",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return result;
            if (f.GetValue(bridge) is IDictionary<Vector2Int, EffectTileData> dict)
                foreach (var kv in dict) result.Add(kv.Key);
            return result;
        }

        private static Entity EntityAt(BattleBridge bridge, Vector2Int cell)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            if (!dict.Contains(cell)) return Entity.Null;
            var val = dict[cell];
            return (Entity)val.GetType().GetField("Item1").GetValue(val);
        }
    }
}
