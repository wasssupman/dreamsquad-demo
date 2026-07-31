using UnityEngine;

namespace Wassup.Core.Api
{
    // abandoned-match-reconciliation unit 0 — persists the one attempt the client
    // has NOT yet initiated a terminal complete for, so a hard-killed match can be
    // reconciled (completed with 0) on the next lobby entry. PlayerPrefs single JSON
    // key; Save/Clear flush immediately — kill survival is the whole point, and an
    // unflushed clear would let a zombie record resurrect and clobber a real score.
    public static class PendingMatchStore
    {
        private const string Key = "Wassup.PendingMatch";

        [System.Serializable]
        public struct PendingMatchRecord
        {
            public string attemptId;
            public string userId;
            public long startedAtUnix;
            // tournament-deck-info unit 4 — 이 attempt 의 덱 페이로드. reconcile 은 이전
            // 세션/하드킬의 attempt 를 닫으므로 그 시점 메모리에는 덱이 없다(로비에 로거도
            // GameManager 도 없다) — 판 중에 여기 적어둔 것이 그 경로의 유일한 출처다.
            // 이 필드가 없는 구 레코드는 JsonUtility 가 null 로 남겨 빈 값과 동치다.
            public string deckInfo;
        }

        public static void Save(string attemptId, string userId, long startedAtUnix)
        {
            // deckInfo 는 비운다 — 새 attempt 는 아직 덱이 없다(캐리인에서 SaveDeckInfo).
            Write(new PendingMatchRecord
            {
                attemptId = attemptId ?? string.Empty,
                userId = userId ?? string.Empty,
                startedAtUnix = startedAtUnix,
                deckInfo = string.Empty,
            });
        }

        // tournament-deck-info unit 4 — compare-and-write. 덱은 attempt 에 귀속되므로
        // 저장된 레코드가 **그 attempt** 일 때만 쓴다(ClearIfMatches 와 같은 형태). 이미
        // 다음 판이 자기 레코드를 저장한 뒤 이전 판의 늦은 호출이 도착하면, 그대로 쓰면
        // reconcile 이 남의 덱을 올린다. 반환값은 실제로 썼는지.
        public static bool SaveDeckInfo(string attemptId, string deckInfoJson)
        {
            if (string.IsNullOrEmpty(attemptId)) return false;
            if (!TryLoad(out var record) || record.attemptId != attemptId) return false;
            record.deckInfo = deckInfoJson ?? string.Empty;
            Write(record);
            return true;
        }

        private static void Write(PendingMatchRecord record)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(record));
            PlayerPrefs.Save();
        }

        // false when: key absent, empty/corrupt JSON, or a record with no attemptId
        // (nothing the client can complete).
        public static bool TryLoad(out PendingMatchRecord record)
        {
            record = default;
            string json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return false;
            try { record = JsonUtility.FromJson<PendingMatchRecord>(json); }
            catch { return false; }
            // tournament-deck-info unit 4 — 누락 필드를 여기서 정규화한다(구 빌드가 남긴
            // 레코드엔 deckInfo 키가 없다). 소비처가 null 체크를 반복하지 않게.
            if (record.deckInfo == null) record.deckInfo = string.Empty;
            return !string.IsNullOrEmpty(record.attemptId);
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        // tournament-flow-guards unit 9 — compare-and-clear. complete 가 성공한 뒤에야
        // 레코드를 지우는데, 그 왕복 동안 새 매치가 시작돼 자기 attemptId 를 저장했을 수
        // 있다. 무조건 Clear 하면 **다음 판의 안전망을 지운다** — 방금 마감한 그 attempt
        // 의 레코드일 때만 제거한다. 반환값은 실제로 지웠는지.
        public static bool ClearIfMatches(string attemptId)
        {
            if (string.IsNullOrEmpty(attemptId)) return false;
            if (!TryLoad(out var record) || record.attemptId != attemptId) return false;
            Clear();
            return true;
        }
    }
}
