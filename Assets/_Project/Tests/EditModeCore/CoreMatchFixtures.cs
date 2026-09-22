using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Wave;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 4 — 매치 담당자 테스트의 공용 고정구.
    //
    // ⚠ 여기 수치는 **게임 값이 아니라 픽스처**다. 게임 값의 정본은 판 밖이고(시트 → SO →
    // 빌더) 그 경로는 `configHash` 가 감시한다.
    public static class CoreMatchFixtures
    {
        /// <summary>12×5 빈 판, 레인 2, 골 1, 보너스 포탈 2. 담당자 테스트의 기본 판.</summary>
        public static MapSnapshot Board()
        {
            var map = new MapSnapshot
            {
                Width = 12,
                Height = 5,
                TileSize = 1f,
                Goals = new[] { new int2(11, 2) },
                Spawns = new[] { new int2(0, 1), new int2(0, 3) },
                BonusSpawns = new[] { new int2(0, 0), new int2(0, 4) },
            };
            map.Normalize();
            return map;
        }

        /// <summary>
        /// 담당자 테스트용 정의표. 배치 창은 **플레이어가 닫는다**(길이 0) — 그래야 테스트가
        /// 「닫는 순간」을 직접 만들 수 있고, 그 순간이 코스트 재생의 시작이다(X24).
        /// </summary>
        public static MatchDefinition Definition(int seed = 777)
        {
            var def = new MatchDefinition
            {
                Seed = seed,
                Map = Board(),
                Units = new[] { Defender("fixture_defender") },
                Enemies = new[]
                {
                    Enemy("grunt", health: 60f, cls: 1, reward: 2),
                    Enemy("ranger", health: 60f, cls: 2, reward: 2),
                    Enemy("boss", health: 200f, cls: 3, reward: 5),
                },
                Heart = new HeartDef { MaxHealth = 300f, KillHealPerAwakening = 10f },
            };

            var mode = ModeDef.Default();
            mode.PlacementInputEnabled = true;
            mode.PlacementSeconds = 0f;
            mode.BoardCap = 0;
            def.Mode = mode;

            def.WaveDeck = Deck();
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>웨이브 2종·보스 1종의 작은 덱. 컨셉 없음 = 레거시 2종 경로.</summary>
        public static WaveDeckDef Deck(int waveCount = 3, float interval = 5f, int pullCap = 2)
            => new WaveDeckDef
            {
                GeneratorVersion = 1,
                WaveSeed = 12345,
                TimerDurationSec = 180f,
                MinWaveCount = waveCount,
                MaxWaveCount = waveCount,
                MinUnitsPerWave = 2,
                MaxUnitsPerWave = 2,
                WaveCountJitter = 0,
                IntraWaveSpacingSec = 0.5f,
                MaxWaveIntervalSec = interval,
                SpawnLeadInSec = 1f,
                UnitGrowthPerWave = 1f,
                MaxPullsPerClear = pullCap,
                BossWaveInterval = 0,
                EnemyPool = new[] { 0, 1 },
                BossPool = System.Array.Empty<int>(),
                Concepts = System.Array.Empty<WaveConceptDef>(),
                ConceptHoldWaves = 3,
            };

        public static UnitDef Defender(string id) => new UnitDef
        {
            Id = id,
            Health = 500f,
            AttackRange = 3f,
            AttackCooldown = 1f,
            AttackTargetCount = 1,
            BodyRadiusTiles = 0.5f,
            FootprintWidth = 1,
            FootprintHeight = 1,
            PlacementLayers = LayerBits.Ground | LayerBits.Path,
            TraversalLayers = 0,
            Cost = 2,
            PlacementCooldown = 0f,
            DeathCooldown = 10f,
            RetireCooldownRatio = 0.4f,
            MaxOnBoard = 4,
            DeployMotionSeconds = 0f,
            AwakeningReward = 4,
            Attack = AttackDef.Default(),
        };

        public static EnemyDef Enemy(string id, float health, int cls, int reward) => new EnemyDef
        {
            Id = id,
            Health = health,
            MoveSpeed = 1.5f,
            AttackRange = 1f,
            AttackCooldown = 1f,
            AttackTargetCount = 1,
            BodyRadius = 0.25f,
            TraversalLayers = LayerBits.Path,
            EnemyClass = cls,
            MinWaveNumber = 1,
            MaxPerWave = 0,
            StabilityDamage = 30,
            DetectionRange = 0f,
            AwakeningReward = reward,
            Attack = AttackDef.Default(),
        };

        /// <summary>판을 열고 배치 창을 닫는다 — 대부분의 테스트가 전투부터 보고 싶어 한다.</summary>
        public static BattleMatch BeginBattle(MatchDefinition def)
        {
            var match = new BattleMatch(def);
            match.Begin();
            match.Apply(Command.FinishPlacement());
            return match;
        }

        public static List<CoreEvent> Listen(BattleMatch match, CoreEventKind kind)
        {
            var log = new List<CoreEvent>();
            match.Bus.Subscribe(kind, 0, log.Add);
            return log;
        }
    }
}
