using System.Collections.Generic;
using Wassup.Sim.Effects;

namespace Wassup.Sim.Units
{
    /// <summary>
    /// battle-sim-extraction unit 18-C/5 — 캡처 #31 · <see cref="SimPhase.ModifierTick"/>(P7).
    /// 구 `Wassup.Battle.Units.MaxHealthScaleSystem` 이식.
    ///
    /// `ModifierStats.maxHealthMul`(Effects 소유, **읽기 전용**)을 소비해 `Health.max` 를
    /// 재계산한다. **`Health` 쓰기는 Units 맥락 안에서만** 일어난다 — 맥락 경계(제약 2)의
    /// 후계이고, 폴더가 그것을 표시한다.
    ///
    /// ⚠ 게이트가 **없다** — 구 sim 에도 `RequireForUpdate` 가 없어 매 틱 돈다(18-B 주의).
    ///
    /// ⚠ **구 sim 의 "중간 Playback" 을 보존한다.** Pass 1 의 지연 부착을 Pass 2 **전에** 적용해,
    /// 부착된 그 프레임에 이미 배율이 먹는다. 다음 틱으로 미루면 전 유닛의 체력 스케일이
    /// 1틱씩 밀린다(특성화 `Attach_AndApply_InTheSameFrame` 가 박제).
    /// </summary>
    public sealed class MaxHealthScaleSystem
    {
        // 부착 대상 임시 목록 — 순회 중 구조 변경을 피하려고 모았다가 루프 뒤에 적용한다
        // (구 sim 의 `EntityCommandBuffer` + 중간 Playback 대응). 인스턴스 필드로 재사용한다.
        private readonly List<SimEntityId> _toAttach = new List<SimEntityId>();

        public void Run(SimWorld world)
        {
            // ── Pass 1 — lazy attach ──────────────────────────────────────────
            // 배율이 1 에서 벗어난 **첫 프레임**에만 baseMax 를 캡처한다.
            // `mul <= 0` 은 미초기화 방어다 — 스폰 init 이 base 1 을 넣기 전 프레임에 부착하면
            // baseMax 를 잡고 max 를 1 HP 로 깎아버린다.
            _toAttach.Clear();
            foreach (SimEntityId e in world.With<Health>())
            {
                if (world.Has<MaxHealthScaleState>(e)) continue;
                if (!world.TryGet(e, out ModifierStats stats)) continue;

                float mul = stats.maxHealthMul;
                if (mul > 0f && mul != 1f) _toAttach.Add(e);
            }
            for (int i = 0; i < _toAttach.Count; i++)
            {
                SimEntityId e = _toAttach[i];
                world.Set(e, new MaxHealthScaleState
                {
                    baseMax = world.Get<Health>(e).max,
                    appliedMul = 1f,
                });
            }

            // ── Pass 2 — 배율 변화 시에만 재계산 (복원 mul=1 포함) ────────────
            foreach (SimEntityId e in world.With<MaxHealthScaleState>())
            {
                if (!world.TryGet(e, out ModifierStats stats)) continue;
                if (!world.TryGet(e, out Health health)) continue;

                MaxHealthScaleState scale = world.Get<MaxHealthScaleState>(e);
                float mul = stats.maxHealthMul;
                // 부착 후에도 같은 가드가 걸린다 — 상류가 순간적으로 0 을 흘려도 깎지 않는다.
                if (mul <= 0f || mul == scale.appliedMul) continue;

                SimVec2 scaled = Health.ScaleMax(health.value, scale.baseMax, mul);
                health.value = scaled.x;
                health.max = scaled.y;
                scale.appliedMul = mul;

                world.Set(e, health);
                world.Set(e, scale);
            }
        }
    }
}
