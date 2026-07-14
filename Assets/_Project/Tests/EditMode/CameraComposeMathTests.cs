using NUnit.Framework;
using UnityEngine;
using Wassup.Presentation;

// camera-direction unit 0 — 포즈 합성/킥 envelope 순수 함수 회귀 테스트.
public class CameraComposeMathTests
{
    private static readonly Vector3 HomePos = new Vector3(3f, 12f, -8f);
    private static readonly Quaternion HomeRot = Quaternion.Euler(55f, 0f, 0f);
    private const float HomeFov = 40f;
    private const float FovMin = 30f;
    private const float FovMax = 60f;

    [Test]
    public void Compose_IdentityDelta_ReturnsHomeExactly()
    {
        CameraComposeMath.Compose(HomePos, HomeRot, HomeFov, CameraPoseDelta.Identity,
            FovMin, FovMax, out var pos, out var rot, out var fov);
        Assert.That(pos, Is.EqualTo(HomePos));
        Assert.That(Quaternion.Angle(rot, HomeRot), Is.LessThan(1e-4f));
        Assert.That(fov, Is.EqualTo(HomeFov));
    }

    [Test]
    public void Compose_LocalPosOffset_MovesAlongHomeAxes()
    {
        var delta = new CameraPoseDelta { localPos = new Vector3(0f, -1f, 0f) };
        CameraComposeMath.Compose(HomePos, HomeRot, HomeFov, delta,
            FovMin, FovMax, out var pos, out _, out _);
        Vector3 expected = HomePos + HomeRot * new Vector3(0f, -1f, 0f);
        Assert.That((pos - expected).magnitude, Is.LessThan(1e-5f));
    }

    [Test]
    public void Compose_FovDelta_Adds()
    {
        var delta = new CameraPoseDelta { fovDelta = -3.5f };
        CameraComposeMath.Compose(HomePos, HomeRot, HomeFov, delta,
            FovMin, FovMax, out _, out _, out var fov);
        Assert.That(fov, Is.EqualTo(HomeFov - 3.5f).Within(1e-5f));
    }

    [Test]
    public void Compose_FovClamped_ToConfigRange()
    {
        CameraComposeMath.Compose(HomePos, HomeRot, HomeFov,
            new CameraPoseDelta { fovDelta = -45f }, FovMin, FovMax, out _, out _, out var low);
        CameraComposeMath.Compose(HomePos, HomeRot, HomeFov,
            new CameraPoseDelta { fovDelta = +45f }, FovMin, FovMax, out _, out _, out var high);
        Assert.That(low, Is.EqualTo(FovMin));
        Assert.That(high, Is.EqualTo(FovMax));
    }

    [Test]
    public void Compose_PitchDelta_RotatesAroundHomeRight()
    {
        var delta = new CameraPoseDelta { pitchDeg = 10f };
        CameraComposeMath.Compose(HomePos, HomeRot, HomeFov, delta,
            FovMin, FovMax, out _, out var rot, out _);
        var expected = Quaternion.AngleAxis(10f, HomeRot * Vector3.right) * HomeRot;
        Assert.That(Quaternion.Angle(rot, expected), Is.LessThan(1e-4f));
    }

    [Test]
    public void ShakeDelta_Deterministic_SameInputsSameOutput()
    {
        var a = CameraComposeMath.ShakeDelta(0.37f, 0.81f, 0.6f, 0.04f, 0.12f);
        var b = CameraComposeMath.ShakeDelta(0.37f, 0.81f, 0.6f, 0.04f, 0.12f);
        Assert.That(a.localPos, Is.EqualTo(b.localPos));
        Assert.That(a.rollDeg, Is.EqualTo(b.rollDeg));
    }

    [Test]
    public void ShakeDelta_ScalesWithWeight_AndZeroWeightIsIdentity()
    {
        var half = CameraComposeMath.ShakeDelta(0.2f, 0.7f, 0.5f, 0.04f, 0.12f);
        var full = CameraComposeMath.ShakeDelta(0.2f, 0.7f, 1.0f, 0.04f, 0.12f);
        var none = CameraComposeMath.ShakeDelta(0.2f, 0.7f, 0f, 0.04f, 0.12f);
        Assert.That(half.localPos.x, Is.EqualTo(full.localPos.x * 0.5f).Within(1e-6f));
        Assert.That(none.localPos, Is.EqualTo(Vector3.zero));
        Assert.That(none.rollDeg, Is.EqualTo(0f));
        Assert.That(none.pitchDeg, Is.EqualTo(0f));
        Assert.That(none.fovDelta, Is.EqualTo(0f));
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
    public void Lerp_Boundaries_ReturnEndpoints()
    {
        var a = new CameraPoseDelta { localPos = Vector3.up, pitchDeg = 2f, rollDeg = 3f, fovDelta = 4f };
        var b = new CameraPoseDelta { localPos = Vector3.right * 5f, pitchDeg = -8f, rollDeg = 0f, fovDelta = -2f };
        var at0 = CameraComposeMath.Lerp(a, b, 0f);
        var at1 = CameraComposeMath.Lerp(a, b, 1f);
        Assert.That(at0.localPos, Is.EqualTo(a.localPos));
        Assert.That(at0.pitchDeg, Is.EqualTo(a.pitchDeg));
        Assert.That(at1.localPos, Is.EqualTo(b.localPos));
        Assert.That(at1.fovDelta, Is.EqualTo(b.fovDelta));
    }

    [Test]
    public void Lerp_Midpoint_IsComponentwiseAverage()
    {
        var a = new CameraPoseDelta { pitchDeg = -8f, fovDelta = 0f };
        var b = new CameraPoseDelta { pitchDeg = 0f, fovDelta = -4f };
        var mid = CameraComposeMath.Lerp(a, b, 0.5f);
        Assert.That(mid.pitchDeg, Is.EqualTo(-4f).Within(1e-5f));
        Assert.That(mid.fovDelta, Is.EqualTo(-2f).Within(1e-5f));
    }

    [Test]
    public void Lerp_OvershootT_PassesThroughUnclamped()
    {
        var a = new CameraPoseDelta { pitchDeg = 0f };
        var b = new CameraPoseDelta { pitchDeg = 10f };
        Assert.That(CameraComposeMath.Lerp(a, b, 1.2f).pitchDeg, Is.EqualTo(12f).Within(1e-5f));
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
    public void BreathWaveDelta_Deterministic_SameInputsSameOutput()
    {
        var a = CameraComposeMath.BreathWaveDelta(0.42f, new Vector2(1f, 0.3f), 0.5f, 0.8f, 0.03f, 0.06f);
        var b = CameraComposeMath.BreathWaveDelta(0.42f, new Vector2(1f, 0.3f), 0.5f, 0.8f, 0.03f, 0.06f);
        Assert.That(a.localPos, Is.EqualTo(b.localPos));
        Assert.That(a.pitchDeg, Is.EqualTo(b.pitchDeg));
    }

    [Test]
    public void BreathWaveDelta_AmplitudeBounded_ByAmpTimesWeights()
    {
        // sin ∈ [-1,1] 이므로 어떤 위상에서도 |offset| ≤ amp × |axisWeight| × weight.
        for (int i = 0; i <= 20; i++)
        {
            var d = CameraComposeMath.BreathWaveDelta(i / 20f, new Vector2(1f, 0.5f), 1f, 1f, 0.03f, 0.06f);
            Assert.That(Mathf.Abs(d.localPos.x), Is.LessThanOrEqualTo(0.03f + 1e-6f));
            Assert.That(Mathf.Abs(d.localPos.y), Is.LessThanOrEqualTo(0.015f + 1e-6f));
            Assert.That(Mathf.Abs(d.pitchDeg), Is.LessThanOrEqualTo(0.06f + 1e-6f));
        }
    }

    [Test]
    public void BreathWaveDelta_ZeroWeight_IsIdentity()
    {
        var d = CameraComposeMath.BreathWaveDelta(0.3f, Vector2.one, 1f, 0f, 0.03f, 0.06f);
        Assert.That(d.localPos, Is.EqualTo(Vector3.zero));
        Assert.That(d.pitchDeg, Is.EqualTo(0f));
        Assert.That(d.fovDelta, Is.EqualTo(0f));
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
