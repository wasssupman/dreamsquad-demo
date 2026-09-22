using System.Collections.Generic;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.Data;

namespace Wassup.BattleCoreUnity
{
    // battle-core-rebuild unit 3 — SO → **전투** 정의표.
    //
    // `MatchDefinitionBuilder` 의 같은 규율이다: 여기가 `ScriptableObject` 를 아는 마지막
    // 자리이고, 이 함수를 지나면 값은 plain 이다.
    //
    // ⚠ **저작 토큰 8 → 런타임 2축 번역이 여기 있다.** 저작은 1축(`ProjectileFlightMode`)이고
    // 런타임은 (궤적, 페이로드) 두 축이다 — **번역은 전사가 아니다**: 같은 궤적이 다른
    // 페이로드와 짝지으면 다른 토큰이 되고(낙하 × 칸 ↔ 낙하 × 적), 수류탄·궤도 두 궤적은
    // 저작 토큰이 아예 없다(코드 경로가 직접 고른다).
    //
    // ⚠ 탄·패턴 표는 **참조에서 자동으로 모인다.** 호출자가 목록을 따로 넘기지 않는 이유:
    // 두 벌이 되면 「정의표엔 있는데 아무도 안 가리키는 탄」과 그 반대가 조용히 생긴다.
    public static class CombatDefinitionBuilder
    {
        /// <summary>
        /// 유닛·적 정의표에 **공격 저작을 채워 넣고** 탄·패턴 표를 만든다.
        /// `MatchDefinitionBuilder.Build` 가 기본 줄을 만든 뒤 이 함수를 부른다.
        /// </summary>
        public static void Fill(MatchDefinition def,
                                DefenderUnitData[] defenders,
                                AttackUnitData[] enemies)
        {
            var projectiles = new List<ProjectileData>();
            var patterns = new List<ProjectilePatternData>();

            // 순찰 소환물은 카탈로그에 없다 — 소환사 능력이 **가리키는** 에셋이라 여기서
            // 정의표에 편입한다. 안 하면 소환 참조가 표 밖을 가리켜 근접으로 접힌다.
            var unitList = new List<DefenderUnitData>(defenders ?? System.Array.Empty<DefenderUnitData>());
            int authored = unitList.Count;
            for (int i = 0; i < authored; i++)
            {
                var summon = unitList[i] != null ? unitList[i].GetAbility<SummonPatrolAbility>() : null;
                if (summon?.patrolUnit == null) continue;
                if (!unitList.Contains(summon.patrolUnit)) unitList.Add(summon.patrolUnit);
            }
            if (unitList.Count != authored) def.Units = Grow(def.Units, unitList, authored);

            for (int i = 0; i < def.Units.Length && i < unitList.Count; i++)
            {
                var d = unitList[i];
                if (d == null) continue;
                def.Units[i].MoveSpeed = d.moveSpeed;
                def.Units[i].Attack = BuildDefenderAttack(d, unitList, projectiles, patterns);
            }
            for (int i = 0; i < def.Enemies.Length && i < (enemies?.Length ?? 0); i++)
            {
                var e = enemies[i];
                if (e == null) continue;
                def.Enemies[i].Attack = BuildEnemyAttack(e, projectiles, patterns);
            }

            def.Projectiles = new ProjectileDef[projectiles.Count];
            for (int i = 0; i < projectiles.Count; i++) def.Projectiles[i] = ToDef(projectiles[i]);

            def.Patterns = new PatternDef[patterns.Count];
            for (int i = 0; i < patterns.Count; i++)
                def.Patterns[i] = ToDef(patterns[i], projectiles);
        }

        private static UnitDef[] Grow(UnitDef[] rows, List<DefenderUnitData> all, int authored)
        {
            var next = new UnitDef[all.Count];
            System.Array.Copy(rows, next, System.Math.Min(rows.Length, authored));
            for (int i = authored; i < all.Count; i++) next[i] = MatchDefinitionBuilder.ToUnitDef(all[i]);
            return next;
        }

        private static AttackDef BuildDefenderAttack(DefenderUnitData d,
                                                     List<DefenderUnitData> units,
                                                     List<ProjectileData> projectiles,
                                                     List<ProjectilePatternData> patterns)
        {
            var a = AttackDef.Default();
            a.TargetLayers = (int)d.EffectiveAttackTargetLayers;
            a.ProjectileDefIndex = IndexOf(projectiles, d.projectile);
            a.Outputs = ToOutputs(d.outputs);
            a.KnockbackDistance = d.knockbackDistance;
            a.KnockbackDuration = d.knockbackDuration;
            a.SleepOnHitSec = d.sleepOnHitSec;
            a.KnockupOnHitSec = d.knockupOnHitSec;
            a.KnockupVisualHeight = d.knockupVisualHeight;
            Bake(d.attackShape, ref a);

            // 지속 락 — 방어유닛은 저작 축이 없다. 「한 번 문 대상은 죽거나 벗어날 때까지」가
            // 기본이고, 제외 4종(최전방·힐러·가디언·은퇴한 facing)은 런타임이 판정한다.
            a.Mode = (int)TargetMode.Nearest;

            // 아키타입 → 정책 값. **타입 체크가 아니라 값**이다(census 「HasComponent 자연 분기」).
            var bomb = d.GetAbility<BombThrowAbility>();
            if (bomb != null)
            {
                a.Policy = (int)AttackPolicy.Bomb;
                a.BombProjectileDefIndex = IndexOf(projectiles, d.projectile);
                a.BombDamage = bomb.damage;
                a.BombAoeTileRange = bomb.aoeTileRange;
                a.BombAoeTargetCap = bomb.aoeTargetCap;
                a.BombTravelSeconds = bomb.travelSec;
                a.BombFuseSeconds = bomb.fuseSec;
                a.BombArcHeight = bomb.arcHeight;
            }

            var summon = d.GetAbility<SummonPatrolAbility>();
            if (summon?.patrolUnit != null)
            {
                a.Policy = (int)AttackPolicy.Summon;
                a.SummonPatrolDefIndex = units.IndexOf(summon.patrolUnit);
            }

            var volley = d.GetAbility<DirectionalVolleyAbility>();
            if (volley != null) a.PatternDefIndices = CollectPatterns(volley, patterns, projectiles);

            return a;
        }

        private static AttackDef BuildEnemyAttack(AttackUnitData e,
                                                  List<ProjectileData> projectiles,
                                                  List<ProjectilePatternData> patterns)
        {
            var a = AttackDef.Default();
            a.TargetLayers = (int)e.EffectiveTraversalLayers;
            a.ProjectileDefIndex = IndexOf(projectiles, e.projectile);
            a.Outputs = ToOutputs(e.outputs);
            a.Mode = (int)e.targetMode;
            // `DefenderClass.None`(0) = 우선 없음 · `DefenderClassFlags.Everything`(~0) = 전부.
            // 둘 다 정의표의 「0/전체 비트 = 제약 없음」 규약과 그대로 맞는다.
            a.PriorityClass = (int)e.targetPriorityClass;
            a.ClassMask = (int)e.targetClassMask;
            // **보스 면역은 등급에서 나온다** — 별도 토글을 만들지 않는다(옛 전투도 `tier` 가
            // 유일한 출처였고, 토글을 두면 「보스인데 면역이 아닌」 저작이 가능해진다).
            a.BossImmune = e.tier == EnemyTier.Boss;
            Bake(e.attackShape, ref a);
            return a;
        }

        private static int[] CollectPatterns(DirectionalVolleyAbility volley,
                                             List<ProjectilePatternData> patterns,
                                             List<ProjectileData> projectiles)
        {
            var pat = volley != null ? volley.pattern : null;
            if (pat == null) return System.Array.Empty<int>();
            if (pat.barrel != null) IndexOf(projectiles, pat.barrel);
            int i = patterns.IndexOf(pat);
            if (i < 0) { patterns.Add(pat); i = patterns.Count - 1; }
            return new[] { i };
        }

        private static void Bake(AttackShape authored, ref AttackDef a)
        {
            var baked = AttackShapeBake.From(in authored, out bool ok);
            a.ShapeKind = baked.kind;
            a.ShapeSinHalf = baked.sinHalf;
            a.ShapeCosHalf = baked.cosHalf;
            a.ShapeHalfWidth = baked.halfWidth;
            // bake 는 순수 함수라 로그를 찍지 않는다 — 「정의역 밖」을 말하는 것은 호출부의 몫이다.
            if (!ok)
                UnityEngine.Debug.LogWarning(
                    "[CombatDefinitionBuilder] 공격 도형 각도가 정의역 밖이다(180° 초과 360° 미만) — 전방위로 읽는다.");
        }

        private static AttackOutputDef[] ToOutputs(AttackOutput[] src)
        {
            if (src == null || src.Length == 0) return System.Array.Empty<AttackOutputDef>();
            var outp = new AttackOutputDef[src.Length];
            for (int i = 0; i < src.Length; i++)
                outp[i] = new AttackOutputDef
                {
                    Kind = (Wassup.BattleCore.AttackOutputKind)(int)src[i].kind,
                    Magnitude = src[i].magnitude,
                    Duration = src[i].duration,
                    Stat = (int)src[i].stat,
                    Op = (int)src[i].op,
                    StackKind = (int)src[i].stackKind,
                    StackMaxStack = src[i].stackMaxStack,
                };
            return outp;
        }

        private static int IndexOf(List<ProjectileData> table, ProjectileData asset)
        {
            if (asset == null) return -1;
            int i = table.IndexOf(asset);
            if (i >= 0) return i;
            table.Add(asset);
            return table.Count - 1;
        }

        private static ProjectileDef ToDef(ProjectileData p)
        {
            var (movement, payload) = Translate(p.flightMode);
            var d = ProjectileDef.Default();
            d.Id = p.id;
            d.Speed = p.speed;
            d.HitThreshold = p.hitThreshold;
            d.ArcHeight = p.arcHeight;
            d.MinFlightTime = p.minFlightTime;
            d.Movement = (int)movement;
            d.Payload = (int)payload;
            d.SplashRadius = p.splashRadius;
            d.SplashDamageMul = p.splashDamageMul;
            d.ImpactTileRange = p.impactTileRange;
            d.PierceCount = p.pierceCount;
            d.RehitCooldownSec = p.rehitCooldownSec;
            d.KnockbackDistance = p.knockbackDistance;
            d.KnockbackDuration = p.knockbackDuration;
            d.BezierLateral = p.bezierLateral;
            d.BezierForwardBias = p.bezierForwardBias;
            d.BlockerHealth = p.spawnBlocker != null ? p.spawnBlocker.maxHp : 0f;
            d.BlockerBodyRadius = 0.5f;
            return d;
        }

        // 저작 토큰 → (궤적, 페이로드). **전사가 아니다** — 같은 궤적이 다른 페이로드와
        // 짝지으면 다른 토큰이고, 궤도·수류탄은 저작 토큰이 없다(코드 경로가 직접 고른다).
        private static (MovementKind, PayloadKind) Translate(ProjectileFlightMode mode)
        {
            switch (mode)
            {
                case ProjectileFlightMode.BallisticToCell:
                    return (MovementKind.BallisticArcToPoint, PayloadKind.TileAoe);
                case ProjectileFlightMode.Directional:
                    return (MovementKind.DirectionalLinear, PayloadKind.PathHit);
                case ProjectileFlightMode.BezierHoming:
                    return (MovementKind.BezierHomingToEntity, PayloadKind.SingleSplash);
                case ProjectileFlightMode.SkyFall:
                    return (MovementKind.SkyFall, PayloadKind.TileAoe);
                case ProjectileFlightMode.SkyFallOnTarget:
                    return (MovementKind.SkyFallOnEntity, PayloadKind.SingleSplash);
                case ProjectileFlightMode.Boomerang:
                    return (MovementKind.BoomerangReturn, PayloadKind.PathHit);
                case ProjectileFlightMode.BallisticBlocker:
                    return (MovementKind.BallisticArcToPoint, PayloadKind.SpawnBlocker);
                default:
                    return (MovementKind.HomingToEntity, PayloadKind.SingleSplash);
            }
        }

        private static PatternDef ToDef(ProjectilePatternData p, List<ProjectileData> projectiles)
        {
            var d = new PatternDef
            {
                Id = p.id,
                BarrelProjectileDefIndex = IndexOf(projectiles, p.barrel),
                Damage = p.damage,
                Selection = (int)p.selection,
                MinAngleDeg = p.minAngleDeg,
                MaxAngleDeg = p.maxAngleDeg,
                RandomizeShotsPerTrigger = p.randomizeShotsPerTrigger,
                RandomIntervalMinSec = p.randomIntervalMinSec,
                RandomIntervalMaxSec = p.randomIntervalMaxSec,
                ReselectPerShot = p.reselectPerShot,
                TelegraphSec = p.telegraphSec,
                ScopeTileRange = p.scopeTileRange,
                FanOutToAllCandidates = p.fanOutToAllCandidates,
                FanOutStaggerSec = p.fanOutStaggerSec,
            };
            int shots = p.shots != null ? p.shots.Length : 0;
            d.Shots = new PatternShotDef[shots];
            for (int i = 0; i < shots; i++)
                d.Shots[i] = new PatternShotDef
                {
                    DirectionT = p.shots[i].directionT,
                    IntervalAfterPreviousSec = p.shots[i].intervalAfterPreviousSec,
                };
            return d;
        }
    }
}
