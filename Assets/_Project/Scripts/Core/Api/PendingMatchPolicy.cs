namespace Wassup.Core.Api
{
    // abandoned-match-reconciliation unit 0 — pure decision for a persisted pending
    // attempt: complete-with-0 within the grace window, otherwise discard and let
    // the server's own round cleanup finalize it. Plain value in/out, architecture-
    // blind (CLAUDE.md rule 10) — EditMode tested.
    public enum PendingMatchAction { Complete0, DiscardOnly }

    public static class PendingMatchPolicy
    {
        // Grace window's single owner (README contract). Client-side heuristic
        // aligned with the server's attempt/round TTL: a complete sent past this
        // would land in a closed round, so we discard instead. Keep conservative
        // (below the actual server TTL).
        public const long DefaultTtlSeconds = 600;

        // A negative elapsed (device clock rewound) is <= ttl, so it reads as a
        // just-started match → Complete0, never DiscardOnly — we don't drop a
        // fresh attempt on a clock glitch.
        public static PendingMatchAction Decide(long elapsedSeconds, long ttlSeconds)
            => elapsedSeconds <= ttlSeconds ? PendingMatchAction.Complete0 : PendingMatchAction.DiscardOnly;
    }
}
