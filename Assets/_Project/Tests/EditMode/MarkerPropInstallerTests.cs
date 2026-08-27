using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    // map-diorama-stage unit 6 — 공용 마커 프랍 규칙: visualRoot 가 빈 마커에만, 호스트 밑 identity, visualRoot 등록, 멱등.
    // 프리팹이 직접 채운 visualRoot(맵 전용 연출)는 건드리지 않는다.
    public class MarkerPropInstallerTests
    {
        GameObject _stageGo, _spawnProp, _goalProp;
        MarkerPropStyle _style;

        [SetUp]
        public void SetUp()
        {
            _stageGo = new GameObject("stage");
            _stageGo.AddComponent<MapStage>();
            _spawnProp = new GameObject("SpawnProp");
            _goalProp = new GameObject("GoalProp");
            _style = ScriptableObject.CreateInstance<MarkerPropStyle>();
            _style.spawnProp = _spawnProp;
            _style.goalProp = _goalProp;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_stageGo);
            Object.DestroyImmediate(_spawnProp);
            Object.DestroyImmediate(_goalProp);
            Object.DestroyImmediate(_style);
        }

        SpawnMarker Spawn(int lane, Vector3 pos)
        {
            var go = new GameObject($"spawn{lane}");
            go.transform.SetParent(_stageGo.transform, false);
            go.transform.localPosition = pos;
            var m = go.AddComponent<SpawnMarker>(); m.laneIndex = lane; return m;
        }

        GoalMarker Goal(Vector3 pos)
        {
            var go = new GameObject("goal");
            go.transform.SetParent(_stageGo.transform, false);
            go.transform.localPosition = pos;
            return go.AddComponent<GoalMarker>();
        }

        [Test]
        public void Apply_FillsEmptyMarkers_AsChildAtIdentity()
        {
            var s0 = Spawn(0, new Vector3(20.5f, 0f, 3.5f));
            var s1 = Spawn(1, new Vector3(20.5f, 0f, 5.5f));
            var g = Goal(new Vector3(2.5f, 0f, 4.5f));

            int n = MarkerPropInstaller.Apply(_stageGo.GetComponent<MapStage>(), _style);

            Assert.AreEqual(3, n);
            foreach (var (marker, root, prop) in new[] { (s0.transform, s0.visualRoot, "SpawnProp"), (s1.transform, s1.visualRoot, "SpawnProp"), (g.transform, g.visualRoot, "GoalProp") })
            {
                Assert.IsNotNull(root, marker.name);
                Assert.AreEqual(marker, root.parent, "프랍은 마커의 자식");
                Assert.AreEqual(Vector3.zero, root.localPosition, "호스트 = 셀 중심이라 프랍은 로컬 0");
                Assert.AreEqual(Quaternion.identity, root.localRotation, "수직 포탈 = identity");
                Assert.IsTrue(root.name.StartsWith(prop), $"{marker.name} 에 {prop} 가 아니라 {root.name}");
            }
        }

        [Test]
        public void Apply_RespectsAuthoredVisualRoot_AndIsIdempotent()
        {
            var s0 = Spawn(0, Vector3.zero);
            var custom = new GameObject("custom");
            custom.transform.SetParent(s0.transform, false);
            s0.visualRoot = custom.transform;
            var g = Goal(Vector3.one);

            Assert.AreEqual(1, MarkerPropInstaller.Apply(_stageGo.GetComponent<MapStage>(), _style), "빈 마커(골)만");
            Assert.AreEqual(custom.transform, s0.visualRoot, "맵 전용 연출은 건드리지 않는다");
            Assert.AreEqual(0, MarkerPropInstaller.Apply(_stageGo.GetComponent<MapStage>(), _style), "두 번째 호출은 0");
            Assert.AreEqual(1, g.transform.childCount, "골 밑 프랍은 하나");
        }

        [Test]
        public void Apply_SkipsNullSlots()
        {
            var s0 = Spawn(0, Vector3.zero);
            var g = Goal(Vector3.one);
            _style.spawnProp = null;

            Assert.AreEqual(1, MarkerPropInstaller.Apply(_stageGo.GetComponent<MapStage>(), _style));
            Assert.IsNull(s0.visualRoot);
            Assert.IsNotNull(g.visualRoot);
        }
    }
}
