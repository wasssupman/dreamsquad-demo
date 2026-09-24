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

        [Test]
        public void 비행_적은_지상_경로를_걷는_순찰병을_때린다()
        {
            // 옛 전투의 적 공격은 통행 층을 거르지 않았다(`AttackState.targetTraversalLayers`
            // 미설정 = 0). 빌더가 「자기 통행 층」을 실어 드래곤(하늘 4)이 순찰병(지상|경로 3)을
            // 조준 후보에서 떨궜다.
            var patrol = Load<DefenderUnitData>(DefendersDir + "Defender_PatrolSoldier.asset");
            var flyer = Dummy();
            flyer.traversalLayers = PlacementLayer.Air;
            flyer.attackMethod = EnemyAttackMethod.Melee;
            flyer.attackRange = 1f;
            flyer.attackCooldown = 0.5f;
            flyer.outputs = new[] { new AttackOutput { kind = Wassup.Data.AttackOutputKind.Damage, magnitude = 10f } };
            try
            {
                var def = MatchDefinitionBuilder.Build(new[] { patrol }, new[] { flyer }, 1, ModeDef.Default());
                Assert.AreEqual(0, def.Enemies[0].Attack.TargetLayers,
                    "적의 공격 대상 층은 무필터(0)다 — 자기 통행 층이 아니다");

                var m = Run(def);
                m.Apply(Command.DebugSpawnDefender(0, new int2(6, 4)));
                m.Apply(Command.DebugSpawnEnemy(0, new int2(7, 4)));
                m.Tick();
                var soldier = Nth(m, UnitKind.Defender, 0);
                // 순찰병은 걷는 유닛이다 — 통행 층을 가진 이동 상태를 보장한다.
                if (soldier.Move == null) soldier.Move = new MoveState();
                soldier.Move.TraversalLayers = LayerBits.Path;
                soldier.Move.Speed = 0f;
                float before = soldier.Health;
                for (int t = 0; t < 120; t++) m.Tick();
                var foe = Nth(m, UnitKind.Enemy, 0);
                Assert.Less(soldier.Health, before, "비행 적이 옆의 순찰병을 때리지 않았다 — "
                    + $"soldier pos={soldier.Position} dead={soldier.Dead} dep={soldier.Deploying} lay={soldier.Move.TraversalLayers} role={def.Units[0].Role} | "
                    + $"foe pos={foe?.Position} range={foe?.Attack?.Range} mask={foe?.Attack?.TargetMask} tl={foe?.Attack?.TargetLayers} cm={foe?.Attack?.ClassMask} hcf={foe?.Attack?.HasClassFilter} un={foe?.Attack?.Unarmed} cd={foe?.Attack?.CooldownRemaining} out={foe?.Attack?.Outputs.Length}");
            }
            finally { Object.DestroyImmediate(flyer); }
        }

        // ── 빌더 의미(잠복 결함) ─────────────────────────────────────────────

        private static DefenderUnitData Target(string id = "drift_target")
        {
            var d = ScriptableObject.CreateInstance<DefenderUnitData>();
            d.id = id;
            d.health = 500f;
            d.attackRange = 0f;
            d.outputs = System.Array.Empty<AttackOutput>();
            return d;
        }

        private static AttackUnitData Hitter(EnemyAttackMethod method)
        {
            var e = Dummy();
            e.attackMethod = method;
            e.attackRange = 1f;
            e.attackCooldown = 0.5f;
            e.outputs = new[] { new AttackOutput { kind = Wassup.Data.AttackOutputKind.Damage, magnitude = 10f } };
            return e;
        }

        /// <summary>적 하나가 방어유닛 옆에 2초 서 있을 때 방어유닛이 받은 피해.</summary>
        private static float DamageTakenNextTo(DefenderUnitData target, AttackUnitData enemy)
        {
            var def = MatchDefinitionBuilder.Build(new[] { target }, new[] { enemy }, 1, ModeDef.Default());
            var m = Run(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(6, 4)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(7, 4)));
            m.Tick();
            var d = Nth(m, UnitKind.Defender, 0);
            float before = d.Health;
            for (int t = 0; t < 120; t++) m.Tick();
            return before - d.Health;
        }
    }
}
