using System.Collections.Generic;
using Wassup.Sim.Combat;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-E/5 — 캡처 **#5** · <see cref="SimPhase.FieldsAndPeriodic"/>(P1).
    /// 구 `ZoneApplySystem` 이식. **#2 가 구운 인덱스의 소비자**다.
    ///
    /// 장판 안에 있는 적에게 **매 프레임 재발행**한다 — 나가면 재발행이 끊겨 자연 소멸한다.
    /// P1 이라 소비자(#9 모디파이어 P2 · #10 CC P2 · #15 DoT P3)보다 모두 앞 = 같은 프레임 적용.
    ///
    /// ⚠ **진영을 명시로 본다.** 이전엔 `PathFollowState` 보유만으로 걸었는데 그건 "이동체 = 적"
    /// 이라는 암묵 전제였고, 거점 수비 아군이 그 전제를 깬다(아군이 아군 장판에 오폭당한다).
    /// 존의 대상 진영은 오늘 적 하나뿐이라 `HazardEffect` 에 진영 축을 열지 않는다(제약 8).
    ///
    /// ⚠ **효과 순회 순서는 <see cref="HazardCellIndex"/> 가 정한다**(역-삽입순). 그 순서가 곧
    /// 세 채널의 적재 순서이고 병합 결과에 먹힌다 — tie-break ⑥.
    /// </summary>
    public sealed class ZoneApplySystem
    {
        private readonly SimChannel<EnemyCcEvent> _ccChannel;
        private readonly SimChannel<DotApplyEvent> _dotChannel;
        private readonly SimChannel<StatModifierApplyEvent> _statChannel;
        private readonly SimChannel<HazardRuntimeEvent> _runtimeLog;

        public ZoneApplySystem(SimChannel<EnemyCcEvent> cc, SimChannel<DotApplyEvent> dot,
                               SimChannel<StatModifierApplyEvent> stat,
                               SimChannel<HazardRuntimeEvent> runtimeLog)
        {
            _ccChannel = cc; _dotChannel = dot; _statChannel = stat; _runtimeLog = runtimeLog;
        }

        public void Run(SimWorld world)
        {
            if (!SimSingleton.TryGet(world, out HazardSingleton hazards)) return;      // 분류 C
            if (!hazards.IsCreated || hazards.cellToEffects.Count == 0) return;
            if (!SimSingleton.TryGet(world, out FlowFieldSingleton field)) return;     // 분류 C
            if (!field.IsCreated) return;

            foreach (SimEntityId e in world.With<FactionTag>())
            {
                if (!world.Has<PathFollowState>(e)) continue;
                if (!world.TryGet(e, out SimTransform t)) continue;
                if (((int)world.Get<FactionTag>(e).value & (int)Faction.Enemy) == 0) continue;

                SimInt2 cell = GridMath.WorldToCell(t.Position, field.tileSize, field.gridSize,
                                                    origin: field.origin);
                int n = hazards.cellToEffects.CountFor(cell);
                for (int i = 0; i < n; i++)
                {
                    HazardEffect effect = hazards.cellToEffects.Get(cell, i);

                    if (effect.kind == CcKind.Slow)
                    {
                        // `CcKind.Slow` 는 저작 토큰으로만 남는다 — 실체는 스탯 감속이다.
                        _statChannel.Enqueue(new StatModifierApplyEvent
                        {
                            target = e,
                            stat = StatKind.MoveSpeedMul,
                            op = CombineOp.Multiplicative,
                            magnitude = effect.param1,
                            duration = effect.restDuration,
                            source = SimEntityId.Null,
                            stackId = 0,
                            origin = ModifierOrigin.Zone,
                        });
                    }
                    else if (effect.kind == CcKind.DoT)
                    {
                        // 지속 피해는 전용 파이프라인으로 — `CcKind.DoT` 도 저작 토큰이다.
                        _dotChannel.Enqueue(new DotApplyEvent { target = e, effect = ToDot(effect) });
                    }
                    else
                    {
                        _ccChannel.Enqueue(new EnemyCcEvent { target = e, effect = ToCc(effect) });
                    }

                    _runtimeLog.Enqueue(new HazardRuntimeEvent
                    {
                        eventType = HazardRuntimeEventType.ZoneApply,
                        kind = effect.kind,
                        cell = cell,
                        target = e,
                        scalar = effect.param1,
                    });
                }
            }
        }

        /// `tickTimer` 를 채우지 않는다 — `DotEffectMerge` 의 add-path 가 첫 tick 즉발용으로 초기화한다.
        private static DotEffect ToDot(in HazardEffect h) => new DotEffect
        {
            origin = DotOrigin.Zone,
            element = h.element,
            scalar = h.param1,
            remainingTime = h.restDuration,
            tickInterval = h.tickInterval,
        };

        /// `tickTimer` 미설정 — `CcEffectMerge` 의 add-path 가 초기화한다.
        private static CcEffect ToCc(in HazardEffect h) => new CcEffect
        {
            kind = h.kind,
            scalar = h.param1,
            vector = SimVec3.Zero,
            remainingTime = h.restDuration,
            tickInterval = h.tickInterval,
        };
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/5 — 캡처 **#16** · <see cref="SimPhase.PreCombat"/>(P3).
    /// 구 `PatrolFieldSystem` 이식. `PatrolStep` 의 **유일한 writer** 이고 #17 이 읽는다.
    ///
    /// ⚠ P3 이라 이동(#17 P4) **직전**이다 — 이번 틱에 구운 방향을 이번 틱에 쓴다.
    /// </summary>
    public sealed class PatrolFieldSystem
    {
        // 프레임당 1회 hoist 되는 버퍼들. 그리드 크기가 바뀌면 다시 잡는다.
        private byte[] _fullMask;
        private byte[] _areaMask;
        private SimVec2[] _scratchFlow;
        private int[] _scratchDist;
        private SimInt2[] _enemyCells = new SimInt2[32];
        private SimInt2[] _sourceArray = new SimInt2[64];
        private readonly List<SimInt2> _inArea = new List<SimInt2>();
        private readonly List<SimInt2> _sources = new List<SimInt2>();

        public void Run(SimWorld world)
        {
            if (!SimSingleton.TryGet(world, out FlowFieldSingleton field)) return;   // 분류 C
            if (!field.IsCreated) return;

            SimInt2 grid = field.gridSize;
            int n = grid.x * grid.y;
            EnsureGridBuffers(n);

            bool hasObstacles = SimSingleton.TryGet(world, out ObstacleSingleton obstacles)
                                && obstacles.IsCreated;

            // 적 셀 스냅샷. `PastGoalTag`(유출 대기)는 제외 — 쫓아갈 이유가 없다.
            int enemyCount = 0;
            foreach (SimEntityId e in world.With<AttackUnitTag>())
            {
                if (world.Has<DeadTag>(e) || world.Has<PastGoalTag>(e)) continue;
                if (!world.TryGet(e, out SimTransform t)) continue;
                if (enemyCount == _enemyCells.Length) Grow(ref _enemyCells);
                _enemyCells[enemyCount++] = GridMath.WorldToCell(
                    t.Position, field.tileSize, grid, origin: field.origin);
            }

            // 구역 무시 walk 마스크 — **프레임당 1회.** 외력으로 구역 밖에 밀려난 순찰병의
            // 복귀 경로에만 쓰인다. 벽 술어는 `MovementCellTrim` 이 단독 소유한다.
            MovementCellTrim.FillWalkMask(in field, hasObstacles, in obstacles, _fullMask);

            foreach (SimEntityId e in world.With<PatrolAnchor>())
            {
                if (world.Has<DeadTag>(e)) continue;
                if (!world.Has<PatrolStep>(e)) continue;
                if (!world.TryGet(e, out SimTransform t)) continue;

                PatrolAnchor anchor = world.Get<PatrolAnchor>(e);
                SimInt2 selfCell = GridMath.WorldToCell(t.Position, field.tileSize, grid,
                                                        origin: field.origin);

                // `FillAreaMask` 가 **스스로 0 으로 지운다** — 그래서 이 버퍼를 재사용해도
                // 앞 엔티티의 구역이 뒤 엔티티에 새지 않는다(그 함수 주석의 근거).
                PatrolAreaMath.FillAreaMask(_fullMask, grid, anchor.cell, anchor.tileRadius, _areaMask);

                int attackTiles = world.TryGet(e, out AttackState atk)
                    ? GridMath.RangeToTiles(atk.range)
                    : 1;

                world.Set(e, new PatrolStep
                {
                    dir = PatrolAreaMath.StepDir(
                        _areaMask, _fullMask, grid,
                        anchor.cell, anchor.tileRadius,
                        selfCell, attackTiles,
                        _enemyCells, enemyCount,
                        _scratchFlow, _scratchDist,
                        _inArea, _sources, ref _sourceArray),
                });
            }
        }

        private void EnsureGridBuffers(int n)
        {
            if (_fullMask != null && _fullMask.Length == n) return;
            _fullMask = new byte[n];
            _areaMask = new byte[n];
            _scratchFlow = new SimVec2[n];
            _scratchDist = new int[n];
        }

        private static void Grow(ref SimInt2[] a)
        {
            var bigger = new SimInt2[a.Length * 2];
            System.Array.Copy(a, bigger, a.Length);
            a = bigger;
        }
    }
}
