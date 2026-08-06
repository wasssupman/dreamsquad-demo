using System.Collections.Generic;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-J/4 — 오라 펄스 대상 선정. 구 `AuraPulse` 이식.
    ///
    /// host 셀에서 Chebyshev `tileRange` 안의 모든 후보(경계 포함). 순수 수학이고
    /// **host 자기 제외는 여기서 하지 않는다** — 같은 셀의 아군은 맞아야 하므로 신원 판정은
    /// 호출부의 몫이다. 음수 반경은 아무것도 고르지 않는다(퇴화 가드).
    /// </summary>
    public static class AuraPulse
    {
        /// `results` 는 진입 시 비워진다 — 펄스 간 재사용해도 안전하다.
        public static void SelectTargets(List<SimInt2> candidateCells, SimInt2 hostCell,
                                         int tileRange, List<int> results)
        {
            results.Clear();
            if (tileRange < 0) return;
            for (int i = 0; i < candidateCells.Count; i++)
            {
                int dx = SimMath.Abs(candidateCells[i].x - hostCell.x);
                int dy = SimMath.Abs(candidateCells[i].y - hostCell.y);
                if (SimMath.Max(dx, dy) <= tileRange) results.Add(i);
            }
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/4 — 캡처 **#4** · <see cref="SimPhase.FieldsAndPeriodic"/>(P1).
    /// 구 `BossPeriodicTriggerSystem` 이식.
    ///
    /// ⚠ **이름과 달리 진영 중립이다** — 게이트는 `DcTriggerSlot` 버퍼 존재뿐이고, 방어유닛 카드
    /// 슬롯은 트리거 kind 디스패치에서 걸러진다(그쪽 `periodSeconds` 는 0 이라 가드에도 걸린다).
    ///
    /// ⚠ **P1 이라 `EnvironmentCluster` 의 phase 한가운데 끼어든다.** 그래서 이 시스템은
    /// `GimmickCluster` 가 신고하고 정렬은 `SimPipeline` 이 한다 — 클러스터에 직접 넣으면
    /// 경계가 무너진다.
    ///
    /// ⚠ **죽은 유닛은 새 발동을 시작하지 않는다.** `DeadTag` 부착과 파괴 사이에 이 시스템이
    /// 끼면 시체가 한 번 더 스킬을 쓴다. 시스템 순서로 가리는 대신 **규칙으로** 표현한다 —
    /// 이미 시작된 버스트는 emitter 가 완주시킨다(action-lock 의 "START 는 막고 RESOLVE 는
    /// 완료" 와 같은 결).
    ///
    /// ⚠ 기본 공격과 **직교**한다 — 여기서 `AttackState`/AI 상태/이동을 건드리지 않는다.
    /// </summary>
    public sealed class BossPeriodicTriggerSystem
    {
        private readonly SimChannels _channels;

        private readonly List<SimEntityId> _hosts = new List<SimEntityId>();
        private readonly List<SimInt2> _defCells = new List<SimInt2>();
        private readonly List<SimEntityId> _defEntities = new List<SimEntityId>();
        private readonly List<SimInt2> _enemyCells = new List<SimInt2>();
        private readonly List<SimEntityId> _enemyEntities = new List<SimEntityId>();
        private readonly List<int> _whipTargets = new List<int>();

        public BossPeriodicTriggerSystem(SimChannels channels) => _channels = channels;

        public void Run(SimWorld world)
        {
            if (!SimSingleton.TryGet<FlowFieldSingleton>(world, out var ff)) return;

            float dt = world.DeltaTime;
            bool enemyPoolBuilt = false, defPoolBuilt = false;

            _hosts.Clear();
            foreach (SimEntityId e in world.WithBuffer<DcTriggerSlot>()) _hosts.Add(e);

            for (int hi = 0; hi < _hosts.Count; hi++)
            {
                SimEntityId entity = _hosts[hi];
                if (world.Has<DeadTag>(entity)) continue;
                var slots = world.GetBuffer<DcTriggerSlot>(entity);
                if (slots == null) continue;

                for (int si = 0; si < slots.Count; si++)
                {
                    var slot = slots[si];
                    if (slot.trigger != DcTriggerKind.PeriodicTimer) continue;

                    float elapsed = slot.elapsed;
                    bool fired = DcTrigger.PeriodicTick(ref elapsed, dt, slot.periodSeconds);
                    slot.elapsed = elapsed;
                    slots[si] = slot;
                    if (!fired) continue;

                    if (slot.payload == DcPayloadKind.AllyMoveSpeedAura)
                    {
                        RunWhipPulse(world, entity, in slot, in ff, ref enemyPoolBuilt, ref defPoolBuilt);
                    }
                    else if (slot.payload == DcPayloadKind.EmitProjectilePattern)
                    {
                        PushPattern(world, entity, in slot);
                    }
                    else
                    {
                        // payload 가 arm 없이 착지했다 — 조용히 발동을 소모하지 않는다.
                        // (`AreaBarrage` arm 은 제거됐다 — 융단폭격은 발사 패턴으로 이관됐고
                        //  enum 값은 append-only 계약상 남아 bake 가 loud 거절한다.)
                        _channels.Warnings.Enqueue(new SimWarning
                        {
                            code = SimWarningCode.PeriodicUnhandledPayload,
                            entity = entity,
                            detail = (int)slot.payload,
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 채찍 오라 — host 셀에서 반경 안의 **같은 진영** 유닛에 이동속도 모디파이어(TTL)를 건다.
        ///
        /// ⚠ **해제는 TTL 만료뿐이다** — 사거리 이탈이나 host 사망으로 회수하지 않는다.
        /// ⚠ 퇴화 저작(배율 0 / TTL 없음)은 발동을 **조용히 소모**한다.
        /// ⚠ **host 자신은 제외**한다(신원 비교 — 셀 비교로는 같은 셀 아군까지 빠진다).
        /// ⚠ 연출은 **버프가 실제로 나간 펄스만** 재생한다(효과 없는 연출 금지).
        /// </summary>
        private void RunWhipPulse(SimWorld world, SimEntityId entity, in DcTriggerSlot slot,
                                  in FlowFieldSingleton ff, ref bool enemyPoolBuilt, ref bool defPoolBuilt)
        {
            if (slot.magnitude == 0f || slot.duration <= 0f) return;
            if (!world.TryGet<SimTransform>(entity, out var hostXf)) return;

            bool hostIsEnemy = world.Has<AttackUnitTag>(entity);
            bool hostIsDefender = !hostIsEnemy && world.Has<DefenderUnitTag>(entity);
            if (!hostIsEnemy && !hostIsDefender) return; // 진영 불명 host = no-op

            if (hostIsEnemy && !enemyPoolBuilt)
            {
                BuildPool<AttackUnitTag>(world, in ff, _enemyEntities, _enemyCells);
                enemyPoolBuilt = true;
            }
            if (hostIsDefender && !defPoolBuilt)
            {
                BuildPool<DefenderUnitTag>(world, in ff, _defEntities, _defCells);
                defPoolBuilt = true;
            }

            var poolEntities = hostIsEnemy ? _enemyEntities : _defEntities;
            var poolCells = hostIsEnemy ? _enemyCells : _defCells;

            SimVec3 hostPos = hostXf.Position;
            SimInt2 hostCell = GridMath.WorldToCell(hostPos, ff.tileSize, ff.gridSize, ff.origin);
            AuraPulse.SelectTargets(poolCells, hostCell, slot.tileRange, _whipTargets);

            float mul = 1f + slot.magnitude / 100f;
            int buffed = 0;
            for (int ti = 0; ti < _whipTargets.Count; ti++)
            {
                SimEntityId target = poolEntities[_whipTargets[ti]];
                if (target == entity) continue; // host 자신 제외
                _channels.StatApply.Enqueue(new StatModifierApplyEvent
                {
                    target = target,
                    stat = StatKind.MoveSpeedMul,
                    op = CombineOp.Multiplicative,
                    magnitude = mul,
                    duration = slot.duration,
                    source = entity,
                    stackId = 0,
                    origin = ModifierOrigin.Boss,
                });
                buffed++;
            }

            if (buffed > 0 && slot.projectileDataIndex >= 0)
            {
                _channels.ProjectileHit.Enqueue(new ProjectileHitEvent
                {
                    position = hostPos,
                    dataIndex = slot.projectileDataIndex,
                    payload = PayloadKind.SingleSplash,
                    source = entity,
                });
            }
        }

        /// <summary>
        /// 발사 명세 트리거 — 인스턴스 하나를 host 버퍼에 넣는 것이 전부이고 전개는 emitter 소유다.
        ///
        /// ⚠ spec/template 을 **값으로 복사**하므로 발사 도중 무엇이 바뀌어도 이미 시작된 버스트는
        /// 불변이다. 영속시키는 것은 **발사 카운터 하나**뿐이고 그것만 durable 소유자에 남아
        /// 다음 발화가 이어받는다 — 안 그러면 선택 규칙이 고정된다.
        /// </summary>
        private static void PushPattern(SimWorld world, SimEntityId entity, in DcTriggerSlot slot)
        {
            if (slot.patternIndex < 0) return;
            var pats = world.GetBuffer<PatternSlot>(entity);
            var instances = world.GetBuffer<EmitterInstance>(entity);
            if (pats == null || instances == null) return;
            if (slot.patternIndex >= pats.Count) return;

            var pat = pats[slot.patternIndex];
            var inst = new EmitterInstance
            {
                spec = pat.spec,
                template = pat.template,
                lockedTarget = SimEntityId.Null,
            };
            EmitterTick.Begin(ref inst.runtime, inst.spec, pat.fireCountBase);
            pat.fireCountBase += pat.spec.ShotCount;
            pats[slot.patternIndex] = pat;
            instances.Add(inst);
        }

        private static void BuildPool<TTag>(SimWorld world, in FlowFieldSingleton ff,
                                            List<SimEntityId> entities, List<SimInt2> cells)
            where TTag : struct
        {
            entities.Clear();
            cells.Clear();
            foreach (SimEntityId e in world.With<TTag>())
            {
                if (!world.TryGet<SimTransform>(e, out var xf)) continue;
                entities.Add(e);
                cells.Add(GridMath.WorldToCell(xf.Position, ff.tileSize, ff.gridSize, ff.origin));
            }
        }
    }
}
