using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// spine-runtime-4-2-upgrade 검증용 임시 스크립트 — 업그레이드 종료 시 삭제.
// batchmode: -executeMethod SpineUpgradeSmoke.OpenBattleScene
public static class SpineUpgradeSmoke
{
    public static void OpenBattleScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/BattleScene.unity");
        Debug.Log($"[SMOKE] scene loaded: {scene.name}, rootCount={scene.rootCount}, isLoaded={scene.isLoaded}");
        EditorApplication.Exit(0);
    }

    // 4.2 데이터 로드 + AnimationState + 스킨 + SkeletonFlipXModifier 를 배치에서 검증.
    public static void SpinePipelineSmoke()
    {
        bool ok = true;
        var targets = new (string path, string skin, string[] anims)[]
        {
            ("Assets/_Project/Data/Defenders/Defender_Scout.asset", "full_skins", new[] { "Idle", "Attack1", "Die", "Run" }),
            ("Assets/_Project/Data/Enemies/Enemy_Vanguard.asset", "full_skins", new[] { "Walk", "Attack1", "Die" }),
        };
        foreach (var t in targets)
        {
            var so = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(t.path);
            var sda = new UnityEditor.SerializedObject(so).FindProperty("skeletonDataAsset")
                .objectReferenceValue as Spine.Unity.SkeletonDataAsset;
            if (sda == null) { Debug.LogError($"[SMOKE] {t.path}: skeletonDataAsset null"); ok = false; continue; }
            var data = sda.GetSkeletonData(false);
            if (data == null) { Debug.LogError($"[SMOKE] {t.path}: GetSkeletonData null"); ok = false; continue; }
            foreach (var a in t.anims)
                if (data.FindAnimation(a) == null) { Debug.LogError($"[SMOKE] {sda.name}: anim '{a}' missing"); ok = false; }
            if (data.FindSkin(t.skin) == null) { Debug.LogError($"[SMOKE] {sda.name}: skin '{t.skin}' missing"); ok = false; }

            var sa = Spine.Unity.SkeletonAnimation.NewSkeletonAnimationGameObject(sda);
            sa.Initialize(false);
            sa.Skeleton.SetSkin(t.skin);
            sa.Skeleton.SetSlotsToSetupPose();
            sa.AnimationState.SetAnimation(0, t.anims[0], true);
            sa.Update(0.1f);
            Debug.Log($"[SMOKE] {sda.name}: version={data.Version}, skin={t.skin}, anim={t.anims[0]} OK, ScaleX={sa.Skeleton.ScaleX}, A={sa.Skeleton.A}");
        }

        var flip = UnityEditor.AssetDatabase.LoadAssetAtPath<Wassup.Presentation.SkeletonFlipXModifier>(
            "Assets/_Project/Characters/SkeletonFlipX.asset");
        if (flip == null) { Debug.LogError("[SMOKE] SkeletonFlipX.asset load failed"); ok = false; }
        else
        {
            var sda = UnityEditor.AssetDatabase.LoadAssetAtPath<Spine.Unity.SkeletonDataAsset>(
                "Assets/Layer Lab/2D Art Maker/AMCasual Character/Demo/SpineAnimation/Casual Character_SkeletonData.asset");
            var data = sda.GetSkeletonData(false);
            flip.Apply(data);
            float rootScaleX = data.Bones.Items[0].ScaleX;
            if (rootScaleX >= 0f) { Debug.LogError($"[SMOKE] FlipX modifier no-op: rootScaleX={rootScaleX}"); ok = false; }
            else Debug.Log($"[SMOKE] FlipX modifier OK: rootScaleX={rootScaleX} (in-memory only)");
        }

        // unit-parts-appearance 1 — combined skin 합성 + 캐시 히트 검증
        {
            var sda = UnityEditor.AssetDatabase.LoadAssetAtPath<Spine.Unity.SkeletonDataAsset>(
                "Assets/Layer Lab/2D Art Maker/AMCasual Character/Demo/SpineAnimation/Casual Character_SkeletonData.asset");
            var data = sda.GetSkeletonData(false);
            var parts = new System.Collections.Generic.List<string> { "skin/skin_1", "top/top_c_1", "helmet/helmet_c_1" };
            var single = Wassup.Presentation.SpineCombinedSkinCache.GetOrBuild(data, new System.Collections.Generic.List<string> { "skin/skin_1" });
            var combined = Wassup.Presentation.SpineCombinedSkinCache.GetOrBuild(data, parts);
            var combined2 = Wassup.Presentation.SpineCombinedSkinCache.GetOrBuild(data, parts);
            int singleCount = single.Attachments.Count, combinedCount = combined.Attachments.Count;
            if (combinedCount <= singleCount) { Debug.LogError($"[SMOKE] combined({combinedCount}) <= single({singleCount})"); ok = false; }
            if (!ReferenceEquals(combined, combined2)) { Debug.LogError("[SMOKE] combined skin cache MISS on identical parts"); ok = false; }
            var missing = Wassup.Presentation.SpineCombinedSkinCache.GetOrBuild(data, new System.Collections.Generic.List<string> { "skin/skin_1", "no/such_skin" });
            if (missing.Attachments.Count != singleCount) { Debug.LogError("[SMOKE] missing-part skip mismatch"); ok = false; }
            Debug.Log($"[SMOKE] combined skin OK: single={singleCount}, combined={combinedCount}, cacheHit={ReferenceEquals(combined, combined2)}");
        }

        Debug.Log(ok ? "[SMOKE] SpinePipelineSmoke PASS" : "[SMOKE] SpinePipelineSmoke FAIL");
        EditorApplication.Exit(ok ? 0 : 1);
    }
}
