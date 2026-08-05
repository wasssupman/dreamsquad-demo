namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-F/1 — 어그로 링크. 구 `Aggroed` 이식.
    /// **first-come, sticky** — 먼저 맞춘 가디언이 가져가고 해제는 가디언 사망뿐이다.
    /// ⚠ `Aggroed`·`AggroCapacity` 의 **유일한 writer 는 #8** 이고 이동·공격은 읽기만 한다.
    /// </summary>
    public struct Aggroed
    {
        public SimEntityId guardian;
    }

    /// <summary>
    /// 가디언이 동시에 붙들 수 있는 적 수. 구 `AggroCapacity` 이식.
    /// ⚠ `held` 는 **매 틱 full recompute** 다(증분 아님) — 드리프트를 구조적으로 없앤다.
    /// </summary>
    public struct AggroCapacity
    {
        public int max;
        public int held;
    }

    /// <summary>
    /// 어그로된 적이 가디언까지 하강할 chase field 의 셀 1칸. 구 `AggroChaseCell` 이식.
    /// ⚠ **수명이 <see cref="Aggroed"/> 와 동기**다 — 해제 시 함께 제거된다.
    /// </summary>
    public struct AggroChaseCell
    {
        public int dist;
    }

    /// <summary>
    /// Combat→Effects 히트 구동 어그로. 구 `AggroHitEvent` 이식.
    /// ⚠ 소비자(#8, P2)가 생산자(#33 공격, P8)보다 **앞**이라 **구조적 영구 1틱 지연**이다.
    /// 선언이 아니라 phase 배치가 보장한다 — 청사진이 이 쌍을 두고 *"선언 없음, 구조가 보장"*
    /// 이라고 적은 그 자리다.
    /// </summary>
    public struct AggroHitEvent
    {
        public SimEntityId guardian;
        public SimEntityId enemy;
    }
}
