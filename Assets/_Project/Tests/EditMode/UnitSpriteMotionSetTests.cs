using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Data;

// sprite-unit-backend unit 0 — 빈 슬롯 폴백 규칙. 입력은 직렬화 참조, 출력은 참조 하나 —
// 아키텍처 타입을 모르는 순수 결정이라 EditMode 대상이다.
// Spine 쪽 후보 체인(ResolveLocomotionAnimation / PlayDeploy)과 같은 순서여야 두 백엔드가
// 같은 저작 공백에서 같은 그림을 낸다.
public class UnitSpriteMotionSetTests
{
    private SpriteFlipbookData _idle, _walk, _attack, _deploy, _drag;
    private UnitSpriteMotionSet _set;

    [SetUp]
    public void SetUp()
    {
        _idle = ScriptableObject.CreateInstance<SpriteFlipbookData>();
        _walk = ScriptableObject.CreateInstance<SpriteFlipbookData>();
        _attack = ScriptableObject.CreateInstance<SpriteFlipbookData>();
        _deploy = ScriptableObject.CreateInstance<SpriteFlipbookData>();
        _drag = ScriptableObject.CreateInstance<SpriteFlipbookData>();
        _set = ScriptableObject.CreateInstance<UnitSpriteMotionSet>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_set);
        Object.DestroyImmediate(_idle);
        Object.DestroyImmediate(_walk);
        Object.DestroyImmediate(_attack);
        Object.DestroyImmediate(_deploy);
        Object.DestroyImmediate(_drag);
    }

    private void Slot(string name, SpriteFlipbookData value)
    {
        var so = new SerializedObject(_set);
        so.FindProperty(name).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [Test]
    public void HasIdle_IsFalse_WhenIdleEmpty()
    {
        Assert.IsFalse(_set.HasIdle);
        Slot("idle", _idle);
        Assert.IsTrue(_set.HasIdle);
    }

    [Test]
    public void Locomotion_WithoutWalk_IsIdleRegardlessOfMoving()
    {
        Slot("idle", _idle);
        Assert.AreSame(_idle, _set.ResolveLocomotion(false));
        Assert.AreSame(_idle, _set.ResolveLocomotion(true), "walk 미저작 = 이동/정지 구분 없음");
    }

    [Test]
    public void Locomotion_WithWalk_SwitchesOnMoving()
    {
        Slot("idle", _idle);
        Slot("walk", _walk);
        Assert.AreSame(_idle, _set.ResolveLocomotion(false));
        Assert.AreSame(_walk, _set.ResolveLocomotion(true));
    }

    [Test]
    public void Deploy_FallsBack_DeployDragAttackIdle()
    {
        Slot("idle", _idle);
        Assert.AreSame(_idle, _set.ResolveDeploy());
        Slot("attack", _attack);
        Assert.AreSame(_attack, _set.ResolveDeploy());
        Slot("drag", _drag);
        Assert.AreSame(_drag, _set.ResolveDeploy());
        Slot("deploy", _deploy);
        Assert.AreSame(_deploy, _set.ResolveDeploy());
    }

    [Test]
    public void Drag_FallsBack_ToIdle_NotAttack()
    {
        Slot("idle", _idle);
        Slot("attack", _attack);
        Assert.AreSame(_idle, _set.ResolveDrag(), "손끝에서 공격 모션이 돌면 안 된다");
        Slot("drag", _drag);
        Assert.AreSame(_drag, _set.ResolveDrag());
    }

    [Test]
    public void Death_HasNoFallback()
    {
        Slot("idle", _idle);
        Assert.IsNull(_set.Death, "death 미저작 = 즉시 파괴. idle 로 대체하면 시체가 서서 대기한다");
    }
}
