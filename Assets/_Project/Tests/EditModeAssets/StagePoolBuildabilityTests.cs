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

            // 2026-08-26 사건의 회귀 가드 — 라이브 엔트리 deck 이 null 이면 BattleBridge 가 인스펙터 폴백 덱으로 **조용히**
            // 떨어져(ActiveDeck), 그 판의 웨이브가 어느 세대인지 로그 한 줄로만 보인다. 시드 로테이션에 잡히는 라이브는
            // 덱을 명시해야 한다(dev 슬롯은 실험장이라 null 허용 — 등록 버튼이 entries[0].deck 을 물려준다).
            for (int i = 0; i < pool.Count; i++)
                Assert.IsNotNull(pool.Get(i).deck,
                    $"{pool.Get(i).stage.name}: 라이브 엔트리 deck 이 비었다 — 레거시 폴백 덱으로 조용히 떨어진다. 풀에서 덱을 짝지을 것.");

            // 카메라 불변 상한 (구 MapDocumentPoolDevEntriesTests ⑥ 승계). fitToBoard 는 어떤 크기든
            // 프레임에 넣지만 진짜 상한은 폰에서 읽히는 판 크기다. 가로 30 = 2026-08-26 사용자가
            // 16:9 게임뷰에서 확정한 Street/Subway/StreetDay(30폭) 라이브 승격 기준 — 이보다 넓히려면
            // 전투 카메라(pitch 55·fov 클램프 31)에서 판이 더 작아지는 걸 감수하는 결정이 먼저다.
            // 시드 로테이션에 잡히는 라이브 엔트리만 구속한다(dev 슬롯은 실험장).
            for (int i = 0; i < pool.Count; i++)
            {
                var cells = pool.Get(i).stage.playAreaCells;
                Assert.LessOrEqual(cells.x, 30, $"{pool.Get(i).stage.name}: 가로 상한 — 더 넓히면 폰에서 판이 작아진다");
                Assert.LessOrEqual(cells.y, 12, $"{pool.Get(i).stage.name}: 세로 상한 — 배치·전투 두 카메라 상태를 같이 물린다");
            }

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
