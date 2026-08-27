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

        // 씬 파일 정본 검사 — 설치자 컴포넌트가 있고(켜져 있고), 그 오브젝트가 활성이며, style 이 스타일 에셋을 가리킨다.
        // 11400000 은 에셋 .meta 의 mainObjectFileID 로 고정, guid 는 경로에서 파생 — 이동해도 살아 있다.
        [Test]
        public void BattleScene_WiresInstallerToStyleAsset()
        {
            string guid = AssetDatabase.AssetPathToGUID(StylePath);
            Assert.IsFalse(string.IsNullOrEmpty(guid));
            string scene = File.ReadAllText(ScenePath);

            string mb = YamlBlockContaining(scene, "Wassup.Presentation.MarkerPropInstaller");
            Assert.IsNotNull(mb, "BattleScene 에 MarkerPropInstaller 가 없다");
            Assert.IsTrue(mb.Contains("m_Enabled: 1"), "MarkerPropInstaller 가 꺼져 있다");
            Assert.IsTrue(mb.Contains($"style: {{fileID: 11400000, guid: {guid}, type: 2}}"), "MarkerPropInstaller.style 이 MarkerPropStyle.asset 을 가리키지 않는다");

            string go = YamlBlockContaining(scene, "m_Name: _MarkerProps");
            Assert.IsNotNull(go, "BattleScene 에 _MarkerProps 오브젝트가 없다");
            Assert.IsTrue(go.Contains("m_IsActive: 1"), "_MarkerProps 가 비활성 — 설치자 OnEnable 이 돌지 않는다");
        }

        // 라이브 풀의 스테이지는 **공용 프랍을 내장하지 않는다** — 내장하면 스타일 교체가 그 맵엔 반쪽만 먹는다.
        // 맵 전용 저작(다른 프리팹을 visualRoot 로 채움)은 계약상 허용 — 설치자가 건너뛴다.
        [Test]
        public void LivePoolStages_DoNotEmbedSharedProps()
        {
            var style = AssetDatabase.LoadAssetAtPath<MarkerPropStyle>(StylePath);
            var pool = AssetDatabase.LoadAssetAtPath<MapStagePool>("Assets/_Project/Data/Maps/MapStagePool.asset");
            Assert.IsNotNull(style); Assert.IsNotNull(pool);
            for (int i = 0; i < pool.Count; i++)
            {
                var stage = pool.Get(i).stage;
                if (stage == null) continue;
                foreach (var s in stage.GetComponentsInChildren<SpawnMarker>(true))
                    AssertNotSharedProp(stage.name, $"spawn lane {s.laneIndex}", s.visualRoot, style.spawnProp);
                foreach (var g in stage.GetComponentsInChildren<GoalMarker>(true))
                    AssertNotSharedProp(stage.name, "goal", g.visualRoot, style.goalProp);
            }
        }

        static void AssertNotSharedProp(string stageName, string marker, Transform visualRoot, GameObject sharedProp)
        {
            if (visualRoot == null || sharedProp == null) return;
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(visualRoot.gameObject);
            bool embedded = source == sharedProp || visualRoot.name.StartsWith(sharedProp.name);
            Assert.IsFalse(embedded, $"{stageName} {marker} 가 공용 프랍 {sharedProp.name} 을 내장 — 프리팹에서 빼고 MarkerPropStyle 에 맡길 것(맵 전용 프랍은 허용)");
        }

        // "--- !u!" 로 시작하는 YAML 오브젝트 블록 중 needle 을 포함하는 첫 블록.
        static string YamlBlockContaining(string yaml, string needle)
        {
            int at = yaml.IndexOf(needle, System.StringComparison.Ordinal);
            if (at < 0) return null;
            int start = yaml.LastIndexOf("--- !u!", at, System.StringComparison.Ordinal);
            int end = yaml.IndexOf("--- !u!", at, System.StringComparison.Ordinal);
            return yaml.Substring(start, (end < 0 ? yaml.Length : end) - start);
        }
    }
}
