using Unity.Mathematics;

namespace Wassup.UI
{
    // defender-directional-volley unit 5 — pure gesture interpretation for the
    // aim phase (제약 10). Pixels in, board cardinal out; knows nothing about
    // cameras, boards, or input devices.
    //
    // The caller passes the board axes as the caller sees them on screen, which
    // is what makes this camera-agnostic: a swipe is matched against those, not
    // against screen up/right. Under the battle camera (pitch only, no yaw) the
    // axes come out near (1,0)/(0,1) and this degenerates to a plain screen
    // snap; under an isometric board they arrive rotated ~45° and the same code
    // still picks the lane the player actually swiped along.
    public static class DirectionAimLogic
    {
        public struct AimSample
        {
            public bool hasDirection;
            public int2 cardinal; // board space: +x/+y along the grid axes
        }

        public struct AimPhaseResult
        {
            public bool confirmed;
            public int2 cardinal;
        }

        // axisRight / axisUp: screen-space projections of the board's +X / +Y
        // axes (normalized by the caller — it owns the camera). Ties resolve to
        // the board's X axis: deterministic, test-pinned.
        public static AimSample Evaluate(
            float2 pressOrigin, float2 currentPos, float deadZonePx, float2 axisRight, float2 axisUp)
        {
            float2 delta = currentPos - pressOrigin;
            if (math.length(delta) < deadZonePx) return default;

            float alongX = math.dot(delta, axisRight);
            float alongY = math.dot(delta, axisUp);
            int2 cardinal = math.abs(alongX) >= math.abs(alongY)
                ? new int2(alongX >= 0f ? 1 : -1, 0)
                : new int2(0, alongY >= 0f ? 1 : -1);
            return new AimSample { hasDirection = true, cardinal = cardinal };
        }

        // Release with a live direction confirms it; a dead-zone release keeps the
        // phase open for another swipe (feature contract 9 — no cancel path).
        public static AimPhaseResult OnRelease(AimSample lastSample)
            => new AimPhaseResult { confirmed = lastSample.hasDirection, cardinal = lastSample.cardinal };
    }
}
