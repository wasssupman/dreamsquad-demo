using System.Collections.Generic;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — 캡처 #38(P11). 구 `ProjectileEmitterSystem` 이식.
    ///
    /// 활성 발사 인스턴스를 tick 하고, 로직이 산출한 <see cref="ShotOrder"/> 를 기존 스폰 요청
    /// 캐리어로 번역한다.
    ///
    /// ⚠ **투사체 수명을 신설하지 않는다** — 캐리어 → 소비 → `ProjectileState` → 이동/착탄 →
    /// 파괴라는 기존 경로를 그대로 탄다. 이 시스템은 "언제 몇 발이 어디로" 만 정한다.
    ///
    /// ⚠ **분기 축은 바인딩 클래스**지 개별 궤적이 아니다(<see cref="MovementBinding"/>).
    ///
    /// ⚠ 트리거가 인스턴스를 push 한 **그 프레임에 첫 발**이 나가야 하므로 P11 은 공격(#33 P8)
    /// 뒤다.
    /// </summary>
    public sealed class ProjectileEmitterSystem
    {
        private readonly SimCommandBuffer _ecb = new SimCommandBuffer();

        // 진영별 lazy 후보 풀 — 인스턴스가 없거나 이 프레임에 발사가 없으면 아예 만들지 않는다.
        private readonly List<SimEntityId> _defEntities = new List<SimEntityId>();
        private readonly List<SimInt2> _defCells = new List<SimInt2>();
        private readonly List<SimEntityId> _enemyEntities = new List<SimEntityId>();
        private readonly List<SimInt2> _enemyCells = new List<SimInt2>();

        public void Run(SimWorld world)
        {
            if (!SimSingleton.TryGet<FlowFieldSingleton>(world, out var ff)) return;

            float dt = world.DeltaTime;
            bool defBuilt = false, enemyBuilt = false;

            foreach (var entity in world.WithBuffer<EmitterInstance>())
            {
                if (world.Has<DeadTag>(entity)) continue;
                var instances = world.GetBuffer<EmitterInstance>(entity);
                if (instances.Count == 0) continue;
                // host 위치가 발사 원점이다. 없으면(소멸 중) 이 프레임은 건너뛴다.
                if (!world.TryGet<SimTransform>(entity, out var hostTransform)) continue;
                SimVec3 hostPos = hostTransform.Position;

                // ⚠ 진영은 **host 에서 도출**한다 — 패턴 저작에 진영 필드를 두지 않는다.
                bool hostIsEnemy = world.Has<AttackUnitTag>(entity);
                bool hostIsDefender = !hostIsEnemy && world.Has<DefenderUnitTag>(entity);
                if (!hostIsEnemy && !hostIsDefender) continue; // 진영 불명 host = no-op

                for (int i = instances.Count - 1; i >= 0; i--)
                {
                    var inst = instances[i];
                    var binding = MovementBinding.Of(inst.template.movement);
                    int shots = EmitterTick.Advance(ref inst.runtime, dt, inst.spec);

                    if (shots > 0 && binding != BindingClass.Direction)
                    {
                        if (hostIsEnemy && !defBuilt)
                        {
                            BuildDefenderPool(world, ff);
                            defBuilt = true;
                        }
                        else if (hostIsDefender && !enemyBuilt)
                        {
                            BuildEnemyPool(world, ff);
                            enemyBuilt = true;
                        }
                    }

                    // ⚠ **`Advance` 가 돌려준 발수를 전부 소비해야 한다.** 한 발만 만들면
                    //   `burstRemaining` 은 N 만큼 깎였는데 캐리어는 하나뿐이라 나머지가 증발하고,
                    //   중간에 빠져나가면 write-back·완주 판정까지 건너뛰어 인스턴스가 영구 적재된다.
                    for (int s = 0; s < shots; s++)
                    {
                        var req = inst.template;
                        ShotOrder order;

                        if (binding == BindingClass.Direction)
                        {
                            // 무타겟 정상 경로 — 원점·기준방향·최대거리는 트리거가 template 에
                            // 스냅샷했고 emitter 는 개별 각도만 정한다.
                            order = PatternLogic.BuildOrder(inst.spec, ref inst.runtime, -1);
                            req.direction = PatternDirection.Resolve(
                                inst.template.direction, inst.spec.minAngleDeg, inst.spec.maxAngleDeg, order.directionT);
                        }
                        else
                        {
                            var poolEntities = hostIsEnemy ? _defEntities : _enemyEntities;
                            var poolCells = hostIsEnemy ? _defCells : _enemyCells;

                            int idx = PatternTargeting.Select(poolCells, inst.spec.selection, inst.runtime.fireCount, ff.gridSize);
                            order = PatternLogic.BuildOrder(inst.spec, ref inst.runtime, idx);

                            // 잠금 해석 — ⚠ **index 를 재사용하지 않는다**(후보 스냅샷은 프레임-로컬).
                            var target = SimEntityId.Null;
                            int cellIdx = order.targetCandidateIndex;
                            if (!inst.spec.reselectPerShot && !inst.lockedTarget.IsNull)
                            {
                                target = inst.lockedTarget;
                                cellIdx = IndexOf(poolEntities, target);
                                // 잠근 대상이 버스트 도중 사라졌다 → 남은 발을 조용히 소모한다.
                                if (cellIdx < 0) continue;
                            }
                            else if (cellIdx >= 0)
                            {
                                target = poolEntities[cellIdx];
                                if (!inst.spec.reselectPerShot) inst.lockedTarget = target;
                            }

                            if (cellIdx < 0) continue; // 후보 0 = 발사 소모(위상은 이미 전진했다)
                            req.origin = hostPos;

                            switch (binding)
                            {
                                case BindingClass.Entity:
                                    req.target = target;
                                    // 비-베지어 궤적은 이 필드를 읽지 않아 무해하다. 제어점 산출은
                                    // 소비 지점의 몫이다 — emitter 는 저작 파라미터를 모른다.
                                    req.swingIndex = order.shotIndex;
                                    break;

                                case BindingClass.Cell:
                                    req.impact = GridMath.CellToWorldCenter(poolCells[cellIdx], ff.tileSize, 0f, ff.origin);
                                    req.flightTime = order.telegraphSec;
                                    break;

                                default:
                                    continue;
                            }
                        }

                        // ⚠ **명령이 결정한 값을 그대로 쓴다.** 오늘은 template 의 `dataIndex` 와
                        //   같지만(같은 barrel index 를 양쪽에 넣는다), 거기 기대면 로직의 결정이
                        //   저작 불변식에 묶인다 — order 가 source 다.
                        req.damage = order.damage;
                        req.dataIndex = order.barrelDataIndex;

                        _ecb.Defer(w =>
                        {
                            var carrier = w.Create();
                            w.Set(carrier, req);
                            w.Set(carrier, new ProjectileRequestCarrier());
                        });
                    }

                    if (EmitterTick.IsComplete(inst.runtime)) RemoveAtSwapBack(instances, i);
                    else instances[i] = inst;
                }
            }

            _ecb.Playback(world);
        }

        /// 융단폭격 대상 풀 — 살아 있는 방어유닛.
        private void BuildDefenderPool(SimWorld world, FlowFieldSingleton ff)
        {
            _defEntities.Clear();
            _defCells.Clear();
            foreach (var e in world.With<DefenderUnitTag>())
            {
                if (!world.TryGet<SimTransform>(e, out var xf)) continue;
                _defEntities.Add(e);
                _defCells.Add(GridMath.WorldToCell(xf.Position, ff.tileSize, ff.gridSize, ff.origin));
            }
        }

        /// <summary>
        /// 적 대상 풀 — 재조준 풀과 같은 관례로 죽은·유출된 적을 뺀다.
        /// ⚠ **판 밖(궁극기 이탈) 적도 뺀다.** 빠뜨리면 패턴 유닛이 화면 밖 보스를 골라 빈 타일에
        /// 쏜다 — 피해는 버퍼 드랍이 막지만 "사라졌다" 는 읽힘이 깨진다(구 sim 실측 사고).
        /// </summary>
        private void BuildEnemyPool(SimWorld world, FlowFieldSingleton ff)
        {
            _enemyEntities.Clear();
            _enemyCells.Clear();
            foreach (var e in world.With<AttackUnitTag>())
            {
                if (world.Has<DeadTag>(e)) continue;
                if (world.Has<PastGoalTag>(e)) continue;
                if (world.Has<UltimateLeapState>(e)) continue;
                if (!world.TryGet<SimTransform>(e, out var xf)) continue;
                _enemyEntities.Add(e);
                _enemyCells.Add(GridMath.WorldToCell(xf.Position, ff.tileSize, ff.gridSize, ff.origin));
            }
        }

        /// 잠근 대상의 이번 프레임 후보 index. 없으면 -1(소멸/유출).
        private static int IndexOf(List<SimEntityId> pool, SimEntityId target)
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] == target) return i;
            return -1;
        }

        private static void RemoveAtSwapBack<T>(List<T> list, int index)
        {
            list[index] = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — 투사체 클러스터.
    ///
    /// 셋이 **두 phase 에 갈린다**(P6 ×2 · P11). 그 갈림이 계약이다:
    /// <list type="bullet">
    /// <item><b>#26 → #27 이 P6 에 붙어 있다</b> — 이동이 세운 도착 플래그를 착탄이 **같은 틱**에
    ///       읽어야 하고, 착탄이 넣은 피해는 #34(P9)가 **같은 틱**에 소비한다.</item>
    /// <item><b>#38 은 P11 로 떨어져 있다</b> — 발사는 공격(#33 P8)이 인스턴스를 push 한 뒤여야
    ///       그 프레임에 첫 발이 나간다. 앞으로 당기면 한 틱씩 밀린다.</item>
    /// </list>
    ///
    /// ⚠ **#38 이 만드는 것은 투사체가 아니라 요청 캐리어**다. 실제 투사체는 그 요청을 소비하는
    /// 지점(18-K)이 만든다 — 그래서 이 클러스터 안에 스폰이 없다.
    /// </summary>
    public sealed class ProjectileCluster
    {
        public ProjectileMoveSystem Move { get; }
        public ProjectileHitSystem Hit { get; }
        public ProjectileEmitterSystem Emitter { get; }

        public ProjectileCluster(SimChannels channels)
        {
            Move = new ProjectileMoveSystem();
            Hit = new ProjectileHitSystem(channels);
            Emitter = new ProjectileEmitterSystem();
        }

        /// ⚠ **#26 → #27 순서가 계약이다** — 뒤집히면 착탄이 이번 틱의 도착을 못 보고 한 틱 늦는다.
        public IEnumerable<SimStep> Steps()
        {
            yield return new SimStep(26, SimPhase.Projectiles, nameof(ProjectileMoveSystem), Move.Run);
            yield return new SimStep(27, SimPhase.Projectiles, nameof(ProjectileHitSystem), Hit.Run);
            yield return new SimStep(38, SimPhase.PostProcess, nameof(ProjectileEmitterSystem), Emitter.Run);
        }
    }
}
