using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.UI
{
    // dreamcatcher-orb-dock unit 2b — SkeletonGraphic 미니어처를 지정 애니(기본 Idle) 마지막
    // 프레임에 동결하는 공용 셋업. 항아리 피규어(JarFigurePile)와 흡수 비행 고스트
    // (AwakeningGaugeView) 가 공유(2 호출처 = 추출 정당). RectTransform 앵커/스케일은 호출처가
    // 소유(물리 좌표계 vs 화면 좌표계가 달라서). 여기선 Spine 셋업만.
    public static class SpineFigureBuilder
    {
        public static bool CanBuild(ISpineUnitVisualData data, Material mat)
            => data != null && data.SpineSkeletonDataAsset != null && mat != null;

        // 기존 SkeletonGraphic 에 스켈레톤/스킨/동결 포즈를 세팅. sizeDelta 는 rig 원본 rect.
        public static void Setup(SkeletonGraphic sg, ISpineUnitVisualData data, Material mat, string animName)
        {
            sg.material = mat;
            sg.raycastTarget = false;
            sg.skeletonDataAsset = data.SpineSkeletonDataAsset;
            sg.Initialize(true);
            if (sg.Skeleton != null)
                SpineCombinedSkinCache.Apply(sg.Skeleton, data);
            string anim = string.IsNullOrEmpty(animName) ? data.SpineDeathAnimation : animName;
            if (sg.Skeleton != null && !string.IsNullOrEmpty(anim) && sg.Skeleton.Data.FindAnimation(anim) != null)
            {
                var te = sg.AnimationState.SetAnimation(0, anim, false);
                te.TrackTime = te.AnimationEnd; // 마지막 프레임
                sg.Update(0f);                  // 포즈 확정
                sg.UpdateMesh();                // CanvasRenderer 메시 반영
            }
            sg.freeze = true; // 이후 정지(이동/회전은 transform 만, 메시 재빌드 없음)
            sg.rectTransform.sizeDelta = new Vector2(400f, 600f);
        }
    }
}
