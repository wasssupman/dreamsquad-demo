using NUnit.Framework;
using Wassup.Presentation;

// sprite-unit-backend unit 1 — 두 백엔드가 공유하는 반전 판정. 규칙은 SpineUnitView 의 것을
// 그대로 옮겼으므로 여기가 빨개지면 Spine 쪽 동작이 바뀐 것이다.
public class UnitFacingTests
{
    [Test]
    public void Epsilon_IgnoresJitter()
    {
        float acc = 0f;
        Assert.IsFalse(UnitFacing.ShouldFlip(0.0005f, facingRight: false, immediate: false, ref acc));
        Assert.IsFalse(UnitFacing.ShouldFlip(-0.0005f, facingRight: true, immediate: true, ref acc));
    }

    [Test]
    public void SameDirection_ResetsAccumulation()
    {
        float acc = 0.04f;   // 거의 뒤집기 직전
        Assert.IsFalse(UnitFacing.ShouldFlip(0.01f, facingRight: true, immediate: false, ref acc));
        Assert.AreEqual(0f, acc, "같은 방향이 나오면 누적이 리셋된다");
    }

    [Test]
    public void OppositeDirection_FlipsOnlyAfterAccumulation()
    {
        float acc = 0f;
        // 왼쪽을 보는데 오른쪽으로 2mm 씩 — 25번(0.05) 전엔 안 뒤집힌다
        for (int i = 0; i < 24; i++)
            Assert.IsFalse(UnitFacing.ShouldFlip(0.002f, facingRight: false, immediate: false, ref acc), $"step {i}");
        Assert.IsTrue(UnitFacing.ShouldFlip(0.002f, facingRight: false, immediate: false, ref acc));
        Assert.AreEqual(0f, acc, "뒤집은 뒤 누적 리셋");
    }

    [Test]
    public void Immediate_FlipsWithoutAccumulation()
    {
        float acc = 0f;
        Assert.IsTrue(UnitFacing.ShouldFlip(0.002f, facingRight: false, immediate: true, ref acc));
        Assert.IsTrue(UnitFacing.ShouldFlip(-0.002f, facingRight: true, immediate: true, ref acc));
    }
}
