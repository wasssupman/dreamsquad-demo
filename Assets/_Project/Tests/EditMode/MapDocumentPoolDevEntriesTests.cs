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
            doc.SetFrom(w, h, tiles,
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

        // siege-duel-map 후속(라이브 풀 편입) — Duel 을 dev 슬롯에 배선한 계약 pin.
        //
        // 이 pin 이 지키는 것은 «Duel 이 존재한다» 가 아니라 **웨이브 세대가 라이브와 같다**는
        // 것이다. dev 엔트리의 deck 이 null 이면 브리지의 직렬화 `deck`(구 Deck_WaveA — 컨셉 0·
        // 보스 0·적 9종)으로 폴백해, 맵은 뜨는데 웨이브 개선이 하나도 안 붙은 판이 된다.
        // 실제로 그 상태였고 사용자가 «적용되어 있는지 모르겠다» 고 보고했다.
        // 공성 맵 3종(듀얼 계열). 셋 다 같은 제작 철학이고 같은 웨이브 세대를 써야 한다.
        private static readonly string[] SiegeMaps = { "Duel", "Ford", "Isle" };

        [Test]
        public void SiegeDevSlot_IsWiredWithCurrentGenerationDeck(
            [ValueSource(nameof(SiegeMaps))] string mapName)
        {
            var pool = UnityEditor.AssetDatabase.LoadAssetAtPath<MapDocumentPool>(
                "Assets/_Project/Data/Maps/MapDocumentPool.asset");
            var duelDoc = UnityEditor.AssetDatabase.LoadAssetAtPath<MapDocument>(
                $"Assets/_Project/Data/Maps/MapDocument_{mapName}.asset");
            var live = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackDeck>(
                "Assets/_Project/Scripts/Data/Decks/Deck_Serpent.asset");
            Assert.IsNotNull(pool); Assert.IsNotNull(duelDoc); Assert.IsNotNull(live);

            AttackDeck duelDeck = null;
            for (int i = 0; i < pool.DevCount; i++)
                if (pool.GetDev(i).document == duelDoc) duelDeck = pool.GetDev(i).deck;

            Assert.IsNotNull(duelDeck,
                $"{mapName} 이 devEntries 에 없거나 deck 이 비었다 — deck 이 null 이면 구 Deck_WaveA 로 "
                + "폴백해 컨셉·보스·엘리트가 전부 빠진 판이 된다");

            // 본편 Count 불변 = 시드/토너먼트 맵 선택에 Duel 이 불가시.
            Assert.AreEqual(6, pool.Count, "dev 슬롯 추가가 본편 Count 를 건드리면 시드 결정론이 바뀐다");

            Assert.IsTrue(duelDeck.useGeneratedWaves, "생성 웨이브를 써야 컨셉 블록이 돈다");
            Assert.AreEqual(live.waveGeneratorVersion, duelDeck.waveGeneratorVersion,
                "라이브 덱과 같은 생성기 세대여야 한다 — 갈리면 개선이 한쪽에만 붙는다");
            Assert.AreEqual(live.attackUnitPool.Length, duelDeck.attackUnitPool.Length,
                "적 풀 크기가 라이브와 다르다 — 비행·엘리트가 빠졌을 수 있다");
            Assert.AreEqual(5, duelDeck.waveConceptPool.Length, "컨셉 5종");
            Assert.AreEqual(3, duelDeck.conceptHoldWaves);
            Assert.AreEqual(live.bossWaveInterval, duelDeck.bossWaveInterval);
            Assert.AreEqual(1, duelDeck.bossPool.Length, "맵당 보스 1종");

            // 공성 맵이라 레인 경로 축은 적용되지 않는다(파생 스폰). 저작하지 않은 것이 계약이다.
            Assert.IsTrue(duelDoc.SpawnRoutes == null || duelDoc.SpawnRoutes.Count == 0,
                "공성 문서에 spawnRoutes 를 저작하면 파생 스폰과 길이가 갈린다 — 투영이 버리지만 "
                + "저작 자체를 하지 않는 것이 계약이다");
        }

        // siege-duel-map — **제작 철학을 회귀선으로 박는다.** 공성 맵이 늘어날 때 좌표만 베끼고
        // 원칙을 놓치는 것을 막는 것이 목적이다. Duel 에서 뽑은 원칙:
        //   ① 적 마음 정확히 1기 = 공성 모드 파생 조건  ② spawns 저작 0(파생이 채운다)
        //   ③ 마음이 좌우 미러 = HP 레이스가 공평  ④ 배치는 내 진영만 = 전선이 지형에 고정
        //   ⑤ 강(Env)이 지상을 가른다 = 「비행은 강을 무시한다」가 규칙으로 읽힘
        //   ⑥ 21×12 = 카메라 불변 상한  ⑦ 적 마음 → 골 연결
        [Test]
        public void SiegeMap_KeepsDuelDesignPhilosophy(
            [ValueSource(nameof(SiegeMaps))] string mapName)
        {
            var doc = UnityEditor.AssetDatabase.LoadAssetAtPath<MapDocument>(
                $"Assets/_Project/Data/Maps/MapDocument_{mapName}.asset");
            Assert.IsNotNull(doc);

            Assert.AreEqual(21, doc.Width, "⑥ 카메라 불변 상한");
            Assert.AreEqual(12, doc.Height, "⑥ 카메라 불변 상한");

            // ② 저작 스폰 0 — 적 마음 셀이 스폰이 된다(battle-structures unit 6 파생).
            Assert.IsTrue(doc.Spawns == null || doc.Spawns.Count == 0,
                "② 공성 문서는 spawns 를 저작하지 않는다 — 저작하면 파생이 그것을 덮어 «저작해도 " +
                "아무 일이 없는 필드» 가 된다");

            // ① 적 마음 정확히 1기.
            int enemyCores = StructureAuthoringRules.CountEnemyCores(doc.Structures);
            Assert.AreEqual(1, enemyCores, "① 적 마음 1기가 곧 공성 모드다");
            Assert.AreEqual(MapMode.Siege, StructureAuthoringRules.DeriveMode(enemyCores));

            // ③ 마음이 좌우 미러 — goal.x + heart.x == Width-1, 같은 행.
            Vector2Int goal = doc.Goals[0];
            Vector2Int heart = default; bool found = false;
            foreach (var s in doc.Structures)
                if (s.data != null && StructurePlacements.DeriveFaction(s.side, s.data.kind)
                    == Wassup.Battle.Units.Faction.EnemyCore) { heart = s.cell; found = true; }
            Assert.IsTrue(found);
            Assert.AreEqual(doc.Width - 1, goal.x + heart.x,
                "③ 마음이 좌우 미러가 아니면 HP 레이스가 불공평해진다");
            Assert.AreEqual(goal.y, heart.y, "③ 같은 행이어야 미러다");

            // ④ 배치 가능 칸이 전부 내 진영(미러축 왼쪽)에 있다.
            var mask = doc.PlaceMask;
            Assert.IsNotNull(mask); Assert.AreEqual(doc.Width * doc.Height, mask.Count,
                "placeMask 를 손저작해야 한다 — 파생 폴백은 Walk→Path 만 열어 Ground 배치칸이 0 이 된다");
            int placeable = 0, wrongSide = 0;
            for (int i = 0; i < mask.Count; i++)
            {
                if ((mask[i] & (byte)PlacementLayer.Ground) == 0) continue;
                placeable++;
                if (i % doc.Width >= doc.Width / 2) wrongSide++;
            }
            Assert.Greater(placeable, 40, "④ 배치칸이 너무 적으면 판이 성립하지 않는다");
            Assert.AreEqual(0, wrongSide, "④ 적 진영·중립 지대에 배치칸이 열려 있다 — 전선이 무너진다");

            // ⑤ 강(Env)이 존재하고, 그 열이 부분만 열려 지상 전선을 만든다.
            int env = 0;
            for (int i = 0; i < doc.Tiles.Count; i++) if (doc.Tiles[i] == MapTileType.Env) env++;
            Assert.Greater(env, 0,
                "⑤ 강이 없으면 「비행은 강을 무시한다」는 규칙이 이 맵에서 안 읽힌다");

            // ⑦ 적 마음 → 골 연결 — 프로덕션 판정 함수를 그대로 쓴다(재구현 금지).
            var map = MapDocumentBuilder.ToGeneratedMap(doc, Unity.Collections.Allocator.Temp);
            try
            {
                Assert.AreEqual(1, map.spawns.Length, "② 파생 스폰은 적 마음 1기다");
                Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(map), "⑦ 적 마음에서 골까지 못 간다");
            }
            finally { map.Dispose(); }
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
