using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // 풀에 등록된 모든 스테이지(라이브+dev)가 스캔→조립(형식 검증 포함)→연결성을 통과하는지.
    // 조립 실패는 런타임 하드 실패(map-diorama-stage 계약 9)라 Assets lane 에서 선제 차단한다.
    public class StagePoolBuildabilityTests
    {
        const string PoolPath = "Assets/_Project/Data/Maps/MapStagePool.asset";

        [Test]
        public void AllPoolStages_ScanAssembleAndConnect()
        {
            var pool = AssetDatabase.LoadAssetAtPath<MapStagePool>(PoolPath);
            Assert.IsNotNull(pool, $"{PoolPath} 로드 실패");

            var stages = new List<MapStage>();
            for (int i = 0; i < pool.Count; i++) stages.Add(pool.Get(i).stage);
            for (int i = 0; i < pool.DevCount; i++) stages.Add(pool.GetDev(i).stage);
            Assert.IsTrue(stages.TrueForAll(s => s != null), "풀에 빈 스테이지 슬롯이 있다.");

            foreach (var prefab in stages)
            {
                // 브리지와 동일 경로: 인스턴스 스캔 → 조립(내부 Validate, 실패 시 throw) → 연결성.
                var instance = Object.Instantiate(prefab);
                try
                {
                    var scan = MapStageScanner.Scan(instance, 1f);
                    var map = DioramaMapBuilder.Assemble(scan, Allocator.Temp);
                    try
                    {
                        Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(map),
                            $"{prefab.name}: 스폰→골 연결성 실패 — 차단 프랍 배치를 확인할 것.");
                    }
                    finally { map.Dispose(); }
                }
                finally { Object.DestroyImmediate(instance.gameObject); }
            }
        }
    }
}
