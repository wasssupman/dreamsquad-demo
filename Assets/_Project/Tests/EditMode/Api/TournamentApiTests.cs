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
        public void TryParsePlay_BindsTournamentSeed()
        {
            // tournament-seed-map-select unit 0 — live dev-server shape (2026-07-23):
            // the uint64 seed sits next to fields we don't consume.
            const string body = @"{ ""success"": true, ""data"": {
                ""tournament"": { ""status"": ""IN_PROGRESS"", ""typeId"": 1,
                    ""seed"": 9128566303723636648, ""name"": ""기본 토너먼트 테스트"" },
                ""userTournamentState"": {
                    ""status"": ""IN_PROGRESS"",
                    ""tournamentEntryId"": ""e-1"",
                    ""tournamentEntryAttemptId"": ""a-1"" } } }";

            var state = TournamentApi.TryParsePlay(body, out string error);

            Assert.IsNull(error);
            Assert.AreEqual(9128566303723636648UL, state.tournament.seed);
            Assert.AreEqual("a-1", state.userTournamentState.tournamentEntryAttemptId);
        }

        [Test]
        public void TryParsePlay_MissingTournamentNode_StillBindsAttempt()
        {
            // old-schema defence: no tournament node → PlayState stays valid,
            // tournament is null (seed availability is unit 1's judgement).
            const string body = @"{ ""success"": true, ""data"": {
                ""userTournamentState"": {
                    ""status"": ""IN_PROGRESS"",
                    ""tournamentEntryId"": ""e-1"",
                    ""tournamentEntryAttemptId"": ""a-1"" } } }";

            var state = TournamentApi.TryParsePlay(body, out string error);

            Assert.IsNull(error);
            Assert.IsNull(state.tournament);
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
            // tournament-deck-info unit 1 — deck JSON is a string value inside the
            // body; quotes must survive the embedding.
            const string deckInfoJson = "{\"v\":1,\"squad\":{\"units\":[\"u1\"],\"stones\":[]},\"dc\":{\"cards\":[]}}";

            string body = TournamentApi.BuildCompleteBody(deckInfoJson);
            var parsed = JObject.Parse(body);

            Assert.AreEqual(deckInfoJson, parsed.Value<string>("deckInfo"));
        }

        [Test]
        public void BuildCompleteBody_NoDeck_OmitsKeyEntirely()
        {
            // 0점 마감(나가기·reconcile)이 빈 값을 실어 최고점 판의 덱 기록을 덮어쓰는
            // 것을 막는다 — 키를 아예 빼서 `{}` 를 보낸다.
            foreach (string noDeck in new[] { null, "" })
            {
                var parsed = JObject.Parse(TournamentApi.BuildCompleteBody(noDeck));
                Assert.IsFalse(parsed.ContainsKey("deckInfo"), $"input: {noDeck ?? "null"}");
            }
        }

        [Test]
        public void BuildCompleteBody_OmitsDebugKey()
        {
            // tournament-deck-info 계약 5 — 배틀 로그 전문을 네트워크로 올리는 경로는
            // 종료됐다. 빈 값으로 채우는 것도 아니고 키 자체가 나가지 않는다.
            var parsed = JObject.Parse(TournamentApi.BuildCompleteBody("{\"v\":1}"));

            Assert.IsFalse(parsed.ContainsKey("debug"));
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
                    { ""userId"": ""u-1"", ""userName"": ""sj"", ""score"": 900, ""rank"": 1,
                      ""deckInfo"": ""{\""v\"":1,\""squad\"":{\""units\"":[\""u1\""]}}"" },
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

            // tournament-deck-info unit 2 — 엔트리의 덱 정보는 문자열 그대로 실려오고,
            // 기록이 없는 참가(구 엔트리)는 null 로 남는다.
            var deck = TournamentDeckInfo.Deserialize(result.entries[0].deckInfo);
            CollectionAssert.AreEqual(new[] { "u1" }, deck.squad.units);
            Assert.IsNull(result.entries[1].deckInfo);
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
