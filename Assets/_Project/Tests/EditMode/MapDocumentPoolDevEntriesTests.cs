using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // map-painter-tool unit 5 — dev 슬롯(devEntries) 계약 회귀 방지.
    //   ① 등록 dedup: 풀 본편/dev 어디에 있든 재등록 거부 (Count 불변 = 시드 결정론 불가시)
    //   ② BattleBridge 가 풀 뒤 이어붙은 dev 인덱스를 devEntries 로 해석
    public class MapDocumentPoolDevEntriesTests
    {
        private static MapDocument MakeUsableDoc(int seed)
        {
            const int w = 6, h = 4; int n = w * h;
            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            for (int x = 0; x < w; x++) tiles[2 * w + x] = MapTileType.Walk;
            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(w, h, tiles, new byte[n], new bool[n], new byte[n],
                new[] { new Vector2Int(w - 1, 2) },
                new[] { new Vector2Int(0, 2), new Vector2Int(1, 2) },
                seed: seed, version: 1);
            return doc;
        }

        private static void AddPoolEntry(MapDocumentPool pool, MapDocument doc)
        {
            var fi = typeof(MapDocumentPool).GetField("entries",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var list = (System.Collections.IList)fi.GetValue(pool);
            list.Add(new MapDocumentPool.Entry { document = doc, deck = null });
        }

        [Test]
        public void RegisterDevDocument_Adds_Dedupes_AndKeepsCountUntouched()
        {
            var pool = ScriptableObject.CreateInstance<MapDocumentPool>();
            var mainDoc = MakeUsableDoc(1);
            var newDoc = MakeUsableDoc(2);
            try
            {
                AddPoolEntry(pool, mainDoc);
                Assert.AreEqual(1, pool.Count);

                Assert.IsTrue(pool.EditorRegisterDevDocument(newDoc), "신규 문서 → 등록");
                Assert.AreEqual(1, pool.DevCount);
                Assert.AreEqual(newDoc, pool.GetDev(0).document);
                Assert.IsNull(pool.GetDev(0).deck, "dev 엔트리 deck null = 레거시 deck 폴백 계약");

                Assert.IsFalse(pool.EditorRegisterDevDocument(newDoc), "dev 중복 → 거부");
                Assert.IsFalse(pool.EditorRegisterDevDocument(mainDoc), "풀 본편 존재 → 거부");
                Assert.IsFalse(pool.EditorRegisterDevDocument(null), "null → 거부");
                Assert.AreEqual(1, pool.DevCount);
                Assert.AreEqual(1, pool.Count, "본편 Count 불변 — 시드 선택(seed%Count) 불가시");
            }
            finally
            {
                Object.DestroyImmediate(pool);
                Object.DestroyImmediate(mainDoc);
                Object.DestroyImmediate(newDoc);
            }
        }

        [Test]
        public void Bridge_DevIndexBeyondPool_ResolvesDevEntry()
        {
            // BattleBridgeDraftMapTests 픽스처 미러 + DevMapOverride 세이브/복원.
            int prevIndex = DevMapOverride.Index;
            bool prevEndless = DevMapOverride.Endless;

            var world = new World("MapPoolDevEntriesTests");
            var deck = ScriptableObject.CreateInstance<AttackDeck>();
            var poolDoc = MakeUsableDoc(42);
            var devDoc = MakeUsableDoc(99);
            var pool = ScriptableObject.CreateInstance<MapDocumentPool>();
            var go = new GameObject("BattleBridge_DevSlotTest");
            try
            {
                AddPoolEntry(pool, poolDoc);
                Assert.IsTrue(pool.EditorRegisterDevDocument(devDoc));

                var bridge = go.AddComponent<BattleBridge>();
                SetField(bridge, "deck", deck);
                SetField(bridge, "mapPool", pool);
                SetField(bridge, "_world", world);
                SetField(bridge, "_em", world.EntityManager);

                DevMapOverride.Endless = false;
                DevMapOverride.Index = pool.Count;   // 풀 뒤 첫 dev 슬롯

                CallPrivateMethod(bridge, "EnsureQueriesAndQueues");
                CallPrivateMethod(bridge, "BuildMapForBattle");

                Assert.IsTrue(bridge.HasGeneratedMap);
                Assert.AreEqual(99, GetGeneratedMapSeed(bridge),
                    "dev 인덱스(Count+0)는 devEntries[0] 문서로 해석되어야 한다");
            }
            finally
            {
                DevMapOverride.Index = prevIndex;
                DevMapOverride.Endless = prevEndless;
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(pool);
                Object.DestroyImmediate(poolDoc);
                Object.DestroyImmediate(devDoc);
                Object.DestroyImmediate(deck);
                world.Dispose();
            }
        }

        private static void CallPrivateMethod(object target, string name)
        {
            var mi = target.GetType().GetMethod(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"Method '{name}' not found");
            mi.Invoke(target, null);
        }

        private static void SetField(object target, string name, object value)
        {
            var type = target.GetType();
            FieldInfo fi = null;
            while (fi == null && type != null)
            {
                fi = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(fi, $"Field '{name}' not found");
            fi.SetValue(target, value);
        }

        private static int GetGeneratedMapSeed(BattleBridge bridge)
        {
            var fi = typeof(BattleBridge).GetField("_generatedMap",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return ((GeneratedMap)fi.GetValue(bridge)).seed;
        }
    }
}
