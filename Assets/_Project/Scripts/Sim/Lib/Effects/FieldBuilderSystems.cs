using System.Collections.Generic;
using Wassup.Sim.Combat;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-E/4 — 캡처 **#3** · <see cref="SimPhase.FieldsAndPeriodic"/>(P1).
    /// 구 `AllyBuffFieldSystem` 이식.
    ///
    /// 살아 있는 장판 × 배치 완료 방어유닛을 체비셰프로 맞춰 **매 프레임** 스탯 모디파이어를
    /// 재발행한다. P1 이라 소비자(#9 P2)보다 앞 — 같은 프레임에 적용된다.
    ///
    /// ⚠ **겹친 장판은 누적하지 않고 최댓값이 이긴다.** 누적하면 "어느 값이 이기나" 가 순회
    /// 순서에 맡겨지고, 만료 swap-back 이 그 순서를 런타임에 바꿔 **배율이 다른 장판 2장이
    /// 겹치면 승자가 무작위**가 된다. 부수 효과로 유닛당 발행이 (장판 수)에서 (stat 수 ≤2)로 준다.
    ///
    /// ⚠ cadence 누산기를 두지 않는다 — 매 프레임이 이 레포의 관용구이고, 주기를 두면
    /// "정지/슬로모에서 어느 시계를 쓰나" 라는 결정이 새로 생긴다.
    /// </summary>
    public sealed class AllyBuffFieldSystem
    {
        private readonly SimChannel<StatModifierApplyEvent> _statChannel;
        private readonly List<AllyBuffField> _fields = new List<AllyBuffField>();

        public AllyBuffFieldSystem(SimChannel<StatModifierApplyEvent> statChannel)
            => _statChannel = statChannel;

        public void Run(SimWorld world)
        {
            _fields.Clear();
            foreach (SimEntityId f in world.With<AllyBuffField>()) _fields.Add(world.Get<AllyBuffField>(f));
            if (_fields.Count == 0) return;   // self-gate(구 `RequireForUpdate<AllyBuffField>` 와 같은 효과)

            foreach (SimEntityId e in world.With<DefenderTile>())
            {
                if (world.Has<PendingDeployment>(e)) continue;   // 아직 판에 없다
                if (world.Has<DeadTag>(e)) continue;

                SimInt2 cell = world.Get<DefenderTile>(e).cell;

                float bestDamage = 0f, bestSpeed = 0f;
                for (int i = 0; i < _fields.Count; i++)
                {
                    AllyBuffField f = _fields[i];
                    if (GridMath.ChebyshevDistance(cell, f.centerCell) > f.tileRange) continue;
                    if (f.stat == StatKind.DamageMul) bestDamage = SimMath.Max(bestDamage, f.magnitude);
                    else if (f.stat == StatKind.AttackSpeedMul) bestSpeed = SimMath.Max(bestSpeed, f.magnitude);
                }

                if (bestDamage > 0f) Enqueue(e, StatKind.DamageMul, bestDamage);
                if (bestSpeed > 0f) Enqueue(e, StatKind.AttackSpeedMul, bestSpeed);
            }
        }

        /// duration 은 **여기 한 곳에서만** 정해진다 — 호출부가 실수로 스킬 지속시간을 넣을 여지를 없앤다.
        private void Enqueue(SimEntityId target, StatKind stat, float multiplier)
        {
            SimModifierAuthoring.FromMultiplier(multiplier, out CombineOp op, out float magnitude);
            _statChannel.Enqueue(new StatModifierApplyEvent
            {
                target = target,
                stat = stat,
                op = op,
                magnitude = magnitude,
                duration = AllyBuffField.ApplySec,
                source = target,
                stackId = AllyBuffField.StackId,
                origin = ModifierOrigin.Skill,
            });
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/4 — 캡처 **#7** · <see cref="SimPhase.FieldsAndPeriodic"/>(P1).
    /// 구 `DefenderFieldSystem` 이식.
    ///
    /// 살아 있는 방어유닛들의 "공격 가능한 walk 셀" 을 소스로 multi-source BFS 를 **매 프레임
    /// 재빌드**한다. 그리드가 작아 배치/사망 훅·dirty 추적 없이 매 프레임이 가장 단순하다.
    ///
    /// ⚠ **보스가 없으면 재빌드하지 않는다.** 필드 소비자는 보스뿐이고, 보스 스폰 프레임엔
    /// 이 시스템이 Movement(#17) 앞에서 다시 돌아 신선한 필드가 보장된다.
    ///
    /// ⚠ **`rangeTiles` 는 동시 헌터 사거리의 min fold** 다. 소스가 "**모든** 헌터가 발사 가능한
    /// 셀" 이어야 사거리 짧은 보스가 dist-0 셀에서 발사 불가로 서버리는 스톨이 구조적으로
    /// 불가능해진다. 사거리 긴 보스는 FSM 이 소스 도달 전에 Engaging 으로 먼저 멈춘다.
    /// </summary>
    public sealed class DefenderFieldSystem
    {
        // 재사용 버퍼 — 매 프레임 재빌드이므로 새로 할당하면 틱당 쓰레기가 유닛 수만큼 생긴다.
        private SimInt2[] _defenderCells = new SimInt2[16];
        private readonly List<SimInt2> _sources = new List<SimInt2>();
        private SimInt2[] _sourceArray = new SimInt2[64];

        public void Run(SimWorld world)
        {
            if (!SimSingleton.TryGet(world, out DefenderFieldSingleton field)) return;   // 분류 C 게이트
            if (!field.IsCreated) return;

            // 보스 부재 = 재빌드 skip(위 ⚠). 사거리 fold 도 보스에서만 걷는다.
            bool hasBoss = false;
            int rangeTiles = int.MaxValue;
            foreach (SimEntityId b in world.With<BossTag>())
            {
                hasBoss = true;
                if (world.TryGet(b, out AttackState atk))
                    rangeTiles = SimMath.Min(rangeTiles, GridMath.RangeToTiles(atk.range));
            }
            if (!hasBoss) return;
            if (rangeTiles == int.MaxValue) rangeTiles = 1;   // AttackState 없는 보스뿐 — 인접 폴백
            rangeTiles = SimMath.Max(1, rangeTiles);

            // 방어유닛 스냅샷 — FSM 후보 풀과 같은 조건 + 진영 필터.
            int count = 0;
            foreach (SimEntityId e in world.With<FactionTag>())
            {
                if (!world.Has<Health>(e)) continue;
                if (world.Has<PendingDeployment>(e) || world.Has<DeadTag>(e)) continue;
                if (((int)world.Get<FactionTag>(e).value & (int)Faction.Defender) == 0) continue;
                if (!world.TryGet(e, out SimTransform t)) continue;

                if (count == _defenderCells.Length) Grow(ref _defenderCells);
                _defenderCells[count++] = GridMath.WorldToCell(
                    t.Position, field.tileSize, field.gridSize, origin: field.origin);
            }

            FlowFieldBuilder.CollectDefenderSources(field.walkMask, field.gridSize,
                _defenderCells, count, rangeTiles, _sources);

            if (_sourceArray.Length < _sources.Count) _sourceArray = new SimInt2[_sources.Count];
            for (int i = 0; i < _sources.Count; i++) _sourceArray[i] = _sources[i];

            // 방어유닛 0 → 유효 소스 0 → 빌더가 전 셀을 `int.MaxValue` 로 리셋(goal 폴백).
            FlowFieldBuilder.BuildFromSources(field.walkMask, field.gridSize,
                _sourceArray, _sources.Count, field.flow, field.dist);
        }

        private static void Grow(ref SimInt2[] a)
        {
            var bigger = new SimInt2[a.Length * 2];
            System.Array.Copy(a, bigger, a.Length);
            a = bigger;
        }
    }
}
