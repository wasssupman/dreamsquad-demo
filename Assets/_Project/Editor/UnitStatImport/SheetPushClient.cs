using System;
using System.Text;
using Newtonsoft.Json.Linq;
using Wassup.SheetSync;

namespace Wassup.Editor.UnitStatImport
{
    // sheet-export-push unit 3 — payload 를 Apps Script /exec 로 POST 하고 반영 결과
    // 봉투를 사람이 읽는 요약으로 만든다. 전송/봉투검증은 SheetSync 코어(SheetHttp/
    // SheetEnvelope). 비파괴 불변식: 클라이언트는 절대 삭제를 요청하지 않는다 — 고아는
    // 서버가 계산해 리포트하고 여기선 그 목록을 보여주기만 한다.
    public static class SheetPushClient
    {
        public static void Push(string scriptUrl, string payloadJson, Action<string> onDone)
        {
            SheetHttp.Post(scriptUrl, payloadJson, result =>
            {
                string report;
                try { report = BuildReport(result.transportError, result.body); }
                catch (Exception e) { report = $"Push failed: {e}"; }
                onDone(report);
            });
        }

        // 문자열 시그니처 — SheetHttp.Result 구조에 묶지 않아 EditMode 에서 봉투/응답
        // 파싱만 단위 검증 가능(전송 없이).
        internal static string BuildReport(string transportError, string body)
        {
            if (!string.IsNullOrEmpty(transportError))
                return $"Push transport error: {transportError}";

            if (!SheetEnvelope.TryGetData(body, out var data, out var error))
                return $"Push rejected: {error}";

            var results = data["results"] as JObject;
            if (results == null)
                return "Push OK. (응답에 results 없음)";

            var lines = new StringBuilder();
            int totalOrphans = 0, tabErrors = 0;
            foreach (var prop in results.Properties())
            {
                // 서버가 그 탭을 스킵한 경우(키 컬럼 결측 등) — 카운트가 아니라 경고로.
                var tabError = prop.Value["error"]?.ToString();
                if (!string.IsNullOrEmpty(tabError))
                {
                    lines.AppendLine($"  ⚠ {prop.Name}: SKIPPED — {tabError}");
                    tabErrors++;
                    continue;
                }
                int updated = prop.Value["updated"]?.Value<int>() ?? 0;
                int added = prop.Value["added"]?.Value<int>() ?? 0;
                int orphanCount = (prop.Value["orphans"] as JArray)?.Count ?? 0;
                totalOrphans += orphanCount;
                lines.AppendLine($"  {prop.Name}: updated {updated}, added {added}"
                    + (orphanCount > 0 ? $", orphans {orphanCount}" : ""));
            }

            var sb = new StringBuilder();
            sb.AppendLine(tabErrors > 0
                ? $"Push 완료 (⚠ {tabErrors}개 탭 스킵 — 확인 요망):"
                : "Push OK.");
            sb.Append(lines);

            if (totalOrphans > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"⚠ 고아 행 {totalOrphans}건 — 시트엔 있고 이번 payload 엔 없음. "
                    + "삭제하지 않았으니 확인 요망:");
                foreach (var prop in results.Properties())
                {
                    var orphans = prop.Value["orphans"] as JArray;
                    if (orphans == null || orphans.Count == 0) continue;
                    sb.AppendLine($"  {prop.Name}: {string.Join(", ", orphans)}");
                }
            }

            return sb.ToString();
        }
    }
}
