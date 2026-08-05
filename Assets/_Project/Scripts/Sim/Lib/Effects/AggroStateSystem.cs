using System.Collections.Generic;
using Wassup.Sim.Combat;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-F/2 — 캡처 **#8** · <see cref="SimPhase.Intake"/>(P2).
    /// 구 `AggroStateSystem` 이식. **P2 의 첫 스텝**(#9 모디파이어·#10 CC 보다 앞).
    ///
    /// 어그로 상태의 **단일 권한**이다 — `Aggroed`·`AggroCapacity`·`AggroChaseCell` 을 이 시스템만
    /// 쓰고 이동·공격은 읽기만 한다. 근접 즉시 배정이 아니라 **히트 구동**이다.
    ///
    /// ⚠ 게이트가 `RequireAnyForUpdate`(**OR**)다 — provider(`AggroCapacity`) **또는** `Aggroed`
    /// 가 하나라도 있으면 돈다. AND 로 오번역하면 마지막 가디언이 죽은 뒤 orphan 해제 패스가
    /// 죽어 **적이 영원히 어그로된 채로 남는다**(계획서가 경고한 `RequireAnyForUpdate` 4건 중 하나).
    ///
    /// ⚠ **구조 변경을 전부 지연시킨다.** 구 sim 의 ECB 가 그랬고, 그 지연이 관측 가능하다:
    /// Pass 1 이 해제를 **예약만** 하므로 Pass 3 에서 그 적은 여전히 `Aggroed` 로 보인다 =
    /// **같은 틱에 재획득되지 않는다.** 즉시 제거로 바꾸면 해제된 적이 같은 틱에 다시 끌려간다.
    /// </summary>
    public sealed class AggroStateSystem
    {
        private readonly SimChannel<AggroHitEvent> _hitChannel;

        // 지연 구조 변경 예약.
        private readonly List<SimEntityId> _toRelease = new List<SimEntityId>();
        private readonly List<Attach> _toAttach = new List<Attach>();
        // 회계 · 선점.
        private readonly Dictionary<SimEntityId, int> _countByGuardian = new Dictionary<SimEntityId, int>();
        private readonly Dictionary<SimEntityId, int> _runningHeld = new Dictionary<SimEntityId, int>();
        private readonly HashSet<SimEntityId> _claimed = new HashSet<SimEntityId>();
        // 기하 게이트용 — 첫 필요 시 1회 lazy 할당(구 sim 과 같은 배치).
        private byte[] _walkMask;
        private SimVec2[] _tmpFlow;
        private int[] _tmpDist;
        private readonly List<SimInt2> _sources = new List<SimInt2>();
        private SimInt2[] _sourceArray = new SimInt2[64];

        private readonly struct Attach
        {
            public readonly SimEntityId Enemy;
            public readonly SimEntityId Guardian;
            /// null = 기하 게이트를 건너뛴 경우(flow field 부재) — 버퍼를 붙이지 않는다.
            public readonly int[] ChaseDist;
            public Attach(SimEntityId enemy, SimEntityId guardian, int[] chaseDist)
            { Enemy = enemy; Guardian = guardian; ChaseDist = chaseDist; }
        }

        public AggroStateSystem(SimChannel<AggroHitEvent> hitChannel) => _hitChannel = hitChannel;

        public void Run(SimWorld world)
        {
            if (!HasAnyProviderOrAggroed(world)) return;   // RequireAnyForUpdate (OR)

            _toRelease.Clear();
            _toAttach.Clear();
            _countByGuardian.Clear();

            // ── Pass 1: 링크 가디언 사망/소멸 시 해제 + 가디언별 카운트 ──────────
            foreach (SimEntityId enemy in world.With<Aggroed>())
            {
                SimEntityId g = world.Get<Aggroed>(enemy).guardian;
                // 사망 3중 판정 — 파괴분 + 죽음-프레임 DeadTag + HP<=0. 셋 중 하나만 봐도
                // 새는 프레임이 있다(파괴 전 1틱 창이 존재하므로).
                bool guardianAlive = !g.IsNull
                    && world.Exists(g)
                    && !world.Has<DeadTag>(g)
                    && world.TryGet(g, out Health gh) && gh.value > 0f;

                if (AggroPolicy.ShouldRelease(guardianAlive))
                {
                    _toRelease.Add(enemy);
                    continue;
                }
                if (world.Has<DeadTag>(enemy)) continue;   // 죽는 중인 적은 회계에서 뺀다

                _countByGuardian.TryGetValue(g, out int c);
                _countByGuardian[g] = c + 1;
            }

            // ── Pass 2: 가디언별 held **full recompute**(증분 아님 → 드리프트 없음) ──
            foreach (SimEntityId guardian in world.With<AggroCapacity>())
            {
                _countByGuardian.TryGetValue(guardian, out int held);
                AggroCapacity cap = world.Get<AggroCapacity>(guardian);
                cap.held = held;
                world.Set(guardian, cap);
            }

            // ── Pass 3: 히트 드레인 → 게이트 → 부착 예약 ───────────────────────
            DrainHits(world);

            // ── 지연 적용 ───────────────────────────────────────────────────────
            for (int i = 0; i < _toRelease.Count; i++)
            {
                world.RemoveComponent<Aggroed>(_toRelease[i]);
                // chase field 는 `Aggroed` 와 **수명 동기**다. 비우는 게 아니라 **없앤다** —
                // 소비자가 `HasBuffer` 로 분기하므로 빈 버퍼는 "전부 dist 0" 이라는 없는 상태다.
                world.RemoveBuffer<AggroChaseCell>(_toRelease[i]);
            }
            for (int i = 0; i < _toAttach.Count; i++)
            {
                Attach a = _toAttach[i];
                world.Set(a.Enemy, new Aggroed { guardian = a.Guardian });
                if (a.ChaseDist == null) continue;
                List<AggroChaseCell> chase = world.AddBuffer<AggroChaseCell>(a.Enemy);
                chase.Clear();
                for (int k = 0; k < a.ChaseDist.Length; k++)
                    chase.Add(new AggroChaseCell { dist = a.ChaseDist[k] });
            }
        }

        private void DrainHits(SimWorld world)
        {
            List<AggroHitEvent> hits = _hitChannel.Drain();
            if (hits.Count == 0) return;

            _claimed.Clear();
            _runningHeld.Clear();
            foreach (var kv in _countByGuardian) _runningHeld[kv.Key] = kv.Value;

            bool hasFlow = SimSingleton.TryGet(world, out FlowFieldSingleton flowField) && flowField.IsCreated;
            bool hasObstacles = SimSingleton.TryGet(world, out ObstacleSingleton obstacles) && obstacles.IsCreated;
            bool maskBuilt = false;

            for (int i = 0; i < hits.Count; i++)
            {
                AggroHitEvent ev = hits[i];

                if (!world.Has<AggroCapacity>(ev.guardian)) continue;     // 비-가디언
                if (!world.Exists(ev.enemy)) continue;                    // 발행↔드레인 사이 파괴
                // 선점: 기존 어그로 + 이번 틱 부착분. Pass 1 의 해제가 **지연**이라
                // 해제 예약된 적도 여기선 아직 Aggroed 로 보인다(같은 틱 재획득 금지).
                if (_claimed.Contains(ev.enemy) || world.Has<Aggroed>(ev.enemy)) continue;
                if (world.Has<DeadTag>(ev.enemy)) continue;
                // 보스는 어그로 면역 — **부착 1곳에서 막는다.** 소비 지점이 6곳이라
                // "붙은 것을 무시" 는 훨씬 비싸다. held 는 full recompute 라 회계도 무변경.
                if (world.Has<Wassup.Sim.Combat.BossTag>(ev.enemy)) continue;

                // 전투수단 없는 적은 가디언을 때릴 수 없으므로 거부(Chasing 고착 원천 차단).
                bool hasAtk = world.TryGet(ev.enemy, out AttackState atk);
                bool hasProf = world.TryGet(ev.enemy, out AggroAttackProfile prof);
                int tileRange = AggroChaseMath.ResolveTileRange(
                    hasAtk, hasAtk ? atk.range : 0f, hasProf, hasProf ? prof.range : 0f);
                if (tileRange == AggroChaseMath.NoAttack) continue;

                _runningHeld.TryGetValue(ev.guardian, out int held);
                int cap = world.Get<AggroCapacity>(ev.guardian).max;
                if (!AggroPolicy.CanAcquire(held, cap, alreadyAggroed: false)) continue;

                // 기하 게이트. flow field 부재(합성 테스트 월드)면 **기하를 생략하고 부착만** 한다.
                int[] chaseSnapshot = null;
                if (hasFlow
                    && world.TryGet(ev.guardian, out SimTransform gt)
                    && world.TryGet(ev.enemy, out SimTransform et))
                {
                    if (!maskBuilt)
                    {
                        EnsureGridBuffers(flowField.gridSize.x * flowField.gridSize.y);
                        MovementCellTrim.FillWalkMask(in flowField, hasObstacles, in obstacles, _walkMask);
                        maskBuilt = true;
                    }
                    SimInt2 gCell = GridMath.WorldToCell(gt.Position, flowField.tileSize,
                                                         flowField.gridSize, origin: flowField.origin);
                    SimInt2 eCell = GridMath.WorldToCell(et.Position, flowField.tileSize,
                                                         flowField.gridSize, origin: flowField.origin);
                    int srcCount = AggroChaseMath.BuildChaseField(
                        _walkMask, flowField.gridSize, gCell, tileRange,
                        _tmpFlow, _tmpDist, _sources, ref _sourceArray);
                    if (srcCount == 0) continue;                                        // 목적지 후보 없음
                    if (_tmpDist[GridMath.CellIndex(eCell, flowField.gridSize)] == int.MaxValue)
                        continue;                                                       // 도달 불가 — 좀비 금지

                    // 스크래치는 다음 이벤트가 덮으므로 **복사**해서 예약에 싣는다.
                    chaseSnapshot = new int[_tmpDist.Length];
                    System.Array.Copy(_tmpDist, chaseSnapshot, _tmpDist.Length);
                }

                _toAttach.Add(new Attach(ev.enemy, ev.guardian, chaseSnapshot));
                _claimed.Add(ev.enemy);
                _runningHeld[ev.guardian] = held + 1;
            }
        }

        /// provider **또는** 어그로된 적이 하나라도 있으면 실행(구 `RequireAnyForUpdate`).
        private static bool HasAnyProviderOrAggroed(SimWorld world)
        {
            foreach (SimEntityId _ in world.With<AggroCapacity>()) return true;
            foreach (SimEntityId _ in world.With<Aggroed>()) return true;
            return false;
        }

        private void EnsureGridBuffers(int n)
        {
            if (_walkMask != null && _walkMask.Length == n) return;
            _walkMask = new byte[n];
            _tmpFlow = new SimVec2[n];
            _tmpDist = new int[n];
        }
    }
}
