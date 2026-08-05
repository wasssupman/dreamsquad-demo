using System;
using System.Collections.Generic;

namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-A — "루프 중 기록, 루프 후 적용"(청사진 ③ §5).
    ///
    /// 구 sim 실측: ECB 를 쓰는 시스템이 **28개**이고 전부 `Allocator.Temp` + 같은 `OnUpdate` 내
    /// Playback 이다(시스템 ECB·지연 재생 0). 즉 지연 범위는 **한 phase 안**이고 틱을 넘지 않는다.
    ///
    /// **"루프 중 즉시 적용" 으로 바꾸면 안 되는 이유**는 성능이 아니다 — 같은 엔티티에 2연산이
    /// 걸리는 함정(`ModifierApplySystem` 선례)과 순회 중 컬렉션 변경 계열 버그가 **재현된다**.
    /// 그 재현이 계약이다. 신 sim 이 "더 낫게" 고치면 골든이 갈린다.
    ///
    /// 재사용 규약: phase 마다 새로 만들거나 <see cref="Clear"/> 후 재사용한다. Playback 은
    /// **기록 순서대로** 적용한다 — 같은 엔티티에 add→remove 가 쌓이면 마지막이 이긴다.
    /// </summary>
    public sealed class SimCommandBuffer
    {
        private readonly List<Action<SimWorld>> _ops = new List<Action<SimWorld>>();

        public int Count => _ops.Count;

        public void Set<T>(SimEntityId e, T value) where T : struct
            => _ops.Add(w => w.Set(e, value));

        public void RemoveComponent<T>(SimEntityId e) where T : struct
            => _ops.Add(w => w.RemoveComponent<T>(e));

        /// <summary>
        /// ⚠ **P12(UnitLifecycle)의 파괴 루프에서만 기록한다.** 다른 phase 가 이걸 쓰면 사망 창이
        /// 사라진다(청사진 ③ §3 — 마킹과 파괴의 분리). 여기서 막지 않는 이유는 버퍼가 호출 지점을
        /// 알 수 없어서이고, 대신 `SimTick` 의 phase 배치가 그 규율을 진다.
        /// </summary>
        public void Destroy(SimEntityId e) => _ops.Add(w => w.Destroy(e));

        /// 임의 지연 연산. 위 3종으로 표현 안 되는 구조 변경(버퍼 추가 등)에.
        public void Defer(Action<SimWorld> op) => _ops.Add(op);

        public void Playback(SimWorld world)
        {
            for (int i = 0; i < _ops.Count; i++) _ops[i](world);
            _ops.Clear();
        }

        public void Clear() => _ops.Clear();
    }
}
