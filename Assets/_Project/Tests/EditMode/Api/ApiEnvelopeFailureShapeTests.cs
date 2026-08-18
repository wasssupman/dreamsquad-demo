using NUnit.Framework;
using Wassup.Core.Api;

namespace Wassup.Tests.EditMode.Api
{
    // 증상 회귀 방지 (2026-08-18, 실기기): 판을 끝내고 로비로 나오자
    // InvalidCastException 이 콘솔에 떴다.
    //
    //   ApiEnvelope.TryGetData → TournamentApi.Complete 의 응답 콜백 → AsyncOperation
    //
    // 서버가 실패 응답의 errorDetail 안 문자열 칸에 객체를 넣어 보내면, 실패를 **설명하는**
    // 줄(detail.Value<string>(...))이 예외를 던졌다. 그 예외는 UnityWebRequest 완료 콜백
    // 안에서 삼켜져 onDone 이 영영 불리지 않고 → TournamentMatchReporter 의 in-flight
    // 카운터가 1 에 박혀 이후 로비 reconcile 이 영구히 skip 됐다(서버 락 미해제).
    //
    // 계약: 이 seam 은 **어떤 바디가 와도 던지지 않는다**. 실패는 항상 false + 사람이 읽을
    // 수 있는 error 로 나온다.
    public sealed class ApiEnvelopeFailureShapeTests
    {
        [Test]
        public void FailureEnvelope_WithObjectInDetailMessage_ReportsInsteadOfThrowing()
        {
            const string body = @"{ ""success"": false, ""errorDetail"": {
                ""errorCode"": ""INTERNAL_SERVER_ERROR"",
                ""errorMessage"": ""처리 중 오류"",
                ""detailMessage"": { ""path"": ""/tournament/complete"", ""cause"": ""null attempt"" } } }";

            bool ok = ApiEnvelope.TryGetData(body, out var data, out string error);

            Assert.IsFalse(ok);
            Assert.IsNull(data);
            StringAssert.Contains("INTERNAL_SERVER_ERROR", error);
            // 스칼라가 아니어도 내용을 버리지 않는다 — 원문을 그대로 붙여 진단이 가능해야 한다.
            StringAssert.Contains("/tournament/complete", error);
        }

        [Test]
        public void FailureEnvelope_WithObjectInErrorMessage_ReportsInsteadOfThrowing()
        {
            const string body = @"{ ""success"": false, ""errorDetail"": {
                ""errorCode"": ""BAD_REQUEST"", ""errorMessage"": { ""ko"": ""잘못된 요청"" } } }";

            Assert.IsFalse(ApiEnvelope.TryGetData(body, out _, out string error));
            StringAssert.Contains("BAD_REQUEST", error);
            StringAssert.Contains("잘못된 요청", error);
        }

        [Test]
        public void FailureEnvelope_WithNonObjectErrorDetail_ReportsInsteadOfThrowing()
        {
            const string body = @"{ ""success"": false, ""errorDetail"": ""서버 점검 중"" }";

            Assert.IsFalse(ApiEnvelope.TryGetData(body, out _, out string error));
            StringAssert.Contains("서버 점검 중", error);
        }

        [Test]
        public void FailureEnvelope_WithErrorDetailArray_ReportsInsteadOfThrowing()
        {
            const string body = @"{ ""success"": false, ""errorDetail"": [ { ""field"": ""score"" } ] }";

            Assert.IsFalse(ApiEnvelope.TryGetData(body, out _, out string error));
            StringAssert.Contains("score", error);
        }

        [Test]
        public void SuccessFlag_ThatIsNotAScalar_IsTreatedAsFailure()
        {
            const string body = @"{ ""success"": { ""value"": true }, ""data"": { ""x"": 1 } }";

            Assert.IsFalse(ApiEnvelope.TryGetData(body, out _, out string error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void SuccessFlag_AsQuotedBoolean_StillCountsAsSuccess()
        {
            // 일부 게이트웨이는 불리언을 문자열로 직렬화한다. 이전 구현(Value<bool?>)이
            // 받아주던 모양이라 계약을 좁히지 않는다.
            const string body = @"{ ""success"": ""true"", ""data"": { ""x"": 1 } }";

            Assert.IsTrue(ApiEnvelope.TryGetData(body, out var data, out string error), error);
            Assert.IsNotNull(data);
        }

        // ── 기존 동작 보존 ────────────────────────────────────────────────────────

        [Test]
        public void FailureEnvelope_WithPlainStrings_KeepsTheOriginalMessageShape()
        {
            const string body = @"{ ""success"": false, ""errorDetail"": {
                ""errorCode"": ""AUTHENTICATION_FAIL"", ""errorMessage"": ""인증 실패"" } }";

            Assert.IsFalse(ApiEnvelope.TryGetData(body, out _, out string error));
            StringAssert.Contains("AUTHENTICATION_FAIL", error);
            StringAssert.Contains("인증 실패", error);
        }

        [Test]
        public void FailureEnvelope_WithoutErrorDetail_KeepsTheOriginalMessage()
        {
            Assert.IsFalse(ApiEnvelope.TryGetData(@"{ ""success"": false }", out _, out string error));
            StringAssert.Contains("no errorDetail", error);
        }
    }
}
