using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // test-suite-fast-lane unit 0 — WaveConceptBossTests 에서 추출한 라이브 덱 저작 검증.
    // 보스 재케이던스 로직 테스트(합성 덱)는 코어 lane 에 남는다.
    public class LiveDeckBossAuthoringTests
    {
        private static readonly string[] MapDecks =
        {
            "Deck_Serpent", "Deck_Coil", "Deck_Twin", "Deck_Spiral", "Deck_Zig", "Deck_Hook",
        };

        private static AttackDeck Load(string name) =>
            AssetDatabase.LoadAssetAtPath<AttackDeck>(
                $"Assets/_Project/Scripts/Data/Decks/{name}.asset");

        [Test]
        public void LiveDecks_UseIntervalNine()
        {
            foreach (string name in MapDecks)
            {
                var deck = Load(name);
                Assert.IsNotNull(deck, name);
                Assert.AreEqual(9, deck.bossWaveInterval, $"{name}: 보스 간격");
            }
            Assert.AreEqual(9, Load("Deck_Endless").bossWaveInterval, "Endless 도 같은 간격");
        }

        // 판당 보스가 1기면 3종 풀에서 시드로 뽑는 것은 의미가 없다 — waveSeed 고정이라
        // 어차피 맵마다 영구 고정되고, 어느 맵이 어느 보스를 받는지만 시드에 맡겨진다.
        [Test]
        public void MapDecks_PinExactlyOneBoss()
        {
            foreach (string name in MapDecks)
            {
                var deck = Load(name);
                Assert.AreEqual(1, deck.bossPool.Length, $"{name}: 맵 덱은 보스 1종을 저작한다");
                Assert.IsNotNull(deck.bossPool[0], $"{name}: 보스가 비어 있다");
                Assert.AreSame(deck.bossPool[0], deck.bossUnit,
                    $"{name}: bossUnit 폴백이 풀과 달라지면 두 값이 갈린다");
            }
        }

        // bossUnit 키를 잃으면 생성기가 「보스 없음」을 graceful no-op 으로 처리해
        // **에러도 경고도 없이** 전 맵에서 보스가 사라진다(boss-jjangssen unit 0).
        [Test]
        public void EveryLiveDeck_HasABossAssigned()
        {
            var all = new List<string>(MapDecks) { "Deck_Endless" };
            foreach (string name in all)
            {
                var deck = Load(name);
                bool hasPool = deck.bossPool != null && deck.bossPool.Length > 0 && deck.bossPool[0] != null;
                Assert.IsTrue(hasPool || deck.bossUnit != null, $"{name}: 보스가 하나도 없다");
            }
        }

        [Test]
        public void EndlessDeck_KeepsTheRotation()
        {
            var deck = Load("Deck_Endless");
            Assert.Greater(deck.bossPool.Length, 1,
                "무한 모드는 판이 길어 여러 기를 만나므로 로테이션을 유지한다");
        }

        // 6맵에 3종을 배분한다 — 한 종이 어느 맵에도 안 가면 만든 보스가 낭비된다.
        [Test]
        public void MapDecks_SpreadTheThreeBossesEvenly()
        {
            var counts = new Dictionary<AttackUnitData, int>();
            foreach (string name in MapDecks)
            {
                var boss = Load(name).bossPool[0];
                counts.TryGetValue(boss, out int c);
                counts[boss] = c + 1;
            }

            Assert.AreEqual(3, counts.Count, "보스 3종이 모두 어느 맵에든 배정돼야 한다");
            foreach (var kv in counts)
                Assert.AreEqual(2, kv.Value, $"{kv.Key.id}: 6맵 ÷ 3종 = 각 2맵");
        }
    }
}
