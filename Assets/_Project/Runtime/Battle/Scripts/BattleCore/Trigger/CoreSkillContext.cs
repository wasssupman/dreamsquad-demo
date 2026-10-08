using Unity.Mathematics;
using Somnia.Battle.BattleCore.Map;
using Somnia.Battle.Skills;

namespace Somnia.Battle.BattleCore.Trigger
{
    // battle-core-rebuild unit 7a — `ISkillContext` 의 코어 구현(← 옛 `Battle/Skills/EcsSkillContext.cs` 1,215줄).
    //
    // **질의와 `Emit` 둘뿐이다.** 옛 어댑터의 나머지(ECS 핸들 역변환 · 풀 복사 · 큐 싱크 14개 · ECB)는 버렸다 —
    // foundation README 가 「버려지는 것은 이것뿐이고 그것이 포트 패턴의 비용」이라 선언한 그 부분이다.
    // `ISkillContext`·concrete·seam 규칙은 그대로 산다(`Somnia.Battle.Skills` 는 한 줄도 안 고쳤다).
    //
    // concrete 는 상태를 **안 바꾼다**(계약 3). `Emit` 은 곧장 `IntentApplier` 로 간다 — 그것이
    // `BattleWorld` 를 바꾸는 스킬 경로의 **유일한 표면**이다(S20 — `CoreArchitectureTests` 가 소스로 못박는다).
    //
    // S22 가 여기서 구조적으로 닫힌다: 옛 어댑터는 두 풀(적·방어유닛) 밖의 개체에 핸들은 만들고 역변환은 실패해
    // **효과가 조용히 사라졌다**(대상 쪽엔 경고도 없었다). 새 코어는 개체가 곧 핸들이다(`SimEntityId`).
    public sealed class CoreSkillContext : ISkillContext
    {
        // 후보 버퍼 상한은 concrete 가 넘기는 배열(`MaxTargets` 64)이 정한다 — **가까운 64 가 아니라 목록 순서
        // 선착 64 다**(S23 — 현행 박제). 목록 순서 = `SimEntityId` 오름차순(결정론). 넘으면 말한다.

        private readonly BattleWorld _world;
        private readonly MapRuntime _map;
        private readonly IntentApplier _applier;

        private bool _warnedMetric;
        private bool _warnedOverflow;

        public System.Action<string> Report;

        public CoreSkillContext(BattleWorld world, MapRuntime map, IntentApplier applier)
        {
            _world = world;
            _map = map;
            _applier = applier;
        }

        public IntentApplier Applier => _applier;

        /// <summary>한 발동의 문맥을 연다 — 쓰기 표면이 「누가 · 어느 규칙이」를 안다(칸 판별자·발사 명세 슬롯).</summary>
        internal void Begin(Binding b, in TriggerEvent e, in EffectDef effect, Faction casterFaction, TickContext ctx)
        {
            _warnedMetric = false;
            _warnedOverflow = false;
            _applier.Begin(b, in effect, casterFaction, ctx);
        }

        internal void End() => _applier.End();

        public static SkillEntityId ToSkill(SimEntityId id)
            => id.IsNone ? SkillEntityId.None : new SkillEntityId(id.Value);

        public static SimEntityId FromSkill(SkillEntityId id)
            => id.IsValid ? new SimEntityId(id.Value) : SimEntityId.None;

        private Unit U(SkillEntityId id) => id.IsValid ? _world.Find(new SimEntityId(id.Value)) : null;

        // ── 자리 ─────────────────────────────────────────────────────────────

        public float3 Position(SkillEntityId id) { var u = U(id); return u != null ? u.Position : float3.zero; }
        public int2 CellOf(SkillEntityId id) => CellOfPosition(Position(id));
        public int2 CellOfPosition(float3 world) => _map != null ? _map.CellOf(world) : new int2((int)world.x, (int)world.z);
        public float3 CellCenter(int2 cell) => _map != null ? _map.CenterOf(cell) : new float3(cell.x, 0f, cell.y);
        public float TileSize => _map != null ? _map.TileSize : 1f;

        /// <summary>
        /// 배치 방향. **조준 배치는 은퇴했다**(distance-based-range — 옛 `DeployedFacing` 부착이 0) —
        /// 그래서 언제나 「무조준」이고, 발사 명세는 후보에서 방향을 고른다.
        /// </summary>
        public bool TryFacing(SkillEntityId id, out float2 dirXZ) { dirXZ = default; return false; }

        // ── 정체 ─────────────────────────────────────────────────────────────

        public Faction FactionOf(SkillEntityId id) { var u = U(id); return u != null ? u.Faction : Faction.None; }
        public float Health(SkillEntityId id) { var u = U(id); return u != null ? u.Health : 0f; }
        public float MaxHealth(SkillEntityId id) { var u = U(id); return u != null ? u.MaxHealth : 0f; }

        public float Stat(SkillEntityId id, UnitStat stat)
        {
            var u = U(id);
            if (u == null) return 0f;
            switch (stat)
            {
                case UnitStat.BodyRadius: return u.HitRadius;   // 몸은 공격 무관
                case UnitStat.EffectiveHpRatio:
                {
                    float max = u.MaxHealth > 0f ? u.MaxHealth : 1f;
                    return (u.Health + Combat.ShieldMath.Sum(u.Shield.Slots)) / max;
                }
            }
            var a = u.Attack;
            if (a == null) return 0f;
            switch (stat)
            {
                case UnitStat.AttackRange: return a.Range;
                case UnitStat.AttackTargetCount: return a.TargetCount;
                case UnitStat.TargetTraversalLayers: return a.TargetLayers;
                case UnitStat.AggroCapacity: return a.AggroCapacity;
                case UnitStat.AttackCooldownRemaining: return a.CooldownRemaining;
                case UnitStat.KnockupVisualHeight: return a.Cc.KnockupVisualHeight;
                case UnitStat.KnockupHopSeconds: return a.Cc.KnockupSeconds;
                default:
                    Warn($"[SkillContext] Stat({stat}) 은 배선되지 않았다 — 0 으로 답한다.");
                    return 0f;
            }
        }

        public bool Has(SkillEntityId id, UnitPredicate pred)
        {
            var u = U(id);
            if (u == null) return false;
            switch (pred)
            {
                case UnitPredicate.Alive: return !u.Dead;
                case UnitPredicate.PendingDeployment: return u.Deploying;
                case UnitPredicate.InUltimateLeap: return u.Progressive != null && u.Progressive.LeapActive;
                // 실드 그릇 — 옛 전투는 방어유닛·순찰·적 **전원**에 붙였다(거점은 제외).
                case UnitPredicate.HasShieldBuffer: return IsCombatant(u);
                case UnitPredicate.HasAggroCapacity: return u.Aggro != null && u.Aggro.Capacity > 0;
                case UnitPredicate.IsPathFollowing: return u.Move != null;
                case UnitPredicate.CanReceiveDamage: return !u.HealthExternal;
                case UnitPredicate.HasPosition: return true;
                default: return false;
            }
        }

        public byte TraversalLayers(SkillEntityId id) => (byte)Stat(id, UnitStat.TargetTraversalLayers);

        public float ShieldValueFrom(SkillEntityId target, SkillEntityId source)
        {
            var u = U(target);
            return u != null ? Combat.ShieldMath.ValueFromSource(u.Shield.Slots, FromSkill(source)) : 0f;
        }

        // ── 후보 ─────────────────────────────────────────────────────────────

        public int Opponents(CasterRef caster, float3 center, int tileRange,
                             CandidateFilter filter, RangeMetric metric, SkillEntityId[] into)
            => Collect(FactionRelation.OpponentUnitsOf(caster.Faction), caster, center, tileRange, filter, metric, into);

        public int Allies(CasterRef caster, float3 center, int tileRange,
                          CandidateFilter filter, RangeMetric metric, SkillEntityId[] into)
            => Collect(FactionRelation.AllyUnitsOf(caster.Faction), caster, center, tileRange, filter, metric, into);

        // 옛 풀 = 적(`AttackUnitTag`)·방어유닛(`DefenderUnitTag`) 두 벌 — 거점·길막은 후보가 아니었다.
        private static bool IsCombatant(Unit u)
            => u.Kind == UnitKind.Defender || u.Kind == UnitKind.Patrol || u.Kind == UnitKind.Enemy;

        private int Collect(Faction wanted, CasterRef caster, float3 center, int tileRange,
                            CandidateFilter filter, RangeMetric metric, SkillEntityId[] into)
        {
            if (wanted == Faction.None) return 0;
            // 형 → 원점 항. **매핑은 `TryOriginRadius` 하나**다(페이크·프리뷰가 같은 함수를 부른다).
            if (!SkillMath.TryOriginRadius(metric, caster.BodyRadius, out float originR))
            {
                if (!_warnedMetric)
                {
                    _warnedMetric = true;
                    Warn($"[SkillContext] RangeMetric {(int)metric} 을 매핑할 수 없다 — 후보 0. 0(None) 이면 concrete 의 배선 누락이다.");
                }
                return 0;
            }
            var casterUnit = U(caster.Unit);
            byte hostLayers = casterUnit != null && casterUnit.Attack != null ? casterUnit.Attack.TargetLayers : (byte)0;
            float inv = TileSize > 1e-6f ? 1f / TileSize : 1f;

            int n = 0, overflow = 0;
            var units = _world.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (!IsCombatant(u) || ((int)u.Faction & (int)wanted) == 0) continue;
                if ((filter & CandidateFilter.ExcludeSelf) != 0 && u == casterUnit) continue;
                if ((filter & CandidateFilter.ExcludeDead) != 0 && u.Dead) continue;
                if ((filter & CandidateFilter.ExcludePendingDeployment) != 0 && u.Deploying) continue;
                if ((filter & CandidateFilter.ExcludeInUltimateLeap) != 0
                    && u.Progressive != null && u.Progressive.LeapActive) continue;
                if ((filter & CandidateFilter.RequireDamageable) != 0 && u.HealthExternal) continue;
                if ((filter & CandidateFilter.RequireHealth) != 0 && u.MaxHealth <= 0f) continue;
                if ((filter & CandidateFilter.MatchTraversalLayers) != 0
                    && !LayerBits.CanTarget(hostLayers, u.Move != null ? u.Move.TraversalLayers : (byte)0)) continue;

                // 제약 13 — 진입점 하나. 대상 몸은 후보의 `HitRadius`.
                if (!SkillMath.ReachWithOrigin((u.Position.x - center.x) * inv, (u.Position.z - center.z) * inv,
                                               tileRange, originR, u.HitRadius)) continue;
                if (n >= into.Length) { overflow++; continue; }
                into[n++] = ToSkill(u.Id);
            }
            if (overflow > 0 && !_warnedOverflow)
            {
                _warnedOverflow = true;
                Warn($"[SkillContext] 범위 안 후보 {overflow}기가 상한({into.Length})에 걸려 잘렸다 — 대상 수만 조용히 준다(S23).");
            }
            return n;
        }

        // ── 격자 위의 판단(도약) ─────────────────────────────────────────────

        /// <summary>
        /// 상대가 가장 많이 모인 칸. ⚠ **사각 자(체비셰프)를 유지한다** — 사용자 결정 ②(2026-09-24 기본값 =
        /// 현행 박제). 원으로 바꾸면 보스가 내려앉는 칸이 달라진다(밸런스 변경). 동률은 row-major 칸 키 오름차순.
        /// </summary>
        public bool TryDensestOpponentCluster(CasterRef caster, int densityRadius, out int2 cell, out int count)
        {
            cell = default; count = 0;
            if (_map == null) return false;
            var wanted = FactionRelation.OpponentUnitsOf(caster.Faction);
            if (wanted == Faction.None) return false;
            // 옛 풀과 같게 — 그 진영의 전투 개체 전원(사망 표시만 된 시체도 소멸 전까진 풀에 있었다).
            var units = _world.Units;
            int n = 0;
            for (int i = 0; i < units.Count; i++)
                if (IsCombatant(units[i]) && ((int)units[i].Faction & (int)wanted) != 0) n++;
            if (n == 0) return false;
            var cells = new int2[n];
            n = 0;
            for (int i = 0; i < units.Count; i++)
                if (IsCombatant(units[i]) && ((int)units[i].Faction & (int)wanted) != 0)
                    cells[n++] = _map.CellOf(units[i].Position);
            return LandingMath.TryDensestCell(cells, densityRadius, _map.GridSize, out cell, out count);
        }

        public bool TryLandingCellNear(int2 desired, int maxRing, out int2 cell)
        {
            cell = default;
            if (_map == null) return false;
            var slot = _map.Flow.GoalSlot(TraversalSlots.DefaultMask);
            if (!slot.Exists) return false;
            return LandingMath.TryLandingCell(desired, slot, _map.GridSize, math.max(0, maxRing), out cell);
        }

        // ── 발사 명세 ────────────────────────────────────────────────────────

        /// <summary>
        /// `patternIndex` = 정의표의 패턴 줄. 스킬 경로의 탄 템플릿은 방향을 미리 싣지 않으므로
        /// 「방향 바인딩이면 조준 필요」가 곧 옛 `NeedsAim` 이다.
        /// </summary>
        public PatternAimNeed AimNeedOfPattern(SkillEntityId host, int patternIndex)
        {
            var def = _applier.Definition;
            if (U(host) == null || patternIndex < 0 || patternIndex >= def.Patterns.Length) return PatternAimNeed.Missing;
            int barrel = def.Patterns[patternIndex].BarrelProjectileDefIndex;
            if (barrel < 0 || barrel >= def.Projectiles.Length) return PatternAimNeed.Missing;
            var mv = (Combat.Projectile.MovementKind)def.Projectiles[barrel].Movement;
            return Combat.Projectile.MovementBinding.Of(mv) == Combat.Projectile.BindingClass.Direction
                ? PatternAimNeed.NeedsAim : PatternAimNeed.Preaimed;
        }

        // ── 의도 ─────────────────────────────────────────────────────────────

        public void Emit(in SimIntent intent) => _applier.Apply(in intent);
        public void Emit(in MetaIntent intent) => _applier.Apply(in intent);

        private void Warn(string msg) => Report?.Invoke(msg);
    }

    /// <summary>도약 착지의 순수 계산(← 옛 `DefenderDensity` · `BlinkMath` — 순수 함수라 EditMode 가 고정한다).</summary>
    public static class LandingMath
    {
        /// <summary>
        /// 각 후보 칸의 체비셰프 `radius` 안 개체 수가 최다인 칸. 동률은 row-major 키(y·w + x) 오름차순 —
        /// 순회 순서에 기대지 않는다(결정론). radius ≤ 0 = 자기 칸만.
        /// </summary>
        public static bool TryDensestCell(int2[] cells, int radius, int2 gridSize, out int2 densest, out int count)
        {
            densest = default; count = 0;
            if (cells == null || cells.Length == 0) return false;
            int r = math.max(0, radius);
            int best = -1;
            long bestKey = long.MaxValue;
            for (int i = 0; i < cells.Length; i++)
            {
                var c = cells[i];
                int n = 0;
                for (int j = 0; j < cells.Length; j++)
                    if (math.max(math.abs(cells[j].x - c.x), math.abs(cells[j].y - c.y)) <= r) n++;
                long key = (long)c.y * math.max(1, gridSize.x) + c.x;
                if (n > best || (n == best && key < bestKey)) { best = n; bestKey = key; densest = c; }
            }
            count = best;
            return true;
        }

        /// <summary>
        /// `desired` 에 가장 가까운, 흐름장이 닿는(걸을 수 있고 이어진) 칸. 체비셰프 고리 r = 0..max, 고리 안은
        /// row-major — 완전 결정론. 상한 안에 없으면 false(호출부가 도약을 건너뛴다).
        /// </summary>
        public static bool TryLandingCell(int2 desired, FlowSlot dist, int2 gridSize, int maxRing, out int2 landing)
        {
            for (int r = 0; r <= maxRing; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        var c = new int2(desired.x + dx, desired.y + dy);
                        if (c.x < 0 || c.y < 0 || c.x >= gridSize.x || c.y >= gridSize.y) continue;
                        if (dist.DistAt(c) == int.MaxValue) continue;
                        landing = c;
                        return true;
                    }
            landing = default;
            return false;
        }
    }
}
