using System.Collections.Generic;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Combat.Projectile;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 3 — 전투 규칙 테스트의 공용 고정구.
    //
    // ⚠ 여기 수치는 **게임 값이 아니라 픽스처**다(`CoreMapFixtures` 와 같은 규율).
    // 테스트가 묻는 것은 「규칙이 서 있나」이지 「스탯이 얼마인가」가 아니다.
    public static class CoreCombatFixtures
    {
        public static MatchDefinition Definition(
            float defenderDamage = 10f,
            float defenderRange = 3f,
            float defenderCooldown = 1f,
            float defenderHitDelay = 0f,
            int defenderTargetCount = 1,
            float enemyDamage = 0f,
            float enemyRange = 1f,
            float enemyHealth = 100f,
            float enemySpeed = 0f,
            int aggroCapacity = 0,
            AttackPolicy policy = AttackPolicy.Target,
            TargetMode mode = TargetMode.Nearest)
        {
            var map = CoreMapFixtures.Open(12, 5, new int2(11, 2), new int2(0, 1), new int2(0, 3));

            var defenderAttack = AttackDef.Default();
            defenderAttack.Mode = (int)mode;
            defenderAttack.Policy = (int)policy;
            defenderAttack.Outputs = defenderDamage > 0f
                ? new[] { new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = defenderDamage } }
                : System.Array.Empty<AttackOutputDef>();

            var enemyAttack = AttackDef.Default();
            enemyAttack.Mode = (int)TargetMode.Nearest;
            enemyAttack.Outputs = enemyDamage > 0f
                ? new[] { new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = enemyDamage } }
                : System.Array.Empty<AttackOutputDef>();

            var def = new MatchDefinition
            {
                Seed = 7,
                Mode = ModeDef.Default(),
                Map = map,
                Units = new[]
                {
                    new UnitDef
                    {
                        Id = "fixture_defender",
                        Health = 500f,
                        AttackRange = defenderRange,
                        AttackCooldown = defenderCooldown,
                        HitDelaySeconds = defenderHitDelay,
                        AttackTargetCount = defenderTargetCount,
                        BodyRadiusTiles = 0.5f,
                        FootprintWidth = 1,
                        FootprintHeight = 1,
                        PlacementLayers = LayerBits.Ground,
                        TraversalLayers = 0,
                        AggroCapacity = aggroCapacity,
                        MoveSpeed = 1.5f,
                        Attack = defenderAttack,
                    },
                    // ⚠ 순찰 소환물은 **별도 줄**이다. 소환사가 자기 정의를 가리키면 소환물이
                    // 또 소환해 판이 기하급수로 불어난다(테스트가 그대로 멈춘다).
                    new UnitDef
                    {
                        Id = "fixture_patrol",
                        Health = 60f,
                        AttackRange = 1f,
                        AttackCooldown = 1f,
                        AttackTargetCount = 1,
                        BodyRadiusTiles = 0.25f,
                        FootprintWidth = 1,
                        FootprintHeight = 1,
                        PlacementLayers = LayerBits.Ground,
                        TraversalLayers = LayerBits.Path,
                        MoveSpeed = 1.5f,
                        Attack = AttackDef.Default(),
                    },
                },
                Enemies = new[]
                {
                    new EnemyDef
                    {
                        Id = "fixture_enemy",
                        Health = enemyHealth,
                        MoveSpeed = enemySpeed,
                        AttackRange = enemyRange,
                        AttackCooldown = 1f,
                        AttackTargetCount = 1,
                        BodyRadius = 0.25f,
                        TraversalLayers = LayerBits.Path,
                        StabilityDamage = 1,
                        WaypointPathIndex = -1,
                        Attack = enemyAttack,
                    },
                },
            };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>탄 하나를 정의표에 얹고 방어유닛이 그것을 쏘게 한다.</summary>
        public static void GiveProjectile(MatchDefinition def, MovementKind movement, PayloadKind payload,
                                          float speed = 10f, float hitThreshold = 0.3f,
                                          int impactTileRange = 1, int pierce = 1,
                                          float maxDistance = 0f)
        {
            var p = ProjectileDef.Default();
            p.Id = "fixture_projectile";
            p.Movement = (int)movement;
            p.Payload = (int)payload;
            p.Speed = speed;
            p.HitThreshold = hitThreshold;
            p.ImpactTileRange = impactTileRange;
            p.PierceCount = pierce;
            p.MaxDistance = maxDistance;
            p.MinFlightTime = 0.05f;
            def.Projectiles = new[] { p };
            def.Units[0].Attack.ProjectileDefIndex = 0;
            def.ConfigHash = def.ComputeConfigHash();
        }

        public static List<CoreEvent> Listen(BattleMatch m, CoreEventKind kind)
        {
            var got = new List<CoreEvent>();
            m.Bus.Subscribe(kind, 0, e => got.Add(e));
            return got;
        }

        public static Unit First(BattleMatch m, UnitKind kind)
        {
            var units = m.World.Units;
            for (int i = 0; i < units.Count; i++) if (units[i].Kind == kind) return units[i];
            return null;
        }

        public static void Tick(BattleMatch m, int ticks)
        {
            for (int t = 0; t < ticks; t++) m.Tick();
        }
    }
}
