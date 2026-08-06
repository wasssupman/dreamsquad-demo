using System.Collections.Generic;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-J/4 — 캡처 **#42** · <see cref="SimPhase.Destruction"/>(P12).
    /// 구 `HealthThresholdSystem` 이식.
    ///
    /// **책임 둘** — 이름과 달리 보스 전용이 아니다(진영 중립 쿼리):
    /// <list type="number">
    /// <item><b>위협 드레인</b> — 이번 프레임 귀속 히트를 피격자 테이블에 누적한다.
    ///       ⚠ 그 누적의 **현재 소비자는 없다**(도약 목적지가 밀집도 정책으로 바뀌면서 끊겼다).
    ///       채널·버퍼 독립 가드라 `ThreatEntry` 가 없어도 무손상이다.</item>
    /// <item><b>체력 임계 평가</b> — 슬롯을 현재 체력에 맞춰 굴리고 payload 를 해결한다
    ///       (self 버프 · 보스 도약 · 궁극기 이탈 · 자기중심 폭발).</item>
    /// </list>
    ///
    /// #34(피해 정산) 뒤라 **같은 틱 피해가 임계에 보인다**.
    ///
    /// ⚠ **죽은 유닛은 새 발동을 시작하지 않는다.** `DeadTag` 는 #34 가 자기 끝에 붙이므로
    /// **죽는 프레임에 이미 붙어 있고**, 오버킬로 여러 경계를 한 번에 관통하면 시체가 마지막
    /// 경계에서 폭발/도약한다.
    ///
    /// ⚠ 방어유닛 셀 풀은 **첫 도약 발동 때 1회 생성**한다(`_defBuilt`) — 디펜더-only 판에서
    /// 매 프레임 쿼리+할당을 피한다. 방어유닛 전멸 시 길이 0 이 되므로 "비었나" 로 대체 금지.
    /// </summary>
    public sealed class HealthThresholdSystem
    {
        private readonly SimChannels _channels;
        private readonly List<SimInt2> _defCells = new List<SimInt2>();
        private readonly List<SimEntityId> _hosts = new List<SimEntityId>();
        private readonly SimCommandBuffer _ecb = new SimCommandBuffer();

        public HealthThresholdSystem(SimChannels channels) => _channels = channels;

        public void Run(SimWorld world)
        {
            // 1. 위협 드레인 — enqueue 후 사라진 피격자의 사건은 조용히 버린다.
            var hits = _channels.ThreatHit.Drain();
            for (int i = 0; i < hits.Count; i++)
            {
                var table = world.GetBuffer<ThreatEntry>(hits[i].victim);
                if (table == null) continue;
                ThreatTable.Accumulate(table, hits[i].attacker, hits[i].amount);
            }

            if (!SimSingleton.TryGet<FlowFieldSingleton>(world, out var ff)) return;

            bool defBuilt = false;

            // ⚠ 순회 중 `_ecb` 가 구조를 바꾸므로 host 목록을 먼저 스냅샷한다.
            _hosts.Clear();
            foreach (SimEntityId e in world.WithBuffer<DcTriggerSlot>()) _hosts.Add(e);

            for (int hi = 0; hi < _hosts.Count; hi++)
            {
                SimEntityId entity = _hosts[hi];
                if (world.Has<DeadTag>(entity)) continue;
                if (!world.TryGet<Health>(entity, out var health)) continue;
                if (!world.TryGet<SimTransform>(entity, out var transform)) continue;
                var slots = world.GetBuffer<DcTriggerSlot>(entity);
                if (slots == null) continue;

                for (int si = 0; si < slots.Count; si++)
                {
                    var slot = slots[si];
                    if (slot.trigger != DcTriggerKind.HealthThreshold) continue;

                    int k = slot.nextBoundaryIndex;
                    bool fired = DcTrigger.HealthThresholdEval(health.value, slot.maxHpRef, slot.fraction, ref k);
                    slot.nextBoundaryIndex = k;
                    slots[si] = slot;
                    if (!fired) continue;

                    switch (slot.payload)
                    {
                        case DcPayloadKind.SelfStatBuff:
                            ResolveSelfStatBuff(entity, in slot);
                            break;

                        case DcPayloadKind.SelfBlink:
                            if (!defBuilt) { BuildDefenderCells(world, in ff); defBuilt = true; }
                            ResolveBossLeap(world, entity, in slot, in ff, transform.Position);
                            break;

                        case DcPayloadKind.UltimateLeap:
                            if (!defBuilt) { BuildDefenderCells(world, in ff); defBuilt = true; }
                            ResolveUltimateLeap(world, entity, in slot, in ff, transform.Position);
                            break;

                        case DcPayloadKind.SelfTileAoe:
                            ResolveSelfTileAoe(world, entity, in slot, transform.Position);
                            break;

                        default:
                            _channels.Warnings.Enqueue(new SimWarning
                            {
                                code = SimWarningCode.HealthThresholdUnhandledPayload,
                                entity = entity,
                                detail = (int)slot.payload,
                            });
                            break;
                    }
                }
            }

            _ecb.Playback(world);
        }

        /// last_stand — self 에 모디파이어. ⚠ `duration &lt;= 0` = **영구**(무한 컨벤션)이고
        /// `FromMultiplier` 로 분해한다(+% 는 Additive 버킷).
        private void ResolveSelfStatBuff(SimEntityId entity, in DcTriggerSlot slot)
        {
            float ttl = slot.duration > 0f ? slot.duration : float.PositiveInfinity;
            SimModifierAuthoring.FromMultiplier(slot.magnitude, out var buffOp, out float buffMag);
            _channels.StatApply.Enqueue(new StatModifierApplyEvent
            {
                target = entity,
                stat = slot.buffStat,
                op = buffOp,
                magnitude = buffMag,
                duration = ttl,
                source = entity,
                stackId = slot.statBuffStackId,
                origin = ModifierOrigin.HealthThreshold,
            });
        }

        /// <summary>
        /// 보스 도약 — 착지 앵커는 **방어유닛 밀집도 최대 셀**이다(구 "위협 리더 근처" 정책을 교체).
        /// 밀집을 응징하러 뛰는 것이므로 밀집 셀 **자체**가 desired 이고, 그 셀이 배치칸이면
        /// 링 탐색이 인접 walkable·연결 셀로 스냅한다.
        ///
        /// 목적지 실패(방어유닛 전멸/링 상한 초과) = skip — `k` 는 이미 전진해 **재발동이 없다**.
        /// </summary>
        private void ResolveBossLeap(SimWorld world, SimEntityId entity, in DcTriggerSlot slot,
                                     in FlowFieldSingleton ff, SimVec3 fromWorld)
        {
            if (!TryResolveBlinkDest(slot.tileRange, (int)slot.magnitude, in ff, out SimVec3 destWorld, out _))
                return;

            _channels.BlinkRequest.Enqueue(new BlinkRequestEvent { entity = entity, destWorld = destWorld });
            // ⚠ sim 은 이번 프레임에 텔레포트한다. 뷰는 아치로 날리고 **퍼프도 뷰가 비행
            //   시작/종료에 맞춰** 재생한다 — 여기서 직접 쏘면 착지 퍼프가 뷰 도착보다 먼저 터진다.
            _channels.BossLeapVisual.Enqueue(new BossLeapVisualEvent
            {
                entity = entity,
                fromWorld = fromWorld,
                toWorld = destWorld,
                dataIndex = slot.projectileDataIndex,
                slamDamage = slot.slamDamage,
                slamTileRange = slot.slamTileRange,
            });
        }

        /// <summary>
        /// 궁극기 이탈 개시 — 여기서 하는 일은 **착지점 고정과 상태 부착**뿐이고 카운트다운·착지는
        /// #43 이 굴린다.
        ///
        /// ⚠ **착지 셀을 지금 고정하는 것이 계약이다** — 예고는 약속이라, 착지 직전 재계산하면
        /// 빨간 타일을 보고 유닛을 빼는 회피 플레이가 거짓말이 된다.
        ///
        /// ⚠ 목적지 실패면 **loud fail** 한다. `k` 가 이미 전진해 생존당 1회라 **재시도가 없고**,
        /// 조용히 넘기면 "궁극기가 왜 안 나왔는지" 를 영영 알 수 없다(1회성이라 재현도 안 된다).
        /// </summary>
        private void ResolveUltimateLeap(SimWorld world, SimEntityId entity, in DcTriggerSlot slot,
                                         in FlowFieldSingleton ff, SimVec3 fromWorld)
        {
            if (!TryResolveBlinkDest(slot.tileRange, (int)slot.magnitude, in ff,
                                     out SimVec3 ultDest, out SimInt2 ultCell))
            {
                _channels.Warnings.Enqueue(new SimWarning
                {
                    code = SimWarningCode.UltimateLeapNoLanding,
                    entity = entity,
                    detail = 0,
                });
                return;
            }

            // 잠금(`LeapFlight`)과 무적(`UltimateLeapState`)은 **함께 붙는다** —
            // 레이어는 갈리지만 수명은 하나다.
            _ecb.Set(entity, new UltimateLeapState
            {
                remaining = SimMath.Max(0.01f, slot.duration),
                landingCell = ultCell,
                landingWorld = ultDest,
                slamDamage = slot.slamDamage,
                slamTileRange = SimMath.Max(0, slot.slamTileRange),
                projectileDataIndex = slot.projectileDataIndex,
            });
            _ecb.Set(entity, new LeapFlight());

            _channels.UltimateLeapVisual.Enqueue(new UltimateLeapVisualEvent
            {
                entity = entity,
                kind = UltimateLeapVisualKind.Ascend,
                world = fromWorld,
                dataIndex = -1,
            });
        }

        /// <summary>
        /// 진동갑주 — 자기 위치 즉발 TileAoe 캐리어. `owner = self` 라 폭발 킬이 이 유닛에 귀속된다.
        ///
        /// ⚠ **피해 풀 진영을 host 에서 도출한다.** 기본값이 Enemy 라 그냥 두면 **보스의 폭발이
        /// 자기 진영을 때린다**. `FactionTag` 부재 시 Enemy 유지 = 기존 방어유닛 경로와 동일.
        /// </summary>
        private void ResolveSelfTileAoe(SimWorld world, SimEntityId entity, in DcTriggerSlot slot, SimVec3 pos)
        {
            bool hostIsEnemy = world.TryGet<FactionTag>(entity, out var ft) && ft.value == Faction.Enemy;
            var req = new ProjectileSpawnRequest
            {
                movement = MovementKind.SkyFall,
                payload = PayloadKind.TileAoe,
                impact = pos,
                damage = slot.magnitude,
                impactTileRange = slot.tileRange,
                flightTime = 0f,
                dataIndex = slot.projectileDataIndex,
                visualScale = slot.visualScale > 0f ? slot.visualScale : 1f,
                owner = entity,
                targetFaction = hostIsEnemy
                    ? ProjectileTargetFaction.Defender
                    : ProjectileTargetFaction.Enemy,
            };
            _ecb.Defer(w =>
            {
                var carrier = w.Create();
                w.Set(carrier, req);
                w.Set(carrier, new ProjectileRequestCarrier());
            });
        }

        private void BuildDefenderCells(SimWorld world, in FlowFieldSingleton ff)
        {
            _defCells.Clear();
            foreach (SimEntityId e in world.With<DefenderUnitTag>())
            {
                if (!world.TryGet<SimTransform>(e, out var xf)) continue;
                _defCells.Add(GridMath.WorldToCell(xf.Position, ff.tileSize, ff.gridSize, ff.origin));
            }
        }

        /// <summary>
        /// 착지 **셀도 함께** 돌려준다 — 셀은 뷰(예고 타일)가, 월드는 sim 이 쓴다. 한쪽에서 다른
        /// 쪽을 파생하려면 그 계층이 없는 의존(격자 파라미터 / 흐름장)을 새로 져야 한다.
        /// </summary>
        private bool TryResolveBlinkDest(
            int maxRingRadius, int densityRadius, in FlowFieldSingleton ff,
            out SimVec3 destWorld, out SimInt2 landingCell)
        {
            destWorld = default;
            landingCell = default;

            if (!DefenderDensity.TryFindDensestCell(_defCells, densityRadius, ff.gridSize, out var desiredCell, out _))
                return false; // 방어유닛 전멸 → skip
            if (!BlinkMath.TryFindLandingCell(desiredCell, ff.dist, ff.gridSize,
                                              SimMath.Max(0, maxRingRadius), out var landing))
                return false; // 링 상한 내 착지 불가 → skip

            landingCell = landing;
            destWorld = GridMath.CellToWorldCenter(landing, ff.tileSize, 0f, ff.origin);
            return true;
        }
    }
}
