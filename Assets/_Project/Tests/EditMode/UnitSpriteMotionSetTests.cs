using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
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
        // 슬롯별 루프 정책은 상수(idle/walk/drag = 루프, attack/deploy = 원샷). OnValidate 가 위반을 LogError 로
        // 알리고 러너는 그걸 실패로 치므로, 픽스처부터 정책대로 만든다.
        SetLoop(_idle, true); SetLoop(_walk, true); SetLoop(_drag, true);
        SetLoop(_attack, false); SetLoop(_deploy, false);
    }

    private static void SetLoop(SpriteFlipbookData data, bool loop)
    {
        var so = new SerializedObject(data);
        so.FindProperty("loop").boolValue = loop;
        so.ApplyModifiedPropertiesWithoutUndo();
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

    // unit 6 — 대기 컷 풀: 0번 = idle, 1번~ = 대기 컷. 비면 컷 모드가 아니다.
    [Test]
    public void IdlePool_IsIdlePlusBreaks_InOrder()
    {
        Slot("idle", _idle);
        Assert.IsFalse(_set.HasIdleBreaks);
        Assert.AreEqual(1, _set.IdlePoolCount);
        // 합성 시트는 프레임이 0 이라 OnValidate 가 「비었거나 프레임 0」 에러를 낸다 — 그 검증이 의도된 동작이다.
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("idleBreaks\\[0\\]"));
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("idleBreaks\\[1\\]"));
        var so = new SerializedObject(_set);
        var list = so.FindProperty("idleBreaks");
        list.arraySize = 2;
        list.GetArrayElementAtIndex(0).objectReferenceValue = _walk;   // 아무 시트나 — 순서만 본다
        list.GetArrayElementAtIndex(1).objectReferenceValue = _attack;
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.IsTrue(_set.HasIdleBreaks);
        Assert.AreEqual(3, _set.IdlePoolCount);
        Assert.AreSame(_idle, _set.IdlePoolAt(0), "쉬는 그림의 출처 = 0번 = idle");
        Assert.AreSame(_walk, _set.IdlePoolAt(1));
        Assert.AreSame(_attack, _set.IdlePoolAt(2));
        Assert.IsNull(_set.IdlePoolAt(3));
    }

    [Test]
    public void IdleRestGap_LerpsWithinRange_AndNeverNegative()
    {
        var so = new SerializedObject(_set);
        so.FindProperty("idleRestGap").vector2Value = new Vector2(1f, 3f);
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.AreEqual(1f, _set.PickIdleRestGap(0f), 1e-5f);
        Assert.AreEqual(2f, _set.PickIdleRestGap(0.5f), 1e-5f);
        Assert.AreEqual(3f, _set.PickIdleRestGap(1f), 1e-5f);
        Assert.AreEqual(3f, _set.PickIdleRestGap(7f), 1e-5f, "roll 은 0..1 로 클램프");
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("idleRestGap"));   // 음수 저작 = 검증기가 잡는 저작 오류
        so.FindProperty("idleRestGap").vector2Value = new Vector2(-2f, -1f);
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.AreEqual(0f, _set.PickIdleRestGap(0.5f), "음수 저작은 0 으로 — 뷰는 쉼 0 을 「다음 틱에 바로 다음 컷」으로 읽는다(연속 재생)");
    }

    [Test]
    public void Death_HasNoFallback()
    {
        Slot("idle", _idle);
        Assert.IsNull(_set.Death, "death 미저작 = 즉시 파괴. idle 로 대체하면 시체가 서서 대기한다");
    }
}
