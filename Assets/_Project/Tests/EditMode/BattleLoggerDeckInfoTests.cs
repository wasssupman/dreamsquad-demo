using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core.Api;
using Wassup.Logging;

namespace Wassup.Tests.EditMode
{
    // tournament-deck-info unit 1 — DeckInfoJson 은 로거의 어느 서브트리가 페이로드의
    // 어느 슬롯으로 가는지를 정하는 **유일한 배선**이다. 포맷(TournamentDeckInfo)과
    // 와이어 바디(BuildCompleteBody)는 각각 따로 고정돼 있지만, 그 둘을 잇는 매핑이
    // 어긋나면 두 테스트 모두 green 인 채로 서버에는 잘못된 덱이 쌓인다.
    public class BattleLoggerDeckInfoTests
    {
        private GameObject _go;
        private BattleLogger _logger;
        private HashSet<string> _preExistingLogs;
        private string _logDir;

        [SetUp]
        public void SetUp()
        {
            _logDir = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "GameLogs");
            _preExistingLogs = Directory.Exists(_logDir)
                ? new HashSet<string>(Directory.GetFiles(_logDir))
                : new HashSet<string>();

            _go = new GameObject("BattleLoggerDeckInfoTests");
            _logger = _go.AddComponent<BattleLogger>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            if (!Directory.Exists(_logDir)) return;
            foreach (var file in Directory.GetFiles(_logDir).Where(f => !_preExistingLogs.Contains(f)))
                File.Delete(file);
        }

        [Test]
        public void DeckInfoJson_WithoutSession_ReturnsNull()
        {
            // 로거는 있는데 세션이 없는 창(씬 전환 직후 등). 호출자는 null 을 빈 값으로 흘린다.
            Assert.IsNull(_logger.DeckInfoJson());
        }

        [Test]
        public void DeckInfoJson_MapsEachSubtreeToItsPayloadSlot()
        {
            _logger.StartSession();
            _logger.SetSquad("sq-1", "Squad 1",
                new[] { "u_a", "u_b" }, new[] { "이름은", "안 실린다" });
            _logger.SetDreamstones(new List<DreamstoneRecord>
            {
                new DreamstoneRecord { id = "ds_1", name = "n1", slotIndex = 0 },
                new DreamstoneRecord { id = "ds_2", name = "n2", slotIndex = 2 },
            });
            _logger.SetDreamcatcherDeck("deck-1", "Deck 1",
                new[] { "c_1", "c_2", "gift_1" },   // 조합 덱(선물 포함)
                new[] { "c_1", "c_2" });            // 고른 덱

            var payload = TournamentDeckInfo.Deserialize(_logger.DeckInfoJson());

            Assert.IsNotNull(payload);
            CollectionAssert.AreEqual(new[] { "u_a", "u_b" }, payload.squad.units);
            CollectionAssert.AreEqual(new[] { "ds_1", "ds_2" }, payload.squad.stones);
            // 선물 카드(gift_1)는 빠진다 — 매 판 랜덤이라 로드아웃 비교의 노이즈다.
            CollectionAssert.AreEqual(new[] { "c_1", "c_2" }, payload.dc.cards);
        }

        [Test]
        public void DeckInfoJson_EmptySession_YieldsEmptyString()
        {
            // 아무것도 캐리인하지 않은 세션 → 빈 문자열 → BuildCompleteBody 가 키를 뺀다.
            _logger.StartSession();

            Assert.AreEqual(string.Empty, _logger.DeckInfoJson());
        }
    }
}
