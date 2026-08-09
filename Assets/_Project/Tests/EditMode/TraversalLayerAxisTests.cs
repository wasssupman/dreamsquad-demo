using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // traversal-layers unit 2 — 유닛 통행 층 축.
    //
    // 이 unit 은 «값이 흐르는 것»까지다. 소비자는 unit 3 이므로 여기서 검증할 것은
    // 폴백이 **현행을 정확히 재현**하는가 하나다.
    public class TraversalLayerAxisTests
    {
        [Test]
        public void Enemy_Unauthored_FallsBackToPath()
        {
            // 저작 전 = 현행. 적은 지금 walkMask(= Walk 칸 = Path 층)에서만 움직인다.
            var so = ScriptableObject.CreateInstance<AttackUnitData>();
            try
            {
                Assert.AreEqual(PlacementLayer.None, so.traversalLayers, "이니셜라이저는 미지정");
                Assert.AreEqual(PlacementLayer.Path, so.EffectiveTraversalLayers);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Defender_Unauthored_FallsBackToPath_NotItsPlacementLayer()
        {
            // ★ 이 spec 에서 가장 위험했던 지점(README §4).
            //
            // 폴백을 «방어유닛 = 자기 placementLayers» 로 두면 순찰 소환물이 Ground 로
            // 떨어진다 — 그런데 소환물의 placementLayers 는 **이니셜라이저 잔여값**이고
            // 실제로는 Walk 층에서 돈다(앵커가 TryGetNearestWalkCell 로 Walk 셀에 스냅).
            // Ground 로 폴백하면 앵커가 자기 마스크 밖 → 영역 마스크 전부 0 → 순찰병이 굳는다.
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            try
            {
                Assert.AreEqual(PlacementLayer.Ground, so.EffectivePlacementLayers,
                    "배치 축은 그대로 Ground");
                Assert.AreEqual(PlacementLayer.Path, so.EffectiveTraversalLayers,
                    "통행 축은 Path — 두 축이 갈린다는 것이 이 unit 의 요점");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Authored_WinsOverFallback()
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            try
            {
                so.traversalLayers = PlacementLayer.Ground;
                Assert.AreEqual(PlacementLayer.Ground, so.EffectiveTraversalLayers);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void TwoAxes_AreIndependent()
        {
            // 배치와 통행이 서로를 안 건드린다 — README §0 회귀의 재발 방지축.
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            try
            {
                so.placementLayers = PlacementLayer.Path;      // 경로 칸에 설 수 있다
                so.traversalLayers = PlacementLayer.Ground;    // 지면만 지난다
                Assert.AreEqual(PlacementLayer.Path, so.EffectivePlacementLayers);
                Assert.AreEqual(PlacementLayer.Ground, so.EffectiveTraversalLayers);
            }
            finally { Object.DestroyImmediate(so); }
        }
    }
}
