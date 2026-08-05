using System.Collections.Generic;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-F/3 — 캡처 **#13** · <see cref="SimPhase.PreCombat"/>(P3).
    /// 구 `TauntAttackGrantSystem` 이식.
    ///
    /// **맥락 경계를 지키는 장치다**: 어그로 배정(#8, Effects)은 `Aggroed` 만 쓰고, 그것을 읽어
    /// **Combat 컴포넌트**(`AttackState`·`AttackOutputElement`)를 구조 변경하는 것은 여기다.
    /// #8 뒤·#33 앞이라 부여된 공격이 **같은 프레임에 발사**된다.
    ///
    /// ⚠ 게이트가 **OR** 다(`Aggroed` 또는 `TauntAttackGranted`) — strip 패스가 살아 있어야
    /// 해제된 적의 도발 공격이 회수된다. AND 면 어그로가 0 이 되는 순간 회수가 멈춘다.
    /// </summary>
    public sealed class TauntAttackGrantSystem
    {
        private readonly List<SimEntityId> _toGrant = new List<SimEntityId>();
        private readonly List<SimEntityId> _toStrip = new List<SimEntityId>();

        public void Run(SimWorld world)
        {
            bool any = false;
            foreach (SimEntityId _ in world.With<Aggroed>()) { any = true; break; }
            if (!any) foreach (SimEntityId _ in world.With<TauntAttackGranted>()) { any = true; break; }
            if (!any) return;

            _toGrant.Clear();
            _toStrip.Clear();

            // Grant — 방금 어그로됐고 자기 공격이 없으며 도발 프로파일이 있는 적.
            foreach (SimEntityId e in world.With<AggroAttackProfile>())
            {
                if (!world.Has<Aggroed>(e)) continue;
                if (world.Has<AttackState>(e)) continue;
                if (world.Has<TauntAttackGranted>(e)) continue;
                _toGrant.Add(e);
            }

            // Strip — 부여받았는데 더 이상 어그로가 아닌 적(해제 → 원래 거동 복귀).
            foreach (SimEntityId e in world.With<TauntAttackGranted>())
            {
                if (!world.Has<AttackState>(e)) continue;
                if (world.Has<Aggroed>(e)) continue;
                _toStrip.Add(e);
            }

            for (int i = 0; i < _toGrant.Count; i++)
            {
                SimEntityId e = _toGrant[i];
                AggroAttackProfile p = world.Get<AggroAttackProfile>(e);
                world.Set(e, new AttackState
                {
                    range = p.range,
                    cooldownDuration = p.cooldown,
                    cooldownRemaining = 0f,
                    attackTargetCount = 1,
                    targetMask = (int)Faction.Defender,
                });
                List<AttackOutputElement> outputs = world.AddBuffer<AttackOutputElement>(e);
                outputs.Clear();
                outputs.Add(new AttackOutputElement
                {
                    value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = p.damage },
                });
                world.Set(e, default(TauntAttackGranted));
            }

            for (int i = 0; i < _toStrip.Count; i++)
            {
                SimEntityId e = _toStrip[i];
                world.RemoveComponent<AttackState>(e);
                // 버퍼는 **없앤다**(비우는 게 아니다) — 소비자가 보유로 분기한다.
                world.RemoveBuffer<AttackOutputElement>(e);
                world.RemoveComponent<TauntAttackGranted>(e);
            }
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-F/3 — 캡처 **#14** · <see cref="SimPhase.PreCombat"/>(P3).
    /// 구 `EnemyAiStateSystem` 이식. `EnemyAiState` 의 **유일한 writer**.
    ///
    /// #13 뒤(부여된 `AttackState.range` 를 같은 프레임에 본다)·#17 앞(상태가 이동을 결정한다).
    /// </summary>
    public sealed class EnemyAiStateSystem
    {
        private SimEntityId[] _cand = new SimEntityId[32];
        private int _candCount;

        /// <summary>
        /// 순수 전이 함수. **aggro 가 우선**이고, 비-aggro 는 "공격 루프가 fire 할 타겟이 있는가"
        /// 로 `Engaging`/`Marching` 을 가른다.
        /// </summary>
        public static AiState Evaluate(bool aggroed, bool guardianInRange, bool hasFireTarget)
        {
            if (aggroed) return guardianInRange ? AiState.Standoff : AiState.Chasing;
            return hasFireTarget ? AiState.Engaging : AiState.Marching;
        }

        public void Run(SimWorld world)
        {
            bool anyAi = false;
            foreach (SimEntityId _ in world.With<EnemyAiState>()) { anyAi = true; break; }
            if (!anyAi) return;

            bool hasField = SimSingleton.TryGet(world, out FlowFieldSingleton field);
            float tileSize = hasField ? field.tileSize : 1f;
            SimInt2 gridSize = hasField ? field.gridSize : new SimInt2(128, 128);
            SimVec3 origin = hasField ? field.origin : SimVec3.Zero;

            SnapshotCandidates(world);

            foreach (SimEntityId enemy in world.With<EnemyAiState>())
            {
                if (!world.TryGet(enemy, out SimTransform t)) continue;
                SimInt2 atkCell = GridMath.WorldToCell(t.Position, tileSize, gridSize, origin: origin);

                bool hasAttack = world.TryGet(enemy, out AttackState atk);
                int tileRange = hasAttack ? GridMath.RangeToTiles(atk.range) : 0;
                int mask = hasAttack ? atk.targetMask : 0;

                bool aggroed = world.Has<Aggroed>(enemy);
                bool guardianInRange = false;
                bool hasFireTarget = false;

                if (aggroed)
                {
                    // ⚠ 가디언 사거리 판정에 `AttackState.range` 가 필요하다 — 없으면
                    // 영원히 `Chasing` 이다(#8 의 `NoAttack` 거부가 그 원천을 막는다).
                    if (hasAttack)
                    {
                        SimEntityId g = world.Get<Aggroed>(enemy).guardian;
                        if (!g.IsNull && world.TryGet(g, out SimTransform gt))
                        {
                            SimInt2 gCell = GridMath.WorldToCell(gt.Position, tileSize, gridSize, origin: origin);
                            guardianInRange = GridMath.ChebyshevDistance(gCell, atkCell) <= tileRange;
                        }
                    }
                }
                else if (hasAttack)
                {
                    hasFireTarget = HasFireTarget(world, enemy, atkCell, tileRange, mask,
                                                  tileSize, gridSize, origin);
                }

                world.Set(enemy, new EnemyAiState
                {
                    value = Evaluate(aggroed, guardianInRange, hasFireTarget),
                });
            }
        }

        /// 타겟 후보 스냅샷 — **공격 루프(#33)와 같은 후보 풀**이어야 한다.
        private void SnapshotCandidates(SimWorld world)
        {
            _candCount = 0;
            foreach (SimEntityId e in world.With<FactionTag>())
            {
                if (!world.Has<Health>(e)) continue;
                if (!world.Has<SimTransform>(e)) continue;
                if (world.Has<PendingDeployment>(e) || world.Has<DeadTag>(e)) continue;
                if (_candCount == _cand.Length)
                {
                    var bigger = new SimEntityId[_cand.Length * 2];
                    System.Array.Copy(_cand, bigger, _cand.Length);
                    _cand = bigger;
                }
                _cand[_candCount++] = e;
            }
        }

        /// <summary>
        /// ⚠ **공격 루프의 fire 조건 미러다.** 타겟 선정 로직이 바뀌면 여기도 같이 바꿔야 한다 —
        /// 어긋나면 "Engaging 인데 안 쏘는" 또는 "Marching 인데 쏘는" 상태가 생긴다.
        ///
        /// `FocusUntilDead` 락이 걸린 적은 **락 타겟이 사거리 안일 때만** fire 한다 —
        /// 그때만 `Engaging` 이어야 데드락이 안 생긴다.
        /// </summary>
        private bool HasFireTarget(SimWorld world, SimEntityId attacker, SimInt2 atkCell,
                                   int tileRange, int mask,
                                   float tileSize, SimInt2 gridSize, SimVec3 origin)
        {
            if (world.TryGet(attacker, out EnemyBehavior behavior)
                && behavior.targetMode == EnemyTargetMode.FocusUntilDead
                && world.TryGet(attacker, out FocusTarget focus))
            {
                SimEntityId cur = focus.current;
                bool curValid = !cur.IsNull
                    && world.TryGet(cur, out Health ch) && ch.value > 0f
                    && !world.Has<DeadTag>(cur);
                if (curValid)
                {
                    if (!world.TryGet(cur, out SimTransform ct)) return false;
                    SimInt2 cCell = GridMath.WorldToCell(ct.Position, tileSize, gridSize, origin: origin);
                    return GridMath.ChebyshevDistance(cCell, atkCell) <= tileRange;
                }
                // 락이 무효 → 아래 nearest/filter 경로로 진행
            }

            bool hasFilter = world.TryGet(attacker, out EnemyTargetFilter filter);
            int filterMask = hasFilter ? filter.classMask : -1;

            for (int i = 0; i < _candCount; i++)
            {
                SimEntityId c = _cand[i];
                if (c == attacker) continue;
                if (((int)world.Get<FactionTag>(c).value & mask) == 0) continue;

                // 태그가 없으면 마스크를 **우회**한다(해저드 등).
                int cclass = world.TryGet(c, out DefenderClassTag tag) ? (int)tag.value : -1;
                if (hasFilter && cclass >= 0 && (filterMask & (1 << cclass)) == 0) continue;

                SimInt2 tgtCell = GridMath.WorldToCell(world.Get<SimTransform>(c).Position,
                                                       tileSize, gridSize, origin: origin);
                if (GridMath.ChebyshevDistance(tgtCell, atkCell) <= tileRange) return true;
            }
            return false;
        }
    }
}
