using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Wassup.Core.Api;

namespace Wassup.Tests.EditMode.Api
{
    // tournament-play-report Unit 0 — pure parse/build coverage (no live network).
    public class TournamentApiTests
    {
        // ── play ─────────────────────────────────────────────────────────────────

        [Test]
        public void TryParsePlay_Success_BindsConsumedFields()
        {
            // server nests the attempt fields under data.userTournamentState (2026-07
            // schema change); data.tournament is present but not consumed.
            const string body = @"{ ""success"": true, ""data"": {
                ""tournament"": { ""status"": ""IN_PROGRESS"", ""typeId"": 1 },
                ""userTournamentState"": {
                    ""status"": ""IN_PROGRESS"",
                    ""tournamentTypeId"": 1,
                    ""tournamentEntryId"": ""e-1"",
                    ""tournamentEntryAttemptId"": ""a-1"" } } }";

            var state = TournamentApi.TryParsePlay(body, out string error);

            Assert.IsNull(error);
            Assert.AreEqual("IN_PROGRESS", state.userTournamentState.status);
            Assert.AreEqual("e-1", state.userTournamentState.tournamentEntryId);
            Assert.AreEqual("a-1", state.userTournamentState.tournamentEntryAttemptId);
        }

        [Test]
        public void TryParsePlay_ErrorDetail_ReportsCode()
        {
            const string body = @"{ ""success"": false, ""errorDetail"": {
                ""errorCode"": ""SHOP_NOT_ENOUGH_ASSET"", ""errorMessage"": ""잔액 부족"" } }";

            var state = TournamentApi.TryParsePlay(body, out string error);

            Assert.IsNull(state);
            StringAssert.Contains("SHOP_NOT_ENOUGH_ASSET", error);
        }

        [Test]
        public void TryParsePlay_MissingData_ReturnsError()
        {
            Assert.IsNull(TournamentApi.TryParsePlay(@"{ ""success"": true }", out string error));
            StringAssert.Contains("data", error);
        }

        // ── URLs ─────────────────────────────────────────────────────────────────

        [Test]
        public void BuildUrls_TrimTrailingSlashAndComposePathParams()
        {
            const string baseUrl = "https://dev-api-somnia.cashroyale.games/ ";

            Assert.AreEqual("https://dev-api-somnia.cashroyale.games/tournament/play",
                TournamentApi.BuildPlayUrl(baseUrl));
            Assert.AreEqual("https://dev-api-somnia.cashroyale.games/tournament/complete/a-1/420",
                TournamentApi.BuildCompleteUrl(baseUrl, "a-1", 420));
            Assert.AreEqual("https://dev-api-somnia.cashroyale.games/tournament/result/tournament/e-1",
                TournamentApi.BuildResultUrl(baseUrl, "e-1"));
            Assert.AreEqual("https://dev-api-somnia.cashroyale.games/tournament/result/entry/unclaimed",
                TournamentApi.BuildUnclaimedUrl(baseUrl));
        }

        // ── complete body ────────────────────────────────────────────────────────

        [Test]
        public void BuildCompleteBody_EscapesEmbeddedJson_RoundTrips()
        {
            // battle log JSON is a string value inside the body — quotes and
            // newlines must survive the embedding.
            const string debugJson = "{\"result\":{\"outcome\":\"victory\"},\n\"note\":\"line1\nline2\"}";

            string body = TournamentApi.BuildCompleteBody(debugJson);
            var parsed = JObject.Parse(body);

            Assert.AreEqual(debugJson, parsed.Value<string>("debug"));
        }

        [Test]
        public void BuildCompleteBody_NullDebug_YieldsEmptyString()
        {
            var parsed = JObject.Parse(TournamentApi.BuildCompleteBody(null));
            Assert.AreEqual(string.Empty, parsed.Value<string>("debug"));
        }

        // ── result ───────────────────────────────────────────────────────────────

        [Test]
        public void TryParseResult_Success_BindsEntries()
        {
            const string body = @"{ ""success"": true, ""data"": {
                ""name"": ""Weekly Cup"",
                ""entryCount"": 2,
                ""maxEntryCount"": 10,
                ""entries"": [
                    { ""userId"": ""u-1"", ""userName"": ""sj"", ""score"": 900, ""rank"": 1 },
                    { ""userId"": ""u-2"", ""userName"": ""bot"", ""score"": 450, ""rank"": 2 } ] } }";

            var result = TournamentApi.TryParseResult(body, out string error);

            Assert.IsNull(error);
            Assert.AreEqual("Weekly Cup", result.name);
            Assert.AreEqual(2, result.entryCount);
            Assert.AreEqual(10, result.maxEntryCount);
            Assert.AreEqual(2, result.entries.Count);
            Assert.AreEqual("sj", result.entries[0].userName);
            Assert.AreEqual(900, result.entries[0].score);
            Assert.AreEqual(1, result.entries[0].rank);
            Assert.AreEqual("u-2", result.entries[1].userId);
        }

        // ── unclaimed list ─────────────────────────────────────────────────────────

        [Test]
        public void TryParseUnclaimed_Success_BindsList()
        {
            // envelope data is a bare array of UserTournamentResultEntry.
            const string body = @"{ ""success"": true, ""data"": [
                { ""tournamentEntryId"": ""e-1"", ""userId"": ""u-1"", ""tournamentTypeId"": 1,
                  ""tournamentName"": ""Weekly Cup"", ""score"": 900, ""rank"": 3,
                  ""claimed"": false, ""createdTime"": ""2026-07-20T04:05:06Z"" },
                { ""tournamentEntryId"": ""e-2"", ""tournamentName"": ""Daily Rush"",
                  ""score"": 120, ""rank"": 7, ""claimed"": true } ] }";

            var list = TournamentApi.TryParseUnclaimed(body, out string error);

            Assert.IsNull(error);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("e-1", list[0].tournamentEntryId);
            Assert.AreEqual("Weekly Cup", list[0].tournamentName);
            Assert.AreEqual(900, list[0].score);
            Assert.AreEqual(3, list[0].rank);
            Assert.IsFalse(list[0].claimed);
            Assert.AreEqual("2026-07-20T04:05:06Z", list[0].createdTime);
            Assert.AreEqual("e-2", list[1].tournamentEntryId);
            Assert.IsTrue(list[1].claimed);
        }

        [Test]
        public void TryParseUnclaimed_EmptyArray_ReturnsEmpty()
        {
            var list = TournamentApi.TryParseUnclaimed(@"{ ""success"": true, ""data"": [] }", out string error);

            Assert.IsNull(error);
            Assert.IsNotNull(list);
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void TryParseUnclaimed_NullData_ReturnsEmpty()
        {
            // some servers omit [] and send null for an empty list — a success
            // envelope with null data must read as empty, not as a fetch failure.
            var list = TournamentApi.TryParseUnclaimed(@"{ ""success"": true, ""data"": null }", out string error);

            Assert.IsNull(error);
            Assert.IsNotNull(list);
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void TryParseUnclaimed_ErrorDetail_ReportsCode()
        {
            const string body = @"{ ""success"": false, ""errorDetail"": {
                ""errorCode"": ""UNAUTHORIZED"", ""errorMessage"": ""토큰 만료"" } }";

            var list = TournamentApi.TryParseUnclaimed(body, out string error);

            Assert.IsNull(list);
            StringAssert.Contains("UNAUTHORIZED", error);
        }

        // ── UserSession baseUrl carry ────────────────────────────────────────────

        [Test]
        public void UserSession_Set_CarriesBaseUrl_AndClearDropsIt()
        {
            UserSession.Clear();

            UserSession.Set(new UserSignApi.SignedInUser { userId = "u-1" }, "id-token",
                "https://dev-api-somnia.cashroyale.games");
            Assert.AreEqual("https://dev-api-somnia.cashroyale.games", UserSession.GameServerBaseUrl);

            // the 2-arg form (pre-existing callers) must not wipe a stored URL.
            UserSession.Set(new UserSignApi.SignedInUser { userId = "u-2" }, "id-token-2");
            Assert.AreEqual("https://dev-api-somnia.cashroyale.games", UserSession.GameServerBaseUrl);

            UserSession.Clear();
            Assert.IsNull(UserSession.GameServerBaseUrl);
        }
    }
}
