using System.Collections.Generic;
using Wassup.Sim.Combat;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/1 — 캡처 #18(P5). 구 `HazardCastSystem` 이식.
    ///
    /// 사거리 안 최근접 대상의 **셀**에 해저드 스폰을 요청한다. 대상은 겨냥 지점을 정할 뿐이고
    /// 해저드는 그 자리에 놓인다.
    ///
    /// ⚠ **18-E 에서 여기로 이관된 조각**이다. 근거였던 `DcTriggerSlot` 버퍼 존재 확인이
    /// 18-G/2 로 해소되어 이제 자립한다.
    ///
    /// ⚠ **캐스트 성사 = 이 host 의 공격 사건**이다. 이 캐스터들은 `attackRange` 가 0 이라
    /// 공격 루프의 RESOLVE 에 못 가므로, 카운터를 직접 쓰지 않고
    /// <see cref="SimChannels.Cast"/> 로 넘긴다(Combat 소유 필드는 Combat 이 쓴다).
    /// P5 → P8 이라 **같은 틱**에 소비된다.
    ///
    /// ⚠ 생산자 게이트: **카드가 붙은 캐스터만** 사건을 낸다. 없으면 주기마다 이벤트가
    /// 쌓이기만 한다. `DcTriggerSlot` 을 여기서 **읽는 것**은 맥락 위반이 아니다.
    /// </summary>
    public sealed class HazardCastSystem
    {
        private readonly SimChannels _channels;
        private readonly List<SimEntityId> _targetEntities = new List<SimEntityId>();
        private readonly List<SimVec3> _targetPositions = new List<SimVec3>();
        private readonly List<Faction> _targetFactions = new List<Faction>();

        public HazardCastSystem(SimChannels channels) => _channels = channels;

        public void Run(SimWorld world)
        {
            if (!SimSingleton.TryGet<FlowFieldSingleton>(world, out var flowField)) return;

            float dt = world.DeltaTime;

            // 후보 = 진영·위치·경로 상태를 가진, 배치 완료된 산 유닛.
            _targetEntities.Clear();
            _targetPositions.Clear();
            _targetFactions.Clear();
            foreach (var e in world.With<FactionTag>())
            {
                if (world.Has<PendingDeployment>(e)) continue;
                if (world.Has<DeadTag>(e)) continue;
                if (!world.Has<PathFollowState>(e)) continue;
                if (!world.TryGet<SimTransform>(e, out var xf)) continue;
                _targetEntities.Add(e);
                _targetPositions.Add(xf.Position);
                _targetFactions.Add(world.Get<FactionTag>(e).value);
            }

            foreach (var casterEntity in world.With<HazardCastState>())
            {
                if (!world.Has<DefenderUnitTag>(casterEntity)) continue;
                if (world.Has<PendingDeployment>(casterEntity)) continue;
                if (world.Has<DeadTag>(casterEntity)) continue;
                if (!world.TryGet<SimTransform>(casterEntity, out var casterTransform)) continue;

                var cast = world.Get<HazardCastState>(casterEntity);

                // ⚠ 쿨다운은 **저작 유효성 검사보다 먼저** 흐른다 — 구 sim 의 순서다.
                if (cast.cooldownRemaining > 0f)
                {
                    cast.cooldownRemaining = SimMath.Max(0f, cast.cooldownRemaining - dt);
                    world.Set(casterEntity, cast);
                }

                if (cast.kind == HazardCastKind.None || cast.dataIndex < 0) continue;

                SimVec3 casterPos = casterTransform.Position;
                SimInt2 casterCell = GridMath.WorldToCell(casterPos, flowField.tileSize, flowField.gridSize, flowField.origin);
                int tileRange = GridMath.RangeToTiles(cast.range);
                int mask = cast.targetMask;

                float bestSq = float.MaxValue;
                int bestSimId = int.MaxValue;
                var bestTarget = SimEntityId.Null;
                SimInt2 bestTargetCell = default;

                for (int i = 0; i < _targetEntities.Count; i++)
                {
                    if (_targetEntities[i] == casterEntity) continue;
                    if (((int)_targetFactions[i] & mask) == 0) continue;

                    SimVec3 targetPos = _targetPositions[i];
                    SimInt2 targetCell = GridMath.WorldToCell(targetPos, flowField.tileSize, flowField.gridSize, flowField.origin);
                    int tileDist = SimMath.Max(SimMath.Abs(targetCell.x - casterCell.x),
                                               SimMath.Abs(targetCell.y - casterCell.y));
                    if (tileDist > tileRange) continue;

                    float distSq = SimMath.DistanceSq(casterPos, targetPos);
                    int candSimId = _targetEntities[i].Value;
                    // ⚠ **등거리 동률은 낮은 simId 가 이긴다.** 이 축이 없으면 스냅샷(청크) 순서에
                    //   결과가 걸려 같은 판이 실행마다 갈린다.
                    if (distSq < bestSq || (distSq == bestSq && candSimId < bestSimId))
                    {
                        bestSq = distSq;
                        bestSimId = candSimId;
                        bestTarget = _targetEntities[i];
                        bestTargetCell = targetCell;
                    }
                }

                // ⚠ 쿨다운 검사가 **대상 탐색 뒤**인 것이 구 sim 의 순서다(결과는 같지만 유지한다).
                if (bestTarget.IsNull || cast.cooldownRemaining > 0f) continue;

                _channels.UnitAttackVisual.Enqueue(new UnitAttackVisualEvent
                {
                    attacker = casterEntity,
                    targetWorld = GridMath.CellToWorldCenter(bestTargetCell, flowField.tileSize, casterPos.y, flowField.origin),
                });

                _channels.HazardSpawnRequest.Enqueue(new HazardSpawnRequest
                {
                    kind = cast.kind,
                    dataIndex = cast.dataIndex,
                    centerCell = bestTargetCell,
                    // ⚠ 저작의 footprint 를 **쓰지 않는다** — 구 sim 이 1×1 로 고정해 보낸다.
                    width = 1,
                    height = 1,
                    caster = casterEntity,
                    target = bestTarget,
                });

                if (world.HasBuffer<DcTriggerSlot>(casterEntity))
                    _channels.Cast.Enqueue(new CastEvent { caster = casterEntity, casterPos = casterPos });

                cast.cooldownRemaining = cast.cooldownDuration;
                world.Set(casterEntity, cast);
            }
        }
    }
}
