using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;
using Wassup.BattleCoreUnity;
using UnityEngine.TestTools;
using Wassup.Data;
using Faction = Wassup.Battle.Units.Faction;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild 2026-09-24 드리프트 감사 — **빌더가 저작 의미를 옮기는가.**
    //
    // 코어 규칙은 맞는데 SO → 정의표 번역에서 한 칸이 빠져 라이브가 틀리는 결함들이다.
    // 코어 lane 은 `Wassup.Runtime` 을 못 불러 SO 를 이름조차 모르므로 여기(Assets lane)에 둔다.
    // 판정은 **빌더를 거친 정의표로 판을 돌려** 사용자 증상으로 묻는다 — 필드 값만 보면
    // 「빌더는 옳은데 코어가 다르게 읽는」 반대편 결함을 못 본다.
    //
    // ⚠ 실 카탈로그 SO 는 **읽기만** 한다(프로세스 전역 상태). 흔들 값이 필요하면
    // `ScriptableObject.CreateInstance` 로 만든 사본을 쓰고 끝나면 지운다.
    public class CoreBuilderDriftTests
    {
        private const string DefendersDir = "Assets/_Project/Data/Defenders/";

        /// <summary>적 칸을 채우는 무해한 사본(적이 주제가 아닌 테스트용). 테스트마다 새로 만든다.</summary>
        private AttackUnitData dummy;

        [SetUp] public void MakeDummy() => dummy = Dummy();
        [TearDown] public void DropDummy() { if (dummy != null) Object.DestroyImmediate(dummy); }

        private static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(a, $"{path} 가 없다");
            return a;
        }

        /// <summary>제자리에 서 있는 무해한 적. 코어 규칙만 보려고 이동·공격을 끈다.</summary>
        private static AttackUnitData Dummy(float health = 1000f)
        {
            var e = ScriptableObject.CreateInstance<AttackUnitData>();
            e.id = "drift_dummy";
            e.health = health;
            e.moveSpeed = 0f;
            e.outputs = System.Array.Empty<AttackOutput>();
            return e;
        }

        /// <summary>빌더가 만든 정의표에 **열린 판**을 끼운다(빌더 입력에 맵이 없어서).</summary>
        private static BattleMatch Run(MatchDefinition def)
        {
            var map = new MapSnapshot
            {
                Width = 16, Height = 8, TileSize = 1f,
                Goals = new[] { new int2(15, 4) },
                Spawns = new[] { new int2(0, 4) },
            };
            map.Normalize();
            def.Map = map;
            def.ConfigHash = def.ComputeConfigHash();
            var m = new BattleMatch(def);
            m.Begin();
            return m;
        }

        private static Unit Nth(BattleMatch m, UnitKind kind, int n)
        {
            var units = m.World.Units;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Kind == kind && n-- == 0) return units[i];
            return null;
        }

        // ── H4 · 힐러가 적을 회복시킨다 ─────────────────────────────────────

        [Test]
        public void 힐러는_다친_아군을_회복하고_적은_회복하지_않는다()
        {
            // 라이브 힐러 = `targetAllies: 1` + `targetFactions: 98`(적 전부). 옛 전투는
            // `DefenderTargetDefaults.Resolve` 에서 `targetAllies` 가 저작 마스크를 이겼다.
            var healer = Load<DefenderUnitData>(DefendersDir + "Defender_Healer.asset");
            {
                var def = MatchDefinitionBuilder.Build(new[] { healer }, new[] { dummy }, 1, ModeDef.Default());
                Assert.AreEqual((int)Faction.DefenderUnit, def.Units[0].TargetFactions,
                    "힐러의 대상 진영이 아군 단독이 아니다");
                Assert.AreEqual(0, def.Units[0].Attack.TargetLayers,
                    "아군 대상 유닛은 통행 층을 거르지 않는다(옛 targetTraversalLayers = 0)");

                var m = Run(def);
                m.Apply(Command.DebugSpawnDefender(0, new int2(6, 4)));
                m.Apply(Command.DebugSpawnDefender(0, new int2(8, 4)));
                m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 4)));
                m.Tick();

                var ally = Nth(m, UnitKind.Defender, 1);
                var enemy = Nth(m, UnitKind.Enemy, 0);
                ally.Health = ally.MaxHealth * 0.5f;
                enemy.Health = enemy.MaxHealth * 0.5f;
                float allyBefore = ally.Health, enemyBefore = enemy.Health;

                for (int t = 0; t < 60 * 8; t++) m.Tick();

                Assert.AreEqual(enemyBefore, enemy.Health, 1e-3f, "힐러가 적을 회복시켰다");
                Assert.Greater(ally.Health, allyBefore, "힐러가 다친 아군을 회복하지 않았다");
            }
        }

        // ── M7 · 비행 적이 순찰병을 못 때린다 ───────────────────────────────

    }
}
