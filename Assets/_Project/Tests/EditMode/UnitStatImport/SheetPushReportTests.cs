using NUnit.Framework;
using Wassup.Editor.UnitStatImport;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // sheet-export-push unit 3 — push 응답 봉투 파싱 회귀. 전송 없이 문자열만으로 검증
    // (BuildReport 가 transportError/body 문자열 시그니처라 SheetHttp 없이 호출 가능).
    public class SheetPushReportTests
    {
        [Test]
        public void Report_TransportError_SurfacesVerbatim()
        {
            string report = SheetPushClient.BuildReport("ConnectionError: timeout", null);
            StringAssert.Contains("transport error", report);
            StringAssert.Contains("timeout", report);
        }

        [Test]
        public void Report_SuccessFalse_SurfacesErrorDetail()
        {
            const string body = @"{ ""success"": false, ""errorDetail"": { ""errorMessage"": ""boom"" } }";
            string report = SheetPushClient.BuildReport(null, body);
            StringAssert.Contains("rejected", report);
            StringAssert.Contains("boom", report);
        }

        [Test]
        public void Report_Success_SummarizesPerTabCounts()
        {
            const string body = @"{ ""success"": true, ""data"": { ""results"": {
                ""Defenders"": { ""updated"": 3, ""added"": 1, ""orphans"": [] } } } }";
            string report = SheetPushClient.BuildReport(null, body);
            StringAssert.Contains("Push OK", report);
            StringAssert.Contains("Defenders: updated 3, added 1", report);
            StringAssert.DoesNotContain("고아", report);
        }

        [Test]
        public void Report_Orphans_ListedAndFlagged()
        {
            const string body = @"{ ""success"": true, ""data"": { ""results"": {
                ""DcCardEffects"": { ""updated"": 2, ""added"": 0,
                ""orphans"": [""card_x:3"", ""card_y:0""] } } } }";
            string report = SheetPushClient.BuildReport(null, body);
            StringAssert.Contains("고아", report);
            StringAssert.Contains("card_x:3", report);
            StringAssert.Contains("card_y:0", report);
        }
    }
}
