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

        // tournament-flow-guards unit 6 — reconcile in-flight 가드(성공 확인 후에만 pending
        // 을 지우므로, Awake+onSignedIn 동시 발화 시 중복 complete 를 이 플래그로 막는다).
        private static bool _reconciling;

        // tournament-seed-map-select unit 1 — the server tournament seed from the
        // latest play response. Map-pool selection reads it at map-build time;
        // absent (guest, in-flight, failed) means the caller falls back to index 0.
        private static bool _lobbyIssued;
        public static bool HasTournamentSeed { get; private set; }
        public static ulong TournamentSeed { get; private set; }

        // tournament-flow-guards unit 1 — 게이트 진입. play 응답을 대기해 attemptId 를
        // 확보(성공)해야만 onReady 로 배틀 전환을 허용한다. 실패/무응답(API 10s 타임아웃)
        // 은 onFailed 로 표면화 → 호출자가 알림 후 입장 취소. 게스트는 attempt 자체가
        // 없으므로 게이트 비대상(즉시 onReady). 선발행 목적(seed 를 맵 빌드 전에 확보)은
        // 유지되며, await 로 시드는 오히려 입장 전에 확정된다.
        public static void BeginMatchFromLobby(Action onReady, Action<string> onFailed)
        {
            _lobbyIssued = false; // re-entrance safety: always issue fresh from here
            BeginMatchInternal(onReady, error =>
            {
                // unit 7 — 락 유형(서버에 열린 attempt) 실패는, 클라가 그 attemptId 를
                // 쥔 경우(pending)에 한해 complete(0) 로 락을 풀고 play 를 1회 재시도한다.
                // pending 이 없는 orphan 락은 클라가 못 푼다(unit 4 라이브 프로브 결론)
                // → 그대로 표면화해 호출자가 락 전용 안내를 띄운다.
                if (!IsLockError(error)) { onFailed?.Invoke(error); return; }
                ReconcilePending(released =>
                {
                    if (released) BeginMatchInternal(onReady, onFailed); // 1회 — 재실패는 그대로 표면화
                    else onFailed?.Invoke(error);
                });
            });
        }

        // unit 7 — play-while-locked 판정. 열린 attempt 가 있으면 서버가 500 에
        // "cannot wait" 를 실어 거부한다(unit 4 라이브 프로브로 확정한 계약). 에러
        // 문자열 내 구문 포함 여부로 판정 — 어느 errorDetail 필드에 실리든 견딘다.
        internal static bool IsLockError(string error)
            => !string.IsNullOrEmpty(error)
               && error.IndexOf("cannot wait", StringComparison.OrdinalIgnoreCase) >= 0;

        // GameManager.OnEnable 진입용(비게이트). 로비 발행 attempt 를 adopt 하거나,
        // 로비를 거치지 않은 진입(TestMode/에디터 직접 Play)이면 상태만 리셋하고
        // **발행하지 않는다** (unit 8 — 결함 A: 개발/테스트 진입이 실서버 토너먼트
        // 엔트리를 만들지 않게). ReportResult/AbandonMatch 는 attemptId 부재로 자연
        // 스킵되고, 시드 부재는 맵풀 index 0 폴백으로 흡수된다.
        public static void BeginMatch()
        {
            // adopt: skip the state reset too (resetting would stale-drop the
            // in-flight play response carrying the seed).
            if (_lobbyIssued) { _lobbyIssued = false; return; }

            _epoch++;
            _attemptId = null;   // 직전 매치의 stale attempt 에 이 판의 점수가 제출되면 안 된다
            _entryId = null;
            _completeSent = false;
            HasTournamentSeed = false;
            TournamentSeed = 0;
        }

        // 게이트 진입 전용 발행 경로 — 유일한 play 발행 창구(unit 8).
        private static void BeginMatchInternal(Action onReady, Action<string> onFailed)
        {
            _epoch++;
            _attemptId = null;   // an unfinished previous attempt is abandoned, not completed
            _entryId = null;
            _completeSent = false;
            // clear before any early return — a stale seed from a previous session
            // must never pick the map for a new match (e.g. sign-out → guest play).
            HasTournamentSeed = false;
            TournamentSeed = 0;

            if (!UserSession.HasAccount)
            {
                // guest / not signed in — no tournament attempt, entry is not gated:
                // proceed immediately.
                if (onReady != null) { _lobbyIssued = true; onReady(); }
                return;
            }
            string baseUrl = UserSession.GameServerBaseUrl;
            if (string.IsNullOrEmpty(baseUrl))
            {
                Debug.LogWarning("[TournamentReporter] signed in but no base URL in session; play skipped.");
                onFailed?.Invoke("서버 주소가 없습니다.");
                return;
            }

            int epoch = _epoch;
            TournamentApi.Play(baseUrl, UserSession.Credential, (state, error) =>
            {
                if (epoch != _epoch) return;
                if (state == null)
                {
                    Debug.LogWarning($"[TournamentReporter] play failed: {error}");
                    onFailed?.Invoke(error);   // gated: surface; non-gated: null → no-op
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
                // tournament-flow-guards unit 1 — 게이트는 attemptId 확보 기준. HTTP 200
                // 이어도 attempt 가 비면 실패로 본다 — attempt 없이 입장하면 서버 락만 걸리고
                // 강제 0점 되는 그 버그가 그대로 재발한다.
                if (string.IsNullOrEmpty(_attemptId))
                {
                    Debug.LogWarning("[TournamentReporter] play ok but empty attemptId — treated as failure.");
                    onFailed?.Invoke("attempt 발급 실패");
                    return;
                }
                // abandoned-match-reconciliation unit 1 — persist the just-opened attempt
                // so a hard kill can be reconciled on the next lobby entry (epoch-guarded).
                PendingMatchStore.Save(_attemptId, UserSession.Current?.userId ?? string.Empty,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                Debug.Log($"[TournamentReporter] play ok — status={uts?.status} attemptId={_attemptId} entryId={_entryId}");
                if (onReady != null) { _lobbyIssued = true; onReady(); }
            });
        }

        // onRanking fires only on the full success path (complete ok → result ok)
        // and only while the same match is still current.
        public static void ReportResult(int score, string battleLogJson,
            Action<TournamentApi.ResultData> onRanking = null, Action<string> onError = null)
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
                    onError?.Invoke(error);   // tournament-flow-guards unit 2 — 실제 전송 실패만 알림
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

        // abandoned-match-reconciliation unit 1 (+ tournament-flow-guards unit 5·6) —
        // lobby recovery for a match the client never terminally completed (hard kill /
        // crash / 미완주). Operates purely on the persisted record + the CURRENT session
        // (never the live in-memory _attemptId): account-guards, then **나이 무관 항상**
        // complete(0) 로 그 attempt 를 마감해 서버 락을 푼다. pending 은 complete 가 성공한
        // 뒤에만 지운다(실패면 유지 → 다음 로비 재시도). 재진입 중복 complete 는 _reconciling
        // in-flight 플래그로 막는다.
        public static void ReconcilePending() => ReconcilePending(null);

        // unit 7 — onDone(released): pending attemptId 로 complete(0) 가 **실제 성공**해
        // 락이 풀렸을 때만 true. 그 외(무레코드/무계정/계정 불일치/in-flight/전송 실패)는
        // false. BeginMatchFromLobby 의 락 복구 재시도가 이 신호에 게이트된다.
        public static void ReconcilePending(Action<bool> onDone)
        {
            if (_reconciling) { onDone?.Invoke(false); return; } // in-flight — 중복 complete 방지(Awake+onSignedIn)
            if (!PendingMatchStore.TryLoad(out var rec)) { onDone?.Invoke(false); return; }
            if (!UserSession.HasAccount) { PendingMatchStore.Clear(); onDone?.Invoke(false); return; }

            string currentUserId = UserSession.Current?.userId ?? string.Empty;
            if (currentUserId != rec.userId) { PendingMatchStore.Clear(); onDone?.Invoke(false); return; } // different account

            string baseUrl = UserSession.GameServerBaseUrl;
            if (string.IsNullOrEmpty(baseUrl))
            {
                Debug.LogWarning("[TournamentReporter] pending reconcile skipped — no base URL.");
                onDone?.Invoke(false);
                return;
            }

            // tournament-flow-guards unit 5·6 — 락은 스코어 제출로만 풀린다(사용자 모델).
            // 나이(TTL) 무관 항상 complete(0) 로 마감해 락을 푼다. **핵심**: pending 은
            // complete 가 **성공한 뒤에만** 지운다. 예전엔 전송 전에 optimistic clear 했는데,
            // complete 가 실패하면 attemptId 를 잃어 열린 락을 영영 못 풀었다(영구 500 원인).
            // 실패면 pending 을 그대로 둬서 다음 로비 진입에 재시도한다. 응답 받은 attempt 만
            // pending 에 있으므로 "응답 없으면 세션관리 안 함" 규칙과도 일치.
            _reconciling = true;
            AuthCredential credential = UserSession.Credential;
            string attemptId = rec.attemptId;
            TournamentApi.Complete(baseUrl, credential, attemptId, 0, "", (ok, error) =>
            {
                _reconciling = false;
                if (ok)
                {
                    PendingMatchStore.Clear(); // 성공 확인 후에만 제거
                    Debug.Log($"[TournamentReporter] reconcile complete ok — attemptId={attemptId} score=0");
                }
                else
                {
                    Debug.LogWarning($"[TournamentReporter] reconcile complete failed (pending 유지 → 다음 로비 재시도): {error}");
                }
                onDone?.Invoke(ok);
            });
        }
    }
}
