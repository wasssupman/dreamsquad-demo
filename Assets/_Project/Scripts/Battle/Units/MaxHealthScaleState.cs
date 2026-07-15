using Unity.Entities;

namespace Wassup.Battle.Units
{
    // season-gimmick-overwork unit 1 — 최대체력 배율 적용 상태.
    // baseMax: 스폰 시점 원본 최대체력 (배율 반복 적용의 기준값 — Health.max 를 직접 곱하면 누적 오염).
    // appliedMul: 마지막으로 적용한 배율 캐시 (변화 감지용).
    // MaxHealthScaleSystem 이 lazy-attach (maxHealthMul != 1 인 첫 프레임) — 스폰 경로 무수정.
    public struct MaxHealthScaleState : IComponentData
    {
        public float baseMax;
        public float appliedMul;
    }
}
