using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // map-diorama-stage unit 6 — 공용 마커 프랍의 정본(Data/Maps/MarkerPropStyle.asset)이 채워져 있고 BattleScene 이 설치자를
    // 배선했는지. 둘 중 하나가 빠지면 모든 맵의 스폰/골이 조용히 보이지 않는다(마커는 렌더러가 없다).
    public class MarkerPropStyleAssetTests
    {
        const string StylePath = "Assets/_Project/Data/Maps/MarkerPropStyle.asset";
        const string ScenePath = "Assets/_Project/Scenes/BattleScene.unity";

        [Test]
        public void Style_HasVerticalPortalProps_ForSpawnAndGoal()
        {
            var style = AssetDatabase.LoadAssetAtPath<MarkerPropStyle>(StylePath);
            Assert.IsNotNull(style, StylePath + " 이 없다 — 러너 태스크 marker_prop_style 로 생성");
            foreach (var (slot, prop) in new[] { ("spawnProp", style.spawnProp), ("goalProp", style.goalProp) })
            {
                Assert.IsNotNull(prop, slot + " 비어 있음");
                Assert.Greater(prop.GetComponentsInChildren<ParticleSystem>(true).Length, 0, slot + " = 포탈 파티클 프리팹");
                Assert.AreEqual(Quaternion.identity, prop.transform.rotation, slot + " 루트는 identity(수직 포탈 — 사용자 결정 2026-08-27)");
                Assert.IsNull(prop.GetComponentInChildren<SpawnMarker>(true), slot + " 에 마커가 들어 있으면 스캐너가 이중 스폰으로 읽는다");
                Assert.IsNull(prop.GetComponentInChildren<GoalMarker>(true), slot + " 에 마커가 들어 있으면 스캐너가 이중 골로 읽는다");
            }
            Assert.AreNotEqual(style.spawnProp, style.goalProp, "스폰(빨강)과 골(노랑)은 다른 프리팹");
        }

        [Test]
        public void BattleScene_WiresInstallerToStyleAsset()
        {
            string guid = AssetDatabase.AssetPathToGUID(StylePath);
            Assert.IsFalse(string.IsNullOrEmpty(guid));
            string scene = File.ReadAllText(ScenePath);
            Assert.IsTrue(scene.Contains("Wassup.Presentation.MarkerPropInstaller"), "BattleScene 에 MarkerPropInstaller 가 없다");
            Assert.IsTrue(scene.Contains($"style: {{fileID: 11400000, guid: {guid}, type: 2}}"), "MarkerPropInstaller.style 이 MarkerPropStyle.asset 을 가리키지 않는다");
        }

        // 라이브 풀의 스테이지는 프랍을 내장하지 않는다(공유 구조) — 내장하면 맵마다 다른 그림이 되고 스타일 교체가 반쪽만 먹는다.
        [Test]
        public void LivePoolStages_DoNotEmbedMarkerProps()
        {
            var pool = AssetDatabase.LoadAssetAtPath<MapStagePool>("Assets/_Project/Data/Maps/MapStagePool.asset");
            Assert.IsNotNull(pool);
            for (int i = 0; i < pool.Count; i++)
            {
                var stage = pool.Get(i).stage;
                if (stage == null) continue;
                foreach (var s in stage.GetComponentsInChildren<SpawnMarker>(true))
                    Assert.IsNull(s.visualRoot, $"{stage.name} spawn lane {s.laneIndex} 가 프랍을 내장 — 공용 MarkerPropStyle 로 통일");
                foreach (var g in stage.GetComponentsInChildren<GoalMarker>(true))
                    Assert.IsNull(g.visualRoot, $"{stage.name} goal 이 프랍을 내장 — 공용 MarkerPropStyle 로 통일");
            }
        }
    }
}
