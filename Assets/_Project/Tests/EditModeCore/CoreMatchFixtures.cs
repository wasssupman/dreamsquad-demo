using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;
using Wassup.Battle.Units;
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

        /// <summary>
        /// 저작 거점 한 기를 판에 더한다. 자리(`MapSnapshot.Structures`)와 스탯 줄
        /// (`MatchDefinition.Structures`)이 **쌍으로** 늘어난다 — 라이브에서 그 쌍을 맞추는
        /// 것은 빌더의 일이고(칸으로 저작과 맞춘다), 여기서는 고정구가 직접 맞춘다.
        /// 해시는 호출자가 마지막에 굽는다.
        /// </summary>
        public static void AddStructure(MatchDefinition def, int2 cell, Faction faction,
                                        float health, float attackDamage = 0f,
                                        float attackRange = 0f, int targetFactions = 0)
        {
            var attack = AttackDef.Default();
            attack.Mode = (int)TargetMode.Nearest;
            attack.Outputs = attackDamage > 0f
                ? new[] { new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = attackDamage } }
                : System.Array.Empty<AttackOutputDef>();

            var rows = new List<StructureDef>(def.Structures)
            {
                new StructureDef
                {
                    Id = "fixture_structure",
                    Health = health,
                    AttackRange = attackRange,
                    AttackCooldown = 1f,
                    AttackTargetCount = 1,
                    TargetFactions = targetFactions,
                    Attack = attack,
                },
            };
            var spots = new List<StructureSpot>(def.Map.Structures)
            {
                new StructureSpot
                {
                    Cell = cell,
                    Faction = (int)faction,
                    Footprint = ((int)faction & Factions.AnyInstinct) != 0
                        ? StructureSize.Instinct
                        : StructureSize.Core,
                    DefIndex = rows.Count - 1,
                },
            };
            def.Structures = rows.ToArray();
            def.Map.Structures = spots.ToArray();
            def.Map.CloseReservedPlacement();
        }

        /// <summary>
        /// 판 위의 **첫 방어유닛**. 테스트가 `new SimEntityId(1)` 을 박으면 판 위의 가구가
        /// 하나 늘 때마다(마음 타워가 그랬다) 통째로 깨진다 — 그 리터럴은 결정론의 증거가
        /// 아니라 **우연**이고, 이 판에서 증언할 것은 「방금 놓은 유닛」이다.
        /// (id 발급 순서 자체를 묻는 테스트는 `SimEntityIdTests` 가 따로 진다.)
        /// </summary>
        public static SimEntityId PlacedDefender(BattleMatch match)
        {
            var units = match.World.Units;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Kind == UnitKind.Defender) return units[i].Id;
            return SimEntityId.None;
        }

        public static List<CoreEvent> Listen(BattleMatch match, CoreEventKind kind)
        {
            var log = new List<CoreEvent>();
            match.Bus.Subscribe(kind, 0, log.Add);
            return log;
        }
    }
}
