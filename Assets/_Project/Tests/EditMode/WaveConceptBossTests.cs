using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // wave-concept-blocks unit 3 — 보스 재케이던스.
    //
    // 간격 9 는 conceptHoldWaves=3 의 **블록 마지막** 웨이브다(7·8·9 = 블록 2). 그래서
    // 「컨셉을 두 웨이브 배우고 세 번째에 보스가 그 컨셉을 입고 온다」가 성립한다.
    public class WaveConceptBossTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private T New<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _created.Add(o);
            return o;
        }

        private AttackUnitData Enemy(string id, EnemyClass cls, int maxPerWave = 0)
        {
            var u = New<AttackUnitData>();
            u.id = id;
            u.displayName = id;
            u.enemyClass = cls;
            u.maxPerWave = maxPerWave;
            u.health = 50;
            u.moveSpeed = 2f;
            return u;
        }

        private WaveConceptData Concept(string id, float countMul, params (int lane, EnemyClass cls)[] slots)
        {
            var c = New<WaveConceptData>();
            c.id = id;
            c.displayName = id;
            c.weight = 1f;
            c.minWaveNumber = 1;
            c.countMul = countMul;
            var built = new WaveConceptSlot[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                built[i] = new WaveConceptSlot
                {
                    laneGroup = slots[i].lane,
                    classFilter = slots[i].cls,
                    altitude = SlotAltitude.Ground,
                };
            c.slots = built;
            return c;
        }

        private AttackDeck Deck(
            AttackUnitData[] pool, AttackUnitData boss, WaveConceptData[] concepts, int waveCount = 12)
        {
            var deck = New<AttackDeck>();
            deck.deckId = "bosstest";
            deck.useGeneratedWaves = true;
            deck.waveSeed = 20260813;
            deck.minWaveCount = waveCount;
            deck.maxWaveCount = waveCount;
            deck.minUnitsPerWave = 5;
            deck.maxUnitsPerWave = 24;
            deck.unitGrowthPerWave = 1.12f;
            deck.waveCountJitter = 1;
            deck.intraWaveSpacingSec = 0.5f;
            deck.maxWaveIntervalSec = 20f;
            deck.waveSpawnLeadInSec = 2f;
            deck.timerDurationSec = 180f;
            deck.bossWaveInterval = 9;
            deck.bossEscortMin = 3;
            deck.bossEscortMax = 4;
            deck.bossUnit = boss;
            deck.bossPool = new[] { boss };
            deck.attackUnitPool = pool;
            deck.waveConceptPool = concepts;
            deck.conceptHoldWaves = 3;
            return deck;
        }

        private AttackUnitData[] Pool() => new[]
        {
            Enemy("basic", EnemyClass.Bruiser),
            Enemy("swift", EnemyClass.Runner),
            Enemy("tanker", EnemyClass.Tanker),
            Enemy("sniper", EnemyClass.Shooter),
            Enemy("needler", EnemyClass.Shooter),
        };

        private static bool HasUnit(GeneratedWave w, AttackUnitData unit)
        {
            for (int g = 0; g < w.groups.Count; g++)
                if (ReferenceEquals(w.groups[g].unit, unit)) return true;
            return false;
        }

        [Test]
        public void BossWaves_LandOnNine_NotFiveOrTen()
        {
            var boss = Enemy("boss", EnemyClass.Bruiser);
            var plan = WavePatternGenerator.Generate(
                Deck(Pool(), boss, new[] { Concept("ranged", 0.7f, (0, EnemyClass.Shooter)) }, 18),
                20260813, 3);

            for (int i = 0; i < plan.waves.Count; i++)
            {
                bool expected = (i + 1) % 9 == 0;
                Assert.AreEqual(expected, HasUnit(plan.waves[i], boss), $"웨이브 {i + 1} 보스 여부");
            }
        }

        [Test]
        public void BossWave_IsTheLastWaveOfItsBlock()
        {
            // 9 = 블록 2(웨이브 7·8·9)의 마지막. 학습 두 웨이브 → 시험 한 웨이브.
            const int hold = 3;
            const int bossWave = 9;
            Assert.AreEqual(2, (bossWave - 1) / hold, "보스 웨이브가 속한 블록");
            Assert.AreEqual(hold - 1, (bossWave - 1) % hold, "보스 웨이브는 블록의 마지막 칸이어야 한다");
        }

        [Test]
        public void Escort_FollowsBlockConceptClassFilter()
        {
            var boss = Enemy("boss", EnemyClass.Bruiser);
            var ranged = Concept("ranged", 0.7f, (0, EnemyClass.Shooter));
            var plan = WavePatternGenerator.Generate(
                Deck(Pool(), boss, new[] { ranged }, 9), 20260813, 3);

            var bossWave = plan.waves[8];
            Assert.IsTrue(HasUnit(bossWave, boss), "웨이브 9 는 보스 웨이브");
            for (int g = 0; g < bossWave.groups.Count; g++)
            {
                var unit = bossWave.groups[g].unit;
                if (ReferenceEquals(unit, boss)) continue;
                Assert.AreEqual(EnemyClass.Shooter, unit.enemyClass,
                    "「원거리」 블록의 보스는 사거리 호위를 끼고 와야 한다");
            }
        }

        [Test]
        public void Escort_KeepsBudget_AndDoesNotApplyCountMul()
        {
            var boss = Enemy("boss", EnemyClass.Bruiser);
            // 「중장」은 countMul 0.4 — 호위 예산에 곱하면 3 × 0.4 = 1.2 로 하한에 먹힌다.
            var heavy = Concept("heavy", 0.4f, (0, EnemyClass.Tanker));
            var plan = WavePatternGenerator.Generate(
                Deck(Pool(), boss, new[] { heavy }, 9), 20260813, 3);

            var bossWave = plan.waves[8];
            int escorts = 0;
            for (int g = 0; g < bossWave.groups.Count; g++)
                if (!ReferenceEquals(bossWave.groups[g].unit, boss))
                    escorts += bossWave.groups[g].count;

            Assert.GreaterOrEqual(escorts, 3, "호위 수량은 보스 파라미터가 소유한다(countMul 미적용)");
            Assert.LessOrEqual(escorts, 4);
        }

        [Test]
        public void Boss_TakesTheLaneOfTheConceptsFirstSlot()
        {
            var boss = Enemy("boss", EnemyClass.Bruiser);
            var pincer = Concept("pincer", 0.7f, (0, EnemyClass.Shooter), (1, EnemyClass.Shooter));
            var plan = WavePatternGenerator.Generate(
                Deck(Pool(), boss, new[] { pincer }, 9), 20260813, 4);

            var bossWave = plan.waves[8];
            Assert.AreEqual(boss, bossWave.groups[0].unit, "선봉 = 보스(RoundRobin round 0)");
            Assert.GreaterOrEqual(bossWave.groups[0].laneIndex, 0,
                "협공 컨셉의 보스는 lane 이 지정돼야 «본대»가 읽힌다");
            // 호위는 두 lane 으로 갈리고 보스는 그중 한쪽에 선다.
            var lanes = new HashSet<int>();
            for (int g = 1; g < bossWave.groups.Count; g++) lanes.Add(bossWave.groups[g].laneIndex);
            Assert.IsTrue(lanes.Contains(bossWave.groups[0].laneIndex),
                "보스 lane 이 호위 lane 중 하나와 같아야 한다(첫 슬롯)");
        }

        [Test]
        public void BossWave_CarriesTheConceptLabel()
        {
            var boss = Enemy("boss", EnemyClass.Bruiser);
            var plan = WavePatternGenerator.Generate(
                Deck(Pool(), boss, new[] { Concept("ranged", 0.7f, (0, EnemyClass.Shooter)) }, 9),
                20260813, 3);

            Assert.AreEqual("ranged", plan.waves[8].conceptLabel,
                "보스 웨이브도 블록의 라벨을 유지해야 «강화판»으로 읽힌다");
        }

        // 컨셉 없는 덱은 보스 편성이 현행 그대로(보스 1 + 호위 1종 = 2그룹)여야 한다.
        [Test]
        public void NoConcept_BossWaveKeepsLegacyShape()
        {
            var boss = Enemy("boss", EnemyClass.Bruiser);
            var plan = WavePatternGenerator.Generate(
                Deck(Pool(), boss, new WaveConceptData[0], 9), 20260813, 3);

            var bossWave = plan.waves[8];
            Assert.AreEqual(2, bossWave.groups.Count, "레거시 보스 웨이브는 2그룹");
            Assert.AreEqual(boss, bossWave.groups[0].unit);
            Assert.AreEqual(1, bossWave.groups[0].count);
            Assert.AreEqual("", bossWave.conceptLabel);
            foreach (var g in bossWave.groups)
                Assert.AreEqual(-1, g.laneIndex, "레거시 경로는 lane 무지정");
        }

        // ---------------- 라이브 덱 저작 ----------------

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
