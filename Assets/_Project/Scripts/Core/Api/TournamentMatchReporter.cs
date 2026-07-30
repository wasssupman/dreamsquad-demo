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

        // tournament-flow-guards unit 6 → unit 9 — complete in-flight 가드. 성공 확인
        // 후에만 pending 을 지우므로, 응답 전에는 레코드가 남아 있다. 그 창 동안 다른
        // 경로가 같은 attempt 를 또 마감하는 것을 막는다: reconcile 중복 발화(Awake+
        // onSignedIn) 뿐 아니라, **나가기(AbandonMatch) 직후 로비 진입 reconcile** —
        // 씬 전환(수백 ms)이 complete 왕복보다 대개 빨라 상시 겹친다.
        // 카운터인 이유: 서로 다른 attempt 의 complete 두 개가 겹칠 수 있다(지연된
        // reconcile(A) + 새 매치의 abandon(B)). bool 이면 먼저 돌아온 응답이 가드를
        // 조기 해제해 세 번째 발신이 끼어든다.
        private static int _completesInFlight;

        // DisableDomainReload 환경에서 static 이 Play 세션을 넘어 잔존한다. 이전 세션이
        // complete in-flight 인 채 끝나면(콜백 미발화) 카운터가 박혀 reconcile 이 영구
        // skip 되므로 Play 진입마다 초기화한다(LobbyReactionLock 선례). 이전 세션의
        // 고아 콜백이 뒤늦게 와도 감소는 0 클램프, 제거는 compare-and-clear 라 무해.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInFlightOnPlayEnter() => _completesInFlight = 0;

        private static void CompleteReturned()
            => _completesInFlight = Math.Max(0, _completesInFlight - 1);

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
        public static void ReportResult(int score, string deckInfoJson,
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

            string baseUrl = UserSession.GameServerBaseUrl;
            AuthCredential credential = UserSession.Credential;
            string attemptId = _attemptId;
            string entryId = _entryId;
            int epoch = _epoch;
            // tournament-flow-guards unit 9 — clear-on-success (구 clear-at-send 폐기).
            // 예전엔 전송 **직전**에 pending 을 지웠는데, complete 가 실패하면 attemptId 를
            // 잃어 서버에 열린 락을 클라가 영영 못 풀었다(= 서버 배치까지 대기). 이 안전망이
            // 필요한 유일한 경우가 바로 complete 실패인데 그때 레코드가 없던 셈이다.
            // 이제 실패면 레코드를 남겨 다음 로비 reconcile 이 complete(0) 로 마감한다.
            _completesInFlight++;
            TournamentApi.Complete(baseUrl, credential, attemptId, score, deckInfoJson, (ok, error) =>
            {
                CompleteReturned();
                // 락 해제 성사 여부는 이 매치의 생사와 무관하다 — epoch 가드보다 앞에서
                // 처리해야 RESTART 로 epoch 가 바뀐 뒤 돌아온 성공 응답도 레코드를 정리한다.
                if (ok) PendingMatchStore.ClearIfMatches(attemptId);
                if (epoch != _epoch) return;
                if (!ok)
                {
                    Debug.LogWarning($"[TournamentReporter] complete failed (pending 유지 → 다음 로비 reconcile): {error}");
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

            // unit 9 — clear-on-success. 나가기 직후 씬 전환 → 로비 reconcile 이 이 왕복과
            // 겹치는 것은 _completesInFlight 가 막는다.
            _completesInFlight++;
            // tournament-deck-info unit 1 — deckInfo 는 빈 값. 0점 마감은 최고점 후보가
            // 아니므로 덱을 기록할 이유가 없다.
            TournamentApi.Complete(baseUrl, credential, attemptId, 0, "", (ok, error) =>
            {
                CompleteReturned();
                if (!ok) Debug.LogWarning($"[TournamentReporter] abandon complete failed (pending 유지 → 다음 reconcile 트리거에서 재시도): {error}");
                else
                {
                    PendingMatchStore.ClearIfMatches(attemptId);
                    Debug.Log("[TournamentReporter] abandon complete ok — score=0");
                }
            });
        }

        // abandoned-match-reconciliation unit 1 (+ tournament-flow-guards unit 5·6) —
        // lobby recovery for a match the client never terminally completed (hard kill /
        // crash / 미완주). Operates purely on the persisted record + the CURRENT session
        // (never the live in-memory _attemptId): account-guards, then **나이 무관 항상**
        // complete(0) 로 그 attempt 를 마감해 서버 락을 푼다. pending 은 complete 가 성공한
        // 뒤에만 지운다(실패면 유지 → 다음 로비 재시도). 재진입 중복 complete 는
        // _completesInFlight 카운터로 막는다(unit 9 에서 세 발신 경로 공통으로 확장).
        public static void ReconcilePending() => ReconcilePending(null);

        // unit 7 — onDone(released): pending attemptId 로 complete(0) 가 **실제 성공**해
        // 락이 풀렸을 때만 true. 그 외(무레코드/무계정/계정 불일치/in-flight/전송 실패)는
        // false. BeginMatchFromLobby 의 락 복구 재시도가 이 신호에 게이트된다.
        public static void ReconcilePending(Action<bool> onDone)
        {
            // unit 9 — in-flight complete 가 있으면(reconcile 중복 발화, 또는 방금 나가기로
            // 보낸 abandon) 이번 발화는 skip 한다(대기 아님 — 재시도는 다음 reconcile
            // 트리거: 다음 로비 진입 또는 `시작` 락 복구). 아직 안 지워진 레코드를 보고
            // 같은 attempt 를 두 번 마감하지 않기 위함.
            if (_completesInFlight > 0) { onDone?.Invoke(false); return; }
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
            _completesInFlight++;
            AuthCredential credential = UserSession.Credential;
            string attemptId = rec.attemptId;
            // tournament-deck-info unit 1 — deckInfo 는 빈 값. 이 경로는 **이전 세션/
            // 하드킬의 attempt** 를 뒤늦게 닫는 것이라, 지금 메모리의 덱을 붙이면 남의
            // 판에 엉뚱한 덱이 박힌다.
            TournamentApi.Complete(baseUrl, credential, attemptId, 0, "", (ok, error) =>
            {
                CompleteReturned();
                if (ok)
                {
                    // 성공 확인 후에만 제거. unit 9 — 그 사이 새 매치가 저장한 레코드는
                    // 남긴다(무조건 Clear 면 다음 판의 안전망을 지운다).
                    PendingMatchStore.ClearIfMatches(attemptId);
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
