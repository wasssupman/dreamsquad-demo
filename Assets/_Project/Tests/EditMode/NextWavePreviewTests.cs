using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // wave-pull-revival unit 1 — 예고와 실스폰이 갈리지 않는다(spec 계약 4).
    //
    // 도크가 읽는 `BattleBridge.TryGetNextWaveComposition` 은 플랜의 `GeneratedWave.groups` 를
    // 그대로 돌려준다. 그래서 실제로 지켜야 할 불변식은 **groups 가 그 웨이브를 펼친 결과와
    // 같은가**이고, 그건 브리지 없이(= MonoBehaviour·씬 없이) 여기서 고정할 수 있다.
    //
    // 이 단언이 없으면 「예고는 러너 8기라는데 실제로는 6기가 나오는」 침묵이 가능하다 —
    // 그 상태에서 당김 판단은 전부 거짓말이 되지만 컴파일도 다른 테스트도 초록이다.
    public class NextWavePreviewTests
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

        private AttackUnitData Enemy(string id, EnemyClass cls)
        {
            var unit = New<AttackUnitData>();
            unit.id = id;
            unit.displayName = id;
            unit.enemyClass = cls;
            unit.minWaveNumber = 1;
            unit.health = 50;
            unit.moveSpeed = 2f;
            return unit;
        }

        private WaveConceptData Concept(string id, params (int lane, EnemyClass cls)[] slots)
        {
            var concept = New<WaveConceptData>();
            concept.id = id;
            concept.displayName = id;
            concept.weight = 1f;
            concept.minWaveNumber = 1;
            concept.countMul = 1f;
            var built = new WaveConceptSlot[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                built[i] = new WaveConceptSlot
                {
                    laneGroup = slots[i].lane,
                    classFilter = slots[i].cls,
                    altitude = SlotAltitude.Ground,
                };
            concept.slots = built;
            return concept;
        }

        private AttackDeck Deck(WaveConceptData[] concepts)
        {
            var deck = New<AttackDeck>();
            deck.deckId = "preview-test";
            deck.useGeneratedWaves = true;
            deck.waveSeed = 20260813;
            deck.minWaveCount = 12;
            deck.maxWaveCount = 12;
            deck.minUnitsPerWave = 5;
            deck.maxUnitsPerWave = 24;
            deck.unitGrowthPerWave = 1.12f;
            deck.waveCountJitter = 1;
            deck.intraWaveSpacingSec = 0.5f;
            deck.maxWaveIntervalSec = 20f;
            deck.waveSpawnLeadInSec = 2f;
            deck.timerDurationSec = 180f;
            deck.bossWaveInterval = 0;
            deck.bossUnit = null;
            deck.bossPool = new AttackUnitData[0];
            deck.conceptHoldWaves = 3;
            deck.attackUnitPool = new[]
            {
                Enemy("basic", EnemyClass.Bruiser),
                Enemy("swift", EnemyClass.Runner),
                Enemy("runner", EnemyClass.Runner),
                Enemy("tanker", EnemyClass.Tanker),
                Enemy("sniper", EnemyClass.Shooter),
                Enemy("needler", EnemyClass.Shooter),
            };
            deck.waveConceptPool = concepts;
            return deck;
        }

        [Test]
        public void 예고_구성이_실제_펼침과_유닛별_수량까지_일치한다()
        {
            const int laneCount = 3;
            var deck = Deck(new[]
            {
                Concept("swarm", (0, EnemyClass.Runner), (0, EnemyClass.Runner)),
                Concept("ranged", (0, EnemyClass.Shooter), (1, EnemyClass.Shooter)),
                Concept("heavy", (0, EnemyClass.Tanker)),
            });

            var plan = WavePatternGenerator.Generate(deck, deck.waveSeed, laneCount);

            for (int i = 0; i < plan.waves.Count; i++)
            {
                var wave = plan.waves[i];

                // 예고가 말하는 것 = groups 를 유닛별로 접은 것.
                var promised = new Dictionary<string, int>();
                for (int g = 0; g < wave.groups.Count; g++)
                {
                    var group = wave.groups[g];
                    if (group.unit == null || group.count <= 0) continue;
                    promised.TryGetValue(group.unit.id, out int had);
                    promised[group.unit.id] = had + group.count;
                }

                // 실제로 나오는 것 = 같은 웨이브를 펼친 결과.
                var actual = new Dictionary<string, int>();
                var spawns = WavePatternGenerator.ExpandWave(
                    wave, 0f, laneCount, deck.intraWaveSpacingSec);
                for (int s = 0; s < spawns.Count; s++)
                {
                    var unit = spawns[s].entry.unitType;
                    if (unit == null) continue;
                    actual.TryGetValue(unit.id, out int had);
                    actual[unit.id] = had + 1;
                }

                CollectionAssert.AreEquivalent(promised, actual,
                    $"웨이브 {i + 1}: 예고 구성과 실제 스폰이 다르다 — " +
                    "도크가 보여주는 «무엇이 몇 마리»가 거짓말이 된다");
            }
        }

        [Test]
        public void 마지막_웨이브까지_모두_구성을_갖는다()
        {
            var deck = Deck(new[] { Concept("swarm", (0, EnemyClass.Runner)) });
            var plan = WavePatternGenerator.Generate(deck, deck.waveSeed, 2);

            for (int i = 0; i < plan.waves.Count; i++)
            {
                Assert.IsNotNull(plan.waves[i].groups, $"웨이브 {i + 1} 의 groups 가 null 이다");
                Assert.Greater(plan.waves[i].groups.Count, 0,
                    $"웨이브 {i + 1} 이 빈 편성이다 — 예고 줄이 통째로 사라진다");
            }
        }
    }
}
