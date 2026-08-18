using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // map-diorama-stage unit 7 — 구 MapDocumentPoolDevEntriesTests 에서 이관한 스테이지 dev 슬롯
    // 해석 계약 pin: DevMapOverride 인덱스(Count+i)가 devEntries[i] 로 해석된다.
    // 은퇴한 문서 풀 테스트들의 후계 관계: «전선 너머 배치층 0» 가드(④) → DioramaMapBuilderTests 의
    // BlockZone 폐쇄 테스트(계약 3) · 시드 결정론 불가시(dev Count 미포함) → 이 테스트의 전제.
    public class StagePoolDevEntriesTests
    {
        [Test]
        public void Bridge_DevIndexBeyondPool_ResolvesDevEntry()
        {
            int prevIndex = DevMapOverride.Index;

            var world = new World("StagePoolDevEntriesTests");
            var deck = ScriptableObject.CreateInstance<AttackDeck>();
            var poolStage = MakeUsableStage("MapStage_PoolFixture", 8);
            var devStage = MakeUsableStage("MapStage_DevFixture", 9);
            var pool = ScriptableObject.CreateInstance<MapStagePool>();
            var go = new GameObject("BattleBridge_DevSlotTest");
            var bridge = go.AddComponent<BattleBridge>();
            try
            {
                AddStagePoolEntry(pool, poolStage);
                Assert.IsTrue(pool.EditorRegisterDevStage(devStage));

                SetField(bridge, "deck", deck);
                SetField(bridge, "mapPool", pool);
                SetField(bridge, "_world", world);
                SetField(bridge, "_em", world.EntityManager);

                DevMapOverride.Index = pool.Count;   // 풀 뒤 첫 dev 슬롯

                CallPrivateMethod(bridge, "EnsureQueriesAndQueues");
                CallPrivateMethod(bridge, "BuildMapForBattle");

                Assert.IsTrue(bridge.HasGeneratedMap);
                Assert.AreEqual(9, GetGeneratedMap(bridge).gridSize.x,
                    "dev 인덱스(Count+0)는 devEntries[0] 스테이지(9×6)로 해석되어야 한다");
            }
            finally
            {
                DevMapOverride.Index = prevIndex;
                CallPrivateMethod(bridge, "TeardownGeneratedMap");
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(pool);
                Object.DestroyImmediate(poolStage.gameObject);
                Object.DestroyImmediate(devStage.gameObject);
                Object.DestroyImmediate(deck);
                world.Dispose();
            }
        }

        // 스테이지 픽스처(코드 조립, 프리팹 불요). w×6 열린 마당 + 스폰 2 + 골 1.
        private static MapStage MakeUsableStage(string name, int width)
        {
            var root = new GameObject(name);
            var stage = root.AddComponent<MapStage>();
            stage.playAreaCells = new Vector2Int(width, 6);
            stage.gridOriginLocal = Vector3.zero;

            AddStageChild<SpawnMarker>(root, new Vector2Int(0, 1), m => m.laneIndex = 0);
            AddStageChild<SpawnMarker>(root, new Vector2Int(0, 4), m => m.laneIndex = 1);
            AddStageChild<GoalMarker>(root, new Vector2Int(width - 1, 3), _ => { });
            return stage;
        }

        private static void AddStageChild<T>(GameObject stageRoot, Vector2Int cell, System.Action<T> init)
            where T : Component
        {
            var child = new GameObject($"{typeof(T).Name}_{cell.x}_{cell.y}");
            child.transform.SetParent(stageRoot.transform, false);
            child.transform.localPosition = new Vector3(cell.x + 0.5f, 0f, cell.y + 0.5f);
            init(child.AddComponent<T>());
        }

        private static void AddStagePoolEntry(MapStagePool pool, MapStage stage)
        {
            var fi = typeof(MapStagePool).GetField("entries",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, "MapStagePool.entries field not found");
            var list = (System.Collections.IList)fi.GetValue(pool);
            list.Add(new MapStagePool.Entry { stage = stage, deck = null });
        }

        private static GeneratedMap GetGeneratedMap(BattleBridge bridge)
        {
            var fi = typeof(BattleBridge).GetField("_generatedMap",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, "_generatedMap field not found");
            return (GeneratedMap)fi.GetValue(bridge);
        }

        private static void CallPrivateMethod(object target, string name)
        {
            var mi = target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
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
    }
}
