using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // season-gimmick-overwork unit 3 — 야근 기믹 피로도 누적 타이머 (Effects 소유).
    // FatigueAccrualSystem 이 기믹 활성 시 defender 에 lazy-attach — 스폰 경로 무수정
    // (MaxHealthScaleState 전례). elapsed 가 fatigueInterval 을 넘을 때마다 피로도 스택 enqueue.
    public struct FatigueAccrual : IComponentData
    {
        public float elapsed;
    }
}
