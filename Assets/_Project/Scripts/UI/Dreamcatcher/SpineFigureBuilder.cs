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

        // SkeletonGraphic + SkeletonAnimation 쌍을 만들고 스킨/동결 포즈를 세팅.
        // sizeDelta 는 rig 원본 rect.
        public static SkeletonGraphic Build(GameObject go, ISpineUnitVisualData data, Material mat, string animName)
        {
            var components = SkeletonGraphic.AddSkeletonGraphicAnimationComponents(
                go, data.SpineSkeletonDataAsset, mat);
            var sg = components.skeletonRenderer;
            var skeletonAnimation = components.skeletonAnimation;

            sg.allowMultipleCanvasRenderers = true;
            sg.raycastTarget = false;
            if (sg.Skeleton != null)
                SpineCombinedSkinCache.Apply(sg.Skeleton, data);
            string anim = string.IsNullOrEmpty(animName) ? data.SpineDeathAnimation : animName;
            if (sg.Skeleton != null && !string.IsNullOrEmpty(anim) && sg.Skeleton.Data.FindAnimation(anim) != null)
            {
                var te = skeletonAnimation.AnimationState.SetAnimation(0, anim, false);
                te.TrackTime = te.AnimationEnd; // 마지막 프레임
                skeletonAnimation.Update(0f);   // 포즈 확정
                sg.UpdateMesh();                // CanvasRenderer 메시 반영
            }
            skeletonAnimation.enabled = false; // 동결 포즈는 더 이상 애니메이션 갱신 불필요
            sg.freeze = true; // 이후 정지(이동/회전은 transform 만, 메시 재빌드 없음)
            sg.rectTransform.sizeDelta = new Vector2(400f, 600f);
            return sg;
        }

        // dreamcatcher-orb-dock unit 6 — 이미 만들어진(동결된) 미니어처를 다른 유닛 스킨으로
        // 교체(킬 각성 피규어 = 죽은 적 스킨). **같은 스켈레톤 전제** — 적은 전부 한 스켈레톤을
        // 공유하므로 스킨만 갈아끼운다. 스켈레톤이 다르면(예: 디펜더가 다른 rig) 스킵해 기존
        // 대표 스킨을 유지(스킨 미존재 예외 회피). 동결 해제→스킨 적용→메시 갱신→재동결.
        public static void Reskin(SkeletonGraphic sg, ISpineUnitVisualData data)
        {
            if (sg == null || data == null || sg.Skeleton == null) return;
            if (data.SpineSkeletonDataAsset == null || sg.skeletonDataAsset != data.SpineSkeletonDataAsset) return;
            SpineCombinedSkinCache.Apply(sg.Skeleton, data);
            bool wasFrozen = sg.freeze;
            sg.freeze = false;
            if (sg.Animation is SkeletonAnimation skeletonAnimation)
                skeletonAnimation.Update(0f);
            sg.UpdateMesh();
            sg.freeze = wasFrozen;
        }
    }
}
