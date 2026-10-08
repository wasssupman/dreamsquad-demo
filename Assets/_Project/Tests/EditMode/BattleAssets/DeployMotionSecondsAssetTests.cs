using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using Somnia.Battle.Data;
using Somnia.Battle.Presentation;

namespace Somnia.Battle.Tests.EditModeAssets
{
    // defender-deploy-phase unit 0 — 실에셋 불변식. 정확한 초는 못박지 않는다(리그 재수출마다 바뀐다 — 표는 spec README).
    // 단언하는 것: ① 명시 배치 슬롯이 있으면 > 0, 없으면 0 ② 값 = 실제로 재생될 트랙/시트의 길이(길이의 출처 = 재생의 출처).
    public class DeployMotionSecondsAssetTests
    {
        private static IEnumerable<DefenderUnitData> AllDefenders()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:DefenderUnitData", new[] { "Assets/_Project/Runtime/Battle/Data/Defenders" }))
            {
                var u = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(AssetDatabase.GUIDToAssetPath(guid));
                if (u != null) yield return u;
            }
        }

        [Test]
        public void EveryDefender_DeployMotionSeconds_MatchesAuthoredMotion()
        {
            int checkedUnits = 0;
            foreach (var u in AllDefenders())
            {
                float seconds = u.DeployMotionSeconds;
                var sm = u.SpriteMotions;
                if (sm != null && sm.HasIdle)
                {
                    float expected = sm.Deploy != null ? FlipbookMath.Duration(sm.Deploy.Fps, sm.Deploy.FrameCount) : 0f;
                    Assert.AreEqual(expected, seconds, 1e-5f, $"{u.name}: 시트 유닛은 deploy 시트 길이");
                    if (sm.Deploy != null) Assert.Greater(seconds, 0f, $"{u.name}: deploy 시트가 있는데 길이 0");
                }
                else if (u.skeletonDataAsset != null && !string.IsNullOrEmpty(u.deployAnimation))
                {
                    var anim = u.skeletonDataAsset.GetSkeletonData(true)?.FindAnimation(u.deployAnimation);
                    Assert.IsNotNull(anim, $"{u.name}: deployAnimation '{u.deployAnimation}' 트랙이 리그에 없다(저작 오류)");
                    Assert.AreEqual(anim.Duration, seconds, 1e-5f, $"{u.name}: Spine 트랙 길이");
                    Assert.Greater(seconds, 0f, $"{u.name}: 배치 트랙이 있는데 길이 0");
                }
                else
                {
                    Assert.AreEqual(0f, seconds, $"{u.name}: 배치 모션 없음 = 0");
                }
                checkedUnits++;
            }
            Assert.Greater(checkedUnits, 20, "방어유닛 에셋이 로드되지 않았다");
        }
    }
}
