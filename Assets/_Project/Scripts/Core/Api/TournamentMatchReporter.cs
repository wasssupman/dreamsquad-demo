using System;
using UnityEngine;

namespace Wassup.Core.Api
{
    // tournament-play-report Unit 3 — match-scoped tournament reporting. Static
    // data holder + async chains, no MonoBehaviour (UserSession precedent;
    // UnityWebRequest completion callbacks need no coroutine).
    //
    // One attempt per match: BeginMatch() on battle entry / restart, then
    // ReportResult() at most once when the result popup fires. Matches that end
    // without a result popup send nothing (spec: incomplete attempts are the
    // server's business). Guests (empty IdToken) skip every call. Failures only
    // warn — the game never blocks on reporting.
    public static class TournamentMatchReporter
    {
        // Single stale-response rule for every async callback (play/complete/
        // result): capture the epoch at fire time, drop the response on mismatch.
        // Cheaper than reasoning per-callback about which windows are real
        // (the post-popup RESTART → late ranking response window is).
        private static int _epoch;
        private static string _attemptId;
        private static string _entryId;
        private static bool _completeSent;

        // tournament-seed-map-select unit 1 — the server tournament seed from the
        // latest play response. Map-pool selection reads it at map-build time;
        // absent (guest, in-flight, failed) means the caller falls back to index 0.
        private static bool _lobbyIssued;
        public static bool HasTournamentSeed { get; private set; }
        public static ulong TournamentSeed { get; private set; }

        // tournament-seed-map-select unit 1 — lobby pre-issue: fire play at the
        // lobby start button so the response (tournament.seed) lands during the
        // scene transition, before BuildMapForBattle. GameManager.OnEnable's
        // BeginMatch then adopts this attempt instead of re-issuing.
        public static void BeginMatchFromLobby()
        {
            _lobbyIssued = false; // re-entrance safety: always issue fresh from here
            BeginMatch();
            _lobbyIssued = true;
        }

        public static void BeginMatch()
        {
            // adopt the lobby-issued attempt: skip the re-issue AND the state reset
            // (resetting would stale-drop the in-flight play response carrying the seed).
            if (_lobbyIssued) { _lobbyIssued = false; return; }

            _epoch++;
            _attemptId = null;   // an unfinished previous attempt is abandoned, not completed
            _entryId = null;
            _completeSent = false;
            // clear before any early return — a stale seed from a previous session
            // must never pick the map for a new match (e.g. sign-out → guest play).
            HasTournamentSeed = false;
            TournamentSeed = 0;

            if (!UserSession.HasAccount) return; // guest / not signed in (firebase or username account required)
            string baseUrl = UserSession.GameServerBaseUrl;
            if (string.IsNullOrEmpty(baseUrl))
            {
                Debug.LogWarning("[TournamentReporter] signed in but no base URL in session; play skipped.");
                return;
            }

            int epoch = _epoch;
            TournamentApi.Play(baseUrl, UserSession.Credential, (state, error) =>
            {
                if (epoch != _epoch) return;
                if (state == null)
                {
                    Debug.LogWarning($"[TournamentReporter] play failed: {error}");
                    return;
                }
                var uts = state.userTournamentState;
                _attemptId = uts?.tournamentEntryAttemptId;
                _entryId = uts?.tournamentEntryId;
                // tournament-seed-map-select unit 1 — surface the tournament seed for
                // map-pool selection. Old-schema responses (no tournament node) simply
                // leave it absent → map index 0 fallback.
                if (state.tournament != null)
                {
                    TournamentSeed = state.tournament.seed;
                    HasTournamentSeed = true;
                }
                // abandoned-match-reconciliation unit 1 — persist the just-opened
                // attempt so a hard kill can be reconciled (completed with 0) on the
                // next lobby entry. Guarded by the epoch check above: AbandonMatch's
                // _epoch++ drops this callback so it never Saves post-teardown. Skip
                // when the server returned no attemptId (nothing the client can complete).
                if (!string.IsNullOrEmpty(_attemptId))
                    PendingMatchStore.Save(_attemptId, UserSession.Current?.userId ?? string.Empty,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                Debug.Log($"[TournamentReporter] play ok — status={uts?.status} attemptId={_attemptId} entryId={_entryId}");
            });
        }

        // onRanking fires only on the full success path (complete ok → result ok)
        // and only while the same match is still current.
        public static void ReportResult(int score, string battleLogJson,
            Action<TournamentApi.ResultData> onRanking = null)
        {
            if (!UserSession.HasAccount) return; // guest — nothing to report
            if (string.IsNullOrEmpty(_attemptId))
            {
                // play failed, or its response is still in flight — a match that
                // ends inside the play round-trip is dropped, not queued (demo).
                Debug.LogWarning("[TournamentReporter] no attemptId; complete skipped.");
                return;
            }
            if (_completeSent)
            {
                Debug.LogWarning("[TournamentReporter] complete already sent for this attempt; skipped.");
                return;
            }
            _completeSent = true;
            // abandoned-match-reconciliation unit 1 — clear-at-send: the moment a
            // terminal complete is initiated, drop the pending record so a later
            // lobby reconcile can never overwrite this real score with a 0 (a slow
            // complete + app kill would otherwise leave the record for reconcile).
            PendingMatchStore.Clear();

            string baseUrl = UserSession.GameServerBaseUrl;
            AuthCredential credential = UserSession.Credential;
            string entryId = _entryId;
            int epoch = _epoch;
            TournamentApi.Complete(baseUrl, credential, _attemptId, score, battleLogJson, (ok, error) =>
            {
                if (epoch != _epoch) return;
                if (!ok)
                {
                    Debug.LogWarning($"[TournamentReporter] complete failed: {error}");
                    return;
                }
                Debug.Log($"[TournamentReporter] complete ok — score={score}");
                if (onRanking == null || string.IsNullOrEmpty(entryId)) return;

                TournamentApi.GetResult(baseUrl, credential, entryId, (result, resultError) =>
                {
                    if (epoch != _epoch) return;
                    if (result == null)
                    {
                        Debug.LogWarning($"[TournamentReporter] ranking fetch failed: {resultError}");
                        return;
                    }
                    Debug.Log($"[TournamentReporter] ranking ok — {(result.entries != null ? result.entries.Count : 0)} entries");
                    onRanking(result);
                });
            });
        }

        // abandoned-match-reconciliation unit 1 — menu-exit abandon. The app is
        // alive, so the in-memory attemptId is authoritative. Bump the epoch first
        // so an in-flight play callback is dropped (it would otherwise Save a record
        // from the lobby we're leaving to). Sends complete(0) and clears the record.
        public static void AbandonMatch()
        {
            string attemptId = _attemptId;
            string baseUrl = UserSession.GameServerBaseUrl;
            AuthCredential credential = UserSession.Credential;

            _epoch++; // drop any in-flight play callback (no post-teardown Save)

            if (!UserSession.HasAccount) return;                          // guest
            if (string.IsNullOrEmpty(attemptId) || _completeSent) return; // play not back yet / already sent
            _completeSent = true;

            PendingMatchStore.Clear(); // clear-at-send
            TournamentApi.Complete(baseUrl, credential, attemptId, 0, "", (ok, error) =>
            {
                if (!ok) Debug.LogWarning($"[TournamentReporter] abandon complete failed: {error}");
                else Debug.Log("[TournamentReporter] abandon complete ok — score=0");
            });
        }

        // abandoned-match-reconciliation unit 1 — lobby recovery for a match the
        // client never got to terminally complete (hard kill / crash). Operates
        // purely on the persisted record + the CURRENT session (never the live
        // in-memory _attemptId): account-guards, then within the grace window sends
        // complete(0), otherwise discards and leaves the server's round cleanup to
        // finalize it. Clears the record before sending (optimistic) so a double-
        // fire (Awake + onSignedIn) can't double-complete.
        public static void ReconcilePending()
        {
            if (!PendingMatchStore.TryLoad(out var rec)) return;
            if (!UserSession.HasAccount) { PendingMatchStore.Clear(); return; }

            string currentUserId = UserSession.Current?.userId ?? string.Empty;
            if (currentUserId != rec.userId) { PendingMatchStore.Clear(); return; } // different account

            long elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rec.startedAtUnix;
            var action = PendingMatchPolicy.Decide(elapsed, PendingMatchPolicy.DefaultTtlSeconds);

            PendingMatchStore.Clear(); // optimistic — before send, blocks re-entrant double-complete
            if (action == PendingMatchAction.DiscardOnly)
            {
                Debug.Log($"[TournamentReporter] pending attempt discarded (elapsed={elapsed}s > TTL).");
                return;
            }

            string baseUrl = UserSession.GameServerBaseUrl;
            if (string.IsNullOrEmpty(baseUrl))
            {
                Debug.LogWarning("[TournamentReporter] pending reconcile skipped — no base URL.");
                return;
            }
            AuthCredential credential = UserSession.Credential;
            string attemptId = rec.attemptId;
            TournamentApi.Complete(baseUrl, credential, attemptId, 0, "", (ok, error) =>
            {
                if (!ok) Debug.LogWarning($"[TournamentReporter] reconcile complete failed: {error}");
                else Debug.Log($"[TournamentReporter] reconcile complete ok — attemptId={attemptId} score=0");
            });
        }
    }
}
