using System.Globalization;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class MatchConfigSnapshotTests
    {
        private AttackDeck _deck;
        private AttackUnitData _enemy;
        private AttackUnitData _secondEnemy;
        private ProjectileData _projectile;

        [SetUp]
        public void SetUp()
        {
            _projectile = ScriptableObject.CreateInstance<ProjectileData>();
            _projectile.id = "projectile_test";
            _projectile.speed = 7.25f;
            _projectile.visualScale = 0.3f;

            _enemy = ScriptableObject.CreateInstance<AttackUnitData>();
            _enemy.id = "enemy_test";
            _enemy.health = 125.5f;
            _enemy.projectile = _projectile;

            _secondEnemy = ScriptableObject.CreateInstance<AttackUnitData>();
            _secondEnemy.id = "enemy_second";
            _secondEnemy.health = 80f;

            _deck = ScriptableObject.CreateInstance<AttackDeck>();
            _deck.deckId = "deck_test";
            _deck.attackUnitPool = new[] { _enemy };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_deck);
            Object.DestroyImmediate(_enemy);
            Object.DestroyImmediate(_secondEnemy);
            Object.DestroyImmediate(_projectile);
        }

        [Test]
        public void SameConditions_ProduceSameCanonicalBlobAndHash()
        {
            MatchConfigSnapshot first = MatchConfigSnapshot.Capture(CreateCapture());
            MatchConfigSnapshot second = MatchConfigSnapshot.Capture(CreateCapture());

            Assert.AreEqual(first.CanonicalBlob, second.CanonicalBlob);
            Assert.AreEqual(first.ConfigHash, second.ConfigHash);
            Assert.AreEqual(64, first.ConfigHash.Length);
        }

        [Test]
        public void GameplayStatChange_ChangesHash()
        {
            string before = MatchConfigSnapshot.Capture(CreateCapture()).ConfigHash;
            _enemy.health += 1f;
            string after = MatchConfigSnapshot.Capture(CreateCapture()).ConfigHash;

            Assert.AreNotEqual(before, after);
        }

        [Test]
        public void PresentationOnlyProjectileScale_DoesNotChangeHash()
        {
            string before = MatchConfigSnapshot.Capture(CreateCapture()).ConfigHash;
            _projectile.visualScale += 10f;
            string after = MatchConfigSnapshot.Capture(CreateCapture()).ConfigHash;

            Assert.AreEqual(before, after);
        }

        [Test]
        public void Capture_IsImmutableAfterSourceMutation()
        {
            MatchConfigSnapshot snapshot = MatchConfigSnapshot.Capture(CreateCapture());
            string blob = snapshot.CanonicalBlob;
            string hash = snapshot.ConfigHash;

            _enemy.health = 9999f;
            _deck.attackUnitPool = System.Array.Empty<AttackUnitData>();

            Assert.AreEqual(blob, snapshot.CanonicalBlob);
            Assert.AreEqual(hash, snapshot.ConfigHash);
        }

        [Test]
        public void CanonicalFormat_IsCultureInvariant()
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                MatchConfigSnapshot french = MatchConfigSnapshot.Capture(CreateCapture());
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                MatchConfigSnapshot english = MatchConfigSnapshot.Capture(CreateCapture());

                Assert.AreEqual(french.CanonicalBlob, english.CanonicalBlob);
                Assert.AreEqual(french.ConfigHash, english.ConfigHash);
                StringAssert.Contains("125.5", english.CanonicalBlob);
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Test]
        public void EffectTileMapOrder_DoesNotChangeHash()
        {
            var tileA = ScriptableObject.CreateInstance<EffectTileData>();
            var tileB = ScriptableObject.CreateInstance<EffectTileData>();
            try
            {
                tileA.id = "effect_a";
                tileB.id = "effect_b";
                MatchConfigCapture firstInput = CreateCapture();
                firstInput.effectTiles = new[]
                {
                    new MatchConfigEffectTile(new Vector2Int(3, 4), tileA),
                    new MatchConfigEffectTile(new Vector2Int(1, 2), tileB),
                };
                MatchConfigCapture secondInput = CreateCapture();
                secondInput.effectTiles = new[]
                {
                    new MatchConfigEffectTile(new Vector2Int(1, 2), tileB),
                    new MatchConfigEffectTile(new Vector2Int(3, 4), tileA),
                };

                Assert.AreEqual(
                    MatchConfigSnapshot.Capture(firstInput).ConfigHash,
                    MatchConfigSnapshot.Capture(secondInput).ConfigHash);
            }
            finally
            {
                Object.DestroyImmediate(tileA);
                Object.DestroyImmediate(tileB);
            }
        }

        [Test]
        public void SemanticListOrder_ChangesHash()
        {
            _deck.attackUnitPool = new[] { _enemy, _secondEnemy };
            string first = MatchConfigSnapshot.Capture(CreateCapture()).ConfigHash;
            _deck.attackUnitPool = new[] { _secondEnemy, _enemy };
            string second = MatchConfigSnapshot.Capture(CreateCapture()).ConfigHash;

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void GeneratedMapCellChange_ChangesHash()
        {
            var map = new GeneratedMap
            {
                gridSize = new int2(2, 1),
                tiles = new NativeArray<MapTileType>(2, Allocator.Temp),
                mergeDegree = new NativeArray<byte>(2, Allocator.Temp),
                chokepoint = new NativeArray<byte>(2, Allocator.Temp),
                spawns = new NativeArray<int2>(1, Allocator.Temp),
                goals = new NativeArray<int2>(1, Allocator.Temp),
                goal = new int2(1, 0),
                seed = 77,
                generatorVersion = 3,
            };
            try
            {
                map.tiles[0] = MapTileType.Walk;
                map.tiles[1] = MapTileType.Walk;
                map.spawns[0] = new int2(0, 0);
                map.goals[0] = map.goal;
                MatchConfigCapture capture = CreateCapture();
                capture.generatedMap = map;

                string before = MatchConfigSnapshot.Capture(capture).ConfigHash;
                map.tiles[0] = MapTileType.Place;
                string after = MatchConfigSnapshot.Capture(capture).ConfigHash;

                Assert.AreNotEqual(before, after);
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void GeneratedWaveGroupChange_ChangesHash()
        {
            MatchConfigCapture capture = CreateCapture();
            capture.usesGeneratedWaves = true;
            capture.generatedWavePlan = WavePlanWithCount(1);
            string before = MatchConfigSnapshot.Capture(capture).ConfigHash;
            capture.generatedWavePlan = WavePlanWithCount(2);
            string after = MatchConfigSnapshot.Capture(capture).ConfigHash;

            Assert.AreNotEqual(before, after);
        }

        [Test]
        public void RepresentativeLoadoutAndEconomyChanges_AllChangeHash()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            var stone = ScriptableObject.CreateInstance<DreamstoneData>();
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            var awakening = ScriptableObject.CreateInstance<AwakeningConfig>();
            var score = ScriptableObject.CreateInstance<ScoreRulesData>();
            var cost = ScriptableObject.CreateInstance<CostConfig>();
            var stack = ScriptableObject.CreateInstance<StackModifierSO>();
            try
            {
                skill.id = "skill_test";
                skill.magnitude = 1.5f;
                stone.id = "stone_test";
                stone.effect = new CardEffect { kind = CardBuffKind.AttackDamage, percent = 12f };
                card.id = "card_test";
                card.effects = new[] { new CardEffect { kind = CardBuffKind.AttackSpeed, percent = 8f } };
                awakening.id = "awakening_test";
                awakening.handSize = 5;
                score.timeScorePerSecond = 100;
                cost.id = "cost_test";
                cost.maxCost = 15;
                stack.kind = Wassup.Battle.Effects.StackKind.Fire;
                stack.perAppDuration = 5f;

                MatchConfigCapture capture = CreateCapture();
                capture.skillLoadout = new[] { skill };
                capture.dreamstones = new[] { stone };
                capture.dreamcatcherCards = new[] { card };
                capture.awakeningConfig = awakening;
                capture.scoreRules = score;
                capture.costConfig = cost;
                capture.stackModifiers = new[] { stack };

                AssertFieldChangesHash(capture, () => skill.magnitude += 1f, "skill loadout");
                AssertFieldChangesHash(capture, () => stone.effect = new CardEffect
                    { kind = stone.effect.kind, percent = stone.effect.percent + 1f }, "dreamstone");
                AssertFieldChangesHash(capture, () => card.effects[0] = new CardEffect
                    { kind = card.effects[0].kind, percent = card.effects[0].percent + 1f }, "dreamcatcher card");
                AssertFieldChangesHash(capture, () => awakening.handSize += 1, "awakening config");
                AssertFieldChangesHash(capture, () => score.timeScorePerSecond += 1, "score rules");
                AssertFieldChangesHash(capture, () => cost.maxCost += 1, "cost config");
                AssertFieldChangesHash(capture, () => stack.perAppDuration += 1f, "stack modifier");
            }
            finally
            {
                Object.DestroyImmediate(skill);
                Object.DestroyImmediate(stone);
                Object.DestroyImmediate(card);
                Object.DestroyImmediate(awakening);
                Object.DestroyImmediate(score);
                Object.DestroyImmediate(cost);
                Object.DestroyImmediate(stack);
            }
        }

        private GeneratedWavePlan WavePlanWithCount(int count)
        {
            return new GeneratedWavePlan(
                91, 4, 180f, 10f, 0.25f,
                new[]
                {
                    new GeneratedWave(0, 2f,
                        new[] { new WaveSpawnGroup(_enemy, count) },
                        0.4f, WaveExpandMode.PerGroupTimeline),
                },
                1f);
        }

        private static void AssertFieldChangesHash(MatchConfigCapture capture, System.Action mutate, string category)
        {
            string before = MatchConfigSnapshot.Capture(capture).ConfigHash;
            mutate();
            string after = MatchConfigSnapshot.Capture(capture).ConfigHash;
            Assert.AreNotEqual(before, after, category + " must participate in the canonical hash");
        }

        private MatchConfigCapture CreateCapture()
        {
            return new MatchConfigCapture
            {
                matchSeed = 123456,
                fixedMapSeed = 42,
                usesGeneratedWaves = false,
                timerDurationSec = 180f,
                activeDeck = _deck,
                tileSize = 1f,
                spawnSpreadEnabled = true,
                spawnSpreadFraction = 0.2f,
                spawnSpreadTopScale = 0.5f,
                spawnSubLaneCount = 3,
                enableAdjacencySynergy = true,
                bossLeapTotalSeconds = 0.83f,
            };
        }
    }
}
