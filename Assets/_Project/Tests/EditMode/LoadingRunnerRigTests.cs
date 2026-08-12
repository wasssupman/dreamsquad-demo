using NUnit.Framework;
using UnityEditor;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // summon-patrol-defender unit 8 후속 — 로딩 화면 러너가 리그를 가리지 않는지.
    //
    // 증상(사용자 2026-08-12): 아웃게임에서 게임 시작을 누르면 **간헐적으로**
    // `ArgumentException: Skin not found: full_skins` 로 씬 전환이 실패했다.
    //
    // 원인: SceneTransition 이 «모든 스쿼드 유닛이 Casual Character 리그를 공유한다»고 전제했다.
    // 러너 SkeletonGraphic 에 저작된 initialSkinName('full_skins')을 새 스켈레톤에 그대로
    // 적용하는데, unit 8 이 들여온 고유 리그(CH1)엔 그 스킨이 없다. 간헐적이었던 이유는
    // 러너 3인이 스쿼드에서 **무작위로** 뽑히기 때문(Fisher–Yates).
    // 같은 전제가 애니에도 있었다 — `loadingAnimation = "Run"` 은 CH1 에 없고
    // AnimationState.SetAnimation(string) 은 없는 애니에 예외를 던진다.
    //
    // 이 테스트는 카탈로그 전 유닛을 실제 SkeletonDataAsset 으로 검사한다 —
    // 고유 리그가 하나 더 들어와도 여기서 잡힌다.
    public class LoadingRunnerRigTests
    {
        private const string CatalogPath = "Assets/_Project/Data/DefenderCatalog.asset";
        private const string AuthoredRunnerAnimation = "Run";   // SceneTransition.loadingAnimation 기본값

        private static DefenderCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "DefenderCatalog 이 " + CatalogPath + " 에 있다");
            return catalog;
        }

        [Test]
        public void EveryCatalogUnit_ResolvesARunnerAnimation()
        {
            var catalog = LoadCatalog();
            int checkedCount = 0;
            foreach (var unit in catalog.units)
            {
                if (unit == null || unit.SpineSkeletonDataAsset == null) continue;
                var data = unit.SpineSkeletonDataAsset.GetSkeletonData(false);
                Assert.IsNotNull(data, unit.name + ": SkeletonData 로드");

                string resolved = SceneTransition.ResolveRunnerAnimation(data, AuthoredRunnerAnimation, unit);
                Assert.IsFalse(string.IsNullOrEmpty(resolved),
                    unit.name + ": 로딩 러너로 재생할 애니가 하나도 없다 — 씬 전환이 setup pose 로 굳는다");
                Assert.IsNotNull(data.FindAnimation(resolved),
                    unit.name + ": 리졸브된 '" + resolved + "' 가 실제로 그 스켈레톤에 있다");
                checkedCount++;
            }
            Assert.Greater(checkedCount, 0, "검사한 유닛이 있다");
        }

        [Test]
        public void EveryCatalogUnit_InitialSkinIsSafeForItsOwnRig()
        {
            // SceneTransition.ApplyRunnerSkin 이 세우는 값과 같은 규칙(SpineUnitView.Spawn 관용구).
            // 'default' 는 spine 의 AssignInitialSkin 이 건너뛰므로 항상 안전하고,
            // 그 외 이름은 **그 유닛의 스켈레톤에 실재해야** SetSkin 이 던지지 않는다.
            var catalog = LoadCatalog();
            foreach (var unit in catalog.units)
            {
                if (unit == null || unit.SpineSkeletonDataAsset == null) continue;
                var data = unit.SpineSkeletonDataAsset.GetSkeletonData(false);
                if (data == null) continue;

                string initial = string.IsNullOrEmpty(unit.SpineSkinName) ? "default" : unit.SpineSkinName;
                if (initial == "default") continue;   // spine 이 건너뛴다
                Assert.IsNotNull(data.FindSkin(initial),
                    unit.name + ": 저작된 스킨 '" + initial + "' 이 자기 스켈레톤에 없다 — " +
                    "러너 Initialize 에서 ArgumentException 이 난다");
            }
        }
    }
}
