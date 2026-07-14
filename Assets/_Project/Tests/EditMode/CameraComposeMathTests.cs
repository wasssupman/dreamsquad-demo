using NUnit.Framework;
using UnityEngine;
using Wassup.Presentation;

// camera-direction unit 0 — 포즈 합성/킥 envelope 순수 함수 회귀 테스트.
public class CameraComposeMathTests
{
    private static readonly Vector3 HomePos = new Vector3(3f, 12f, -8f);
    private static readonly Quaternion HomeRot = Quaternion.Euler(55f, 0f, 0f);
    private const float HomeFov = 40f;

    [Test]
    public void Compose_IdentityDelta_ReturnsHomeExactly()
    {
        CameraComposeMath.Compose(HomePos, HomeRot, HomeFov, CameraPoseDelta.Identity,
            out var pos, out var rot, out var fov);
        Assert.That(pos, Is.EqualTo(HomePos));
        Assert.That(Quaternion.Angle(rot, HomeRot), Is.LessThan(1e-4f));
        Assert.That(fov, Is.EqualTo(HomeFov));
    }

    [Test]
    public void Compose_LocalPosOffset_MovesAlongHomeAxes()
    {
        var delta = new CameraPoseDelta { localPos = new Vector3(0f, -1f, 0f) };
        CameraComposeMath.Compose(HomePos, HomeRot, HomeFov, delta,
            out var pos, out _, out _);
        Vector3 expected = HomePos + HomeRot * new Vector3(0f, -1f, 0f);
        Assert.That((pos - expected).magnitude, Is.LessThan(1e-5f));
    }

    [Test]
    public void Compose_FovDelta_Adds()
    {
        var delta = new CameraPoseDelta { fovDelta = -3.5f };
        CameraComposeMath.Compose(HomePos, HomeRot, HomeFov, delta,
            out _, out _, out var fov);
        Assert.That(fov, Is.EqualTo(HomeFov - 3.5f).Within(1e-5f));
    }

    [Test]
    public void Compose_PitchDelta_RotatesAroundHomeRight()
    {
        var delta = new CameraPoseDelta { pitchDeg = 10f };
        CameraComposeMath.Compose(HomePos, HomeRot, HomeFov, delta,
            out _, out var rot, out _);
        var expected = Quaternion.AngleAxis(10f, HomeRot * Vector3.right) * HomeRot;
        Assert.That(Quaternion.Angle(rot, expected), Is.LessThan(1e-4f));
    }

    [Test]
    public void Add_SumsAllFields()
    {
        var a = new CameraPoseDelta { localPos = Vector3.up, pitchDeg = 1f, rollDeg = 2f, fovDelta = 3f };
        var b = new CameraPoseDelta { localPos = Vector3.right, pitchDeg = 10f, rollDeg = 20f, fovDelta = 30f };
        var sum = CameraComposeMath.Add(a, b);
        Assert.That(sum.localPos, Is.EqualTo(Vector3.up + Vector3.right));
        Assert.That(sum.pitchDeg, Is.EqualTo(11f));
        Assert.That(sum.rollDeg, Is.EqualTo(22f));
        Assert.That(sum.fovDelta, Is.EqualTo(33f));
    }

    [Test]
    public void KickEnvelope_FullRemaining_IsOne_AndDecaysToZero()
    {
        Assert.That(CameraComposeMath.KickEnvelope(0.16f, 0.16f), Is.EqualTo(1f).Within(1e-5f));
        Assert.That(CameraComposeMath.KickEnvelope(0f, 0.16f), Is.EqualTo(0f));
        Assert.That(CameraComposeMath.KickEnvelope(-0.01f, 0.16f), Is.EqualTo(0f));
    }

    [Test]
    public void KickEnvelope_IsMonotonicDecay()
    {
        float prev = float.MaxValue;
        for (int i = 10; i >= 0; i--)
        {
            float env = CameraComposeMath.KickEnvelope(0.16f * i / 10f, 0.16f);
            Assert.That(env, Is.LessThanOrEqualTo(prev));
            prev = env;
        }
    }

    [Test]
    public void KickEnvelope_ZeroDuration_IsZero()
    {
        Assert.That(CameraComposeMath.KickEnvelope(0.1f, 0f), Is.EqualTo(0f));
    }

    [Test]
    public void KickDelta_ScalesWithMagnitude_DownwardAndNoFov()
    {
        var d = CameraComposeMath.KickDelta(0.5f, 0.08f, 0.35f);
        Assert.That(d.localPos.y, Is.EqualTo(-0.04f).Within(1e-6f));
        Assert.That(d.localPos.x, Is.EqualTo(0f));
        Assert.That(d.pitchDeg, Is.EqualTo(0.175f).Within(1e-6f));
        Assert.That(d.rollDeg, Is.EqualTo(0.175f).Within(1e-6f));
        Assert.That(d.fovDelta, Is.EqualTo(0f));
    }
}
