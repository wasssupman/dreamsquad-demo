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

        [Test]
        public void 공격_방식_없음인_적은_걷기만_한다()
        {
            // 옛 `BattleBridge`: `attackMethod: None` 또는 산출물 없음 → 공격 상태 없이 굽는다.
            var target = Target();
            var walker = Hitter(EnemyAttackMethod.None);
            try { Assert.AreEqual(0f, DamageTakenNextTo(target, walker), 1e-3f, "걷기만 하는 적이 때렸다"); }
            finally { Object.DestroyImmediate(target); Object.DestroyImmediate(walker); }
        }

        [Test]
        public void 근접_적은_탄이_저작돼_있어도_탄을_쏘지_않는다()
        {
            // 옛: `attackMethod == Projectile && projectile != null` 일 때만 탄을 굽는다.
            var target = Target();
            var melee = Hitter(EnemyAttackMethod.Melee);
            var shot = ScriptableObject.CreateInstance<ProjectileData>();
            shot.id = "drift_shot";
            melee.projectile = shot;
            try
            {
                var def = MatchDefinitionBuilder.Build(new[] { target }, new[] { melee }, 1, ModeDef.Default());
                Assert.AreEqual(-1, def.Enemies[0].Attack.ProjectileDefIndex, "근접 적에 탄이 붙었다");
            }
            finally { Object.DestroyImmediate(target); Object.DestroyImmediate(melee); Object.DestroyImmediate(shot); }
        }

        [Test]
        public void 직업_필터를_전부_끈_적은_방어유닛을_못_때린다()
        {
            // 옛 `AttackSystem` 의 게이트는 필터의 **존재**였다 — 마스크 0 = 아무도 못 때린다.
            // 코어가 「0 = 제약 없음」으로 읽어 뜻이 뒤집혀 있었다.
            var target = Target();
            target.role = DefenderClass.Fighter;
            var picky = Hitter(EnemyAttackMethod.Melee);
            picky.targetClassMask = DefenderClassFlags.None;
            try { Assert.AreEqual(0f, DamageTakenNextTo(target, picky), 1e-3f, "직업 필터 0 인 적이 때렸다"); }
            finally { Object.DestroyImmediate(target); Object.DestroyImmediate(picky); }
        }

        [Test]
        public void 폭탄_비행_시간이_0_이면_폭탄맨이_아니다()
        {
            // 옛 `BattleBridge`: 게이트 = 능력 에셋 존재 **+ travelSec > 0**.
            var d = Target("drift_bomber");
            var bomb = ScriptableObject.CreateInstance<BombThrowAbility>();
            bomb.travelSec = 0f;
            bomb.damage = 50f;
            d.abilities.Add(bomb);
            try
            {
                var def = MatchDefinitionBuilder.Build(new[] { d }, new[] { dummy }, 1, ModeDef.Default());
                Assert.AreNotEqual((int)AttackPolicy.Bomb, def.Units[0].Attack.Policy,
                    "비행 시간 0 인 폭탄 능력이 폭탄맨 정책이 됐다");
            }
            finally { Object.DestroyImmediate(d); Object.DestroyImmediate(bomb); }
        }

        [Test]
        public void 착탄_효과가_광역이_아닌_탄은_광역_반경을_안_싣는다()
        {
            // 옛 `ProjectileHitSystem`: `onHitEffect == Splash && splashRadius > 0` 둘 다.
            var d = Target("drift_archer");
            var shot = ScriptableObject.CreateInstance<ProjectileData>();
            shot.id = "drift_arrow";
            shot.onHitEffect = OnHitEffectType.None;
            shot.splashRadius = 2f;
            d.projectile = shot;
            try
            {
                var def = MatchDefinitionBuilder.Build(new[] { d }, new[] { dummy }, 1, ModeDef.Default());
                Assert.AreEqual(0f, def.Projectiles[def.Units[0].Attack.ProjectileDefIndex].SplashRadius, 1e-6f,
                    "광역 토큰이 없는데 광역 반경이 실렸다");
            }
            finally { Object.DestroyImmediate(d); Object.DestroyImmediate(shot); }
        }

        private static (DefenderUnitData d, ProjectileData barrel, ProjectilePatternData pat, DirectionalVolleyAbility vol)
            Volley()
        {
            var d = Target("drift_gunner");
            var barrel = ScriptableObject.CreateInstance<ProjectileData>();
            barrel.id = "drift_bullet";
            barrel.flightMode = ProjectileFlightMode.Directional;
            d.projectile = barrel;
            var pat = ScriptableObject.CreateInstance<ProjectilePatternData>();
            pat.id = "drift_volley";
            pat.barrel = barrel;
            pat.selection = Wassup.Data.PatternSelectionRule.None;
            pat.minAngleDeg = -10f;
            pat.maxAngleDeg = 10f;
            pat.shots = new[]
            {
                new ProjectileShotStep { directionT = 0f, intervalAfterPreviousSec = 0f },
                new ProjectileShotStep { directionT = 1f, intervalAfterPreviousSec = 0.1f },
            };
            var vol = ScriptableObject.CreateInstance<DirectionalVolleyAbility>();
            vol.pattern = pat;
            d.abilities.Add(vol);
            return (d, barrel, pat, vol);
        }

        private static void Destroy(params Object[] objs) { foreach (var o in objs) if (o != null) Object.DestroyImmediate(o); }

        [Test]
        public void 발사_명세의_탄은_정의표_안을_가리킨다()
        {
            // 탄 표를 굳힌 **뒤** 패턴이 자기 탄을 등록하면 표 밖을 가리킨다. 오늘은 「가림막 한 줄」
            // (`CollectPatterns` 의 선등록)이 막고 있었다 — 순서 자체로 막아야 한다.
            var v = Volley();
            try
            {
                var def = MatchDefinitionBuilder.Build(new[] { v.d }, new[] { dummy }, 1, ModeDef.Default());
                Assert.AreEqual(1, def.Units[0].Attack.PatternDefIndices.Length);
                int barrel = def.Patterns[def.Units[0].Attack.PatternDefIndices[0]].BarrelProjectileDefIndex;
                Assert.That(barrel, Is.InRange(0, def.Projectiles.Length - 1), "패턴의 탄이 탄 표 밖이다");
            }
            finally { Destroy(v.d, v.barrel, v.pat, v.vol); }
        }

        [Test]
        public void 잘못된_발사_명세는_거절된다()
        {
            // 옛 `ProjectilePatternData.TryToSpec` 의 거절 — 각도 뒤집힘 · 발 없음 · 방향 탄인데
            // 대상 선정 규칙이 있음 · 탄이 유닛 탄과 다름. 거절된 패턴은 **붙지 않는다**(단발로 쏜다).
            var v = Volley();
            v.pat.minAngleDeg = 30f;
            v.pat.maxAngleDeg = -30f;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                var def = MatchDefinitionBuilder.Build(new[] { v.d }, new[] { dummy }, 1, ModeDef.Default());
                Assert.AreEqual(0, def.Units[0].Attack.PatternDefIndices.Length, "각도가 뒤집힌 패턴이 붙었다");

                v.pat.minAngleDeg = -10f; v.pat.maxAngleDeg = 10f;
                v.pat.selection = Wassup.Data.PatternSelectionRule.RoundRobin;
                def = MatchDefinitionBuilder.Build(new[] { v.d }, new[] { dummy }, 1, ModeDef.Default());
                Assert.AreEqual(0, def.Units[0].Attack.PatternDefIndices.Length,
                    "방향 탄인데 대상 선정 규칙이 있는 패턴이 붙었다");
            }
            finally { LogAssert.ignoreFailingMessages = false; Destroy(v.d, v.barrel, v.pat, v.vol); }
        }

        [Test]
        public void 발사_명세_값은_정의역으로_접힌다()
        {
            // 옛 `TryToSpec` 의 클램프 — 방향 0~1 · 간격·예고·반경은 음수 금지.
            var v = Volley();
            v.pat.shots[0].directionT = 1.5f;
            v.pat.shots[1].intervalAfterPreviousSec = -1f;
            v.pat.telegraphSec = -2f;
            v.pat.scopeTileRange = -3;
            try
            {
                var def = MatchDefinitionBuilder.Build(new[] { v.d }, new[] { dummy }, 1, ModeDef.Default());
                var p = def.Patterns[def.Units[0].Attack.PatternDefIndices[0]];
                Assert.AreEqual(1f, p.Shots[0].DirectionT, 1e-6f);
                Assert.AreEqual(0f, p.Shots[1].IntervalAfterPreviousSec, 1e-6f);
                Assert.AreEqual(0f, p.TelegraphSec, 1e-6f);
                Assert.AreEqual(0, p.ScopeTileRange);
            }
            finally { Destroy(v.d, v.barrel, v.pat, v.vol); }
        }

        // ── 모드 배선 ────────────────────────────────────────────────────────

        private static MatchModeData Mode()
        {
            var m = ScriptableObject.CreateInstance<MatchModeData>();
            m.modeId = "drift_mode";
            m.costConfig = ScriptableObject.CreateInstance<CostConfig>();
            return m;
        }

        private static void DropMode(MatchModeData m)
        {
            if (m == null) return;
            if (m.costConfig != null) Object.DestroyImmediate(m.costConfig);
            Object.DestroyImmediate(m);
        }

        [Test]
        public void 모드와_저작이_어긋나면_빌드가_크게_알린다()
        {
            // `ModeValidation.Validate` 는 테스트만 불렀다 — 「12웨이브를 막으라는데 플랜이 비었다」
            // 가 판 중간에야 드러났다(그 판은 영영 안 끝난다).
            var mode = Mode();
            mode.goalKind = GoalKind.WaveClear;
            mode.clockKind = ClockKind.CountUp;
            mode.targetWaves = 12;
            mode.waveSourceKind = WaveSourceKind.AuthoredPlan;
            try
            {
                // 문제 둘(목표 12 > 저작 0 · 플랜에 웨이브 없음)을 **전부** 적는다 — 첫 문제에서 멈추면
                // 저작자가 두 번째를 고치려고 다시 돌려야 한다.
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("모드 검증.*목표 웨이브 12"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("모드 검증.*웨이브가 없다"));
                MatchDefinitionBuilder.Build(mode, new DefenderUnitData[0], null, null, null, 1);
            }
            finally { DropMode(mode); }
        }

        [Test]
        public void 배치_자원_저작이_없는_모드는_크게_알린다()
        {
            // 옛 배치 창 폴백은 30초였는데 새 폴백은 0초로 뒤집혀 있었다. 숫자를 다시 지어내지
            // 않고 「저작이 없다」를 loud 하게 만든다.
            var mode = Mode();
            Object.DestroyImmediate(mode.costConfig);
            mode.costConfig = null;
            try
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("costConfig"));
                MatchDefinitionBuilder.ToModeDef(mode);
            }
            finally { DropMode(mode); }
        }

        [Test]
        public void 모드의_덱은_드라이버_덱을_이긴다()
        {
            // 툴팁 「비우면 맵 풀이 짝지은 덱」 — 맵 풀 배선 전까지는 드라이버 덱이 폴백이다.
            var mode = Mode();
            var modeDeck = ScriptableObject.CreateInstance<AttackDeck>();
            modeDeck.waveSeed = 111;
            var driverDeck = ScriptableObject.CreateInstance<AttackDeck>();
            driverDeck.waveSeed = 222;
            mode.deck = modeDeck;
            try
            {
                var def = MatchDefinitionBuilder.Build(mode, new DefenderUnitData[0], driverDeck, null, null, 1);
                Assert.AreEqual(111, def.WaveDeck.WaveSeed, "모드가 고른 덱이 안 쓰였다");
                mode.deck = null;
                def = MatchDefinitionBuilder.Build(mode, new DefenderUnitData[0], driverDeck, null, null, 1);
                Assert.AreEqual(222, def.WaveDeck.WaveSeed, "모드 덱이 비면 드라이버 덱이다");
            }
            finally { DropMode(mode); Object.DestroyImmediate(modeDeck); Object.DestroyImmediate(driverDeck); }
        }

        [Test]
        public void 저작_플랜_모드는_모드의_플랜을_쓴다()
        {
            var mode = Mode();
            var plan = ScriptableObject.CreateInstance<WavePlanAsset>();
            plan.displayName = "drift_plan";
            plan.waves.Add(new AuthoredWave { durationSec = 10f });   // 빈 플랜은 검증이 거절한다
            mode.plan = plan;
            try
            {
                mode.waveSourceKind = WaveSourceKind.AuthoredPlan;
                var def = MatchDefinitionBuilder.Build(mode, new DefenderUnitData[0], null, null, null, 1);
                Assert.AreEqual("drift_plan", def.WavePlan.DisplayName, "저작 플랜 모드가 모드의 플랜을 안 읽었다");

                mode.waveSourceKind = WaveSourceKind.GeneratedFromDeck;
                def = MatchDefinitionBuilder.Build(mode, new DefenderUnitData[0], null, null, null, 1);
                Assert.AreNotEqual("drift_plan", def.WavePlan.DisplayName, "덱 생성 모드가 모드의 플랜을 읽었다");
            }
            finally { DropMode(mode); Object.DestroyImmediate(plan); }
        }
    }
}
