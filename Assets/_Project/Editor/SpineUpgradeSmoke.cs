using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Spine 파이프라인 배치 검증 하네스. spine-runtime-4-2-upgrade 에서 시작해
// unit-parts-appearance 의 상시 스모크로 승격 (합성/캐시/validator/임포터 resolve/16종 시안).
// batchmode: -executeMethod SpineUpgradeSmoke.SpinePipelineSmoke (씬 로드만: OpenBattleScene)
// 에디터가 열려 있으면 격리 리그(worktree + Library CoW 클론, lessons 02)에서 실행할 것.
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
            ("Assets/_Project/Data/Defenders/Defender_Scout.asset", "full_skins", new[] { "Idle", "Attack3", "Die", "Attack1", "Hit" }),
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

        // unit-parts-appearance 2 — validator 규칙 검증 (인스펙터와 같은 코어)
        {
            var sda = UnityEditor.AssetDatabase.LoadAssetAtPath<Spine.Unity.SkeletonDataAsset>(
                "Assets/Layer Lab/2D Art Maker/AMCasual Character/Demo/SpineAnimation/Casual Character_SkeletonData.asset");
            var probe = ScriptableObject.CreateInstance<Wassup.Data.DefenderUnitData>();
            probe.skeletonDataAsset = sda;
            probe.partSkins.AddRange(new[] { "top/top_c_1", "top/top_c_2", "helmet/helmet_c_1", "hair_short/hair_short_c_1", "no/skin" });
            probe.slotColors.Add(new Wassup.Data.SpineSlotColor { slotName = "eye", color = Color.red });
            var warnings = Wassup.Editor.UnitVisualDataValidator.CollectWarnings(probe);
            // 기대 5건: 없는 스킨(no/skin), top 중복, 본체(skin) 누락, helmet+hair_short 배타, eye 색 키잉
            if (warnings.Count != 5)
            {
                Debug.LogError($"[SMOKE] validator expected 5 warnings, got {warnings.Count}:\n - " + string.Join("\n - ", warnings));
                ok = false;
            }
            var keyed = Wassup.Editor.UnitVisualDataValidator.CollectColorKeyedSlots(sda.GetSkeletonData(false));
            if (!keyed.Contains("eye")) { Debug.LogError("[SMOKE] color-keyed slot detection missed 'eye'"); ok = false; }
            Object.DestroyImmediate(probe);
            Debug.Log($"[SMOKE] validator OK: warnings={warnings.Count}, keyedSlots=[{string.Join(",", keyed)}]");
        }

        // unit-parts-appearance 3 — 임포터 resolve 규칙 검증 (helmet/hair 배타 + 색 확장 + validator 왕복)
        {
            var sda = UnityEditor.AssetDatabase.LoadAssetAtPath<Spine.Unity.SkeletonDataAsset>(
                "Assets/Layer Lab/2D Art Maker/AMCasual Character/Demo/SpineAnimation/Casual Character_SkeletonData.asset");
            var data = sda.GetSkeletonData(false);
            var allTypes = (LayerLab.ArtMaker.PartsType[])System.Enum.GetValues(typeof(LayerLab.ArtMaker.PartsType));

            // 시나리오 A: 헬멧 착용 (전 카테고리 index 0 — Layer Lab 이 Hair_Hat 을 미러 저장하는 형태)
            var equipped = new System.Collections.Generic.Dictionary<LayerLab.ArtMaker.PartsType, (int, bool)>();
            foreach (var t in allTypes) if (t != LayerLab.ArtMaker.PartsType.None) equipped[t] = (0, false);
            var issuesA = new System.Collections.Generic.List<string>();
            var partsA = Wassup.Editor.LayerLabPresetImporter.ResolveParts(data, equipped, issuesA);
            bool aOk = partsA.Count == 15 && issuesA.Count == 0
                && partsA.Exists(p => p.StartsWith("hair_hat/")) && !partsA.Exists(p => p.StartsWith("hair_short/"))
                && partsA.Contains("skin/skin_1");
            if (!aOk) { Debug.LogError($"[SMOKE] resolve A(helmet) FAIL: n={partsA.Count}, issues={issuesA.Count}, parts=[{string.Join(",", partsA)}]"); ok = false; }

            // 시나리오 B: 헬멧 미착용 (helmet=-1) → hair_short 포함, hair_hat/helmet 제외
            var bare = new System.Collections.Generic.Dictionary<LayerLab.ArtMaker.PartsType, (int, bool)>(equipped);
            bare[LayerLab.ArtMaker.PartsType.Helmet] = (-1, false);
            var issuesB = new System.Collections.Generic.List<string>();
            var partsB = Wassup.Editor.LayerLabPresetImporter.ResolveParts(data, bare, issuesB);
            bool bOk = partsB.Count == 14 && partsB.Exists(p => p.StartsWith("hair_short/")) && !partsB.Exists(p => p.StartsWith("hair_hat/"));
            if (!bOk) { Debug.LogError($"[SMOKE] resolve B(bare) FAIL: n={partsB.Count}, parts=[{string.Join(",", partsB)}]"); ok = false; }

            // 색 확장: body+hair 논리 키 → 9슬롯 (6+3), setup 동일 색은 스킵됨
            var logical = new System.Collections.Generic.Dictionary<string, Color> { { "body", Color.red }, { "hair", Color.blue } };
            var issuesC = new System.Collections.Generic.List<string>();
            var colors = Wassup.Editor.LayerLabPresetImporter.ResolveColors(data, logical, issuesC);
            if (colors.Count != 9) { Debug.LogError($"[SMOKE] color expansion expected 9, got {colors.Count}: [{string.Join(",", colors.ConvertAll(c => c.slotName))}]"); ok = false; }

            // validator 왕복: resolve 결과는 무경고여야 함
            var probe2 = ScriptableObject.CreateInstance<Wassup.Data.DefenderUnitData>();
            probe2.skeletonDataAsset = sda;
            probe2.partSkins.AddRange(partsA);
            probe2.slotColors.AddRange(colors);
            var w2 = Wassup.Editor.UnitVisualDataValidator.CollectWarnings(probe2);
            if (w2.Count != 0) { Debug.LogError($"[SMOKE] resolved output has warnings:\n - " + string.Join("\n - ", w2)); ok = false; }
            Object.DestroyImmediate(probe2);
            Debug.Log($"[SMOKE] importer resolve OK: A={partsA.Count}(hat), B={partsB.Count}(short), colors={colors.Count}, roundtripWarnings={w2.Count}");
        }

        // unit-parts-appearance 4 — Defender 16종 실에셋 시안 검증 (무경고 + 합성 + 유일성)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:DefenderUnitData", new[] { "Assets/_Project/Data/Defenders" });
            var comboKeys = new System.Collections.Generic.HashSet<string>();
            int checked_ = 0, empty = 0;
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var d = UnityEditor.AssetDatabase.LoadAssetAtPath<Wassup.Data.DefenderUnitData>(path);
                if (d == null) continue;
                checked_++;
                if (d.partSkins.Count == 0) { empty++; Debug.LogError($"[SMOKE] {d.name}: partSkins 비어 있음"); ok = false; continue; }
                var w = Wassup.Editor.UnitVisualDataValidator.CollectWarnings(d);
                if (w.Count != 0) { Debug.LogError($"[SMOKE] {d.name} 경고 {w.Count}건:\n - " + string.Join("\n - ", w)); ok = false; }
                var skel = d.skeletonDataAsset.GetSkeletonData(false);
                var skin = Wassup.Presentation.SpineCombinedSkinCache.GetOrBuild(skel, d.partSkins, d.name);
                if (skin.Attachments.Count <= 6) { Debug.LogError($"[SMOKE] {d.name}: 합성 어태치 {skin.Attachments.Count} — 본체뿐"); ok = false; }
                comboKeys.Add(string.Join("|", d.partSkins));
                // 애니 필드가 실제 클립으로 해석되는지 (스켈레톤 교체/잔존값 검출)
                foreach (var (field, v) in new[] { ("idle", d.idleAnimation), ("attack", d.attackAnimation),
                         ("death", d.deathAnimation), ("drag", d.dragAnimation), ("deploy", d.deployAnimation) })
                    if (!string.IsNullOrEmpty(v) && skel.FindAnimation(v) == null)
                    { Debug.LogError($"[SMOKE] {d.name}: {field}Animation '{v}' 클립 없음"); ok = false; }
            }
            if (checked_ != 16) { Debug.LogError($"[SMOKE] Defender {checked_}종 (기대 16)"); ok = false; }
            if (comboKeys.Count != checked_) { Debug.LogError($"[SMOKE] 조합 중복: unique={comboKeys.Count}/{checked_}"); ok = false; }
            Debug.Log($"[SMOKE] defender looks OK: {checked_}종, unique={comboKeys.Count}, empty={empty}");
        }

        Debug.Log(ok ? "[SMOKE] SpinePipelineSmoke PASS" : "[SMOKE] SpinePipelineSmoke FAIL");
        EditorApplication.Exit(ok ? 0 : 1);
    }
}
