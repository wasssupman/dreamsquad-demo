using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // 웨이브가 **선언한 편성**(`GeneratedWave.groups`)과 **실제로 펼쳐 내보내는 것**
    // (`WavePatternGenerator.ExpandWave`)이 유닛별 수량까지 같다.
    //
    // 출신은 wave-pull-revival unit 1(다음 웨이브 예고)의 계약 4 회귀 방지였다. unit 7 이
    // 예고 UI 를 은퇴시켜 「도크가 거짓말한다」는 증상은 사라졌지만, **불변식 자체는 생성기의
    // 것이라 그대로 산다** — groups 는 밸런스 저작·로그·컨셉 검증이 읽는 «이 웨이브가 무엇인가»의
    // 정본이고, 펼침이 그와 어긋나면 (예: 확장 중 수량이 조용히 깎이면) 컴파일도 다른 테스트도
    // 초록인 채로 저작과 실제가 갈린다. 그래서 예고와 무관하게 여기 남는다.
    //
    // 브리지 없이(= MonoBehaviour·씬 없이) 생성기만으로 고정할 수 있어 EditMode 다.
    public class WaveGroupsMatchSpawnTests
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
            deck.deckId = "wave-groups-test";
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
        public void 선언한_편성이_실제_펼침과_유닛별_수량까지_일치한다()
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

                // 웨이브가 선언한 것 = groups 를 유닛별로 접은 것.
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
                    $"웨이브 {i + 1}: 선언한 편성과 실제 스폰이 다르다 — " +
                    "groups 를 읽는 저작·로그·검증이 전부 거짓말이 된다");
            }
        }

        [Test]
        public void 마지막_웨이브까지_모두_편성을_갖는다()
        {
            var deck = Deck(new[] { Concept("swarm", (0, EnemyClass.Runner)) });
            var plan = WavePatternGenerator.Generate(deck, deck.waveSeed, 2);

            for (int i = 0; i < plan.waves.Count; i++)
            {
                Assert.IsNotNull(plan.waves[i].groups, $"웨이브 {i + 1} 의 groups 가 null 이다");
                Assert.Greater(plan.waves[i].groups.Count, 0,
                    $"웨이브 {i + 1} 이 빈 편성이다 — 아무도 스폰되지 않는 웨이브다");
            }
        }
    }
}
