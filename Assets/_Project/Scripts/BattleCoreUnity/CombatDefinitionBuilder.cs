using System.Collections.Generic;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.Data;
using CoreOp = Wassup.BattleCore.Effects.CombineOp;
using CoreStack = Wassup.BattleCore.Effects.StackKind;
using CoreStat = Wassup.BattleCore.Effects.StatKind;

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
                                AttackUnitData[] enemies,
                                IReadOnlyList<StructureEntry> structures = null,
                                MatchViewAssets viewAssets = null)
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

            // 거점은 **탄 표를 유닛·적과 공유한다**(본능 포탑의 탄이 그 판의 탄 목록에 든다).
            // 표를 굳히기 **전**에 채우는 이유가 이것이다 — 뒤로 미루면 본능의 탄만 표 밖을
            // 가리켜 조용히 근접으로 접힌다.
            FillStructures(def, structures, projectiles, viewAssets);

            def.Projectiles = new ProjectileDef[projectiles.Count];
            for (int i = 0; i < projectiles.Count; i++) def.Projectiles[i] = ToDef(projectiles[i]);

            def.Patterns = new PatternDef[patterns.Count];
            for (int i = 0; i < patterns.Count; i++)
                def.Patterns[i] = ToDef(patterns[i], projectiles);

            // unit 5a — 뷰가 `DefIndex` 로 프리팹을 되찾을 수 있게 **번호를 매긴 그 목록**을 넘긴다.
            viewAssets?.SetProjectiles(projectiles);
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
            // 아군 대상(힐러)은 통행 층을 거르지 않는다(옛 `targetTraversalLayers = targetAllies ? 0 : …`).
            // 대상 진영은 `MatchDefinitionBuilder.ToUnitDef` 가 아군 단독으로 이미 접었다.
            a.TargetLayers = d.targetAllies ? 0 : (int)d.EffectiveAttackTargetLayers;
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

        /// <summary>
        /// 거점 정의표 + 자리마다의 인덱스. **자리는 «어디에 무엇이» 만 말하고 스탯은 표가
        /// 든다** — 같은 `StructureData` 를 여러 자리에 찍는 것이 저작의 기본형이라(본능 3기 =
        /// 같은 SO 세 자리) 값을 자리마다 복제하면 「같은 건물인데 체력이 다른」 상태가
        /// 표현 가능해진다.
        ///
        /// 자리와 저작을 **칸으로 맞춘다.** 격자 투영(`GeneratedMap.structures`)에는 셀과
        /// 진영밖에 없고 SO 참조는 스테이지 저작 목록에만 있기 때문이다. 짝을 못 찾은
        /// 자리는 `DefIndex = -1` 로 남아 **안 세워진다** — 조용히 기본 스탯으로 세우면
        /// 「체력이 어디서 왔는지 아무도 모르는 건물」이 판에 선다.
        /// </summary>
        private static void FillStructures(MatchDefinition def,
                                           IReadOnlyList<StructureEntry> structures,
                                           List<ProjectileData> projectiles,
                                           MatchViewAssets viewAssets = null)
        {
            var spots = def.Map.Structures;
            for (int i = 0; i < spots.Length; i++) spots[i].DefIndex = -1;
            if (structures == null || structures.Count == 0) { viewAssets?.SetStructures(null); return; }

            var assets = new List<StructureData>(structures.Count);
            var rows = new List<StructureDef>(structures.Count);

            for (int i = 0; i < spots.Length; i++)
            {
                var cell = spots[i].Cell;
                StructureData data = null;
                for (int k = 0; k < structures.Count; k++)
                {
                    var e = structures[k];
                    if (e.data == null || e.cell.x != cell.x || e.cell.y != cell.y) continue;
                    data = e.data;
                    break;
                }
                if (data == null)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[CombatDefinitionBuilder] 거점 자리 ({cell.x},{cell.y}) 에 맞는 StructureData 가 "
                        + "스테이지 저작 목록에 없다 — 이 자리는 세우지 않는다.");
                    continue;
                }

                int di = assets.IndexOf(data);
                if (di < 0)
                {
                    assets.Add(data);
                    rows.Add(ToStructureDef(data, projectiles));
                    di = rows.Count - 1;
                }
                spots[i].DefIndex = di;
            }

            def.Structures = rows.ToArray();
            viewAssets?.SetStructures(assets);
        }

        private static StructureDef ToStructureDef(StructureData d, List<ProjectileData> projectiles)
        {
            var a = AttackDef.Default();
            a.ProjectileDefIndex = IndexOf(projectiles, d.projectile);
            a.Outputs = d.attackDamage > 0f
                ? new[]
                {
                    new AttackOutputDef
                    {
                        Kind = Wassup.BattleCore.AttackOutputKind.Damage,
                        Magnitude = d.attackDamage,
                    },
                }
                : System.Array.Empty<AttackOutputDef>();

            return new StructureDef
            {
                Id = string.IsNullOrEmpty(d.displayName) ? d.name : d.displayName,
                Health = d.health,
                AttackRange = d.attackRange,
                AttackCooldown = d.attackCooldown,
                HitDelaySeconds = 0f,
                AttackTargetCount = 1,
                // ⚠ 저작 그대로 싣는다 — **0 은 「아무도 안 때린다」**이지 기본값이 아니다.
                // 거점은 편이 배치에서 오므로 SO 가 자기 상대를 모른다.
                TargetFactions = (int)d.targetFactions,
                Attack = a,
            };
        }

        private static AttackDef BuildEnemyAttack(AttackUnitData e,
                                                  List<ProjectileData> projectiles,
                                                  List<ProjectilePatternData> patterns)
        {
            var a = AttackDef.Default();
            // ⚠ **적의 공격은 통행 층을 거르지 않는다(0)** — 옛 `AttackState.targetTraversalLayers`
            // 는 적에게 설정된 적이 없다. 「자기 통행 층」을 실으면 비행 적(하늘 4)이 지상|경로(3)
            // 순찰병을 조준 후보에서 떨군다(2026-09-24 드리프트 감사 M7). 방어유닛 쪽은 저작
            // 칸(`attackTargetLayers`)이 있어 근접이 하늘로 안 번지는 근거가 된다 — 비대칭이 의도다.
            a.TargetLayers = 0;
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
                    // ⚠ 번호 캐스트 금지 — 이름으로 옮긴다(아래 매핑 넷의 이유).
                    Kind = ToCoreOutputKind(src[i].kind),
                    Magnitude = src[i].magnitude,
                    Duration = src[i].duration,
                    Stat = (int)ToCoreStat(src[i].stat),
                    Op = (int)ToCoreOp(src[i].op),
                    StackKind = (int)ToCoreStackKind(src[i].stackKind),
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

        // ── 저작 어휘 → 코어 어휘 ────────────────────────────────────────────
        //
        // ⚠ **전부 이름으로 옮긴다.** 번호가 지금 같다는 사실에 기대지 않는 이유는
        // `PatternSelectionRule` 이 이미 그 함정에 빠졌기 때문이다 — 코어가 번호를 재배열하고
        // 빌더가 통짜 캐스트로 옮겨서 12개 저작 중 11개가 다른 규칙으로 읽혔다.
        // 두 어휘는 **다른 어셈블리**라 컴파일러가 어긋남을 못 잡고, 저작 값은 이미 에셋에
        // byte 로 구워져 있어 한쪽이 앞에 값을 끼우는 순간 조용히 밀린다.
        // (리뷰가 든 시나리오: `ThresholdMode` 앞에 값이 끼면 `Consume` 이 `Edge` 로 읽혀
        //  소비형 임계가 스택을 안 깎고 **무한 발화**한다.)
        // `BuilderEnumPinTests` 가 일곱 쌍의 이름·개수 일치와 매핑의 이름 보존을 고정한다.

        public static Wassup.BattleCore.AttackOutputKind ToCoreOutputKind(
            Wassup.Data.AttackOutputKind authored)
        {
            switch (authored)
            {
                case Wassup.Data.AttackOutputKind.Damage: return Wassup.BattleCore.AttackOutputKind.Damage;
                case Wassup.Data.AttackOutputKind.Heal: return Wassup.BattleCore.AttackOutputKind.Heal;
                case Wassup.Data.AttackOutputKind.ApplyStat: return Wassup.BattleCore.AttackOutputKind.ApplyStat;
                case Wassup.Data.AttackOutputKind.ApplyStack: return Wassup.BattleCore.AttackOutputKind.ApplyStack;
                default:
                    UnityEngine.Debug.LogError(
                        $"[CombatDefinitionBuilder] 모르는 산출물 종류({authored}) — 피해로 접는다.");
                    return Wassup.BattleCore.AttackOutputKind.Damage;
            }
        }

        public static CoreStat ToCoreStat(Wassup.Battle.Effects.StatKind authored)
        {
            switch (authored)
            {
                case Wassup.Battle.Effects.StatKind.DamageMul: return CoreStat.DamageMul;
                case Wassup.Battle.Effects.StatKind.AttackSpeedMul: return CoreStat.AttackSpeedMul;
                case Wassup.Battle.Effects.StatKind.DmgTakenMul: return CoreStat.DmgTakenMul;
                case Wassup.Battle.Effects.StatKind.RegenPerSec: return CoreStat.RegenPerSec;
                case Wassup.Battle.Effects.StatKind.MoveSpeedMul: return CoreStat.MoveSpeedMul;
                case Wassup.Battle.Effects.StatKind.DamageVsCcMul: return CoreStat.DamageVsCcMul;
                case Wassup.Battle.Effects.StatKind.MaxHealthMul: return CoreStat.MaxHealthMul;
                default:
                    UnityEngine.Debug.LogError(
                        $"[CombatDefinitionBuilder] 모르는 스탯({authored}) — 피해 배율로 접는다.");
                    return CoreStat.DamageMul;
            }
        }

        public static CoreOp ToCoreOp(Wassup.Battle.Effects.CombineOp authored)
        {
            switch (authored)
            {
                case Wassup.Battle.Effects.CombineOp.Multiplicative: return CoreOp.Multiplicative;
                case Wassup.Battle.Effects.CombineOp.Additive: return CoreOp.Additive;
                case Wassup.Battle.Effects.CombineOp.Override: return CoreOp.Override;
                default:
                    UnityEngine.Debug.LogError(
                        $"[CombatDefinitionBuilder] 모르는 결합 연산자({authored}) — 곱셈으로 접는다.");
                    return CoreOp.Multiplicative;
            }
        }

        public static CoreStack ToCoreStackKind(Wassup.Battle.Effects.StackKind authored)
        {
            switch (authored)
            {
                case Wassup.Battle.Effects.StackKind.None: return CoreStack.None;
                case Wassup.Battle.Effects.StackKind.Fire: return CoreStack.Fire;
                case Wassup.Battle.Effects.StackKind.Ice: return CoreStack.Ice;
                case Wassup.Battle.Effects.StackKind.Bleed: return CoreStack.Bleed;
                case Wassup.Battle.Effects.StackKind.Poison: return CoreStack.Poison;
                case Wassup.Battle.Effects.StackKind.Fatigue: return CoreStack.Fatigue;
                default:
                    UnityEngine.Debug.LogError(
                        $"[CombatDefinitionBuilder] 모르는 스택 종류({authored}) — 없음으로 접는다.");
                    return CoreStack.None;
            }
        }

        /// <summary>
        /// 저작 선정 규칙 → 코어 어휘. **이름으로 옮긴다.**
        ///
        /// ⚠ 통짜 캐스트(`(int)p.selection`)로 옮기던 시절, 코어 enum 이 번호를 재배열해
        /// 두어서 **12개 저작 중 11개가 다른 규칙으로 읽혔다**(순회 폭격 → 선택 안 함,
        /// 방향 발사 → 무작위 저격). 번호가 지금은 같아도 캐스트로 되돌리지 말 것 —
        /// 한쪽이 append 하는 날 같은 일이 조용히 다시 난다.
        /// </summary>
        public static Wassup.BattleCore.Combat.Emission.PatternSelectionRule ToCoreSelection(
            PatternSelectionRule authored)
        {
            switch (authored)
            {
                case PatternSelectionRule.RoundRobin:
                    return Wassup.BattleCore.Combat.Emission.PatternSelectionRule.RoundRobin;
                case PatternSelectionRule.DeterministicShuffle:
                    return Wassup.BattleCore.Combat.Emission.PatternSelectionRule.DeterministicShuffle;
                case PatternSelectionRule.None:
                    return Wassup.BattleCore.Combat.Emission.PatternSelectionRule.None;
                case PatternSelectionRule.Nearest:
                    return Wassup.BattleCore.Combat.Emission.PatternSelectionRule.Nearest;
                default:
                    UnityEngine.Debug.LogError(
                        $"[CombatDefinitionBuilder] 모르는 선정 규칙({authored}) — 순회로 접는다.");
                    return Wassup.BattleCore.Combat.Emission.PatternSelectionRule.RoundRobin;
            }
        }

        private static PatternDef ToDef(ProjectilePatternData p, List<ProjectileData> projectiles)
        {
            var d = new PatternDef
            {
                Id = p.id,
                BarrelProjectileDefIndex = IndexOf(projectiles, p.barrel),
                Damage = p.damage,
                Selection = (int)ToCoreSelection(p.selection),
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
