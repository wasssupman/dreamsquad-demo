using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Data;

// defender-deploy-phase unit 0 — 배치 페이즈 길이는 배치 모션에서 나온다(저작 초 없음).
// 입력은 직렬화 참조, 출력은 초 하나 — 아키텍처를 모르는 순수 결정이라 EditMode 대상.
// Spine 양성 케이스(실제 트랙 길이)는 실에셋 lane(DeployMotionSecondsAssetTests)이 맡는다.
public class DeployMotionSecondsTests
{
    private DefenderUnitData _unit;
    private UnitSpriteMotionSet _set;
    private SpriteFlipbookData _idle, _deploy;
    private Texture2D _tex;

    [SetUp]
    public void SetUp()
    {
        _unit = ScriptableObject.CreateInstance<DefenderUnitData>();
        _set = ScriptableObject.CreateInstance<UnitSpriteMotionSet>();
        _idle = ScriptableObject.CreateInstance<SpriteFlipbookData>();
        _deploy = ScriptableObject.CreateInstance<SpriteFlipbookData>();
        _tex = new Texture2D(4, 4);
        Fill(_idle, frames: 4, fps: 12f, loop: true);
        Fill(_deploy, frames: 16, fps: 24f, loop: false);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_unit); Object.DestroyImmediate(_set);
        Object.DestroyImmediate(_idle); Object.DestroyImmediate(_deploy); Object.DestroyImmediate(_tex);
    }

    // 슬롯별 루프 정책(idle 루프·deploy 원샷)은 OnValidate 가 LogError 로 강제하므로 픽스처부터 정책대로.
    private void Fill(SpriteFlipbookData data, int frames, float fps, bool loop)
    {
        var so = new SerializedObject(data);
        so.FindProperty("fps").floatValue = fps;
        so.FindProperty("loop").boolValue = loop;
        var arr = so.FindProperty("frames");
        arr.arraySize = frames;
        for (int i = 0; i < frames; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = Sprite.Create(_tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f));
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private void Slot(string name, Object value)
    {
        var so = new SerializedObject(_set);
        so.FindProperty(name).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private void UseSet()
    {
        var so = new SerializedObject(_unit);
        so.FindProperty("spriteMotions").objectReferenceValue = _set;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [Test]
    public void SpriteDeploy_IsFramesOverFps()
    {
        Slot("idle", _idle); Slot("deploy", _deploy); UseSet();
        Assert.AreEqual(16f / 24f, _unit.DeployMotionSeconds, 1e-5f);
    }

    [Test]
    public void SpriteSet_WithoutDeploy_IsZero_NotSpineFallback()
    {
        // 시트가 그리는 유닛은 Spine 트랙 이름이 남아 있어도 그 길이를 재지 않는다(백엔드 판별 = HasIdle).
        Slot("idle", _idle); UseSet();
        _unit.deployAnimation = "Hit";
        Assert.AreEqual(0f, _unit.DeployMotionSeconds);
    }

    [Test]
    public void NoSet_NoSkeleton_IsZero()
    {
        _unit.deployAnimation = "Hit";
        Assert.AreEqual(0f, _unit.DeployMotionSeconds, "skeletonDataAsset 없음 = 0");
        _unit.deployAnimation = "";
        Assert.AreEqual(0f, _unit.DeployMotionSeconds, "트랙 이름 없음 = 0");
    }

    [Test]
    public void SpriteSet_WithoutIdle_IsInvalid_FallsToSpineRule()
    {
        // idle 이 없는 세트는 풀이 통째로 버린다(TrySpawn) — 길이도 같은 판정을 따라 Spine 규칙으로 간다(여기선 0).
        Slot("deploy", _deploy); UseSet();
        Assert.AreEqual(0f, _unit.DeployMotionSeconds);
    }
}
