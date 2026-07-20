using NUnit.Framework;
using Wassup.Presentation;

// sprite-flipbook-player unit 0 — 프레임 선택 순수 함수 회귀 테스트.
// 경계(루프 되감기 / 원샷 hold / 프레임 경계 / 0 fps / 빈 배열 / 비정상 elapsed)를 고정한다.
public class FlipbookMathTests
{
    private const float Fps = 10f; // 프레임 간격 0.1s — 경계값을 정확히 표현할 수 있는 값
    private const int Count = 4;   // 총 길이 0.4s

    // --- FrameAt: 정상 진행 ---

    [Test]
    public void FrameAt_AtStart_ReturnsFirstFrame()
    {
        Assert.That(FlipbookMath.FrameAt(0f, Fps, Count, loop: false), Is.EqualTo(0));
    }

    [Test]
    public void FrameAt_MidFrame_ReturnsFloorOfElapsedTimesFps()
    {
        // 0.25s * 10fps = 2.5 → 인덱스 2 (내림)
        Assert.That(FlipbookMath.FrameAt(0.25f, Fps, Count, loop: false), Is.EqualTo(2));
    }

    [Test]
    public void FrameAt_ExactFrameBoundary_AdvancesToNextFrame()
    {
        // 경계에서 이전 프레임에 머무르면 마지막 프레임이 두 배로 길어진다.
        Assert.That(FlipbookMath.FrameAt(0.1f, Fps, Count, loop: false), Is.EqualTo(1));
        Assert.That(FlipbookMath.FrameAt(0.3f, Fps, Count, loop: false), Is.EqualTo(3));
    }

    // --- FrameAt: 원샷 hold ---

    [Test]
    public void FrameAt_OneShotAtDuration_HoldsLastFrame()
    {
        // elapsed == Duration 은 이미 완주 지점. 인덱스가 범위를 넘지 않아야 한다.
        Assert.That(FlipbookMath.FrameAt(0.4f, Fps, Count, loop: false), Is.EqualTo(Count - 1));
    }

    [Test]
    public void FrameAt_OneShotPastEnd_KeepsHoldingLastFrame()
    {
        Assert.That(FlipbookMath.FrameAt(99f, Fps, Count, loop: false), Is.EqualTo(Count - 1));
    }

    // --- FrameAt: 루프 되감기 ---

    [Test]
    public void FrameAt_LoopAtDuration_WrapsToFirstFrame()
    {
        Assert.That(FlipbookMath.FrameAt(0.4f, Fps, Count, loop: true), Is.EqualTo(0));
    }

    [Test]
    public void FrameAt_LoopSecondCycle_ContinuesWrapping()
    {
        // 0.55s → raw 5 → 5 % 4 = 1 (두 번째 사이클의 두 번째 프레임)
        Assert.That(FlipbookMath.FrameAt(0.55f, Fps, Count, loop: true), Is.EqualTo(1));
    }

    [Test]
    public void FrameAt_LoopNeverExceedsFrameRange()
    {
        for (int i = 0; i < 200; i++)
        {
            int idx = FlipbookMath.FrameAt(i * 0.037f, Fps, Count, loop: true);
            Assert.That(idx, Is.InRange(0, Count - 1));
        }
    }

    // --- FrameAt: 경계/비정상 입력 ---

    [Test]
    public void FrameAt_NoFrames_ReturnsMinusOne()
    {
        // -1 = "그릴 것 없음". 0 을 돌려주면 소비자가 빈 배열을 인덱싱한다.
        Assert.That(FlipbookMath.FrameAt(1f, Fps, 0, loop: false), Is.EqualTo(-1));
        Assert.That(FlipbookMath.FrameAt(1f, Fps, -3, loop: true), Is.EqualTo(-1));
    }

    [Test]
    public void FrameAt_ZeroOrNegativeFps_HoldsFirstFrame()
    {
        Assert.That(FlipbookMath.FrameAt(5f, 0f, Count, loop: false), Is.EqualTo(0));
        Assert.That(FlipbookMath.FrameAt(5f, -12f, Count, loop: true), Is.EqualTo(0));
    }

    [Test]
    public void FrameAt_NegativeElapsed_ReturnsFirstFrame()
    {
        Assert.That(FlipbookMath.FrameAt(-2f, Fps, Count, loop: true), Is.EqualTo(0));
    }

    [Test]
    public void FrameAt_NonFiniteElapsed_ReturnsFirstFrame()
    {
        // InRange 로 느슨하게 두면 아키텍처별 분기를 은폐한다: float→int 캐스트가 x64 는 int.MinValue,
        // ARM64 는 saturate(+Inf → int.MaxValue) 라 후자에서는 int.MaxValue % Count = 3 이 나온다.
        // 첫 프레임으로 못박아야 Apple Silicon 에디터와 Android ARM64 에서 같은 동작이 보장된다.
        Assert.That(FlipbookMath.FrameAt(float.NaN, Fps, Count, loop: false), Is.EqualTo(0));
        Assert.That(FlipbookMath.FrameAt(float.NaN, Fps, Count, loop: true), Is.EqualTo(0));
        Assert.That(FlipbookMath.FrameAt(float.PositiveInfinity, Fps, Count, loop: true), Is.EqualTo(0));
        Assert.That(FlipbookMath.FrameAt(float.PositiveInfinity, Fps, Count, loop: false), Is.EqualTo(0));
        Assert.That(FlipbookMath.FrameAt(float.NegativeInfinity, Fps, Count, loop: true), Is.EqualTo(0));
    }

    [Test]
    public void FrameAt_NegativeElapsed_OneShotAlsoReturnsFirstFrame()
    {
        Assert.That(FlipbookMath.FrameAt(-2f, Fps, Count, loop: false), Is.EqualTo(0));
    }

    [Test]
    public void FrameAt_SingleFrame_StaysOnIndexZero()
    {
        // 단일 프레임 플립북(정적 스프라이트를 재생기로 다루는 경우)에서 인덱스가 새지 않아야 한다.
        Assert.That(FlipbookMath.FrameAt(0f, Fps, 1, loop: true), Is.EqualTo(0));
        Assert.That(FlipbookMath.FrameAt(5f, Fps, 1, loop: true), Is.EqualTo(0));
        Assert.That(FlipbookMath.FrameAt(5f, Fps, 1, loop: false), Is.EqualTo(0));
    }

    // --- FrameAt × IsFinished 결합 불변식 ---

    [Test]
    public void OneShot_WhenFinished_LastAppliedFrameIsTheFinalFrame()
    {
        // 재생기(SpriteFlipbookPlayer)가 "프레임 반영 → 완료 판정" 순서를 지키는 근거가 이 불변식이다.
        // 순서가 뒤집히거나 경계가 어긋나면 마지막 프레임이 한 번도 그려지지 않는다.
        // 실제 재생처럼 dt 를 쪼개 누적하면서, 완료로 판정되는 시점의 프레임을 확인한다.
        const float Dt = 1f / 60f;
        float elapsed = 0f;
        int lastApplied = -1;

        for (int step = 0; step < 1000; step++)
        {
            elapsed += Dt;
            lastApplied = FlipbookMath.FrameAt(elapsed, Fps, Count, loop: false);
            if (FlipbookMath.IsFinished(elapsed, Fps, Count, loop: false)) break;
        }

        Assert.That(FlipbookMath.IsFinished(elapsed, Fps, Count, loop: false), Is.True,
            "루프를 다 돌고도 완료되지 않았다 — 경계 계산이 어긋났다.");
        Assert.That(lastApplied, Is.EqualTo(Count - 1),
            "완료 시점에 마지막 프레임이 반영되어 있지 않다 — 재생기가 최종 포즈를 건너뛴다.");
    }

    // --- Duration ---

    [Test]
    public void Duration_IsFrameCountOverFps()
    {
        Assert.That(FlipbookMath.Duration(Fps, Count), Is.EqualTo(0.4f).Within(1e-5f));
    }

    [Test]
    public void Duration_InvalidInput_IsZero()
    {
        Assert.That(FlipbookMath.Duration(0f, Count), Is.EqualTo(0f));
        Assert.That(FlipbookMath.Duration(Fps, 0), Is.EqualTo(0f));
    }

    [Test]
    public void Duration_NonFiniteFps_IsZeroNotNaN()
    {
        // NaN 이 새면 IsFinished 의 `elapsed >= NaN` 이 영원히 false 라 원샷이 완주하지 못한다.
        Assert.That(FlipbookMath.Duration(float.NaN, Count), Is.EqualTo(0f));
        Assert.That(FlipbookMath.Duration(float.PositiveInfinity, Count), Is.EqualTo(0f));
    }

    // --- 비유한 fps: elapsed 와 같은 아키텍처 분기가 fps 로 재진입하지 않는지 ---

    [Test]
    public void FrameAt_NonFiniteFps_ReturnsFirstFrame()
    {
        // SO 의 `fps <= 0` 검증은 NaN/+Inf 를 못 잡는다(NaN 비교는 전부 false).
        // 막지 않으면 ARM64 에서 int.MaxValue % Count 로 임의 프레임에 고착된다.
        Assert.That(FlipbookMath.FrameAt(0.1f, float.PositiveInfinity, Count, loop: true), Is.EqualTo(0));
        Assert.That(FlipbookMath.FrameAt(0.1f, float.NaN, Count, loop: true), Is.EqualTo(0));
        Assert.That(FlipbookMath.FrameAt(0.1f, float.NaN, Count, loop: false), Is.EqualTo(0));
    }

    [Test]
    public void IsFinished_NonFiniteInput_DoesNotHangOneShot()
    {
        // 전진할 수 없는 재생이 살아 있으면 IsPlaying 폴링 소비자가 영구 대기한다.
        Assert.That(FlipbookMath.IsFinished(float.NaN, Fps, Count, loop: false), Is.True);
        Assert.That(FlipbookMath.IsFinished(0.1f, float.NaN, Count, loop: false), Is.True);
        Assert.That(FlipbookMath.IsFinished(0.1f, float.PositiveInfinity, Count, loop: false), Is.True);
    }

    // --- IsFinished ---

    [Test]
    public void IsFinished_OneShot_TrueOnlyAtOrPastDuration()
    {
        Assert.That(FlipbookMath.IsFinished(0.39f, Fps, Count, loop: false), Is.False);
        Assert.That(FlipbookMath.IsFinished(0.4f, Fps, Count, loop: false), Is.True);
        Assert.That(FlipbookMath.IsFinished(9f, Fps, Count, loop: false), Is.True);
    }

    [Test]
    public void IsFinished_Loop_IsNeverFinished()
    {
        Assert.That(FlipbookMath.IsFinished(0f, Fps, Count, loop: true), Is.False);
        Assert.That(FlipbookMath.IsFinished(99999f, Fps, Count, loop: true), Is.False);
    }

    [Test]
    public void IsFinished_ZeroFpsOneShot_FinishesImmediately()
    {
        // 진행하지 않는 재생이 영원히 살아남아 소비자를 붙잡아두지 않게 한다.
        Assert.That(FlipbookMath.IsFinished(0f, 0f, Count, loop: false), Is.True);
    }
}
