using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Wassup.Core.Api;

namespace Wassup.Tests.EditMode.Api
{
    // tournament-deck-info unit 0 — deckInfo v1 페이로드 계약. 서버가 opaque string
    // 으로만 다루므로 **이 테스트가 포맷의 유일한 방어선**이다.
    public class TournamentDeckInfoTests
    {
        // ── serialize ────────────────────────────────────────────────────────────

        [Test]
        public void Serialize_KeysAreTheWireContract()
        {
            // 필드명이 곧 서버에 굳는 계약이다 — 리팩터로 조용히 바뀌면 과거 엔트리를
            // 못 읽는다. 문자열 키로 직접 확인한다.
            string json = TournamentDeckInfo.Serialize(
                new[] { "u1" }, new[] { "s1" }, new[] { "c1" });
            var parsed = JObject.Parse(json);

            Assert.AreEqual(1, parsed.Value<int>("v"));
            Assert.AreEqual("u1", parsed["squad"]["units"][0].Value<string>());
            Assert.AreEqual("s1", parsed["squad"]["stones"][0].Value<string>());
            Assert.AreEqual("c1", parsed["dc"]["cards"][0].Value<string>());
        }

        [Test]
        public void Serialize_DropsEmptySlots_KeepsOrder()
        {
            // 스쿼드/스톤 슬롯의 빈칸은 "" 로 저장된다(SquadSave.NormalizeSlots).
            // 압축은 하되 재정렬은 하지 않는다 — 배열 순서가 곧 표시 순서다.
            string json = TournamentDeckInfo.Serialize(
                new[] { "u_c", "", "u_a", "   ", "u_b" },
                new[] { "", "s_1" },
                new[] { "c_2", "" });

            var payload = TournamentDeckInfo.Deserialize(json);

            CollectionAssert.AreEqual(new[] { "u_c", "u_a", "u_b" }, payload.squad.units);
            CollectionAssert.AreEqual(new[] { "s_1" }, payload.squad.stones);
            CollectionAssert.AreEqual(new[] { "c_2" }, payload.dc.cards);
        }

        [Test]
        public void Serialize_AllEmpty_YieldsEmptyString()
        {
            // 빈 문자열 = 서버의 "기록 없음"(null)과 동치. 빈 배열 페이로드를 올리면
            // "빈 덱으로 플레이함"으로 읽혀 미기록과 구분되지 않는다.
            Assert.AreEqual(string.Empty, TournamentDeckInfo.Serialize(null, null, null));
            Assert.AreEqual(string.Empty, TournamentDeckInfo.Serialize(
                new string[0], new[] { "" }, new List<string> { "  " }));
        }

        [Test]
        public void Serialize_PartiallyFilled_StillSerializes()
        {
            // 드림스톤/카드를 하나도 안 낀 판도 유효한 기록이다.
            string json = TournamentDeckInfo.Serialize(new[] { "u1" }, null, null);
            var payload = TournamentDeckInfo.Deserialize(json);

            CollectionAssert.AreEqual(new[] { "u1" }, payload.squad.units);
            CollectionAssert.IsEmpty(payload.squad.stones);
            CollectionAssert.IsEmpty(payload.dc.cards);
        }

        // ── deserialize ──────────────────────────────────────────────────────────

        [Test]
        public void RoundTrip_PreservesAllThreeLists()
        {
            var units = new[] { "defender_shotgunner", "defender_slasher" };
            var stones = new[] { "ds_atk_unique", "ds_hp_rare" };
            var cards = new[] { "dc_push", "dc_freeze", "dc_lullaby" };

            var payload = TournamentDeckInfo.Deserialize(
                TournamentDeckInfo.Serialize(units, stones, cards));

            Assert.AreEqual(TournamentDeckInfo.Version, payload.v);
            CollectionAssert.AreEqual(units, payload.squad.units);
            CollectionAssert.AreEqual(stones, payload.squad.stones);
            CollectionAssert.AreEqual(cards, payload.dc.cards);
        }

        [Test]
        public void Deserialize_BadInput_ReturnsNullWithoutThrowing()
        {
            // 남의 엔트리에서 오는 값이라 빈 값(구 참가)·깨진 값이 전부 정상 입력이다.
            Assert.IsNull(TournamentDeckInfo.Deserialize(null));
            Assert.IsNull(TournamentDeckInfo.Deserialize(""));
            Assert.IsNull(TournamentDeckInfo.Deserialize("   "));
            Assert.IsNull(TournamentDeckInfo.Deserialize("not json"));
            Assert.IsNull(TournamentDeckInfo.Deserialize("{\"v\":1"));
            Assert.IsNull(TournamentDeckInfo.Deserialize("[1,2,3]"));
            Assert.IsNull(TournamentDeckInfo.Deserialize("null"));
        }

        [Test]
        public void Deserialize_FutureVersionOrMissingVersion_ReturnsNull()
        {
            // 미래 버전을 구버전 클라가 오해석하면 있지도 않은 슬롯을 그린다.
            Assert.IsNull(TournamentDeckInfo.Deserialize(
                $"{{\"v\":{TournamentDeckInfo.Version + 1},\"squad\":{{\"units\":[\"u1\"]}}}}"));
            Assert.IsNull(TournamentDeckInfo.Deserialize(
                "{\"squad\":{\"units\":[\"u1\"]}}"));   // v 누락 → 0
            Assert.IsNull(TournamentDeckInfo.Deserialize("{\"v\":0}"));
        }

        [Test]
        public void Deserialize_PastVersion_IsAccepted()
        {
            // Version 을 올린 뒤에도 그 이전 기록이 계속 읽혀야 한다 — 여기서 막으면
            // 버전을 올리는 순간 백카탈로그 전체가 "덱 정보 없음"이 된다.
            // (Version == 1 인 동안은 v1 자체가 그 경계 케이스다.)
            for (int v = 1; v <= TournamentDeckInfo.Version; v++)
                Assert.IsNotNull(TournamentDeckInfo.Deserialize(
                    $"{{\"v\":{v},\"squad\":{{\"units\":[\"u1\"]}}}}"), $"v={v}");
        }

        [Test]
        public void Deserialize_NullElements_AreDropped()
        {
            // 우리 Serialize 는 이런 걸 만들지 않지만, 남의 엔트리에서 오는 값이라
            // 도달할 수 있고 그대로 카탈로그 조회로 흘러간다.
            var payload = TournamentDeckInfo.Deserialize(
                "{\"v\":1,\"squad\":{\"units\":[null,\"u1\",\"\"]},\"dc\":{\"cards\":[\"  \"]}}");

            CollectionAssert.AreEqual(new[] { "u1" }, payload.squad.units);
            CollectionAssert.IsEmpty(payload.dc.cards);
        }

        [Test]
        public void Deserialize_MissingNodes_NormalizedToEmptyLists()
        {
            // 소비처가 null 체크를 반복하지 않게 한다.
            var payload = TournamentDeckInfo.Deserialize("{\"v\":1}");

            Assert.IsNotNull(payload);
            CollectionAssert.IsEmpty(payload.squad.units);
            CollectionAssert.IsEmpty(payload.squad.stones);
            CollectionAssert.IsEmpty(payload.dc.cards);
        }
    }
}
