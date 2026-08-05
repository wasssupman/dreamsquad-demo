using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Data.MapGrid;
using Wassup.Sim.Match;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 14 — **승패 규칙의 매치 조건은 "해결된 덱" 에서 와야 한다.**
    ///
    /// 회귀의 정체(2026-08-05 투트랙 리뷰 H2): 적출 전에는 `EffectiveLeakLimit()` 가 `ActiveDeck` 을
    /// **매 호출 라이브로** 읽어서 순서와 무관하게 수렴했다. unit 14 가 그것을 `Configure` 시점의
    /// 값 스냅샷으로 바꾸면서 **순서가 새 계약이 됐는데**, 최초 구현은 `BeginPlacement` 에서
    /// `BuildMapForBattle`(= `_resolvedDeck` 을 엔들리스/인카운터 덱으로 교체) **보다 먼저** 고정해
    /// 다른 덱의 유출 한계가 굳을 수 있었다.
    ///
    /// 왜 골든이 못 잡나: 코퍼스는 단일 덱·단일 맵 인덱스라 이 축을 아예 지나지 않는다.
    /// 왜 중요한가: 배치 페이즈의 몽마의 계약 지불이 **비가역**이라, 한계가 뒤늦게 줄면
    /// "지불로 즉시 패배 금지" 불변식이 첫 유출에서 깨진다. teardown 이 맵을 dispose 하므로
    /// RESTART 마다 지나는 경로다.
    /// </summary>
    public class OutcomeRulesDeckResolutionTests
    {
        private const int SerializedDeckLimit = 3;
        private const int PoolDeckLimit = 9;

        private World _world;
        private GameObject _go;
        private BattleBridge _bridge;
        private AttackDeck _serializedDeck;   // 인스펙터 폴백 덱
        private AttackDeck _poolDeck;         // 맵 풀 엔트리가 들고 오는 덱(= 해결 결과)
        private MapDocument _doc;
        private MapDocumentPool _pool;

        [SetUp]
        public void SetUp()
        {
            _world = new World("OutcomeRulesDeckResolutionTests");

            _serializedDeck = ScriptableObject.CreateInstance<AttackDeck>();
            _serializedDeck.deckId = "serialized";
            _serializedDeck.defeatGoalReachedCount = SerializedDeckLimit;

            _poolDeck = ScriptableObject.CreateInstance<AttackDeck>();
            _poolDeck.deckId = "pool";
            _poolDeck.defeatGoalReachedCount = PoolDeckLimit;

            _doc = BuildUsableDocument();
            _pool = ScriptableObject.CreateInstance<MapDocumentPool>();
            AddPoolEntry(_pool, _doc, _poolDeck); // 엔트리가 자기 덱을 들고 온다 → 해결 시 교체

            _go = new GameObject("BattleBridge_OutcomeRulesDeckResolutionTests");
            _bridge = _go.AddComponent<BattleBridge>();

            SetField(_bridge, "deck", _serializedDeck);
            SetField(_bridge, "mapPool", _pool);
            SetField(_bridge, "_world", _world);
            SetField(_bridge, "_em", _world.EntityManager);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_serializedDeck != null) Object.DestroyImmediate(_serializedDeck);
            if (_poolDeck != null) Object.DestroyImmediate(_poolDeck);
            if (_doc != null) Object.DestroyImmediate(_doc);
            if (_pool != null) Object.DestroyImmediate(_pool);
            _world?.Dispose();
        }

        // 전제 고정 — 맵 해결이 실제로 덱을 갈아치운다. 이게 거짓이면 아래 테스트는 무의미하다.
        [Test]
        public void BuildMapForBattle_ReplacesTheActiveDeck()
        {
            Assert.AreSame(_serializedDeck, GetActiveDeck(), "해결 전에는 인스펙터 덱");

            Invoke(_bridge, "BuildMapForBattle");

            Assert.AreSame(_poolDeck, GetActiveDeck(),
                "맵 해결이 풀 엔트리의 덱으로 교체해야 한다(이 교체가 H2 의 축이다)");
        }

        /// <summary>
        /// 핵심 — 조건 고정이 **덱 해결 뒤**에 일어나야 한다. 순서가 뒤집히면 인스펙터 덱의
        /// 한계(3)가 굳어 실제 판(9)과 어긋난다.
        /// </summary>
        [Test]
        public void OutcomeRules_TakeTheResolvedDeckLimit_NotTheSerializedOne()
        {
            Invoke(_bridge, "BuildMapForBattle");
            Invoke(_bridge, "ConfigureOutcomeRules", false);

            Assert.AreEqual(PoolDeckLimit, Rules().EffectiveLeakLimit,
                "해결된 덱의 한계가 규칙에 실려야 한다");
            Assert.AreEqual(PoolDeckLimit, Rules().StressLimit);
        }

        /// <summary>
        /// 순서가 뒤집힌 상태를 **명시적으로 재현**해 이 테스트가 무엇을 지키는지 못박는다.
        /// 이것이 리뷰 H2 가 지적한 실제 코드 모양이었다 — 통과하면 안 되는 배치가 아니라,
        /// "그때 값이 이렇게 어긋난다" 는 증거다.
        /// </summary>
        [Test]
        public void ConfiguringBeforeDeckResolution_FreezesTheWrongLimit()
        {
            Invoke(_bridge, "ConfigureOutcomeRules", false);   // ← 해결 전에 고정(옛 버그 순서)
            Invoke(_bridge, "BuildMapForBattle");

            Assert.AreEqual(SerializedDeckLimit, Rules().EffectiveLeakLimit,
                "해결 전에 고정하면 다른 덱의 한계가 남는다 — BeginPlacement 가 이 순서면 안 된다");
            Assert.AreNotEqual(PoolDeckLimit, Rules().EffectiveLeakLimit);
        }

        // ---- helpers ----

        private MatchOutcomeRules Rules() => (MatchOutcomeRules)GetField(_bridge, "_outcome");

        private AttackDeck GetActiveDeck()
        {
            var pi = typeof(BattleBridge).GetProperty("ActiveDeck",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(pi, "ActiveDeck 프로퍼티를 찾지 못했다");
            return (AttackDeck)pi.GetValue(_bridge);
        }

        // 최소 usable 문서: 6×4, y=2 복도 Walk, 스폰 2(connectivity 는 스폰 ≥2 요구), 골 1.
        private static MapDocument BuildUsableDocument()
        {
            const int w = 6;
            const int h = 4;
            int n = w * h;

            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            for (int x = 0; x < w; x++) tiles[2 * w + x] = MapTileType.Walk;

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(
                w, h,
                tiles, new byte[n], new bool[n], new byte[n],
                new[] { new Vector2Int(w - 1, 2) },
                new[] { new Vector2Int(0, 2), new Vector2Int(1, 2) },
                seed: 42,
                version: 1);
            return doc;
        }

        private static void AddPoolEntry(MapDocumentPool pool, MapDocument doc, AttackDeck deck)
        {
            var fi = typeof(MapDocumentPool).GetField("entries",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, "MapDocumentPool.entries field not found");
            var list = (System.Collections.IList)fi.GetValue(pool);
            list.Add(new MapDocumentPool.Entry { document = doc, deck = deck });
        }

        private static FieldInfo FindField(object target, string name)
        {
            var type = target.GetType();
            FieldInfo fi = null;
            while (fi == null && type != null)
            {
                fi = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance
                                       | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(fi, $"Field '{name}' not found on {target.GetType().Name}");
            return fi;
        }

        private static void SetField(object target, string name, object value) =>
            FindField(target, name).SetValue(target, value);

        private static object GetField(object target, string name) =>
            FindField(target, name).GetValue(target);

        private static void Invoke(object target, string name, params object[] args)
        {
            var mi = target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance
                                                    | BindingFlags.Public);
            Assert.IsNotNull(mi, $"Method '{name}' not found on {target.GetType().Name}");
            mi.Invoke(target, args);
        }
    }
}
