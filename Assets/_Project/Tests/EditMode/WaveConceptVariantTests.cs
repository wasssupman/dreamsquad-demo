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

        // ── wave-ramp-two-phase unit 1 — 클라이맥스 변주 격상 ────────────────────

        // break 이후는 매 웨이브가 변주(3/3), 그 전은 기존 가운데(1/3). 게이트 = 덱 break 필드.
        [Test]
        public void 클라이맥스에서는_변주가_상시다()
        {
            var deck = Deck(new[]
            {
                Concept("swarm",
                    new[] { Slot(0, EnemyClass.Runner) },
                    new[] { Slot(0, EnemyClass.Shooter) }),
            });
            deck.minWaveCount = deck.maxWaveCount = 12;
            deck.waveRampBreakWave = 7;
            deck.waveRampBreakUnits = 8;
            var plan = WavePatternGenerator.Generate(deck, deck.waveSeed, 2);

            for (int i = 0; i < plan.waves.Count; i++)
            {
                bool hasShooter = false;
                foreach (var g in plan.waves[i].groups)
                    if (g.unit != null && g.unit.enemyClass == EnemyClass.Shooter) hasShooter = true;
                if (i + 1 >= 7)
                    Assert.IsTrue(hasShooter, $"웨이브 {i + 1}: 클라이맥스인데 변주가 빠졌다");
                else if (i % 3 != 1)
                    Assert.IsFalse(hasShooter, $"웨이브 {i + 1}: 본편 비-가운데 웨이브에 변주가 붙었다");
            }
        }

        // 게이트 on: 본 편성에 없는 laneGroup 의 변주는 미사용 레인을 연다(새 전선).
        // 게이트 off: 기존 접힘 — 공유 컨셉 에셋에 laneGroup 1 을 저작해도 라이브는 무변경.
        [Test]
        public void 미지_laneGroup_변주는_게이트가_켜진_덱에서만_새_레인을_연다()
        {
            WaveConceptData Swarm() => Concept("swarm",
                new[] { Slot(0, EnemyClass.Runner), Slot(0, EnemyClass.Runner) },
                new[] { Slot(1, EnemyClass.Shooter) });   // 본 편성에 없는 그룹 1

            var off = Deck(new[] { Swarm() });
            var offPlan = WavePatternGenerator.Generate(off, off.waveSeed, 2);

            var on = Deck(new[] { Swarm() });
            on.waveRampBreakWave = 4;
            on.waveRampBreakUnits = 8;
            var onPlan = WavePatternGenerator.Generate(on, on.waveSeed, 2);

            // 검사 대상 = 변주가 붙는 웨이브(off 는 가운데, on 은 클라이맥스 포함 전부).
            void AssertVariantLane(GeneratedWavePlan plan, bool expectNewLane, string label)
            {
                bool sawVariant = false;
                for (int i = 0; i < plan.waves.Count; i++)
                {
                    int mainLane = -999, variantLane = -999;
                    foreach (var g in plan.waves[i].groups)
                    {
                        if (g.unit == null) continue;
                        if (g.unit.enemyClass == EnemyClass.Runner) mainLane = g.laneIndex;
                        if (g.unit.enemyClass == EnemyClass.Shooter) variantLane = g.laneIndex;
                    }
                    if (variantLane == -999 || mainLane == -999) continue;
                    sawVariant = true;
                    if (expectNewLane)
                        Assert.AreNotEqual(mainLane, variantLane,
                            $"{label} 웨이브 {i + 1}: 게이트 on 인데 변주가 본 레인으로 접혔다");
                    else
                        Assert.AreEqual(mainLane, variantLane,
                            $"{label} 웨이브 {i + 1}: 게이트 off 인데 변주가 새 레인을 열었다 — 라이브 회귀");
                }
                Assert.IsTrue(sawVariant, $"{label}: 변주 웨이브가 하나도 없다 — 검사가 공회전했다");
            }

            AssertVariantLane(offPlan, expectNewLane: false, "off");
            AssertVariantLane(onPlan, expectNewLane: true, "on");
        }

        // 리뷰 F4 — 미지 그룹 **2+ 슬롯**의 접힘. 같은-그룹 공유 스캔이 게이트(openNewLanes)
        // 밖에 있으면 off(라이브) 덱에서 두 슬롯이 한 레인으로 몰려 기존 접힘(슬롯별 v%len)과
        // 달라진다 — 현행 컨셉은 변주 1슬롯뿐이라 잠복이었고, 이 pin 이 그 침묵을 깬다.
        [Test]
        public void 미지_그룹_다중_변주슬롯_접힘은_게이트_상태를_따른다()
        {
            WaveConceptData C() => Concept("pincer",
                new[] { Slot(0, EnemyClass.Runner), Slot(1, EnemyClass.Tanker) },
                new[] { Slot(2, EnemyClass.Shooter), Slot(2, EnemyClass.Shooter) });

            void Lanes(GeneratedWavePlan plan, out int runner, out int tanker, List<int> shooters)
            {
                shooters.Clear(); runner = -999; tanker = -999;
                for (int i = 0; i < plan.waves.Count; i++)
                {
                    var shooterLanes = new List<int>();
                    int r = -999, t = -999;
                    foreach (var g in plan.waves[i].groups)
                    {
                        if (g.unit == null) continue;
                        if (g.unit.enemyClass == EnemyClass.Runner) r = g.laneIndex;
                        if (g.unit.enemyClass == EnemyClass.Tanker) t = g.laneIndex;
                        if (g.unit.enemyClass == EnemyClass.Shooter) shooterLanes.Add(g.laneIndex);
                    }
                    if (shooterLanes.Count == 2 && r != -999 && t != -999)
                    { runner = r; tanker = t; shooters.AddRange(shooterLanes); return; }
                }
                Assert.Fail("변주 2슬롯이 함께 뽑힌 웨이브가 없다 — 검사가 공회전했다");
            }

            var offDeck = Deck(new[] { C() });
            var offShooters = new List<int>();
            Lanes(WavePatternGenerator.Generate(offDeck, offDeck.waveSeed, 3),
                out int offRunner, out int offTanker, offShooters);
            // off = 기존 접힘: v0 → mainLanes[0](러너 레인), v1 → mainLanes[1](탱커 레인).
            Assert.AreEqual(offRunner, offShooters[0], "off: 첫 변주는 본 편성 첫 입구로 접힌다");
            Assert.AreEqual(offTanker, offShooters[1], "off: 둘째 변주는 본 편성 둘째 입구로 접힌다");

            var onDeck = Deck(new[] { C() });
            onDeck.waveRampBreakWave = 4;
            onDeck.waveRampBreakUnits = 8;
            var onShooters = new List<int>();
            Lanes(WavePatternGenerator.Generate(onDeck, onDeck.waveSeed, 3),
                out int onRunner, out int onTanker, onShooters);
            // on = 같은 그룹 = 같은 새 레인(미사용 레인 하나를 공유).
            Assert.AreEqual(onShooters[0], onShooters[1], "on: 같은 laneGroup 은 같은 레인을 공유한다");
            Assert.AreNotEqual(onRunner, onShooters[0], "on: 새 전선은 본 편성 레인이 아니다");
            Assert.AreNotEqual(onTanker, onShooters[0]);
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
