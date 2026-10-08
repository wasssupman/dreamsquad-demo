using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;
using Wassup.BattleCoreUnity;
using Wassup.Data;
using Faction = Wassup.Skills.Faction;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild 2026-09-24 드리프트 감사 후속 — **라이브 저작으로 구운 정의표의 형태.**
    //
    // 이 감사의 결함 다섯(배치 코스트 0 · 연발 피해 0 · 힐러 진영 · 마음사냥꾼 어그로 · 비행 적
    // 대상 층)은 전부 「고정구는 SO 를 안 읽는다」는 한 구멍으로 새어 나갔다 — 골든 코퍼스와 코어
    // 테스트는 손으로 짠 정의표를 쓰므로 **빌더를 한 번도 지나지 않는다.** 이 파일은 그 구멍을 막는
    // lane 이다: `BattleCoreScene` 의 드라이버가 실제로 가리키는 저작(모드 · 덱 · SO 두 장의 방어유닛 ·
    // 보너스 · 스테이지 · 스택 · 부여 상한 · 이동 튜닝 · 활성 시즌)으로 빌더를 돌리고 **정의표의
    // 불변식**을 단언한다.
    //
    // ⚠ 골든이 아니다 — 판의 결과가 아니라 정의표의 **모양**을 증언한다. 그래서 헤드리스 lane 에
    // 두지 않는다(Runtime 을 못 부른다). 밸런스 수치는 리터럴로 못박지 않는다(test-procedure) —
    // 「0 보다 크다」「저작과 같다」처럼 관계만 본다.
    //
    // 저작을 씬에서 읽는 이유: 목록을 여기 다시 적으면 두 벌이 되고, 씬이 편성을 바꾸는 날 이
    // 테스트는 옛 편성을 증언한다. 씬 YAML 의 드라이버 칸을 GUID 로 풀어 **씬이 쓰는 그것**을 잡는다.
    public class LiveDefinitionSmokeTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/BattleCoreScene.unity";

        private sealed class Live
        {
            public MatchModeData Mode;
            public DefenderUnitData[] Defenders;
            public AttackDeck Deck;
            public BonusWaveData Bonus;
            public Wassup.Core.MapStage Stage;
            public MovementTuningConfig Movement;
            public StackModifierSO[] Stacks;
            public ImbueCapConfig Imbue;
            public MapThemeData Theme;
            public int Seed;
        }

        // ── 씬의 드라이버 칸 → 에셋 ─────────────────────────────────────────

        private static Live ReadScene()
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                "Assets/_Project/Scripts/BattleCoreUnity/BattleDriver.cs");
            Assert.IsNotNull(script, "BattleDriver.cs 가 없다");
            string scriptGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));

            string yaml = System.IO.File.ReadAllText(ScenePath);
            int at = yaml.IndexOf("guid: " + scriptGuid, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, "BattleCoreScene 에 BattleDriver 가 없다");
            int end = yaml.IndexOf("\n--- ", at, System.StringComparison.Ordinal);
            string block = end > 0 ? yaml.Substring(at, end - at) : yaml.Substring(at);

            // battle-content-finish unit 0 — 판 저작은 씬이 가리키는 SO 두 장(`BattleContent` · `DefaultLoadout`)에 있다.
            var content = One<BattleContent>(block, "_content");
            var loadout = One<DefaultLoadout>(block, "_loadout");
            Assert.IsNotNull(content, "씬 드라이버에 BattleContent 가 없다");
            Assert.IsNotNull(loadout, "씬 드라이버에 DefaultLoadout 이 없다");
            // 옛 Many<T> 가 GUID 하나하나의 해석을 단언했듯, 끊어진 참조(null 요소)가 조용히 빌더로 들어가지 않게 본다.
            for (int i = 0; i < loadout.defenders.Length; i++) Assert.IsNotNull(loadout.defenders[i], $"DefaultLoadout.defenders[{i}] 가 비었다");
            for (int i = 0; i < content.stackModifiers.Length; i++) Assert.IsNotNull(content.stackModifiers[i], $"BattleContent.stackModifiers[{i}] 가 비었다");
            var live = new Live
            {
                Mode = One<MatchModeData>(block, "_mode"),
                Defenders = loadout.defenders,
                Deck = One<AttackDeck>(block, "_deck"),
                Bonus = content.bonus,
                Stage = One<Wassup.Core.MapStage>(block, "_stagePrefab"),
                Movement = content.movementTuning,
                Stacks = content.stackModifiers,
                Imbue = content.imbueCaps,
                // 효과 타일의 출처 = **활성 시즌의 맵 테마**(드라이버가 `BattleContent.ActiveMapTheme` 에서 읽는다).
                Theme = content.ActiveMapTheme,
            };
            var seed = Regex.Match(block, @"\n  _seed: (-?\d+)");
            live.Seed = seed.Success ? int.Parse(seed.Groups[1].Value) : 1;

            Assert.IsNotNull(live.Mode, "씬 드라이버에 모드가 없다");
            Assert.IsNotNull(live.Deck, "씬 드라이버에 덱이 없다");
            Assert.IsNotNull(live.Stage, "씬 드라이버에 스테이지가 없다");
            Assert.Greater(live.Defenders.Length, 0, "씬 드라이버에 방어유닛이 없다");
            return live;
        }

        private static T One<T>(string block, string field) where T : Object
        {
            var m = Regex.Match(block, @"\n  " + field + @": \{fileID: -?\d+(?:, guid: ([0-9a-f]{32}))?");
            Assert.IsTrue(m.Success, $"드라이버 칸 {field} 를 못 찾았다(씬 형식이 바뀌었나)");
            return m.Groups[1].Success ? LoadByGuid<T>(m.Groups[1].Value) : null;
        }

        private static T LoadByGuid<T>(string guid) where T : Object
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            if (typeof(Component).IsAssignableFrom(typeof(T)))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                return go != null ? go.GetComponent(typeof(T)) as T : null;
            }
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        // ── 빌드 ─────────────────────────────────────────────────────────────

        private static MatchDefinition BuildLive(Live live) => BuildLive(live, live.Defenders);

        private static MatchDefinition BuildLive(Live live, DefenderUnitData[] defenders)
        {
            var stage = Object.Instantiate(live.Stage);
            var map = default(GeneratedMap);
            try
            {
                stage.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                stage.transform.localScale = Vector3.one;
                var scan = Wassup.Core.MapStageScanner.Scan(stage, 1f);
                map = DioramaMapBuilder.Assemble(scan, Unity.Collections.Allocator.Persistent);
                var structures = new List<StructureEntry>(scan.structures);
                structures.Sort(DioramaMapBuilder.CompareStructureRowMajor);

                return MatchDefinitionBuilder.Build(
                    live.Mode, defenders, live.Deck, null, live.Bonus, live.Seed,
                    costRateMultiplier: 1f, map: in map, tileSize: 1f,
                    structures: structures, viewAssets: null,
                    movement: live.Movement, stackModifiers: live.Stacks, imbueCaps: live.Imbue,
                    board: new BoardEffectAuthoring { Theme = live.Theme, SuppressEffectTiles = stage.suppressEffectTiles });
            }
            finally
            {
                if (map.IsCreated) map.Dispose();
                Object.DestroyImmediate(stage.gameObject);
            }
        }

        private Live _live;
        private MatchDefinition _def;
        // 씬 편성(8)에는 연발 유닛이 없다. 발사 명세 불변식은 **카탈로그 전체**로 같은 모드·덱·
        // 스테이지를 구워 묻는다 — 편성이 바뀌어도 라이브 에셋 전부가 증언 대상이 된다.
        private DefenderUnitData[] _catalog;
        private MatchDefinition _catalogDef;

        [OneTimeSetUp]
        public void BuildOnce()
        {
            _live = ReadScene();
            _def = BuildLive(_live);
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>("Assets/_Project/Data/DefenderCatalog.asset");
            Assert.IsNotNull(catalog, "DefenderCatalog 이 없다");
            _catalog = catalog.units;
            _catalogDef = BuildLive(_live, _catalog);
        }

        // ── 불변식 ───────────────────────────────────────────────────────────

        [Test]
        public void 모드가_저작과_어긋나지_않는다()
        {
            // 모드 검증을 빌더가 안 부르던 시절 「12웨이브를 막으라는데 플랜이 비었다」가 판 중간에야
            // 드러났다. 빌더가 부른 뒤로는 실패가 loud 로그가 되는데, 여기서는 그 결과를 직접 본다.
            var problems = new List<string>();
            Assert.IsTrue(ModeValidation.Validate(_def, problems), string.Join("\n", problems));
        }

        [Test]
        public void 방어유닛은_전부_배치_코스트가_있다()
        {
            // 놓친 결함: unit 4 의 배치 저작 일곱 칸을 빌더가 안 옮겨 **판 전체가 공짜**였다
            // (트레이 칩 0). 고정구는 코스트를 손으로 넣어 못 봤다.
            Assert.AreEqual(_live.Defenders.Length, _def.Roster.Length, "로스터가 편성과 다르다");
            foreach (int i in _def.Roster)
                Assert.Greater(_def.Units[i].Cost, 0, $"{_def.Units[i].Id} 의 배치 코스트가 0 이다");
        }

        [Test]
        public void 아군을_겨누는_유닛은_아군만_본다()
        {
            // 놓친 결함 H4: 라이브 힐러 `targetFactions` 가 적 전부라 raw 로 실려 **적을 회복시켰다.**
            // 씬 편성은 조각 C 플레이 확인용으로 바뀔 수 있어(2026-09-24 힐러가 빠졌다) 발사 명세와
            // 같이 **카탈로그 전체**로 묻는다 — 편성이 아니라 라이브 에셋 전부가 증언 대상이다.
            int allies = 0;
            for (int i = 0; i < _catalog.Length; i++)
            {
                if (!_catalog[i].targetAllies) continue;
                allies++;
                Assert.AreEqual((int)Faction.DefenderUnit, _catalogDef.Units[i].TargetFactions,
                    $"{_catalogDef.Units[i].Id}: 아군 대상 유닛의 대상 진영이 아군 단독이 아니다");
                Assert.AreEqual(0, _catalogDef.Units[i].Attack.TargetLayers, $"{_catalogDef.Units[i].Id}: 아군 대상은 층을 거르지 않는다");
            }
            Assert.Greater(allies, 0, "카탈로그에 아군 대상 유닛(힐러)이 없다 — 이 단언이 아무것도 증언하지 않는다");
        }

        [Test]
        public void 적의_공격은_통행_층을_거르지_않는다()
        {
            // 놓친 결함 M7: 적의 대상 층에 「자기 통행 층」이 실려 비행 적이 순찰병을 못 때렸다.
            Assert.Greater(_def.Enemies.Length, 0);
            foreach (var e in _def.Enemies)
                Assert.AreEqual(0, e.Attack.TargetLayers, $"{e.Id}: 적의 공격 대상 층이 0 이 아니다");
        }

        [Test]
        public void 유닛을_노리지_않는_적은_저작_그대로_유닛_비트가_없다()
        {
            // 놓친 결함 H5: 마음사냥꾼(유닛 비트 없음)이 가디언에게 유인됐다 — 규칙은 코어 게이트
            // (`AiMovePhase.GrantAggro`)가 지고, 여기서는 **빌더가 그 저작을 해석값으로 흐리지 않았나**
            // 를 본다(0 을 기본 마스크로 풀면 유닛 비트가 생겨 게이트가 무력해진다).
            var seeker = AssetDatabase.LoadAssetAtPath<AttackUnitData>("Assets/_Project/Data/Enemies/Enemy_Heartseeker.asset");
            Assert.IsNotNull(seeker, "마음사냥꾼 에셋이 없다");
            var def = _def;
            int at = System.Array.FindIndex(def.Enemies, e => e.Id == seeker.id);
            if (at < 0)
            {
                // 라이브 덱이 안 부르는 판이면 같은 빌더로 그 적만 굽는다.
                def = MatchDefinitionBuilder.Build(_live.Defenders, new[] { seeker }, 1, ModeDef.Default());
                at = 0;
            }
            int mask = Wassup.BattleCore.Combat.TargetDefaults.ResolveEnemy(def.Enemies[at].TargetFactions);
            Assert.AreEqual(0, mask & (int)Faction.DefenderUnit, "마음사냥꾼의 대상에 방어유닛 비트가 있다");
        }

        [Test]
        public void 발사_명세는_전부_저작_규칙과_탄을_가진다()
        {
            // 놓친 결함: 선정 규칙 번호가 어긋나 12 중 11 이 다른 규칙으로 읽혔다(ec10619d) ·
            // 탄 표를 굳힌 뒤 등록된 탄은 표 밖을 가리킬 수 있었다.
            var byId = new Dictionary<string, ProjectilePatternData>();
            foreach (var guid in AssetDatabase.FindAssets("t:ProjectilePatternData"))
            {
                var p = AssetDatabase.LoadAssetAtPath<ProjectilePatternData>(AssetDatabase.GUIDToAssetPath(guid));
                if (p == null) continue;
                Assert.AreEqual(p.selection.ToString(), CombatDefinitionBuilder.ToCoreSelection(p.selection).ToString(),
                    $"패턴 {p.name}: 선정 규칙이 다른 이름으로 옮겨진다");
                if (!string.IsNullOrEmpty(p.id)) byId[p.id] = p;
            }
            Assert.Greater(byId.Count, 0, "프로젝트에 발사 명세 에셋이 없다");

            Assert.Greater(_catalogDef.Patterns.Length, 0, "카탈로그 전체로 구웠는데 발사 명세가 하나도 안 실렸다");
            foreach (var pd in _catalogDef.Patterns)
            {
                Assert.That(pd.BarrelProjectileDefIndex, Is.InRange(0, _catalogDef.Projectiles.Length - 1),
                    $"패턴 {pd.Id}: 탄이 탄 표 밖이다");
                Assert.IsTrue(byId.TryGetValue(pd.Id, out var src), $"패턴 {pd.Id} 의 저작을 못 찾았다");
                Assert.AreEqual(src.selection.ToString(),
                    ((Wassup.BattleCore.Combat.Emission.PatternSelectionRule)pd.Selection).ToString(),
                    $"패턴 {pd.Id}: 정의표의 선정 규칙이 저작과 다르다");
            }
        }

        [Test]
        public void 연발_유닛의_탄_피해는_실효_공격_피해다()
        {
            // 놓친 결함 H1: 연발탄이 패턴 저작 피해(라이브 0)를 실어 머신거너·샷거너가 아무도 못 죽였다.
            // 그 규칙은 공격 루프 안에 있으므로 **판을 한 번 돌려** 첫 연발의 탄 피해를 잰다.
            // 열린 판에 유닛 하나와 적 하나만 세운다(스테이지 맵은 배치·경로가 끼어들어 잡음이 된다).
            int probed = 0;
            var cat = _catalogDef;
            for (int u = 0; u < _catalog.Length; u++)
            {
                if (_catalog[u] == null || _catalog[u].GetAbility<DirectionalVolleyAbility>() == null) continue;
                // 다연발 능력이 있는데 패턴이 안 붙었으면 빌더가 거절한 것이다(loud 로그와 같은 사실).
                Assert.IsTrue(cat.Units[u].Attack.PatternDefIndices != null && cat.Units[u].Attack.PatternDefIndices.Length > 0,
                    $"{cat.Units[u].Id}: 다연발 능력의 패턴이 정의표에 안 붙었다");
                float expected = 0f;
                foreach (var o in cat.Units[u].Attack.Outputs)
                    if (o.Kind == Wassup.BattleCore.AttackOutputKind.Damage) expected += o.Magnitude;
                Assert.Greater(expected, 0f, $"{cat.Units[u].Id}: 연발 유닛의 공격 피해 저작이 0 이다");

                var def = BuildLive(_live, _catalog);
                var map = new MapSnapshot
                {
                    Width = 24, Height = 9, TileSize = 1f,
                    Goals = new[] { new int2(23, 4) }, Spawns = new[] { new int2(0, 4) },
                };
                map.Normalize();
                def.Map = map;
                def.Structures = System.Array.Empty<StructureDef>();
                def.EffectTileCount = 0;   // 열린 판엔 배치 칸이 없다 — 뽑기 잡음을 끈다
                def.ConfigHash = def.ComputeConfigHash();

                var m = new BattleMatch(def);
                m.Begin();
                var spawned = new List<CoreEvent>();
                m.Bus.Subscribe(CoreEventKind.ProjectileSpawned, 0, e => spawned.Add(e));
                m.Apply(Command.DebugSpawnDefender(u, new int2(12, 4)));
                m.Apply(Command.DebugSpawnEnemy(0, new int2(10, 4)));
                for (int t = 0; t < 60 && spawned.Count == 0; t++) m.Tick();
                Assert.Greater(spawned.Count, 0, $"{cat.Units[u].Id}: 2초 안에 한 발도 안 나갔다");
                foreach (var e in spawned)
                    Assert.AreEqual(expected, e.Amount, 1e-3f, $"{cat.Units[u].Id}: 연발탄 피해가 공격 피해와 다르다");
                probed++;
            }
            Assert.Greater(probed, 0, "카탈로그에 연발 유닛이 없다 — 이 단언이 아무것도 증언하지 않는다");
        }

        [Test]
        public void 효과_타일_개수는_활성_시즌_테마를_따른다()
        {
            // 놓친 결함(6b): 빌더가 테마의 개수를 안 실어 라이브 효과 타일 3칸이 0 이었다.
            Assert.IsNotNull(_live.Theme, "활성 시즌에 맵 테마가 없다");
            bool authored = _live.Theme.effectTiles != null && _live.Theme.effectTiles.Length > 0
                            && _live.Theme.effectTileCount > 0;
            Assume.That(authored, "활성 테마가 효과 타일을 저작하지 않았다 — 단언 보류");
            Assert.AreEqual(_live.Theme.effectTileCount, _def.EffectTileCount, "효과 타일 개수가 테마 저작과 다르다");
            Assert.Greater(_def.EffectTileCount, 0);
        }
    }
}
