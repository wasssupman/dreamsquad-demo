using System.Collections.Generic;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-E/3 — 해저드가 주는 효과 1건. 구 `HazardEffect` 이식.
    /// `kind` 는 **저작 토큰**이다 — `CcKind.Slow`/`CcKind.DoT` 는 각각 스탯 감속·전용 도트로
    /// 라우팅되고 실제 CC 버퍼에 들어가지 않는다(`ZoneApplySystem`).
    /// `origin` 은 저작하지 않는다 — 해저드가 만들면 언제나 `DotOrigin.Zone` 이다.
    /// </summary>
    public struct HazardEffect
    {
        public CcKind kind;
        public float param1;
        public float param2;
        public float restDuration;
        /// &gt;0 이면 이 주기마다 `param1` 청크로 1회. 0 이면 연속(`param1` = DPS). append-only.
        public float tickInterval;
        /// 이 해저드가 만드는 지속 피해의 원소. 없으면 Fire·Poison 해저드가 구분되지 않는다.
        public DotElement element;
    }

    /// 구 `Hazard` 이식.
    public struct Hazard
    {
        public float remainingLife;
    }

    /// 구 `HazardCellsBuffer` 이식(버퍼 원소).
    public struct HazardCellsBuffer
    {
        public SimInt2 cell;
    }

    /// 구 `HazardEffectsBuffer` 이식(버퍼 원소).
    public struct HazardEffectsBuffer
    {
        public HazardEffect effect;
    }

    /// 구 `BlockingHazard` 이식 — 이동을 막는 해저드(체력 있음).
    public struct BlockingHazard
    {
        public int hazardSoIndex;
        public float maxHp;
    }

    /// 구 `BlockingHazardCellsBuffer` 이식.
    public struct BlockingHazardCellsBuffer
    {
        public SimInt2 cell;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/3 — 셀 → 효과 인덱스.
    /// 구 `NativeParallelMultiHashMap&lt;int2, HazardEffect&gt;` 이식.
    ///
    /// ⚠ **순회 순서가 계약이다 — 역-삽입순이다.** 계획서가 `HazardLifetime` 재작성의 제약으로
    /// 못박은 tie-break ⑥ 이 이것이다("재작성은 자료구조만, 순회 순서는 보존한다").
    /// 구 `NativeParallelMultiHashMap.Add` 는 버킷 체인에 **prepend** 하므로
    /// `TryGetFirstValue` 가 **가장 최근 추가분**을 먼저 준다. 추정이 아니라 구 sim 에서
    /// 실측했다(`HazardLifetimeSystemTests.EffectOrderWithinCell_IsReverseInsertion_NotInsertion`).
    ///
    /// 관리 `List` 는 삽입 순서로 도는 것이 자연스러워 **그대로 옮기면 순서가 뒤집힌다.**
    /// 그래서 이 타입은 리스트를 노출하지 않고 <see cref="Get"/> 로만 읽게 한다 —
    /// `index 0` 이 가장 최근 추가분이다. 소비자가 순서를 틀릴 방법이 없다.
    /// </summary>
    public sealed class HazardCellIndex
    {
        // 셀별 리스트는 재사용한다 — 매 프레임 재빌드이므로 새로 할당하면 틱당 쓰레기가 셀 수만큼 생긴다.
        private readonly Dictionary<SimInt2, List<HazardEffect>> _map =
            new Dictionary<SimInt2, List<HazardEffect>>();

        /// 총 항목 수. 구 `cellToEffects.Count()` 대응 — `ZoneApply` 가 0 조기 반환에 쓴다.
        public int Count { get; private set; }

        /// 매 프레임 재빌드의 시작. 리스트 객체는 유지하고 내용만 비운다.
        public void Clear()
        {
            foreach (var kv in _map) kv.Value.Clear();
            Count = 0;
        }

        public void Add(SimInt2 cell, in HazardEffect effect)
        {
            if (!_map.TryGetValue(cell, out var list)) _map[cell] = list = new List<HazardEffect>(4);
            list.Add(effect);
            Count++;
        }

        public int CountFor(SimInt2 cell)
            => _map.TryGetValue(cell, out var list) ? list.Count : 0;

        /// <summary>
        /// ⚠ **`index 0` 이 가장 최근에 추가된 효과다**(역-삽입순 — 위 클래스 주석).
        /// 삽입 순서로 읽으려면 이 함수를 쓰면 안 되고, 그럴 이유도 없다.
        /// </summary>
        public HazardEffect Get(SimInt2 cell, int index)
        {
            var list = _map[cell];
            return list[list.Count - 1 - index];
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/3 — 셀 인덱스를 담는 싱글턴. 구 `HazardSingleton` 이식.
    /// 소유자는 `HazardLifetimeSystem`(#2)이고 `ZoneApplySystem`(#5)이 읽는다.
    /// </summary>
    public struct HazardSingleton
    {
        public HazardCellIndex cellToEffects;
        public bool IsCreated => cellToEffects != null;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/3 — 라스트런 기믹 저작값. 구 `RedBullGimmickConfig` 이식.
    /// ⚠ **존재 = 기믹 활성**(분류 B 게이트 — `BurnoutGimmickConfig` 와 같은 형태).
    /// </summary>
    public struct RedBullGimmickConfig
    {
        public float redbullSpawnInterval;
        public float redbullLifetime;
        public int redbullMaxActive;
        public float lastRunAttackSpeedMul;
        public float lastRunDuration;
        /// crash 피해 = 최대체력 × 이 비율.
        public float lastRunDamageFraction;
    }

    /// 라스트런 잔여 시간. 만료 시 crash(자해 피해) → 컴포넌트 제거.
    public struct LastRun
    {
        public float remaining;
    }
}
