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
        }

        public static void Save(string attemptId, string userId, long startedAtUnix)
        {
            var record = new PendingMatchRecord
            {
                attemptId = attemptId ?? string.Empty,
                userId = userId ?? string.Empty,
                startedAtUnix = startedAtUnix,
            };
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
