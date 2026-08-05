using System.Collections.Generic;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/5 — 캡처 #19(P5). 구 `ShieldCastSystem` 이식.
    ///
    /// 주기마다 사거리(Chebyshev 타일) 안의 아군 방어유닛 N명을 골라 실드를 부여한다.
    ///
    /// ⚠ **부여만 한다 — 병합하지 않는다.** 쓰기는 `IncomingShield` append 하나뿐이고,
    /// 출처별 max·교차 출처 합산은 `DamageApplicationSystem`(#34, P9)이 드레인하며 한다.
    /// 그 분리가 이 시스템이 Effects 에 살면서 Units 버퍼를 만질 수 있는 근거다(이벤트 버퍼).
    ///
    /// ⚠ **쿨다운은 대상 유무와 무관하게 리셋된다.** 자신이 항상 후보라 매 주기 발화가 보장되고,
    /// 미발화 시 매 프레임 재스캔하는 낭비를 막는다.
    ///
    /// ⚠ **성사 판정이 있다** — 이 출처의 기존 슬롯이 이미 `amount` 이상이면 #34 의 `Merge` 가
    /// max 로 no-op 이므로 append/VFX 를 건너뛴다. 없으면 만충 아군에게 매 주기 헛불꽃이 튄다.
    ///
    /// ⚠ 후보 스냅샷을 **루프 밖에서 한 번** 뜬다. 캐스터 A 의 부여가 캐스터 B 의 후보 순위를
    /// 같은 틱에 바꾸지 못한다는 뜻이고, 그게 구 sim 의 동작이다(`ToEntityArray` 스냅샷).
    /// </summary>
    public sealed class ShieldCastSystem
    {
        private readonly SimChannels _channels;

        // 틱마다 재사용 — 할당을 루프 안에서 반복하지 않는다(구 sim 의 Allocator.Temp 대응).
        private readonly List<SimEntityId> _candEntities = new List<SimEntityId>();
        private readonly List<SimVec3> _candPositions = new List<SimVec3>();
        private readonly List<Health> _candHealths = new List<Health>();
        private readonly List<ShieldCandidate> _candidates = new List<ShieldCandidate>();
        private readonly List<SimEntityId> _candidateTargets = new List<SimEntityId>();
        private readonly List<SimVec3> _candidatePositions = new List<SimVec3>();
        private readonly List<int> _selected = new List<int>();

        public ShieldCastSystem(SimChannels channels) => _channels = channels;

        public void Run(SimWorld world)
        {
            if (!SimSingleton.TryGet<FlowFieldSingleton>(world, out var flowField)) return;

            float dt = world.DeltaTime;

            // 후보 스냅샷: 생존·배치완료 아군 방어유닛 (**자신 포함** — 계약).
            _candEntities.Clear();
            _candPositions.Clear();
            _candHealths.Clear();
            foreach (var e in world.With<DefenderUnitTag>())
            {
                if (world.Has<PendingDeployment>(e)) continue;
                if (world.Has<DeadTag>(e)) continue;
                if (!world.TryGet<Health>(e, out var health)) continue;
                if (!world.TryGet<SimTransform>(e, out var transform)) continue;
                _candEntities.Add(e);
                _candPositions.Add(transform.Position);
                _candHealths.Add(health);
            }

            foreach (var caster in world.With<ShieldCastState>())
            {
                if (!world.Has<DefenderUnitTag>(caster)) continue;
                if (world.Has<PendingDeployment>(caster)) continue;
                if (world.Has<DeadTag>(caster)) continue;
                if (!world.TryGet<SimTransform>(caster, out var casterTransform)) continue;

                var cast = world.Get<ShieldCastState>(caster);
                if (cast.cooldownRemaining > 0f)
                {
                    cast.cooldownRemaining = SimMath.Max(0f, cast.cooldownRemaining - dt);
                    world.Set(caster, cast);
                    continue;
                }

                SimVec3 casterPos = casterTransform.Position;
                SimInt2 casterCell = GridMath.WorldToCell(casterPos, flowField.tileSize, flowField.gridSize, flowField.origin);
                int tileRange = GridMath.RangeToTiles(cast.range);

                _candidates.Clear();
                _candidateTargets.Clear();
                _candidatePositions.Clear();
                int selfIndex = -1;

                for (int i = 0; i < _candEntities.Count; i++)
                {
                    SimVec3 targetPos = _candPositions[i];
                    SimInt2 targetCell = GridMath.WorldToCell(targetPos, flowField.tileSize, flowField.gridSize, flowField.origin);
                    int tileDist = SimMath.Max(SimMath.Abs(targetCell.x - casterCell.x),
                                               SimMath.Abs(targetCell.y - casterCell.y));
                    if (tileDist > tileRange) continue;

                    var existing = world.GetBuffer<ShieldSlot>(_candEntities[i]);
                    float shieldSum = existing != null ? ShieldMath.Sum(existing) : 0f;
                    float maxHp = SimMath.Max(1f, _candHealths[i].max);
                    _candidates.Add(new ShieldCandidate
                    {
                        distanceSq = SimMath.DistanceSq(casterPos, targetPos),
                        effectiveHpRatio = (_candHealths[i].value + shieldSum) / maxHp,
                    });
                    _candidateTargets.Add(_candEntities[i]);
                    _candidatePositions.Add(targetPos);
                    if (_candEntities[i] == caster) selfIndex = _candidateTargets.Count - 1;
                }

                ShieldTargeting.Select(cast.filter, cast.targetCount, selfIndex, _candidates, _selected);

                for (int s = 0; s < _selected.Count; s++)
                {
                    SimEntityId target = _candidateTargets[_selected[s]];
                    var incoming = world.GetBuffer<IncomingShield>(target);
                    if (incoming == null) continue;

                    var slots = world.GetBuffer<ShieldSlot>(target);
                    if (slots != null && ShieldMath.ValueFromSource(slots, caster) >= cast.amount)
                        continue; // no-op 재부여 — 헛불꽃 방지

                    incoming.Add(new IncomingShield { source = caster, amount = cast.amount });
                    _channels.ShieldGranted.Enqueue(new ShieldGrantedEvent
                    {
                        position = _candidatePositions[_selected[s]],
                    });
                }

                cast.cooldownRemaining = cast.cooldownDuration;
                world.Set(caster, cast);
            }
        }
    }
}
