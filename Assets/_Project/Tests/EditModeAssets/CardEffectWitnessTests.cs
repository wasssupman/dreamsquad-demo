using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;
using Wassup.BattleCoreUnity;
using Wassup.Data;

namespace Wassup.Tests.EditModeAssets
{
    // battle-core-rebuild unit 7e ① — **카드 전량이 「구워졌고 · 발동하고 · 그 종류의 효과가 실제로 걸렸다」.**
    //
    // 카드 커버리지 감사(7b 말미)를 리드가 손으로 한 번 한 것을 매 커밋 자동으로, 그리고 **발동까지** 굳힌다.
    // 정의표는 라이브 경로로 굽는다 — `BattleCoreScene` 드라이버의 저작(모드 · 덱 · 보너스 · 스테이지 · 스택 · 부여 상한 ·
    // 이동 튜닝 · 활성 시즌)에 **카탈로그 전부를 카드로**(기본 편성 덱 자리), 방어유닛은 카탈로그 전부(숙주 후보). 고정구는 SO 를
    // 안 읽어서 빌더를 한 번도 안 지난다 — 그 구멍이 `LiveDefinitionSmokeTests` 가 막은 것과 같다.
    //
    // 판정은 `CardProbe`(코어 · 순수 C#)가 한다. 카드마다 기대값을 적지 않는다 — 실행자가 낸 의도가 기대이고
    // `EffectWitness` 가 그 의도 종류의 흔적을 대조 판과 비교한다.
    //
    // ⚠ **×가 나오면 그 카드가 결함이다 — 여기서 고치지 않는다**(7e 구현 9). 실패 문구가 카드 · 미관측 의도 · 진단을 싣는다.
    // ⚠ 밸런스 수치를 리터럴로 박지 않는다(test-procedure) — 카드 수도 저작에서 센다.
    public class CardEffectWitnessTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/BattleCoreScene.unity";
        private const string CardsRoot = "Assets/_Project/Data/Dreamcatcher";
        private const string CatalogPath = "Assets/_Project/Data/Dreamcatcher/DreamcatcherCardCatalog.asset";

        // ── 카드 목록 — 카탈로그 순 + 카탈로그 밖(몽마의 계약 · 액티브 6)은 경로 순. 스냅샷 메뉴와 같은 규칙이다. ──

        public static List<DreamcatcherCard> Cards()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DreamcatcherCardCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "카드 카탈로그가 없다");
            var list = new List<DreamcatcherCard>();
            foreach (var c in catalog.cards) if (c != null && !list.Contains(c)) list.Add(c);
            var rest = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:DreamcatcherCard", new[] { CardsRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var c = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(path);
                if (c != null && !list.Contains(c)) rest.Add(path);
            }
            rest.Sort(System.StringComparer.Ordinal);
            foreach (var p in rest) list.Add(AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(p));
            return list;
        }

        private static IEnumerable<TestCaseData> CardCases()
        {
            var cards = Cards();
            for (int i = 0; i < cards.Count; i++)
                yield return new TestCaseData(i, cards[i].id).SetName($"카드_{i:00}_{cards[i].id}");
        }

        // ── 라이브 정의표(씬 드라이버 저작 + 카탈로그 카드) ─────────────────────

        private sealed class Live
        {
            public MatchModeData Mode;
            public AttackDeck Deck;
            public BonusWaveData Bonus;
            public Wassup.Core.MapStage Stage;
            public MovementTuningConfig Movement;
            public StackModifierSO[] Stacks;
            public ImbueCapConfig Imbue;
            public MapThemeData Theme;
            public int Seed;
        }

        private static Live ReadScene()
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_Project/Scripts/BattleCoreUnity/BattleDriver.cs");
            Assert.IsNotNull(script, "BattleDriver.cs 가 없다");
            string scriptGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
            string yaml = System.IO.File.ReadAllText(ScenePath);
            int at = yaml.IndexOf("guid: " + scriptGuid, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, "BattleCoreScene 에 BattleDriver 가 없다");
            int end = yaml.IndexOf("\n--- ", at, System.StringComparison.Ordinal);
            string block = end > 0 ? yaml.Substring(at, end - at) : yaml.Substring(at);

            // battle-content-finish unit 0 — 판 저작은 씬이 가리키는 SO(`BattleContent`)에 있다.
            var content = One<BattleContent>(block, "_content");
            Assert.IsNotNull(content, "씬 드라이버에 BattleContent 가 없다");
            var live = new Live
            {
                Mode = One<MatchModeData>(block, "_mode"),
                Deck = One<AttackDeck>(block, "_deck"),
                Bonus = content.bonus,
                Stage = One<Wassup.Core.MapStage>(block, "_stagePrefab"),
                Movement = content.movementTuning,
                Stacks = content.stackModifiers,
                Imbue = content.imbueCaps,
                Theme = content.ActiveMapTheme,
            };
            var seed = Regex.Match(block, @"\n  _seed: (-?\d+)");
            live.Seed = seed.Success ? int.Parse(seed.Groups[1].Value) : 1;
            Assert.IsNotNull(live.Mode, "씬 드라이버에 모드가 없다");
            Assert.IsNotNull(live.Mode.awakeningConfig, "모드에 각성 저작이 없다 — 카드 값의 주인이 없다");
            Assert.IsNotNull(live.Stage, "씬 드라이버에 스테이지가 없다");
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

        private static MatchDefinition BuildLive(Live live, DefenderUnitData[] defenders, List<DreamcatcherCard> cards)
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
                    board: new BoardEffectAuthoring { Theme = live.Theme, SuppressEffectTiles = stage.suppressEffectTiles },
                    cards: cards, dreamstones: null);
            }
            finally
            {
                if (map.IsCreated) map.Dispose();
                Object.DestroyImmediate(stage.gameObject);
            }
        }

        private List<DreamcatcherCard> _cards;
        private MatchDefinition _def;

        [OneTimeSetUp]
        public void BuildOnce()
        {
            var live = ReadScene();
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>("Assets/_Project/Data/DefenderCatalog.asset");
            Assert.IsNotNull(catalog, "DefenderCatalog 이 없다");
            _cards = Cards();
            _def = BuildLive(live, catalog.units, _cards);
            Assert.AreEqual(_cards.Count, _def.Cards.Length, "카드 목록 순서 = 정의표 카드 줄 순서");
        }

        // ── ① 카드 한 장 = 테스트 한 개 ────────────────────────────────────────

        [TestCaseSource(nameof(CardCases))]
        public void 카드는_붙이거나_시전하고_발동하면_그_종류의_효과가_걸린다(int row, string id)
        {
            Assert.AreEqual(id, _def.Cards[row].Id, "카드 줄이 목록과 어긋났다");
            var r = CardProbe.Run(_def, row);
            TestContext.WriteLine(r.ToString());
            Assert.IsTrue(r.Ok, "× — " + r);
        }

        // ── ① 반증 — 장치가 「구워졌는데 아무 일도 없다」를 실제로 잡는다 ──────────

        [Test]
        public void 반증_카드_한_장의_의도_적용을_끄면_실패로_떨어진다()
        {
            // 의도를 내는 첫 ○ 카드(공격 수식자 · 인수인계는 의도가 없다 — 그 둘은 다른 신호로 본다).
            int row = -1;
            CardProbeResult ok = default;
            for (int i = 0; i < _def.Cards.Length && row < 0; i++)
            {
                var r = CardProbe.Run(_def, i);
                if (!r.Ok) continue;
                foreach (var w in r.Witnessed)
                    if (!w.StartsWith("AttackMod:") && w != "RecallToFront") { row = i; ok = r; break; }
            }
            Assume.That(row >= 0, "의도를 내는 ○ 카드가 없다 — 반증할 대상이 없다(① 이 먼저 빨갛다)");

            // ⑴ 적용 표면 우회 — 실행자는 돌고 의도는 기록되지만 판에 안 닿는다.
            var muted = CardProbe.Run(_def, row, new CardProbeOptions { MuteIntents = true });
            TestContext.WriteLine("대조 " + ok + "\n반증 " + muted);
            Assert.IsTrue(muted.Baked && muted.Fired, "구워졌고 발동은 했다 — " + muted);
            Assert.IsFalse(muted.Ok, "의도 적용을 껐는데 ○ — 장치가 효과를 증언하지 못한다: " + muted);
            CollectionAssert.IsNotEmpty(muted.Missing);

            // ⑵ 라우팅 표 항목 제거 — 그 카드의 규칙 줄에서 실행자를 뺀다(메모리에서만 · 끝나면 되돌린다).
            ref var card = ref _def.Cards[row];
            int rule = card.Kind == CardKind.Active ? card.ActiveBinding
                     : card.Bindings != null && card.Bindings.Length > 0 ? card.Bindings[0] : card.SquadBindings[0];
            var effect = _def.Bindings[rule].Skill;
            try
            {
                _def.Bindings[rule].Skill = null;
                var unrouted = CardProbe.Run(_def, row);
                TestContext.WriteLine("라우팅 제거 " + unrouted);
                Assert.IsFalse(unrouted.Ok, "실행자를 뺐는데 ○: " + unrouted);
                Assert.IsFalse(unrouted.Baked, "실행자 없는 규칙은 굽기에서 걸린다");
            }
            finally
            {
                _def.Bindings[rule].Skill = effect;
            }
        }
    }
}
