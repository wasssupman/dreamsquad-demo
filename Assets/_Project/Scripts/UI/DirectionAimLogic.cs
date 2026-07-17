using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.UI
{
    // defender-directional-volley unit 5 — pure gesture interpretation for the aim
    // phase (제약 10). Cells in, board cardinal out; knows nothing about cameras,
    // input devices, or pixels.
    //
    // The pick is a TAP ON A CELL, not a swipe: which lane the finger is over is a
    // question about the board, so the camera cannot make it ambiguous. (A screen
    // swipe can: on an isometric board "screen up" sits exactly between two lanes.)
    //
    // The lane test is the sim's own fire gate — the tiles the player taps are, by
    // construction, the tiles the unit will shoot.
    public static class DirectionAimLogic
    {
        public struct AimSample
        {
            public bool hasDirection;
            public int2 cardinal;
        }

        public struct AimPhaseResult
        {
            public bool confirmed;
            public int2 cardinal;
        }

        static readonly int2[] Cardinals =
        {
            new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1),
        };

        // The arrow sits on a lane's first cell, so tapping the arrow and tapping
        // anywhere down its lane are the same answer — the arrow is the affordance,
        // the whole lane is the target (a one-tile target hides under a finger).
        // Cells outside every lane (and the unit's own) select nothing.
        public static AimSample Evaluate(int2 center, int2 tappedCell, int tileRange)
        {
            for (int i = 0; i < Cardinals.Length; i++)
                if (LaneMath.IsInLane(center, Cardinals[i], tileRange, tappedCell))
                    return new AimSample { hasDirection = true, cardinal = Cardinals[i] };
            return default;
        }

        // Release over a lane confirms it; releasing anywhere else keeps the phase
        // open for another try (feature contract 9 — no cancel path).
        public static AimPhaseResult OnRelease(AimSample lastSample)
            => new AimPhaseResult { confirmed = lastSample.hasDirection, cardinal = lastSample.cardinal };
    }
}
