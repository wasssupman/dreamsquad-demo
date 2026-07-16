using Unity.Mathematics;

namespace Wassup.UI
{
    // defender-directional-volley unit 5 — pure gesture interpretation for the
    // aim phase (제약 10). Screen-space pixels in, screen-space cardinal out;
    // knows nothing about cameras, boards, or input devices. The view→board
    // mapping is the caller's job (unit 6 controller).
    public static class DirectionAimLogic
    {
        public struct AimSample
        {
            public bool hasDirection;
            public int2 cardinal; // screen space: +x right, +y up
        }

        public struct AimPhaseResult
        {
            public bool confirmed;
            public int2 cardinal;
        }

        // Dominant-axis snap once the drag leaves the dead zone (>= deadZonePx).
        // Diagonal ties resolve to the horizontal axis — deterministic, test-pinned.
        public static AimSample Evaluate(float2 pressOrigin, float2 currentPos, float deadZonePx)
        {
            float2 delta = currentPos - pressOrigin;
            if (math.length(delta) < deadZonePx) return default;
            int2 cardinal = math.abs(delta.x) >= math.abs(delta.y)
                ? new int2(delta.x >= 0f ? 1 : -1, 0)
                : new int2(0, delta.y >= 0f ? 1 : -1);
            return new AimSample { hasDirection = true, cardinal = cardinal };
        }

        // Release with a live direction confirms it; a dead-zone release keeps the
        // phase open for another swipe (feature contract 9 — no cancel path).
        public static AimPhaseResult OnRelease(AimSample lastSample)
            => new AimPhaseResult { confirmed = lastSample.hasDirection, cardinal = lastSample.cardinal };
    }
}
