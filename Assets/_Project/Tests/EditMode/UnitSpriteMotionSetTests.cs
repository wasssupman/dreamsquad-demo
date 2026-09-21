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
    public void Deploy_HasNoFallback()
    {
        // defender-deploy-phase — 배치 모션은 명시 슬롯만. 폴백을 되살리면 길이(DeployMotionSeconds)와 재생이 갈린다.
        Slot("idle", _idle); Slot("attack", _attack); Slot("drag", _drag);
        Assert.IsNull(_set.Deploy);
        Slot("deploy", _deploy);
        Assert.AreSame(_deploy, _set.Deploy);
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

    // idle-break-shared — 풀 = 컷만. 기본 idle 은 항상 도는 것이지 끼워 넣는 것이 아니다.
    [Test]
    public void IdleBreaks_ExcludeBaseIdle_InOrder()
    {
        Slot("idle", _idle);
        Assert.IsFalse(_set.HasIdleBreaks);
        Assert.AreEqual(0, _set.IdleBreakCount);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("idleBreaks\\[0\\]"));
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("idleBreaks\\[1\\]"));
        var so = new SerializedObject(_set);
        var list = so.FindProperty("idleBreaks");
        list.arraySize = 2;
        list.GetArrayElementAtIndex(0).objectReferenceValue = _walk;   // 아무 시트나 — 순서만 본다
        list.GetArrayElementAtIndex(1).objectReferenceValue = _attack;
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.IsTrue(_set.HasIdleBreaks);
        Assert.AreEqual(2, _set.IdleBreakCount);
        Assert.AreSame(_walk, _set.IdleBreakAt(0));
        Assert.AreSame(_attack, _set.IdleBreakAt(1));
        Assert.IsNull(_set.IdleBreakAt(2));
        Assert.AreSame(_idle, _set.Idle, "기본 idle 은 풀 밖에 그대로");
    }

    [Test]
    public void IdleBreakInterval_LerpsWithinRange_AndNeverNegative()
    {
        var so = new SerializedObject(_set);
        so.FindProperty("idleBreakInterval").vector2Value = new Vector2(1f, 3f);
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.AreEqual(1f, _set.PickIdleBreakInterval(0f), 1e-5f);
        Assert.AreEqual(2f, _set.PickIdleBreakInterval(0.5f), 1e-5f);
        Assert.AreEqual(3f, _set.PickIdleBreakInterval(1f), 1e-5f);
        Assert.AreEqual(3f, _set.PickIdleBreakInterval(7f), 1e-5f, "roll 은 0..1 로 클램프");
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("idleBreakInterval"));   // 음수 저작 = 검증기가 잡는 저작 오류
        so.FindProperty("idleBreakInterval").vector2Value = new Vector2(-2f, -1f);
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.AreEqual(0f, _set.PickIdleBreakInterval(0.5f), "음수 저작은 0 으로 — 컷 끝나자마자 다음 컷");
    }

    [Test]
    public void Death_HasNoFallback()
    {
        Slot("idle", _idle);
        Assert.IsNull(_set.Death, "death 미저작 = 즉시 파괴. idle 로 대체하면 시체가 서서 대기한다");
    }
}
