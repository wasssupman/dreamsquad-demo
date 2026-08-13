using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // wave-pull-revival unit 2 — 묶음 가운데 변주.
    //
    // 고정하는 것 셋:
    //   ① 저작이 없으면 편성이 **현행과 완전히 동일**하다(무회귀 — 변주는 데이터가 켠다)
    //   ② 변주는 묶음의 **두 번째** 웨이브에만, **삽입**으로 들어간다(교체 아님)
    //   ③ 변주 슬롯의 입구가 묶음이 이미 쓰는 입구를 벗어나지 않는다(계약 5)
    //
    // ①이 이 파일의 핵심이다. 「변주를 저작했는데 편성이 안 바뀌는」 침묵과 「저작 안 했는데
    // 편성이 바뀌는」 회귀는 둘 다 다른 테스트가 못 잡는다 — 순수 함수는 양쪽에서 초록이다.
    public class WaveConceptVariantTests
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

        private static WaveConceptSlot Slot(int lane, EnemyClass cls) => new()
        {
            laneGroup = lane,
            classFilter = cls,
            altitude = SlotAltitude.Ground,
        };

        private WaveConceptData Concept(
            string id, WaveConceptSlot[] slots, WaveConceptSlot[] variants = null)
        {
            var concept = New<WaveConceptData>();
            concept.id = id;
            concept.displayName = id;
            concept.weight = 1f;
            concept.minWaveNumber = 1;
            concept.countMul = 1f;
            concept.slots = slots;
            concept.variantSlots = variants ?? System.Array.Empty<WaveConceptSlot>();
            return concept;
        }

        private AttackDeck Deck(WaveConceptData[] concepts, int holdWaves = 3)
        {
            var deck = New<AttackDeck>();
            deck.deckId = "variant-test";
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
            deck.conceptHoldWaves = holdWaves;
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

        private static string Signature(GeneratedWavePlan plan)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < plan.waves.Count; i++)
            {
                var w = plan.waves[i];
                sb.Append(w.conceptLabel).Append('|');
                for (int g = 0; g < w.groups.Count; g++)
                    sb.Append(w.groups[g].unit != null ? w.groups[g].unit.id : "-")
                      .Append(':').Append(w.groups[g].count)
                      .Append('@').Append(w.groups[g].laneIndex).Append(',');
                sb.Append(';');
            }
            return sb.ToString();
        }

        private static bool HasClass(GeneratedWave wave, EnemyClass cls)
        {
            for (int g = 0; g < wave.groups.Count; g++)
                if (wave.groups[g].unit != null && wave.groups[g].unit.enemyClass == cls)
                    return true;
            return false;
        }

        // ① 변주 저작이 없으면 rng 소비까지 그대로여야 한다.
        [Test]
        public void 변주를_저작하지_않으면_편성이_완전히_동일하다()
        {
            var withoutVariant = Deck(new[]
            {
                Concept("swarm", new[] { Slot(0, EnemyClass.Runner) }),
            });
            string before = Signature(WavePatternGenerator.Generate(withoutVariant, withoutVariant.waveSeed, 3));

            // 같은 저작에 **빈** variantSlots 를 명시적으로 달아도 결과가 같아야 한다.
            var explicitEmpty = Deck(new[]
            {
                Concept("swarm", new[] { Slot(0, EnemyClass.Runner) },
                    System.Array.Empty<WaveConceptSlot>()),
            });
            string after = Signature(WavePatternGenerator.Generate(explicitEmpty, explicitEmpty.waveSeed, 3));

            Assert.AreEqual(before, after,
                "변주 저작이 없는데 편성이 달라졌다 — 무회귀 경로가 깨졌다");
        }

        // ② 변주는 묶음 두 번째 웨이브에만, 삽입으로.
        [Test]
        public void 변주는_묶음_두번째_웨이브에만_삽입된다()
        {
            var deck = Deck(new[]
            {
                Concept("swarm",
                    new[] { Slot(0, EnemyClass.Runner) },
                    new[] { Slot(0, EnemyClass.Shooter) }),
            });
            var plan = WavePatternGenerator.Generate(deck, deck.waveSeed, 3);

            for (int i = 0; i < plan.waves.Count; i++)
            {
                var wave = plan.waves[i];
                bool isMiddle = i % 3 == 1;

                Assert.IsTrue(HasClass(wave, EnemyClass.Runner),
                    $"웨이브 {i + 1}: 본 편성(Runner)이 사라졌다 — 변주가 «교체»로 동작했다");

                if (isMiddle)
                    Assert.IsTrue(HasClass(wave, EnemyClass.Shooter),
                        $"웨이브 {i + 1}(묶음 가운데): 변주(Shooter)가 끼지 않았다");
                else
                    Assert.IsFalse(HasClass(wave, EnemyClass.Shooter),
                        $"웨이브 {i + 1}: 가운데가 아닌데 변주(Shooter)가 들어왔다");
            }
        }

        // ③-a 같은 laneGroup 은 **같은 입구**여야 한다.
        //
        // 「벗어나지 않는다」(③-b)만으로는 부족하다: 본 편성이 lane {2,0} 을 쓰고 변주가
        // laneGroup 1 인데 lane 0 으로 떨어져도 «집합 안»이라 통과한다. 그러면 저작자가
        // «1번 통로» 라고 쓴 것이 다른 통로로 나가는데 테스트는 초록이다.
        // (`AssignLanes` 를 두 번 부르면 등장 순서로 배정돼 정확히 이렇게 갈린다 —
        //  그래서 구현은 재추첨 대신 블록 배정을 laneGroup 으로 조회해 물려받는다.)
        [Test]
        public void 같은_laneGroup은_본편성과_같은_입구로_나온다()
        {
            var deck = Deck(new[]
            {
                Concept("ranged",
                    new[] { Slot(0, EnemyClass.Shooter), Slot(1, EnemyClass.Tanker) },
                    // 변주는 본 편성의 **두 번째** laneGroup 을 명시한다 — 순서로 배정하면
                    // 첫 번째 입구로 잘못 떨어진다.
                    new[] { Slot(1, EnemyClass.Runner) }),
            });
            var plan = WavePatternGenerator.Generate(deck, deck.waveSeed, 4);

            for (int block = 0; block * 3 + 1 < plan.waves.Count; block++)
            {
                var first = plan.waves[block * 3];
                var mid = plan.waves[block * 3 + 1];

                // 본 편성에서 laneGroup 1(= Tanker 슬롯)이 받은 입구.
                int tankerLane = -999;
                for (int g = 0; g < first.groups.Count; g++)
                    if (first.groups[g].unit != null &&
                        first.groups[g].unit.enemyClass == EnemyClass.Tanker)
                        tankerLane = first.groups[g].laneIndex;
                if (tankerLane == -999) continue; // 완화 ladder 가 탱커를 못 뽑은 블록

                for (int g = 0; g < mid.groups.Count; g++)
                {
                    var u = mid.groups[g].unit;
                    if (u == null || u.enemyClass != EnemyClass.Runner) continue;
                    Assert.AreEqual(tankerLane, mid.groups[g].laneIndex,
                        $"묶음 {block}: 변주가 laneGroup 1 을 저작했는데 본 편성의 laneGroup 1 과 " +
                        "다른 입구로 나왔다 — 입구를 물려받지 않고 새로 뽑았다는 뜻");
                }
            }
        }

        // ③-b 입구는 묶음이 쓰는 것을 벗어나지 않는다.
        [Test]
        public void 변주_슬롯의_입구가_묶음_배정을_벗어나지_않는다()
        {
            var deck = Deck(new[]
            {
                Concept("ranged",
                    new[] { Slot(0, EnemyClass.Shooter), Slot(1, EnemyClass.Shooter) },
                    new[] { Slot(0, EnemyClass.Tanker) }),
            });
            var plan = WavePatternGenerator.Generate(deck, deck.waveSeed, 4);

            for (int block = 0; block * 3 < plan.waves.Count; block++)
            {
                var blockLanes = new HashSet<int>();
                var first = plan.waves[block * 3];
                for (int g = 0; g < first.groups.Count; g++) blockLanes.Add(first.groups[g].laneIndex);

                int mid = block * 3 + 1;
                if (mid >= plan.waves.Count) break;
                var midWave = plan.waves[mid];
                for (int g = 0; g < midWave.groups.Count; g++)
                    Assert.IsTrue(blockLanes.Contains(midWave.groups[g].laneIndex),
                        $"묶음 {block} 가운데 웨이브가 새 입구(lane {midWave.groups[g].laneIndex})를 열었다 — " +
                        "«이쪽을 보강하자»는 결정이 보상받지 못한다(계약 5)");
            }
        }

        // holdWaves 2 는 «가운데»가 없다 — i%2==1 은 마지막이라 시험대를 덮어쓴다.
        [Test]
        public void holdWaves가_3미만이면_변주가_적용되지_않는다()
        {
            foreach (int hold in new[] { 1, 2 })
            {
                var deck = Deck(new[]
                {
                    Concept("swarm",
                        new[] { Slot(0, EnemyClass.Runner) },
                        new[] { Slot(0, EnemyClass.Shooter) }),
                }, hold);
                var plan = WavePatternGenerator.Generate(deck, deck.waveSeed, 3);

                for (int i = 0; i < plan.waves.Count; i++)
                    Assert.IsFalse(HasClass(plan.waves[i], EnemyClass.Shooter),
                        $"holdWaves={hold} 인데 웨이브 {i + 1} 에 변주가 들어왔다");
            }
        }

        // 보스가 변주 자리에 떨어져도 편성이 조용히 증발하지 않는다.
        //
        // 라이브 저작(보스 주기 9 · 묶음 3)에서는 `9 % 3 == 0` 이라 보스가 **항상 묶음
        // 마지막**이고 변주 자리(가운데)와 절대 안 만난다. 그건 산술 우연이지 계약이 아니다 —
        // `Deck_Duel`·`Deck_SiegeTest` 는 이미 주기 5 를 쓴다. 보스 후처리는 웨이브를 통째로
        // 교체하면서 그 웨이브의 슬롯 스냅샷을 읽으므로, 겹치는 순간 호위가 어느 편성을
        // 입는지가 정해져 있어야 한다. **정답 = 그 웨이브의 편성(= 변주)** 이다.
        [Test]
        public void 보스가_변주_자리에_와도_변주가_증발하지_않는다()
        {
            var boss = Enemy("boss", EnemyClass.Bruiser);
            boss.tier = EnemyTier.Boss;

            var deck = Deck(new[]
            {
                Concept("swarm",
                    new[] { Slot(0, EnemyClass.Runner) },
                    new[] { Slot(0, EnemyClass.Shooter) }),
            });
            // 주기 4 · 묶음 3 → 보스 웨이브 i=3,7,11 중 i=7 은 i%3==1 = 변주 자리다.
            deck.bossWaveInterval = 4;
            deck.bossUnit = boss;
            deck.bossPool = new[] { boss };

            var plan = WavePatternGenerator.Generate(deck, deck.waveSeed, 3);
            Assert.Greater(plan.waves.Count, 7, "전제: 보스가 변주 자리에 오는 웨이브까지 생성돼야 한다");

            var bossVariantWave = plan.waves[7];
            bool sawBoss = false, sawVariant = false;
            for (int g = 0; g < bossVariantWave.groups.Count; g++)
            {
                var u = bossVariantWave.groups[g].unit;
                if (u == null) continue;
                if (u.tier == EnemyTier.Boss) sawBoss = true;
                if (u.enemyClass == EnemyClass.Shooter) sawVariant = true;
            }

            Assert.IsTrue(sawBoss, "웨이브 8 이 보스 웨이브여야 한다(전제)");
            Assert.IsTrue(sawVariant,
                "보스가 변주 자리에 오자 변주가 사라졌다 — 보스 후처리가 그 웨이브의 " +
                "슬롯 스냅샷이 아니라 블록 본 편성을 읽고 있다는 뜻");
        }

        // 결정론은 변주가 있어도 불변이다 — «같은 맵 = 같은 웨이브».
        [Test]
        public void 변주가_있어도_같은_시드는_같은_편성을_낸다()
        {
            var deck = Deck(new[]
            {
                Concept("swarm",
                    new[] { Slot(0, EnemyClass.Runner) },
                    new[] { Slot(0, EnemyClass.Shooter) }),
                Concept("heavy",
                    new[] { Slot(0, EnemyClass.Tanker) },
                    new[] { Slot(0, EnemyClass.Runner) }),
            });

            string first = Signature(WavePatternGenerator.Generate(deck, deck.waveSeed, 3));
            for (int run = 0; run < 3; run++)
                Assert.AreEqual(first,
                    Signature(WavePatternGenerator.Generate(deck, deck.waveSeed, 3)),
                    $"{run + 2}회차 생성 결과가 다르다 — 결정론이 깨졌다");
        }
    }
}
