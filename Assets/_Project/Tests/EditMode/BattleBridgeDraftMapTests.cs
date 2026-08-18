using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // Unit 4 — BattleBridge draft-map prebuild contracts.
    // map-diorama-stage unit 2 — 픽스처를 문서 풀(MapDocumentPool)에서 **스테이지 풀**
    // (MapStagePool → 프리팹 인스턴스화 → 스캔 → Assemble)로 재작성. 검증하는 라이프사이클
    // 계약(빌드/재빌드/BeginPlacement 무재빌드)은 동일하고, 안전망 계약은 개정을 따른다:
    // 연결성 실패 = 폴백 리니어가 아니라 **하드 실패**(README 계약 9).
    //
    // Fixture notes:
    //   • PrepareDraftMap() always overwrites _world with World.DefaultGameObjectInjectionWorld,
    //     which is null in EditMode tests. We replicate its internals via reflection
    //     (EnsureQueriesAndQueues + BuildMapForBattle) — identical code path.
    //   • 스테이지 «프리팹»은 씬 GameObject 템플릿로 대신한다 — Instantiate(Component) 는
    //     프리팹/씬 오브젝트를 구분하지 않으므로 라이브 경로와 같은 코드가 돈다.
    public class BattleBridgeDraftMapTests
    {
        private World _world;
        private GameObject _go;
        private BattleBridge _bridge;
        private AttackDeck _deck;
        private MapStage _stageTemplate;
        private MapStagePool _pool;

        [SetUp]
        public void SetUp()
        {
            _world = new World("BattleBridgeDraftMapTests");

            _deck = ScriptableObject.CreateInstance<AttackDeck>();
            _stageTemplate = BuildUsableStage();
            _pool = ScriptableObject.CreateInstance<MapStagePool>();
            AddPoolEntry(_pool, _stageTemplate, deck: null); // deck null → serialized deck 폴백 계약

            _go = new GameObject("BattleBridge_Test");
            _bridge = _go.AddComponent<BattleBridge>();

            SetField(_bridge, "deck", _deck);
            SetField(_bridge, "mapPool", _pool);
            SetField(_bridge, "_world", _world);
            SetField(_bridge, "_em", _world.EntityManager);
        }

        [TearDown]
        public void TearDown()
        {
            // 스테이지 인스턴스·GeneratedMap 정리(Persistent 누수/씬 잔존 방지) — 라이브와 같은 경로.
            if (_bridge != null) CallPrivateMethod(_bridge, "TeardownGeneratedMap");
            if (_go != null) Object.DestroyImmediate(_go);
            if (_deck != null) Object.DestroyImmediate(_deck);
            if (_stageTemplate != null) Object.DestroyImmediate(_stageTemplate.gameObject);
            if (_pool != null) Object.DestroyImmediate(_pool);
            _world?.Dispose();
        }

        // 최소 usable 스테이지: 8×6 열린 마당, 스폰 2(lane 0·1), 골 1, 내부 차단 프랍 1.
        internal static MapStage BuildUsableStage(string name = "MapStage_TestFixture")
        {
            var root = new GameObject(name);
            var stage = root.AddComponent<MapStage>();
            stage.playAreaCells = new Vector2Int(8, 6);
            stage.gridOriginLocal = Vector3.zero;
            stage.previewTileSize = 1f;

            AddMarker<SpawnMarker>(root, new Vector2Int(0, 1), m => m.laneIndex = 0);
            AddMarker<SpawnMarker>(root, new Vector2Int(0, 4), m => m.laneIndex = 1);
            AddMarker<GoalMarker>(root, new Vector2Int(7, 3), _ => { });
            AddMarker<PropFootprint>(root, new Vector2Int(3, 3), f => f.size = Vector2Int.one);
            return stage;
        }

        internal static void AddMarker<T>(GameObject stageRoot, Vector2Int cell, System.Action<T> init)
            where T : Component
        {
            var go = new GameObject($"{typeof(T).Name}_{cell.x}_{cell.y}");
            go.transform.SetParent(stageRoot.transform, false);
            go.transform.localPosition = new Vector3(cell.x + 0.5f, 0f, cell.y + 0.5f); // 셀 중심
            init(go.AddComponent<T>());
        }

        internal static void AddPoolEntry(MapStagePool pool, MapStage stage, AttackDeck deck)
        {
            var fi = typeof(MapStagePool).GetField("entries",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, "MapStagePool.entries field not found");
            var list = (System.Collections.IList)fi.GetValue(pool);
            list.Add(new MapStagePool.Entry { stage = stage, deck = deck });
        }

        // Case 1 — Internal prepare path sets HasGeneratedMap to true.
        [Test]
        public void PrepareDraftMap_FirstCall_BuildsMap()
        {
            Assert.IsFalse(_bridge.HasGeneratedMap, "pre-condition: no map yet");
            CallPrepareDraftMapInternal(_bridge);
            Assert.IsTrue(_bridge.HasGeneratedMap);
        }

        // Case 2 — 풀 스테이지 경로가 실제로 쓰였다: 격자 = 스테이지 playArea, 수동 저작 관례 seed(-1).
        [Test]
        public void PrepareDraftMap_UsesPoolStage()
        {
            CallPrepareDraftMapInternal(_bridge);
            var gm = GetGeneratedMap(_bridge);
            Assert.AreEqual(new Unity.Mathematics.int2(8, 6), gm.gridSize,
                "GeneratedMap 은 풀 스테이지의 playArea 에서 나와야 한다");
            Assert.AreEqual(-1, gm.seed, "디오라마 스테이지는 수동 저작 관례 seed(-1)");
            Assert.IsNotNull(GetStageInstance(_bridge), "스테이지 인스턴스(비주얼)가 서 있어야 한다");
        }

        // Case 3 — BeginPlacement after an already-built map does not rebuild
        //           (스테이지 인스턴스 참조가 그대로 = BuildMapForBattle 재호출 없음).
        [Test]
        public void BeginPlacement_AfterPrepare_DoesNotRebuild()
        {
            CallPrepareDraftMapInternal(_bridge);
            var instanceBefore = GetStageInstance(_bridge);
            Assert.IsNotNull(instanceBefore);

            CallBeginPlacementInternal(_bridge);

            Assert.AreSame(instanceBefore, GetStageInstance(_bridge),
                "BeginPlacement must not rebuild map when one already exists");
        }

        // Case 4 — RebuildDraftMap disposes old map/stage and creates new ones.
        [Test]
        public void RebuildDraftMap_DisposesOldAndCreatesNew()
        {
            CallPrepareDraftMapInternal(_bridge);
            var instanceBefore = GetStageInstance(_bridge);
            Assert.IsNotNull(instanceBefore, "stage instance must exist after first prepare");

            _bridge.RebuildDraftMap();

            Assert.IsTrue(_bridge.HasGeneratedMap, "HasGeneratedMap must stay true after rebuild");
            Assert.IsTrue(GetGeneratedMap(_bridge).IsCreated);
            var instanceAfter = GetStageInstance(_bridge);
            Assert.IsNotNull(instanceAfter, "rebuild must stand a new stage instance");
            Assert.AreNotSame(instanceBefore, instanceAfter, "rebuild must replace the stage instance");
        }

        // Case 5 — When no map has been built, the fallback path in BeginPlacement builds one.
        [Test]
        public void BeginPlacement_WithoutPrepare_FallbackBuilds()
        {
            Assert.IsFalse(_bridge.HasGeneratedMap, "pre-condition: no map yet");
            CallBeginPlacementInternal(_bridge);
            Assert.IsTrue(_bridge.HasGeneratedMap,
                "BeginPlacement fallback must build map when none exists");
        }

        // Case 6 — 연결성 실패 스테이지 → **하드 실패**(맵 없음). 조용한 폴백 리니어는 은퇴했다
        //          (README 계약 9 — unit 3 이후 폴백 맵은 렌더러가 없다).
        [Test]
        public void BuildMap_UnconnectedStage_HardFails_NoMap()
        {
            var walledStage = BuildUsableStage("MapStage_Walled");
            // 골 앞 세로 벽(x=6 전체) — 골 셀 자체는 열림(형식 오류 아님), 연결성만 실패.
            AddMarker<PropFootprint>(walledStage.gameObject, new Vector2Int(6, 0),
                f => f.size = new Vector2Int(1, 6));
            var badPool = ScriptableObject.CreateInstance<MapStagePool>();
            AddPoolEntry(badPool, walledStage, deck: null);
            SetField(_bridge, "mapPool", badPool);
            try
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("연결성 실패"));
                CallPrepareDraftMapInternal(_bridge);

                Assert.IsFalse(_bridge.HasGeneratedMap,
                    "connectivity failure must hard-fail with no map (no silent fallback)");
                Assert.IsNull(GetStageInstance(_bridge), "실패 시 스테이지 인스턴스도 정리돼야 한다");
            }
            finally
            {
                Object.DestroyImmediate(walledStage.gameObject);
                Object.DestroyImmediate(badPool);
            }
        }

        // Case 7 — 선택된 풀 엔트리의 stage 가 null → hard-fail: 크래시 없이 맵 없음.
        [Test]
        public void BuildMap_NullStageEntry_HardFails_NoMap()
        {
            var badPool = ScriptableObject.CreateInstance<MapStagePool>();
            AddPoolEntry(badPool, null, deck: null);
            SetField(_bridge, "mapPool", badPool);
            try
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("스테이지 프리팹이 없다"));
                CallPrepareDraftMapInternal(_bridge);

                Assert.IsFalse(_bridge.HasGeneratedMap,
                    "null stage entry must hard-fail with no map");
            }
            finally
            {
                Object.DestroyImmediate(badPool);
            }
        }

        // Case 8 — 풀 미배선 가드: 실제 PrepareDraftMap 이 world 접근 전에 명확히 거부.
        [Test]
        public void PrepareDraftMap_WithoutPool_GuardBlocks()
        {
            SetField(_bridge, "mapPool", null);

            LogAssert.Expect(LogType.Error, "[BattleBridge] deck or map pool reference missing.");
            _bridge.PrepareDraftMap(); // 가드가 world 참조보다 앞이라 EditMode 에서 안전

            Assert.IsFalse(_bridge.HasGeneratedMap, "guard must block map build without a pool");
        }

        // -----------------------------------------------------------------------
        // Helpers

        private static void CallPrepareDraftMapInternal(BattleBridge bridge)
        {
            CallPrivateMethod(bridge, "EnsureQueriesAndQueues");
            CallPrivateMethod(bridge, "BuildMapForBattle");
        }

        private static void CallBeginPlacementInternal(BattleBridge bridge)
        {
            CallPrivateMethod(bridge, "EnsureQueriesAndQueues");
            var gm = GetGeneratedMap(bridge);
            if (!gm.IsCreated)
                CallPrivateMethod(bridge, "BuildMapForBattle");
        }

        internal static void CallPrivateMethod(object target, string name)
        {
            var mi = target.GetType().GetMethod(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"Method '{name}' not found on {target.GetType().Name}");
            mi.Invoke(target, null);
        }

        internal static void SetField(object target, string name, object value)
        {
            var type = target.GetType();
            FieldInfo fi = null;
            while (fi == null && type != null)
            {
                fi   = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance
                                         | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(fi, $"Field '{name}' not found on {target.GetType().Name}");
            fi.SetValue(target, value);
        }

        internal static GeneratedMap GetGeneratedMap(BattleBridge bridge)
        {
            var fi = typeof(BattleBridge).GetField("_generatedMap",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, "_generatedMap field not found");
            return (GeneratedMap)fi.GetValue(bridge);
        }

        internal static MapStage GetStageInstance(BattleBridge bridge)
        {
            var fi = typeof(BattleBridge).GetField("_stageInstance",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, "_stageInstance field not found (이름 변경?)");
            return (MapStage)fi.GetValue(bridge);
        }
    }
}
